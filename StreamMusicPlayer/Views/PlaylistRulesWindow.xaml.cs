using System.Windows;

namespace StreamMusicPlayer.Views;

public partial class PlaylistRulesWindow : Window
{
    public PlaylistRulesWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
