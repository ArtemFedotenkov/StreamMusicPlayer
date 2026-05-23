using System.Windows;

namespace StreamMusicPlayer.Views;

public partial class EventRulesWindow : Window
{
    public EventRulesWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
