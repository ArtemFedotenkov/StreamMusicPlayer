using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.ViewModels;

namespace StreamMusicPlayer;

public partial class MainWindow : Window
{
    private Point tracksDragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Dispose();
        }

        base.OnClosed(e);
    }

    private void TracksDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        tracksDragStartPoint = e.GetPosition(null);
    }

    private void TracksDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not DataGrid dataGrid)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - tracksDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - tracksDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (dataGrid.SelectedItem is Track track)
        {
            DragDrop.DoDragDrop(dataGrid, track, DragDropEffects.Move);
        }
    }

    private void TracksDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel
            || !e.Data.GetDataPresent(typeof(Track))
            || e.Data.GetData(typeof(Track)) is not Track draggedTrack)
        {
            return;
        }

        var targetTrack = FindDataContext<Track>(e.OriginalSource as DependencyObject);
        var targetIndex = targetTrack is null
            ? viewModel.SelectedPlaylist?.Tracks.Count - 1 ?? 0
            : viewModel.SelectedPlaylist?.Tracks.IndexOf(targetTrack) ?? 0;
        viewModel.MoveTrack(draggedTrack, targetIndex);
        TracksDataGrid.Items.Refresh();
        e.Handled = true;
    }

    private static T? FindDataContext<T>(DependencyObject? source)
        where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T dataContext })
            {
                return dataContext;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void PositionSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.BeginSeekPreview();
        }
    }

    private async void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        await Dispatcher.InvokeAsync(() => { });
        await viewModel.SeekToAsync(slider.Value);
    }
}
