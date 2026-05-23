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
    private AudioFileReader? reader;
    private VolumeSampleProvider? activeInput;
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
        get => activeInput?.Volume ?? 0;
        set
        {
            if (activeInput is not null)
            {
                activeInput.Volume = Math.Clamp(value, 0, 1);
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
            CancelVolumeFade();
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
                StartDirect(track, volume: fadeInSeconds > 0 ? 0 : volume);

                if (fadeInSeconds > 0)
                {
                    State = AppPlaybackState.Fading;
                    await FadeInputAsync(activeInput, 0, volume, fadeInSeconds, cancellationToken);
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
                var targetVolume = Math.Clamp(volume, 0, 1);
                if (fadeInSeconds > 0)
                {
                    State = AppPlaybackState.Fading;
                    await FadeInputAsync(newInput, 0, targetVolume, fadeInSeconds, cancellationToken);
                }
                else
                {
                    newInput.Volume = targetVolume;
                }

                State = AppPlaybackState.Playing;
                suppressEndNotification = false;
                return;
            }

            State = AppPlaybackState.Switching;
            await CrossfadeAsync(oldInput, newInput, oldInput.Volume, volume, crossfadeSeconds, cancellationToken);
            mixer.RemoveMixerInput(oldInput);
            oldReader.Dispose();
            State = AppPlaybackState.Playing;
            suppressEndNotification = false;
        }
        finally
        {
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

            if (activeInput is null || seconds <= 0)
            {
                Volume = targetVolume;
                return;
            }

            await FadeInputAsync(activeInput, activeInput.Volume, targetVolume, seconds, cancellationToken);
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
        output?.Dispose();
        transitionLock.Dispose();
        volumeLock.Dispose();
    }

    private void StartMixer(WaveFormat waveFormat)
    {
        DisposeOutput();
        mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
        RestartOutput();
    }

    private void RestartOutput()
    {
        if (mixer is null)
        {
            return;
        }

        DisposeOutput();
        output = new WaveOutEvent
        {
            DesiredLatency = 120,
            DeviceNumber = outputDeviceNumber
        };
        output.Init(mixer);
        output.Play();
    }

    private void DisposeOutput()
    {
        output?.Stop();
        output?.Dispose();
        output = null;
    }

    private void StartDirect(Track track, float volume)
    {
        var newReader = new AudioFileReader(track.FilePath);
        StartMixer(newReader.WaveFormat);
        var input = new VolumeSampleProvider(newReader) { Volume = Math.Clamp(volume, 0, 1) };
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
