using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

/// <summary>
/// A transparent, click-through overlay that covers one monitor and draws
/// zone highlight rectangles during a window drag operation.
/// </summary>
public partial class ZoneOverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE       = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    // ── Frozen brushes (created once, shared across all instances) ─────────────
    private static readonly SolidColorBrush _normalFill;
    private static readonly SolidColorBrush _normalBorder;
    private static readonly SolidColorBrush _hoverFill;
    private static readonly SolidColorBrush _hoverBorder;
    private static readonly SolidColorBrush _labelBrush;

    static ZoneOverlayWindow()
    {
        _normalFill   = Frozen(Color.FromArgb(60,  80, 120, 220));
        _normalBorder = Frozen(Color.FromArgb(180, 100, 150, 255));
        _hoverFill    = Frozen(Color.FromArgb(120, 120, 180, 255));
        _hoverBorder  = Frozen(Color.FromArgb(255, 160, 210, 255));
        _labelBrush   = Frozen(Colors.White);
    }

    private static SolidColorBrush Frozen(Color c)
    { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // ── Instance state ─────────────────────────────────────────────────────────
    private MonitorInfo? _monitor;
    private List<ZoneDefinition> _zones = new();
    private readonly Dictionary<string, Border> _zoneBorders = new();

    public ZoneOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Make the window fully click-through so it doesn't interfere with dragging
        var hwnd = new WindowInteropHelper(this).Handle;
        int ext  = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ext | WS_EX_TRANSPARENT);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Positions this window over <paramref name="monitor"/> and draws all zone rectangles.
    /// </summary>
    public void ShowForMonitor(MonitorInfo monitor, List<ZoneDefinition> zones)
    {
        _monitor = monitor;
        _zones   = zones;

        Left   = monitor.Bounds.Left;
        Top    = monitor.Bounds.Top;
        Width  = monitor.Bounds.Width;
        Height = monitor.Bounds.Height;

        DrawZones();
        Show();
    }

    /// <summary>
    /// Highlights the zone with <paramref name="zoneId"/> and dims all others.
    /// Pass <c>null</c> to clear all highlights.
    /// </summary>
    public void HighlightZone(string? zoneId)
    {
        foreach (var (id, border) in _zoneBorders)
        {
            bool on = id == zoneId;
            border.Background   = on ? _hoverFill   : _normalFill;
            border.BorderBrush  = on ? _hoverBorder : _normalBorder;
            border.BorderThickness = on ? new Thickness(3) : new Thickness(2);
        }
    }

    /// <summary>
    /// Returns the zone that contains the given physical screen coordinates, or <c>null</c>.
    /// </summary>
    public ZoneDefinition? HitTest(int screenX, int screenY)
    {
        if (_monitor == null) return null;
        foreach (var zone in _zones)
            if (zone.HitTest(screenX, screenY, _monitor.Bounds))
                return zone;
        return null;
    }

    // ── Drawing ────────────────────────────────────────────────────────────────

    private void DrawZones()
    {
        ZoneCanvas.Children.Clear();
        _zoneBorders.Clear();

        if (_monitor == null) return;

        for (int i = 0; i < _zones.Count; i++)
        {
            var zone = _zones[i];

            double x = zone.LeftPct  * Width;
            double y = zone.TopPct   * Height;
            double w = zone.WidthPct * Width;
            double h = zone.HeightPct * Height;

            const double pad = 4;

            var label = new TextBlock
            {
                Text       = string.IsNullOrEmpty(zone.Name) ? (i + 1).ToString() : zone.Name,
                Foreground = _labelBrush,
                FontSize   = Math.Min(w, h) * 0.18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                    { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
            };

            var border = new Border
            {
                Width           = w - pad * 2,
                Height          = h - pad * 2,
                Background      = _normalFill,
                BorderBrush     = _normalBorder,
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(8),
                Child           = label
            };

            Canvas.SetLeft(border, x + pad);
            Canvas.SetTop(border,  y + pad);
            ZoneCanvas.Children.Add(border);
            _zoneBorders[zone.Id] = border;
        }
    }
}
