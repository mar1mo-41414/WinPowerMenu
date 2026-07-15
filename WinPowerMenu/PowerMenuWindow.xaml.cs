using System;
using System.Windows;
using System.Windows.Input;

namespace WinPowerMenu;

public partial class PowerMenuWindow : Window
{
    public PowerMenuWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BtnCancel.Focus();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Close if the user clicks away — feels natural for an overlay.
        Close();
    }

    private void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        Close();
        PowerActions.Shutdown();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        Close();
        PowerActions.Restart();
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        Close();
        PowerActions.Sleep();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
