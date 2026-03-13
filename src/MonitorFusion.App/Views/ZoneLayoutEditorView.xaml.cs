using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

public partial class ZoneLayoutEditorView : UserControl
{
    // Colors for zone preview rectangles (cycles through these)
    private static readonly Color[] ZoneColors =
    {
        Color.FromRgb(100, 100, 220),
        Color.FromRgb(220, 100, 100),
        Color.FromRgb(100, 200, 100),
        Color.FromRgb(220, 160,  50),
        Color.FromRgb(160,  80, 220),
        Color.FromRgb( 50, 200, 200),
        Color.FromRgb(220,  80, 180),
        Color.FromRgb(180, 200,  50),
    };

    private ZoneSettings _settings = new();
    private MonitorInfo? _selectedMonitor;
    // The mutable zone list for the selected monitor (being edited)
    private List<ZoneDefinition> _currentZones = new();
    private bool _loading;

    public ZoneLayoutEditorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        PopulateMonitorCombo();
    }

    // ── Data loading ───────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        _loading = true;
        _settings = App.SettingsService.Load().Zones;

        EnabledCheck.IsChecked  = _settings.Enabled;
        OverlayCheck.IsChecked  = _settings.ShowOverlayOnDrag;
        TaskbarsCheck.IsChecked = _settings.ShowZoneTaskbars;

        foreach (ComboBoxItem item in TriggerCombo.Items)
        {
            if ((string)item.Tag == _settings.TriggerModifier)
            {
                TriggerCombo.SelectedItem = item;
                break;
            }
        }
        if (TriggerCombo.SelectedIndex < 0) TriggerCombo.SelectedIndex = 0;

        _loading = false;
    }

    private void PopulateMonitorCombo()
    {
        _loading = true;
        var monitors = App.MonitorService.GetAllMonitors();
        MonitorCombo.Items.Clear();
        foreach (var m in monitors)
        {
            var item = new ComboBoxItem
            {
                Content = $"{m.FriendlyName}  ({m.Width}×{m.Height}){(m.IsPrimary ? "  [Primary]" : "")}",
                Tag     = m
            };
            MonitorCombo.Items.Add(item);
        }
        if (MonitorCombo.Items.Count > 0)
            MonitorCombo.SelectedIndex = 0;
        _loading = false;

        // SelectionChanged was suppressed by _loading above, so set monitor manually
        _selectedMonitor = (MonitorCombo.SelectedItem as ComboBoxItem)?.Tag as MonitorInfo;
        LoadZonesForSelectedMonitor();
    }

    private void LoadZonesForSelectedMonitor()
    {
        if (_selectedMonitor == null) return;

        var layout = _settings.Layouts
            .FirstOrDefault(l => l.MonitorDeviceId == _selectedMonitor.DeviceId);

        _currentZones = layout?.Zones
            .Select(z => new ZoneDefinition
            {
                Id = z.Id, Name = z.Name,
                LeftPct = z.LeftPct, TopPct = z.TopPct,
                WidthPct = z.WidthPct, HeightPct = z.HeightPct
            })
            .ToList() ?? new List<ZoneDefinition>();

        RebuildZoneList();
        DrawPreview();
    }

    // ── Zone list UI ───────────────────────────────────────────────────────────

    private void RebuildZoneList()
    {
        ZoneList.Items.Clear();
        NoZonesLabel.Visibility = _currentZones.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < _currentZones.Count; i++)
        {
            var zone  = _currentZones[i];
            var color = ZoneColors[i % ZoneColors.Length];
            ZoneList.Items.Add(BuildZoneRow(zone, color, i));
        }
    }

    private UIElement BuildZoneRow(ZoneDefinition zone, Color color, int index)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Color swatch
        var swatch = new Rectangle
        {
            Width = 10, Height = 10,
            Fill = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(swatch, 0);

        // Name
        var nameBox = MakeTextBox(zone.Name, 120);
        nameBox.TextChanged += (s, e) =>
        {
            zone.Name = nameBox.Text;
            DrawPreview();
        };
        Grid.SetColumn(nameBox, 1);

        // Left, Top, Width, Height percent inputs
        var leftBox   = MakePctBox(zone.LeftPct   * 100);
        var topBox    = MakePctBox(zone.TopPct    * 100);
        var widthBox  = MakePctBox(zone.WidthPct  * 100);
        var heightBox = MakePctBox(zone.HeightPct * 100);

        leftBox.TextChanged   += (s, e) => { if (TryParsePct(leftBox.Text,   out var v)) { zone.LeftPct   = v; DrawPreview(); } };
        topBox.TextChanged    += (s, e) => { if (TryParsePct(topBox.Text,    out var v)) { zone.TopPct    = v; DrawPreview(); } };
        widthBox.TextChanged  += (s, e) => { if (TryParsePct(widthBox.Text,  out var v)) { zone.WidthPct  = v; DrawPreview(); } };
        heightBox.TextChanged += (s, e) => { if (TryParsePct(heightBox.Text, out var v)) { zone.HeightPct = v; DrawPreview(); } };

        Grid.SetColumn(leftBox,   2);
        Grid.SetColumn(topBox,    3);
        Grid.SetColumn(widthBox,  4);
        Grid.SetColumn(heightBox, 5);

        // Delete button
        var deleteBtn = new Button
        {
            Content = "✕",
            FontSize = 11,
            Width = 28, Height = 24,
            Margin = new Thickness(4, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(80, 30, 30)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        deleteBtn.Click += (s, e) =>
        {
            _currentZones.Remove(zone);
            RebuildZoneList();
            DrawPreview();
        };
        Grid.SetColumn(deleteBtn, 6);

        grid.Children.Add(swatch);
        grid.Children.Add(nameBox);
        grid.Children.Add(leftBox);
        grid.Children.Add(topBox);
        grid.Children.Add(widthBox);
        grid.Children.Add(heightBox);
        grid.Children.Add(deleteBtn);

        return grid;
    }

    private static TextBox MakeTextBox(string text, double width) => new()
    {
        Text = text, Width = width, Margin = new Thickness(0, 0, 6, 0),
        Padding = new Thickness(4, 2, 4, 2),
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 120)),
        BorderThickness = new Thickness(1)
    };

    private static TextBox MakePctBox(double value) =>
        MakeTextBox(Math.Round(value, 1).ToString(), 52);

    private static bool TryParsePct(string text, out double value)
    {
        if (double.TryParse(text, out value))
        {
            value = Math.Clamp(value / 100.0, 0.0, 1.0);
            return true;
        }
        return false;
    }

    // ── Canvas preview ─────────────────────────────────────────────────────────

    private void DrawPreview()
    {
        PreviewCanvas.Children.Clear();
        if (_selectedMonitor == null) return;

        int monW = _selectedMonitor.Width;
        int monH = _selectedMonitor.Height;
        if (monW <= 0 || monH <= 0) return;   // guard against uninitialized bounds

        // Use a fixed preview height; derive width from the monitor's true aspect ratio
        const double previewH = 180.0;
        double previewW = previewH * ((double)monW / monH);

        PreviewBorder.Width  = previewW;
        PreviewCanvas.Width  = previewW;
        PreviewCanvas.Height = previewH;

        const double pad = 3;

        for (int i = 0; i < _currentZones.Count; i++)
        {
            var zone  = _currentZones[i];
            var color = ZoneColors[i % ZoneColors.Length];

            // Snap to whole pixels so adjacent zones share a crisp pixel boundary
            double x = Math.Round(zone.LeftPct   * previewW);
            double y = Math.Round(zone.TopPct    * previewH);
            double w = Math.Round(zone.WidthPct  * previewW);
            double h = Math.Round(zone.HeightPct * previewH);

            // Coloured filled rectangle with border
            var rect = new Rectangle
            {
                Width           = Math.Max(0, w - pad * 2),
                Height          = Math.Max(0, h - pad * 2),
                Fill            = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)),
                Stroke          = new SolidColorBrush(color),
                StrokeThickness = 1.5,
                RadiusX = 4, RadiusY = 4
            };
            Canvas.SetLeft(rect, x + pad);
            Canvas.SetTop(rect,  y + pad);
            PreviewCanvas.Children.Add(rect);

            // Zone number label — always a short number, centered inside the rect via a Border
            var labelBorder = new Border
            {
                Width  = Math.Max(0, w - pad * 2),
                Height = Math.Max(0, h - pad * 2),
                Child  = new TextBlock
                {
                    Text                = (i + 1).ToString(),
                    Foreground          = new SolidColorBrush(color),
                    FontSize            = Math.Clamp(Math.Min(w, h) * 0.28, 10, 40),
                    FontWeight          = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextAlignment       = TextAlignment.Center
                }
            };
            Canvas.SetLeft(labelBorder, x + pad);
            Canvas.SetTop(labelBorder,  y + pad);
            PreviewCanvas.Children.Add(labelBorder);
        }
    }

    // ── Preset factory ─────────────────────────────────────────────────────────

    private static List<ZoneDefinition> BuildPreset(string tag) => tag switch
    {
        "HalvesH" => new List<ZoneDefinition>
        {
            Z("Left",  0,   0, 0.5, 1),
            Z("Right", 0.5, 0, 0.5, 1),
        },
        "HalvesV" => new List<ZoneDefinition>
        {
            Z("Top",    0, 0,   1, 0.5),
            Z("Bottom", 0, 0.5, 1, 0.5),
        },
        "ThirdsH" => new List<ZoneDefinition>
        {
            Z("Left",   0,      0, 1.0/3, 1),
            Z("Center", 1.0/3,  0, 1.0/3, 1),
            Z("Right",  2.0/3,  0, 1.0/3, 1),
        },
        "ThirdsV" => new List<ZoneDefinition>
        {
            Z("Top",    0, 0,      1, 1.0/3),
            Z("Middle", 0, 1.0/3,  1, 1.0/3),
            Z("Bottom", 0, 2.0/3,  1, 1.0/3),
        },
        "Quadrants" => new List<ZoneDefinition>
        {
            Z("Top-Left",     0,   0,   0.5, 0.5),
            Z("Top-Right",    0.5, 0,   0.5, 0.5),
            Z("Bottom-Left",  0,   0.5, 0.5, 0.5),
            Z("Bottom-Right", 0.5, 0.5, 0.5, 0.5),
        },
        "Ultrawide" => new List<ZoneDefinition>
        {
            Z("Left",   0,      0, 0.25, 1),
            Z("Center", 0.25,   0, 0.5,  1),
            Z("Right",  0.75,   0, 0.25, 1),
        },
        "FocusSides" => new List<ZoneDefinition>
        {
            Z("Left Side",  0,    0, 0.25, 1),
            Z("Focus",      0.25, 0, 0.5,  1),
            Z("Right Side", 0.75, 0, 0.25, 1),
        },
        _ => new List<ZoneDefinition>()
    };

    private static ZoneDefinition Z(string name, double l, double t, double w, double h) =>
        new() { Name = name, LeftPct = l, TopPct = t, WidthPct = w, HeightPct = h };

    // ── Event handlers ─────────────────────────────────────────────────────────

    private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _selectedMonitor = (MonitorCombo.SelectedItem as ComboBoxItem)?.Tag as MonitorInfo;
        LoadZonesForSelectedMonitor();
    }

    private void RefreshMonitors_Click(object sender, RoutedEventArgs e)
        => PopulateMonitorCombo();

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor == null) return;
        var tag = (string)((Button)sender).Tag;
        _currentZones = BuildPreset(tag);
        RebuildZoneList();
        DrawPreview();
    }

    private void AddCustomZone_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor == null) return;
        _currentZones.Add(new ZoneDefinition
        {
            Name = $"Zone {_currentZones.Count + 1}",
            LeftPct = 0, TopPct = 0, WidthPct = 0.5, HeightPct = 0.5
        });
        RebuildZoneList();
        DrawPreview();
    }

    private void ClearZones_Click(object sender, RoutedEventArgs e)
    {
        _currentZones.Clear();
        RebuildZoneList();
        DrawPreview();
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SaveGlobalSettings();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor == null)
        {
            MessageBox.Show("Please select a monitor first.", "No Monitor Selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveGlobalSettings();
        SaveLayoutForCurrentMonitor();

        App.ZoneService.ReloadSettings();

        MessageBox.Show("Zone layout saved!", "Saved",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveGlobalSettings()
    {
        var appSettings = App.SettingsService.Load();
        appSettings.Zones.Enabled          = EnabledCheck.IsChecked == true;
        appSettings.Zones.ShowOverlayOnDrag = OverlayCheck.IsChecked == true;
        appSettings.Zones.ShowZoneTaskbars  = TaskbarsCheck.IsChecked == true;
        appSettings.Zones.TriggerModifier   =
            (TriggerCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "None";
        App.SettingsService.Save(appSettings);
        _settings = appSettings.Zones;
    }

    private void SaveLayoutForCurrentMonitor()
    {
        if (_selectedMonitor == null) return;

        var appSettings = App.SettingsService.Load();

        // Remove existing layout for this monitor
        appSettings.Zones.Layouts.RemoveAll(l => l.MonitorDeviceId == _selectedMonitor.DeviceId);

        if (_currentZones.Count > 0)
        {
            appSettings.Zones.Layouts.Add(new ZoneLayout
            {
                MonitorDeviceId = _selectedMonitor.DeviceId,
                Name            = $"{_selectedMonitor.FriendlyName} Layout",
                Zones           = _currentZones.Select(z => new ZoneDefinition
                {
                    Id = z.Id, Name = z.Name,
                    LeftPct = z.LeftPct, TopPct = z.TopPct,
                    WidthPct = z.WidthPct, HeightPct = z.HeightPct
                }).ToList()
            });
        }

        App.SettingsService.Save(appSettings);
        _settings = appSettings.Zones;
    }
}
