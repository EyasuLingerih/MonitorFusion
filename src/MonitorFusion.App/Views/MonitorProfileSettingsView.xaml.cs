using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class MonitorProfileSettingsView : UserControl
{
    private List<MonitorInfo> _monitors = new();
    private readonly MonitorArrangementCanvas _arrangementCanvas = new();

    public MonitorProfileSettingsView()
    {
        InitializeComponent();
        ArrangementBorder.Child = _arrangementCanvas;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    public void Refresh()
    {
        _monitors = App.MonitorService.GetAllMonitors();
        var settings = App.SettingsService.Load();

        int count = _monitors.Count;
        MonitorCountLabel.Text = count == 0
            ? "No monitors detected."
            : $"{count} monitor{(count == 1 ? "" : "s")} connected";

        BuildMonitorCards(_monitors, settings.MonitorNicknames);
        LoadProfiles();
        _arrangementCanvas.SetData(_monitors, settings.MonitorNicknames);
    }

    // ─── Monitor Cards ────────────────────────────────────────────────────────

    private void BuildMonitorCards(List<MonitorInfo> monitors, Dictionary<string, string> nicknames)
    {
        MonitorCardsPanel.Children.Clear();
        foreach (var monitor in monitors)
        {
            nicknames.TryGetValue(monitor.DeviceId, out var customName);
            MonitorCardsPanel.Children.Add(CreateMonitorCard(monitor, customName ?? ""));
        }
    }

    private UIElement CreateMonitorCard(MonitorInfo monitor, string customName)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x38)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 12, 12),
            Width = 280
        };

        var stack = new StackPanel();

        // ── Header: index badge | "Display N" | PRIMARY badge ──────────────
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var indexBadge = new Border
        {
            Width = 30, Height = 22,
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A)),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 8, 0)
        };
        indexBadge.Child = new TextBlock
        {
            Text = (monitor.Index + 1).ToString(),
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED)),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(indexBadge, 0);
        header.Children.Add(indexBadge);

        var displayLabel = new TextBlock
        {
            Text = $"Display {monitor.Index + 1}",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(displayLabel, 1);
        header.Children.Add(displayLabel);

        if (monitor.IsPrimary)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xA2, 0xED)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2)
            };
            badge.Child = new TextBlock
            {
                Text = "PRIMARY",
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED)),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(badge, 2);
            header.Children.Add(badge);
        }
        stack.Children.Add(header);

        // ── Monitor name ────────────────────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text = monitor.FriendlyName,
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // ── Custom label editor ─────────────────────────────────────────────
        var labelRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelTb = new TextBlock
        {
            Text = "Label  ",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelTb, 0);
        labelRow.Children.Add(labelTb);

        var nicknameBox = new TextBox
        {
            Text = customName,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED)),
            Padding = new Thickness(4, 2, 4, 2),
            FontSize = 12,
            Tag = monitor.DeviceId
        };
        nicknameBox.LostFocus += Nickname_LostFocus;
        Grid.SetColumn(nicknameBox, 1);
        labelRow.Children.Add(nicknameBox);
        stack.Children.Add(labelRow);

        // ── Stats grid ──────────────────────────────────────────────────────
        string orientationText = monitor.Orientation switch
        {
            1 => "Portrait",
            2 => "Landscape (Flip)",
            3 => "Portrait (Flip)",
            _ => "Landscape"
        };
        string dpiText = monitor.DpiScale > 0 ? $"{monitor.DpiScale:0}%" : "—";

        var statsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        void AddStatRow(string lbl1, string val1, string lbl2, string val2)
        {
            var lblRow = new Grid();
            lblRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lblRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l1 = new TextBlock { Text = lbl1, Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)), FontSize = 11 };
            var l2 = new TextBlock { Text = lbl2, Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)), FontSize = 11 };
            Grid.SetColumn(l2, 1);
            lblRow.Children.Add(l1);
            lblRow.Children.Add(l2);
            statsPanel.Children.Add(lblRow);

            var valRow = new Grid { Margin = new Thickness(0, 1, 0, 8) };
            valRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            valRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var v1 = new TextBlock { Text = val1, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold };
            var v2 = new TextBlock { Text = val2, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(v2, 1);
            valRow.Children.Add(v1);
            valRow.Children.Add(v2);
            statsPanel.Children.Add(valRow);
        }

        AddStatRow("Resolution", $"{monitor.Width} × {monitor.Height}",
                   "Refresh Rate", $"{monitor.RefreshRate} Hz");
        AddStatRow("DPI Scale", dpiText,
                   "Orientation", orientationText);

        stack.Children.Add(statsPanel);

        // ── Action row: Identify + Configure toggle ─────────────────────────
        var actionRow = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var identifyBtn = new Button
        {
            Tag = monitor.Index,
            Padding = new Thickness(8, 6, 8, 6),
            Content = "Identify",
            Margin = new Thickness(0, 0, 4, 0)
        };
        identifyBtn.SetResourceReference(StyleProperty, "ModernButton");
        identifyBtn.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x3F));
        identifyBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED));
        identifyBtn.Click += IdentifyMonitor_Click;
        Grid.SetColumn(identifyBtn, 0);
        actionRow.Children.Add(identifyBtn);

        // Configure toggle button — we create the panel first so the closure captures it
        var configPanel = CreateConfigPanel(monitor);
        var configToggle = new Button
        {
            Content = "⚙  Configure",
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(4, 0, 0, 0)
        };
        configToggle.SetResourceReference(StyleProperty, "ModernButton");
        configToggle.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x3F));
        configToggle.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xC0, 0xFF));
        configToggle.Click += (s, _) =>
        {
            bool showing = configPanel.Visibility == Visibility.Visible;
            configPanel.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
            configToggle.Foreground = showing
                ? new SolidColorBrush(Color.FromRgb(0xA0, 0xC0, 0xFF))
                : new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED));
        };
        Grid.SetColumn(configToggle, 1);
        actionRow.Children.Add(configToggle);

        stack.Children.Add(actionRow);
        stack.Children.Add(configPanel);

        card.Child = stack;
        return card;
    }

    private StackPanel CreateConfigPanel(MonitorInfo monitor)
    {
        var configPanel = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 12, 0, 0)
        };

        // Separator
        configPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A)),
            Margin = new Thickness(-16, 0, -16, 12)
        });

        // ── Resolution & Refresh Rate ───────────────────────────────────────
        configPanel.Children.Add(MakeLabel("Resolution & Refresh Rate"));

        var modes = App.MonitorService.GetAvailableModes(monitor.DeviceId);
        var modeCombo = MakeDarkComboBox();
        int selectedModeIdx = -1;
        for (int mi = 0; mi < modes.Count; mi++)
        {
            var mode = modes[mi];
            modeCombo.Items.Add($"{mode.Width} × {mode.Height}  @  {mode.RefreshRate} Hz");
            if (selectedModeIdx < 0 &&
                mode.Width == monitor.Width &&
                mode.Height == monitor.Height &&
                mode.RefreshRate == monitor.RefreshRate)
            {
                selectedModeIdx = mi;
            }
        }
        modeCombo.SelectedIndex = selectedModeIdx >= 0 ? selectedModeIdx : 0;
        configPanel.Children.Add(modeCombo);

        // ── Orientation ─────────────────────────────────────────────────────
        configPanel.Children.Add(MakeLabel("Orientation"));

        var orientCombo = MakeDarkComboBox();
        foreach (var o in new[] { "Landscape (0°)", "Portrait (90°)", "Landscape — Flipped (180°)", "Portrait — Flipped (270°)" })
            orientCombo.Items.Add(o);
        orientCombo.SelectedIndex = Math.Clamp(monitor.Orientation, 0, 3);
        configPanel.Children.Add(orientCombo);

        // ── Buttons: Set as Primary | Apply Settings ────────────────────────
        var btnRow = new Grid();
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var primaryBtn = new Button
        {
            Content = monitor.IsPrimary ? "✓ Primary" : "Set as Primary",
            Padding = new Thickness(6, 6, 6, 6),
            Margin = new Thickness(0, 0, 4, 0),
            IsEnabled = !monitor.IsPrimary
        };
        primaryBtn.SetResourceReference(StyleProperty, "ModernButton");
        primaryBtn.Background = monitor.IsPrimary
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A))
            : new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x3F));
        primaryBtn.Foreground = monitor.IsPrimary
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : Brushes.White;
        primaryBtn.Click += (s, _) =>
        {
            var (ok, msg) = App.MonitorProfileService.ApplyMonitorSettings(
                monitor.DeviceId,
                monitor.Width, monitor.Height, monitor.RefreshRate,
                monitor.Orientation,
                setPrimary: true);

            if (ok) Refresh();
            else MessageBox.Show(msg, "Set as Primary Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        Grid.SetColumn(primaryBtn, 0);
        btnRow.Children.Add(primaryBtn);

        var applyBtn = new Button
        {
            Content = "Apply Settings",
            Padding = new Thickness(6, 6, 6, 6),
            Margin = new Thickness(4, 0, 0, 0)
        };
        applyBtn.SetResourceReference(StyleProperty, "ModernButton");
        applyBtn.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x62, 0x9D));
        applyBtn.Foreground = Brushes.White;
        applyBtn.Click += (s, _) =>
        {
            int modeIdx = modeCombo.SelectedIndex;
            if (modeIdx < 0 || modeIdx >= modes.Count)
            {
                MessageBox.Show("Please select a resolution.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var mode = modes[modeIdx];
            int orient = orientCombo.SelectedIndex;

            var (ok, msg) = App.MonitorProfileService.ApplyMonitorSettings(
                monitor.DeviceId,
                mode.Width, mode.Height, mode.RefreshRate,
                orient,
                setPrimary: false);

            if (ok)
            {
                ShowTip($"Settings applied to {monitor.FriendlyName}.");
                Refresh();
            }
            else
            {
                MessageBox.Show(msg, "Apply Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        Grid.SetColumn(applyBtn, 1);
        btnRow.Children.Add(applyBtn);

        configPanel.Children.Add(btnRow);
        return configPanel;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
        FontSize = 11
    };

    private static ComboBox MakeDarkComboBox()
    {
        // Style the dropdown items to match the dark theme
        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E))));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(6, 3, 6, 3)));

        var highlightTrigger = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlightTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A))));
        itemStyle.Triggers.Add(highlightTrigger);

        var selectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x2A, 0x4A, 0x6A))));
        itemStyle.Triggers.Add(selectedTrigger);

        // Style is applied globally via App.xaml — just set margins here
        return new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 12),
            ItemContainerStyle = itemStyle
        };
    }

    private void Nickname_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string deviceId) return;

        var settings = App.SettingsService.Load();
        var name = box.Text.Trim();
        if (string.IsNullOrEmpty(name))
            settings.MonitorNicknames.Remove(deviceId);
        else
            settings.MonitorNicknames[deviceId] = name;

        App.SettingsService.Save(settings);

        _arrangementCanvas.SetData(_monitors, settings.MonitorNicknames);
    }

    private void IdentifyMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int idx) return;

        var monitor = _monitors.FirstOrDefault(m => m.Index == idx);
        if (monitor == null) return;

        var settings = App.SettingsService.Load();
        settings.MonitorNicknames.TryGetValue(monitor.DeviceId, out var customName);
        string displayName = !string.IsNullOrEmpty(customName) ? customName : monitor.FriendlyName;

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new TextBlock
        {
            Text = (monitor.Index + 1).ToString(),
            FontSize = 96, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = displayName,
            FontSize = 20,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 220, 220, 220)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"{monitor.Width} × {monitor.Height}  @  {monitor.RefreshRate} Hz",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var overlay = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true, ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(210, 10, 10, 25)),
            Left = monitor.Bounds.Left, Top = monitor.Bounds.Top,
            Width = monitor.Width, Height = monitor.Height,
            Content = content
        };
        overlay.Show();

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (s, _) => { overlay.Close(); timer.Stop(); };
        timer.Start();
    }

    // ─── Profiles ─────────────────────────────────────────────────────────────

    private void LoadProfiles()
    {
        var settings = App.SettingsService.Load();
        var profiles = settings.MonitorProfiles.ToList();
        ProfilesList.ItemsSource = profiles;
        NoProfilesLabel.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string name = NewProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter a name for the profile.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            App.MonitorProfileService.SaveCurrentProfile(name);
            NewProfileNameBox.Text = string.Empty;
            LoadProfiles();
            ShowTip($"Layout '{name}' saved with {_monitors.Count} monitor(s).");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save profile: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string profileName) return;

        var (success, message) = App.MonitorProfileService.ApplyProfile(profileName);
        if (success)
            ShowTip($"Profile '{profileName}' applied.");
        else
            MessageBox.Show(message, "Apply Profile Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string profileName) return;

        if (MessageBox.Show($"Delete profile '{profileName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        App.MonitorProfileService.DeleteProfile(profileName);
        LoadProfiles();
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        var positions = _arrangementCanvas.GetCurrentPositions();
        if (positions.Count == 0) return;

        var (ok, msg) = App.MonitorProfileService.ApplyMonitorPositions(positions);
        if (ok)
        {
            ShowTip("Monitor layout applied.");
            Refresh();
        }
        else
        {
            MessageBox.Show(msg, "Apply Layout Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowTip(string message)
    {
        if (Application.Current.Windows.OfType<MainWindow>().FirstOrDefault() is { } mw)
        {
            mw.TrayIcon.ShowBalloonTip("Monitors", message,
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }
}

/// <summary>
/// Draggable monitor arrangement. Uses FrameworkElement + DrawingVisual (proven WPF
/// pattern) so ArrangeOverride always provides the correct size. Monitors are hit-tested
/// manually and dragged by tracking their current canvas Rects.
/// </summary>
internal class MonitorArrangementCanvas : FrameworkElement
{
    private readonly DrawingVisual _dv = new();
    private readonly VisualCollection _visuals;

    private List<MonitorInfo> _monitors = [];
    private Dictionary<string, string> _nicknames = [];

    // Scale / origin — recomputed in ArrangeOverride
    private double _scale, _ox, _oy;
    private int _minLeft, _minTop;

    // Current canvas-space rects for each monitor (DeviceId → Rect)
    private readonly Dictionary<string, Rect> _rects = [];

    // Drag state
    private string? _dragId;
    private Point   _dragOffset;   // mouse position relative to rect top-left

    private static readonly Typeface _tf = new("Segoe UI");
    private const double SnapThreshold = 40;

    public MonitorArrangementCanvas()
    {
        _visuals = new VisualCollection(this);
        _visuals.Add(_dv);
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // Accept all hits so mouse events fire even over transparent areas
    protected override HitTestResult HitTestCore(PointHitTestParameters p)
        => new PointHitTestResult(this, p.HitPoint);

    protected override Size MeasureOverride(Size available)
    {
        double w = double.IsInfinity(available.Width) ? 560 : available.Width;

        if (_monitors.Count > 0)
        {
            int totalVW = _monitors.Max(m => m.Bounds.Right) - _monitors.Min(m => m.Bounds.Left);
            if (totalVW <= 0) totalVW = _monitors.Sum(m => Math.Max(m.Width, 1));

            int totalVH = _monitors.Max(m => m.Bounds.Bottom) - _monitors.Min(m => m.Bounds.Top);

            // Sum of all monitor heights (worst-case: all stacked vertically)
            int totalStackH = _monitors.Sum(m => m.Height);

            const double pad = 12;
            double scaleW = (w - pad * 2) / totalVW;

            // Tall enough to show current layout AND to drag monitors into a full vertical stack
            double currentLayoutH = totalVH  * scaleW + pad * 2;
            double fullStackH     = totalStackH * scaleW + pad * 2;
            double ideal = Math.Max(currentLayoutH, fullStackH);

            return new Size(w, Math.Clamp(ideal, 160, 650));
        }
        return new Size(w, 200);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        BuildRects(finalSize.Width, finalSize.Height);
        Redraw(finalSize.Width, finalSize.Height);
        return finalSize;
    }

    public void SetData(List<MonitorInfo> monitors, Dictionary<string, string> nicknames)
    {
        _monitors  = monitors;
        _nicknames = nicknames;
        _rects.Clear();
        _dragId = null;
        InvalidateMeasure();  // recalculates ideal height from new monitor layout
        InvalidateArrange();  // redraws with correct size
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private void BuildRects(double w, double h)
    {
        _rects.Clear();
        if (w < 10 || h < 10 || _monitors.Count == 0) return;

        _minLeft = _monitors.Min(m => m.Bounds.Left);
        _minTop  = _monitors.Min(m => m.Bounds.Top);
        int totalW = _monitors.Max(m => m.Bounds.Right)  - _minLeft;
        int totalH = _monitors.Max(m => m.Bounds.Bottom) - _minTop;

        if (totalW == 0) totalW = _monitors.Sum(m => Math.Max(m.Width,  1));
        if (totalH == 0) totalH = _monitors.Max(m => Math.Max(m.Height, 1));
        if (totalW == 0 || totalH == 0) return;

        const double pad = 12;
        // Scale width-limited so vertical space is available for stacking
        _scale = Math.Min((w - pad * 2) / totalW, (h - pad * 2) / totalH);
        _ox    = pad + (w - pad * 2 - totalW * _scale) / 2;
        _oy    = pad; // top-anchor: keep vertical space free for dragging downward

        double fallbackX = 0;
        foreach (var mon in _monitors)
        {
            int bLeft = mon.Bounds.Left == mon.Bounds.Right ? (int)fallbackX : mon.Bounds.Left;
            fallbackX += Math.Max(mon.Width, 1);

            double cx = _ox + (bLeft - _minLeft) * _scale;
            double cy = _oy + (mon.Bounds.Top - _minTop) * _scale;
            double cw = Math.Max(mon.Width  * _scale, 60);
            double ch = Math.Max(mon.Height * _scale, 40);

            _rects[mon.DeviceId] = new Rect(cx, cy, cw, ch);
        }
    }

    public List<(string DeviceId, int X, int Y)> GetCurrentPositions()
    {
        if (_rects.Count == 0 || _scale == 0) return [];

        var raw = _rects.Select(kv => (
            kv.Key,
            rx: (int)Math.Round((kv.Value.X - _ox) / _scale) + _minLeft,
            ry: (int)Math.Round((kv.Value.Y - _oy) / _scale) + _minTop
        )).ToList();

        int minX = raw.Min(p => p.rx);
        int minY = raw.Min(p => p.ry);
        return raw.Select(p => (p.Key, p.rx - minX, p.ry - minY)).ToList();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void Redraw(double w = 0, double h = 0)
    {
        if (w == 0) w = ActualWidth;
        if (h == 0) h = ActualHeight;

        using var dc = _dv.RenderOpen();
        if (w < 10 || h < 10 || _rects.Count == 0) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var mon in _monitors)
        {
            if (!_rects.TryGetValue(mon.DeviceId, out var r)) continue;

            bool dragging = mon.DeviceId == _dragId;
            var bg  = Frozen(dragging ? Color.FromRgb(0x3A, 0x3A, 0x5F) : Color.FromRgb(0x2D, 0x2D, 0x3F));
            var bc  = mon.IsPrimary ? Color.FromRgb(0x00, 0xA2, 0xED)
                    : dragging      ? Color.FromRgb(0x60, 0x80, 0xFF)
                                    : Color.FromRgb(0x4A, 0x4A, 0x6A);
            var pen = new Pen(Frozen(bc), mon.IsPrimary || dragging ? 2 : 1);
            pen.Freeze();

            dc.DrawRoundedRectangle(bg, pen, r, 2, 2);

            _nicknames.TryGetValue(mon.DeviceId, out var nick);
            string label = !string.IsNullOrEmpty(nick) ? nick : $"Display {mon.DisplayNumber}";

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _tf, 10, Frozen(Colors.White), dpi)
            {
                MaxTextWidth = Math.Max(r.Width - 6, 1),
                Trimming     = TextTrimming.CharacterEllipsis
            };

            if (r.Height > 36)
            {
                var sub = new FormattedText($"{mon.Width}×{mon.Height}",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, _tf, 9,
                    Frozen(Color.FromRgb(0xA0, 0xA0, 0xB0)), dpi);
                double totalH2 = ft.Height + sub.Height + 2;
                double ly = r.Y + (r.Height - totalH2) / 2;
                dc.DrawText(ft,  new Point(r.X + (r.Width - ft.Width)  / 2, ly));
                dc.DrawText(sub, new Point(r.X + (r.Width - sub.Width) / 2, ly + ft.Height + 2));
            }
            else
            {
                dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2,
                                          r.Y + (r.Height - ft.Height) / 2));
            }
        }
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    // ── Mouse interaction ─────────────────────────────────────────────────────

    private string? HitTest(Point p)
    {
        // Draw-order: last drawn = topmost visually; check in reverse
        foreach (var mon in _monitors.AsEnumerable().Reverse())
            if (_rects.TryGetValue(mon.DeviceId, out var r) && r.Contains(p))
                return mon.DeviceId;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);

        if (_dragId != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var r = _rects[_dragId];
            double nx = Math.Clamp(p.X - _dragOffset.X, 0, ActualWidth  - r.Width);
            double ny = Math.Clamp(p.Y - _dragOffset.Y, 0, ActualHeight - r.Height);
            _rects[_dragId] = new Rect(nx, ny, r.Width, r.Height);
            Cursor = Cursors.SizeAll;
            Redraw();
        }
        else
        {
            Cursor = HitTest(p) != null ? Cursors.SizeAll : Cursors.Arrow;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var p  = e.GetPosition(this);
        var id = HitTest(p);
        if (id == null) return;

        _dragId     = id;
        var r       = _rects[id];
        _dragOffset = new Point(p.X - r.X, p.Y - r.Y);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_dragId == null || e.ChangedButton != MouseButton.Left) return;
        Snap(_dragId);
        _dragId = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        Redraw();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_dragId == null) Cursor = Cursors.Arrow;
    }

    private void Snap(string id)
    {
        var r = _rects[id];

        // Find the globally best X snap and best Y snap independently across all neighbors.
        double snapX = r.X, snapY = r.Y;
        double bestXDist = SnapThreshold, bestYDist = SnapThreshold;

        foreach (var (oid, o) in _rects)
        {
            if (oid == id) continue;

            // Candidate X positions: flush-right-of, flush-left-of, left-align, right-align
            foreach (double xc in new[] { o.Right, o.X - r.Width, o.X, o.Right - r.Width })
            {
                double d = Math.Abs(xc - r.X);
                if (d < bestXDist) { bestXDist = d; snapX = xc; }
            }

            // Candidate Y positions: flush-below, flush-above, top-align, bottom-align
            foreach (double yc in new[] { o.Bottom, o.Y - r.Height, o.Y, o.Bottom - r.Height })
            {
                double d = Math.Abs(yc - r.Y);
                if (d < bestYDist) { bestYDist = d; snapY = yc; }
            }
        }

        double dl = bestXDist < SnapThreshold ? snapX : r.X;
        double dt = bestYDist < SnapThreshold ? snapY : r.Y;

        // Clamp to canvas before overlap resolver so pushes don't escape bounds
        dl = Math.Clamp(dl, 0, ActualWidth  - r.Width);
        dt = Math.Clamp(dt, 0, ActualHeight - r.Height);

        // Resolve any remaining overlaps. Try all 4 push directions in penetration order
        // and use the first one that actually produces a non-overlapping position.
        // Using positive-area overlap (WPF IntersectsWith fires on shared edges too).
        bool changed = true;
        for (int pass = 0; pass < 8 && changed; pass++)
        {
            changed = false;
            foreach (var (oid, o) in _rects)
            {
                if (oid == id) continue;

                double overlapX = Math.Min(dl + r.Width, o.Right)  - Math.Max(dl, o.X);
                double overlapY = Math.Min(dt + r.Height, o.Bottom) - Math.Max(dt, o.Y);
                if (overlapX <= 0 || overlapY <= 0) continue;

                // Build candidate positions ordered by minimum penetration (try smallest push first)
                (double, double)[] candidates = overlapX <= overlapY
                    ? new[]
                    {
                        (dl < o.X ? o.X - r.Width : o.Right, dt),   // X push (min)
                        (dl, dt < o.Y ? o.Y - r.Height : o.Bottom),  // Y push (fallback)
                        (dl < o.X ? o.Right : o.X - r.Width, dt),    // X push opposite
                        (dl, dt < o.Y ? o.Bottom : o.Y - r.Height),  // Y push opposite
                    }
                    : new[]
                    {
                        (dl, dt < o.Y ? o.Y - r.Height : o.Bottom),  // Y push (min)
                        (dl < o.X ? o.X - r.Width : o.Right, dt),   // X push (fallback)
                        (dl, dt < o.Y ? o.Bottom : o.Y - r.Height),  // Y push opposite
                        (dl < o.X ? o.Right : o.X - r.Width, dt),    // X push opposite
                    };

                foreach (var (cdl, cdt) in candidates)
                {
                    double ndl = Math.Clamp(cdl, 0, ActualWidth  - r.Width);
                    double ndt = Math.Clamp(cdt, 0, ActualHeight - r.Height);
                    double ox = Math.Min(ndl + r.Width, o.Right)  - Math.Max(ndl, o.X);
                    double oy = Math.Min(ndt + r.Height, o.Bottom) - Math.Max(ndt, o.Y);
                    if (ox <= 0 || oy <= 0)
                    {
                        dl = ndl; dt = ndt;
                        changed = true;
                        break;
                    }
                }
            }
        }

        _rects[id] = new Rect(dl, dt, r.Width, r.Height);
    }
}
