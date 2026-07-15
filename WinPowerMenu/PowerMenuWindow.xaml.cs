using System;
using System.Windows;

namespace WinPowerMenu;

public partial class PowerMenuWindow : Window
{
    private bool _closing;

    public PowerMenuWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BtnCancel.Focus();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            SafeClose();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Clicking away or focus loss dismisses the popup — but guard against
        // the re-entrant case where Close() itself triggers WM_ACTIVATE(false).
        SafeClose();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        try { Close(); } catch { /* already closing */ }
    }

    private void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        PowerActions.Shutdown();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        PowerActions.Restart();
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
        PowerActions.Sleep();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SafeClose();
    }
}
