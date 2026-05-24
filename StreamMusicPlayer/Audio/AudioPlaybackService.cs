using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using StreamMusicPlayer.Models;
using AppPlaybackState = StreamMusicPlayer.Models.PlaybackState;

namespace StreamMusicPlayer.Audio;

public sealed class AudioPlaybackService : IDisposable
{
    private readonly SemaphoreSlim transitionLock = new(1, 1);
    private readonly SemaphoreSlim volumeLock = new(1, 1);
    private readonly Timer endTimer;
    private CancellationTokenSource transitionCancellation = new();
    private CancellationTokenSource volumeCancellation = new();
    private WaveOutEvent? output;
    private MixingSampleProvider? mixer;
    private VolumeSampleProvider? masterVolumeProvider;
    private AudioFileReader? reader;
    private VolumeSampleProvider? activeInput;
    private float masterVolume = 1;
    private int outputDeviceNumber = -1;
    private bool disposed;
    private bool suppressEndNotification;

    public AudioPlaybackService()
    {
        endTimer = new Timer(CheckPlaybackEnded, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    public event EventHandler? PlaybackEnded;

    public Track? CurrentTrack { get; private set; }

    public AppPlaybackState State { get; private set; } = AppPlaybackState.Stopped;

    public double PositionSeconds
    {
        get => reader?.CurrentTime.TotalSeconds ?? 0;
        set
        {
            if (reader is null)
            {
                return;
            }

            var seconds = Math.Clamp(value, 0, reader.TotalTime.TotalSeconds);
            reader.CurrentTime = TimeSpan.FromSeconds(seconds);
        }
    }

    public double DurationSeconds => reader?.TotalTime.TotalSeconds ?? 0;

    public float Volume
    {
        get => masterVolume;
        set
        {
            masterVolume = Math.Clamp(value, 0, 1);
            if (masterVolumeProvider is not null)
            {
                masterVolumeProvider.Volume = masterVolume;
            }
        }
    }

    public async Task PlayAsync(Track track, float volume, double fadeInSeconds, double crossfadeSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateTrack(track);

        await transitionLock.WaitAsync();
        try
        {
            CancelTransition();
            var cancellationToken = transitionCancellation.Token;

            var newReader = new AudioFileReader(track.FilePath);
            var newInput = new VolumeSampleProvider(newReader) { Volume = 0 };

            if (mixer is null || output is null)
            {
                StartMixer(newReader.WaveFormat);
            }

            if (mixer is null || output is null)
            {
                newReader.Dispose();
                throw new InvalidOperationException("Audio output was not initialized.");
            }

            var oldReader = reader;
            var oldInput = activeInput;

            if (oldReader is not null && !WaveFormatsMatch(mixer.WaveFormat, newReader.WaveFormat))
            {
                newReader.Dispose();
                if (activeInput is not null)
                {
                    State = AppPlaybackState.Fading;
                    await FadeInputAsync(activeInput, activeInput.Volume, 0, Math.Min(0.5, crossfadeSeconds), cancellationToken);
                }

                DisposeCurrentPlayback();
                StartDirect(track, inputVolume: fadeInSeconds > 0 ? 0 : 1);

                if (fadeInSeconds > 0)
                {
                    State = AppPlaybackState.Fading;
                    await FadeInputAsync(activeInput, 0, 1, fadeInSeconds, cancellationToken);
                }

                State = AppPlaybackState.Playing;
                return;
            }

            suppressEndNotification = true;
            mixer.AddMixerInput(newInput);

            // Let the new decoder/output path buffer silently before the old track fades.
            await Task.Delay(120, cancellationToken);

            reader = newReader;
            activeInput = newInput;
            CurrentTrack = track;

            if (oldInput is null || oldReader is null || crossfadeSeconds <= 0)
            {
                if (oldInput is not null)
                {
                    mixer.RemoveMixerInput(oldInput);
                }

                oldReader?.Dispose();
                State = AppPlaybackState.Playing;
                if (fadeInSeconds > 0)
                {
                    State = AppPlaybackState.Fading;
                    await FadeInputAsync(newInput, 0, 1, fadeInSeconds, cancellationToken);
                }
                else
                {
                    newInput.Volume = 1;
                }

                State = AppPlaybackState.Playing;
                suppressEndNotification = false;
                return;
            }

            State = AppPlaybackState.Switching;
            await CrossfadeAsync(oldInput, newInput, oldInput.Volume, 1, crossfadeSeconds, cancellationToken);
            mixer.RemoveMixerInput(oldInput);
            oldReader.Dispose();
            State = AppPlaybackState.Playing;
            suppressEndNotification = false;
        }
        finally
        {
            suppressEndNotification = false;
            transitionLock.Release();
        }
    }

    public void Pause()
    {
        output?.Pause();
        State = AppPlaybackState.Paused;
    }

    public void Resume()
    {
        if (output is null)
        {
            return;
        }

        output.Play();
        State = AppPlaybackState.Playing;
    }

    public async Task StopAsync(double fadeOutSeconds)
    {
        await transitionLock.WaitAsync();
        try
        {
            CancelTransition();
            var cancellationToken = transitionCancellation.Token;

            if (activeInput is not null && fadeOutSeconds > 0)
            {
                State = AppPlaybackState.Fading;
                await FadeInputAsync(activeInput, activeInput.Volume, 0, fadeOutSeconds, cancellationToken);
            }

            DisposeCurrentPlayback();
            State = AppPlaybackState.Stopped;
            CurrentTrack = null;
        }
        finally
        {
            transitionLock.Release();
        }
    }

    public void SetVolume(float volume)
    {
        Volume = volume;
    }

    public async Task SeekAsync(double seconds, double fadeSeconds)
    {
        if (reader is null || CurrentTrack is null)
        {
            return;
        }

        await transitionLock.WaitAsync();
        try
        {
            CancelTransition();
            var cancellationToken = transitionCancellation.Token;
            var targetSeconds = Math.Clamp(seconds, 0, reader.TotalTime.TotalSeconds);
            var seekFadeSeconds = Math.Clamp(fadeSeconds, 0, 10);
            if (activeInput is null || mixer is null || output is null || State != AppPlaybackState.Playing || seekFadeSeconds <= 0)
            {
                reader.CurrentTime = TimeSpan.FromSeconds(targetSeconds);
                return;
            }

            var oldReader = reader;
            var oldInput = activeInput;
            var track = CurrentTrack;
            var newReader = new AudioFileReader(track.FilePath)
            {
                CurrentTime = TimeSpan.FromSeconds(targetSeconds)
            };
            var newInput = new VolumeSampleProvider(newReader) { Volume = 0 };

            try
            {
                suppressEndNotification = true;
                mixer.AddMixerInput(newInput);

                // Give the new decoder a tiny moment to buffer before blending positions.
                await Task.Delay(60, cancellationToken);

                reader = newReader;
                activeInput = newInput;
                State = AppPlaybackState.Switching;
                await CrossfadeAsync(oldInput, newInput, oldInput.Volume, 1, seekFadeSeconds, cancellationToken);
                mixer.RemoveMixerInput(oldInput);
                oldReader.Dispose();
                State = AppPlaybackState.Playing;
                suppressEndNotification = false;
            }
            catch
            {
                mixer.RemoveMixerInput(newInput);
                newReader.Dispose();
                suppressEndNotification = false;
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer transition superseded this seek.
            suppressEndNotification = false;
        }
        finally
        {
            transitionLock.Release();
        }
    }

    public void SetOutputDevice(int deviceNumber)
    {
        if (outputDeviceNumber == deviceNumber)
        {
            return;
        }

        outputDeviceNumber = deviceNumber;
        if (mixer is not null && output is not null)
        {
            RestartOutput();
        }
    }

    public async Task FadeVolumeToAsync(float volume, double seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CancelVolumeFade();

        await volumeLock.WaitAsync();
        try
        {
            var cancellationToken = volumeCancellation.Token;
            var targetVolume = Math.Clamp(volume, 0, 1);

            if (masterVolumeProvider is null || seconds <= 0)
            {
                Volume = targetVolume;
                return;
            }

            await FadeMasterVolumeAsync(masterVolumeProvider.Volume, targetVolume, seconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer volume change superseded this fade.
        }
        finally
        {
            volumeLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        transitionCancellation.Cancel();
        transitionCancellation.Dispose();
        volumeCancellation.Cancel();
        volumeCancellation.Dispose();
        endTimer.Dispose();
        DisposeCurrentPlayback();
        masterVolumeProvider = null;
        output?.Dispose();
        transitionLock.Dispose();
        volumeLock.Dispose();
    }

    private void StartMixer(WaveFormat waveFormat)
    {
        DisposeOutput();
        mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
        masterVolumeProvider = new VolumeSampleProvider(mixer) { Volume = masterVolume };
        RestartOutput();
    }

    private void RestartOutput()
    {
        if (masterVolumeProvider is null)
        {
            return;
        }

        DisposeOutput();
        output = new WaveOutEvent
        {
            DesiredLatency = 300,
            NumberOfBuffers = 3,
            DeviceNumber = outputDeviceNumber
        };
        output.Init(masterVolumeProvider);
        output.Play();
    }

    private void DisposeOutput()
    {
        output?.Stop();
        output?.Dispose();
        output = null;
    }

    private void StartDirect(Track track, float inputVolume)
    {
        var newReader = new AudioFileReader(track.FilePath);
        StartMixer(newReader.WaveFormat);
        var input = new VolumeSampleProvider(newReader) { Volume = Math.Clamp(inputVolume, 0, 1) };
        mixer?.AddMixerInput(input);
        reader = newReader;
        activeInput = input;
        CurrentTrack = track;
        output?.Play();
    }

    private void DisposeCurrentPlayback()
    {
        if (mixer is not null && activeInput is not null)
        {
            mixer.RemoveMixerInput(activeInput);
        }

        reader?.Dispose();
        reader = null;
        activeInput = null;
    }

    private void CancelTransition()
    {
        transitionCancellation.Cancel();
        transitionCancellation.Dispose();
        transitionCancellation = new CancellationTokenSource();
    }

    private void CancelVolumeFade()
    {
        volumeCancellation.Cancel();
        volumeCancellation.Dispose();
        volumeCancellation = new CancellationTokenSource();
    }

    private static async Task FadeInputAsync(VolumeSampleProvider? targetInput, float from, float to, double seconds, CancellationToken cancellationToken)
    {
        if (targetInput is null)
        {
            return;
        }

        var steps = Math.Max(1, (int)(seconds * 60));
        var delay = TimeSpan.FromMilliseconds(Math.Max(10, seconds * 1000 / steps));
        for (var step = 0; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progress = step / (float)steps;
            targetInput.Volume = from + ((to - from) * progress);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task FadeMasterVolumeAsync(float from, float to, double seconds, CancellationToken cancellationToken)
    {
        if (masterVolumeProvider is null)
        {
            Volume = to;
            return;
        }

        var steps = Math.Max(1, (int)(seconds * 60));
        var delay = TimeSpan.FromMilliseconds(Math.Max(10, seconds * 1000 / steps));
        for (var step = 0; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progress = step / (float)steps;
            Volume = from + ((to - from) * progress);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static async Task CrossfadeAsync(
        VolumeSampleProvider oldInput,
        VolumeSampleProvider newInput,
        float oldVolume,
        float newVolume,
        double seconds,
        CancellationToken cancellationToken)
    {
        var steps = Math.Max(1, (int)(seconds * 60));
        var delay = TimeSpan.FromMilliseconds(Math.Max(10, seconds * 1000 / steps));
        var targetNewVolume = Math.Clamp(newVolume, 0, 1);

        for (var step = 0; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progress = step / (float)steps;
            oldInput.Volume = oldVolume * (1 - progress);
            newInput.Volume = targetNewVolume * progress;
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool WaveFormatsMatch(WaveFormat first, WaveFormat second)
    {
        return first.SampleRate == second.SampleRate
            && first.Channels == second.Channels
            && first.Encoding == second.Encoding
            && first.BitsPerSample == second.BitsPerSample;
    }

    private static void ValidateTrack(Track track)
    {
        if (string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
        {
            throw new FileNotFoundException("Audio file was not found.", track.FilePath);
        }
    }

    private void CheckPlaybackEnded(object? state)
    {
        if (suppressEndNotification || State != AppPlaybackState.Playing || reader is null)
        {
            return;
        }

        if (reader.Position < reader.Length)
        {
            return;
        }

        State = AppPlaybackState.Stopped;
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }
}
