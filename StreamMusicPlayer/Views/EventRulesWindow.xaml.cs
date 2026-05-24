using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.ViewModels;

namespace StreamMusicPlayer.Views;

public partial class EventRulesWindow : Window
{
    private Point actionsDragStartPoint;

    public EventRulesWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenEventContextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || DataContext is not EventRulesViewModel viewModel)
        {
            return;
        }

        var contextMenu = new ContextMenu();
        var obsItem = new MenuItem { Header = "OBS" };
        foreach (var option in viewModel.ObsEventMenu)
        {
            obsItem.Items.Add(CreateEventMenuItem(option, viewModel));
        }

        var playerItem = new MenuItem { Header = "Player" };
        foreach (var option in viewModel.PlayerEventMenu)
        {
            playerItem.Items.Add(CreateEventMenuItem(option, viewModel));
        }

        contextMenu.Items.Add(obsItem);
        contextMenu.Items.Add(playerItem);
        contextMenu.PlacementTarget = element;
        contextMenu.IsOpen = true;
    }

    private void OpenActionContextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || DataContext is not EventRulesViewModel viewModel)
        {
            return;
        }

        var contextMenu = new ContextMenu();
        var obsItem = new MenuItem { Header = "OBS" };
        foreach (var option in viewModel.ObsActionMenu)
        {
            obsItem.Items.Add(CreateActionMenuItem(option, viewModel));
        }

        var playerItem = new MenuItem { Header = "Player" };
        foreach (var option in viewModel.PlayerActionMenu)
        {
            playerItem.Items.Add(CreateActionMenuItem(option, viewModel));
        }

        contextMenu.Items.Add(obsItem);
        contextMenu.Items.Add(playerItem);
        contextMenu.PlacementTarget = element;
        contextMenu.IsOpen = true;
    }

    private static MenuItem CreateEventMenuItem(AutomationEventMenuOption option, EventRulesViewModel viewModel)
    {
        var menuItem = new MenuItem { Header = option.DisplayName };
        foreach (var child in option.Children)
        {
            menuItem.Items.Add(CreateEventMenuItem(child, viewModel));
        }

        if (option.CanAdd)
        {
            menuItem.Command = viewModel.AddEventOptionCommand;
            menuItem.CommandParameter = option;
        }

        return menuItem;
    }

    private static MenuItem CreateActionMenuItem(AutomationActionMenuOption option, EventRulesViewModel viewModel)
    {
        var menuItem = new MenuItem { Header = option.DisplayName };
        foreach (var child in option.Children)
        {
            menuItem.Items.Add(CreateActionMenuItem(child, viewModel));
        }

        if (option.CanAdd)
        {
            menuItem.Command = viewModel.AddActionOptionCommand;
            menuItem.CommandParameter = option;
        }

        return menuItem;
    }

    private void RuleNameTextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        textBox.Focusable = true;
        textBox.IsReadOnly = false;
        textBox.Focus();
        textBox.SelectAll();
        e.Handled = true;
    }

    private void RuleNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.IsReadOnly = true;
            textBox.Focusable = false;
        }
    }

    private void RuleNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || e.Key != Key.Enter)
        {
            return;
        }

        textBox.IsReadOnly = true;
        textBox.Focusable = false;
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void ActionsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        actionsDragStartPoint = e.GetPosition(null);
    }

    private void ActionsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox listBox)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - actionsDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - actionsDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (listBox.SelectedItem is AutomationAction action)
        {
            DragDrop.DoDragDrop(listBox, action, DragDropEffects.Move);
        }
    }

    private void ActionsListBox_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not EventRulesViewModel viewModel
            || !e.Data.GetDataPresent(typeof(AutomationAction))
            || e.Data.GetData(typeof(AutomationAction)) is not AutomationAction draggedAction)
        {
            return;
        }

        var targetAction = FindDataContext<AutomationAction>(e.OriginalSource as DependencyObject);
        var targetIndex = targetAction is null
            ? viewModel.CurrentActions.Count - 1
            : viewModel.CurrentActions.IndexOf(targetAction);
        viewModel.MoveAction(draggedAction, targetIndex);
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
}
