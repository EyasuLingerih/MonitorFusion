using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

/// <summary>
/// A slim taskbar docked to the bottom edge of a zone.
/// Shows buttons for every window whose centre point is within the zone.
/// </summary>
public partial class ZoneTaskbarWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE         = -20;
    private const int WS_EX_TOOLWINDOW    = 0x00000080;
    private const int SW_RESTORE          = 9;

    private readonly ZoneDefinition _zone;
    private readonly MonitorInfo    _monitor;
    private readonly DispatcherTimer _refreshTimer;

    public ZoneTaskbarWindow(ZoneDefinition zone, MonitorInfo monitor)
    {
        InitializeComponent();
        _zone    = zone;
        _monitor = monitor;

        ZoneNameLabel.Text = string.IsNullOrEmpty(zone.Name) ? "Zone" : zone.Name;

        // Position at bottom of zone
        PositionWindow();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _refreshTimer.Tick += (_, _) => RefreshWindows();
        Loaded += (_, _) => { _refreshTimer.Start(); RefreshWindows(); };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private void PositionWindow()
    {
        var (left, top, width, height) = _zone.ToPixels(_monitor.Bounds);
        Left   = left;
        Top    = top + height - 36;   // stick to bottom of zone
        Width  = width;
    }

    private void RefreshWindows()
    {
        var windows = new List<(IntPtr hwnd, string title)>();
        var (zLeft, zTop, zWidth, zHeight) = _zone.ToPixels(_monitor.Bounds);
        int zRight  = zLeft + zWidth;
        int zBottom = zTop  + zHeight;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

            var sb = new StringBuilder(256);
            if (GetWindowText(hwnd, sb, 256) == 0) return true;

            if (!GetWindowRect(hwnd, out var rect)) return true;

            // Centre of window must be inside the zone
            int cx = (rect.Left + rect.Right)  / 2;
            int cy = (rect.Top  + rect.Bottom) / 2;
            if (cx >= zLeft && cx < zRight && cy >= zTop && cy < zBottom)
                windows.Add((hwnd, sb.ToString()));

            return true;
        }, IntPtr.Zero);

        WindowPanel.Children.Clear();
        foreach (var (hwnd, title) in windows)
        {
            var btn = new Button
            {
                Content = TruncateTitle(title),
                ToolTip = title,
                Height  = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Margin  = new Thickness(2, 4, 2, 4),
                Background      = new SolidColorBrush(Color.FromArgb(180, 40, 40, 80)),
                Foreground      = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize        = 11,
                Cursor          = System.Windows.Input.Cursors.Hand
            };

            var capturedHwnd = hwnd;
            btn.Click += (_, _) =>
            {
                ShowWindow(capturedHwnd, SW_RESTORE);
                SetForegroundWindow(capturedHwnd);
            };

            WindowPanel.Children.Add(btn);
        }

        // If no windows in zone, show a faint placeholder
        if (windows.Count == 0)
        {
            WindowPanel.Children.Add(new TextBlock
            {
                Text = "Empty zone",
                Foreground = new SolidColorBrush(Color.FromArgb(80, 180, 180, 200)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
        }
    }

    private static string TruncateTitle(string title)
        => title.Length > 22 ? title[..19] + "…" : title;
}
