using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

/// <summary>
/// A completely click-through, topmost, borderless black window that dims the monitor.
/// </summary>
public partial class FadingWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public string MonitorId { get; }

    public FadingWindow(MonitorInfo monitor, FadingSettings settings)
    {
        InitializeComponent();

        MonitorId = monitor.DeviceId;

        // Position over the entire monitor
        Left = monitor.Bounds.Left;
        Top = monitor.Bounds.Top;
        Width = monitor.Bounds.Width;
        Height = monitor.Bounds.Height;
        
        Opacity = settings.Opacity;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make the window completely click-through at the OS level
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }
}
