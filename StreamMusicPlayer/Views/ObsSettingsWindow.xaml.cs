using System.Windows;
using StreamMusicPlayer.ViewModels;

namespace StreamMusicPlayer.Views;

public partial class ObsSettingsWindow : Window
{
    public ObsSettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ObsSettingsViewModel viewModel)
        {
            viewModel.Dispose();
        }

        base.OnClosed(e);
    }
}
