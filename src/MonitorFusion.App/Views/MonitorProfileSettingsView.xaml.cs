using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class MonitorProfileSettingsView : UserControl
{
    private List<MonitorInfo> _monitors = new();
    private MonitorArrangementVisual? _arrangementVisual;

    public MonitorProfileSettingsView()
    {
        InitializeComponent();
        // Create the custom drawing element and inject it into the named Border
        _arrangementVisual = new MonitorArrangementVisual();
        ArrangementBorder.Child = _arrangementVisual;
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
        _arrangementVisual?.SetData(_monitors, settings.MonitorNicknames);
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

        _arrangementVisual?.SetData(_monitors, settings.MonitorNicknames);
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

    private void ShowTip(string message)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.TrayIcon.ShowBalloonTip("Monitors", message,
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }
}

/// <summary>
/// Custom FrameworkElement that draws the monitor arrangement diagram using a hosted
/// DrawingVisual (the canonical WPF pattern for custom rendering). Drawing is refreshed
/// whenever data changes or the element is re-arranged (i.e. on resize).
/// </summary>
internal class MonitorArrangementVisual : FrameworkElement
{
    private readonly DrawingVisual _dv = new();
    private readonly VisualCollection _visuals;

    private List<MonitorInfo> _monitors = new();
    private Dictionary<string, string> _nicknames = new();

    private static readonly Typeface _typeface;
    private static readonly SolidColorBrush _bgBrush;
    private static readonly SolidColorBrush _accentBrush;
    private static readonly SolidColorBrush _borderBrush;
    private static readonly SolidColorBrush _textBrush;
    private static readonly SolidColorBrush _subBrush;

    static MonitorArrangementVisual()
    {
        _typeface    = new Typeface("Segoe UI");
        _bgBrush     = Frozen(Color.FromRgb(0x2D, 0x2D, 0x3F));
        _accentBrush = Frozen(Color.FromRgb(0x00, 0xA2, 0xED));
        _borderBrush = Frozen(Color.FromRgb(0x4A, 0x4A, 0x6A));
        _textBrush   = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));
        _subBrush    = Frozen(Color.FromRgb(0xA0, 0xA0, 0xB0));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public MonitorArrangementVisual()
    {
        _visuals = new VisualCollection(this);
        _visuals.Add(_dv);
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    public void SetData(List<MonitorInfo> monitors, Dictionary<string, string> nicknames)
    {
        _monitors  = monitors;
        _nicknames = nicknames;
        Draw(ActualWidth, ActualHeight);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width)  ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

    protected override Size ArrangeOverride(Size finalSize)
    {
        Draw(finalSize.Width, finalSize.Height);
        return finalSize;
    }

    private void Draw(double w, double h)
    {
        using var dc = _dv.RenderOpen();   // always produces a fresh drawing
        if (w < 10 || h < 10 || _monitors.Count == 0) return;

        int minLeft = _monitors.Min(m => m.Bounds.Left);
        int minTop  = _monitors.Min(m => m.Bounds.Top);
        int totalW  = _monitors.Max(m => m.Bounds.Right)  - minLeft;
        int totalH  = _monitors.Max(m => m.Bounds.Bottom) - minTop;

        if (totalW == 0) totalW = _monitors.Sum(m => Math.Max(m.Bounds.Width, 1));
        if (totalH == 0) totalH = _monitors.Max(m => Math.Max(m.Bounds.Height, 1));
        if (totalW == 0 || totalH == 0) return;

        const double pad = 12;
        double scale = Math.Min((w - pad * 2) / totalW, (h - pad * 2) / totalH);
        double ox = pad + (w - pad * 2 - totalW * scale) / 2;
        double oy = pad + (h - pad * 2 - totalH * scale) / 2;

        double fallbackX = 0;
        foreach (var monitor in _monitors)
        {
            int bLeft = monitor.Bounds.Left == monitor.Bounds.Right ? (int)fallbackX : monitor.Bounds.Left;
            int bTop  = monitor.Bounds.Top;
            int bW    = Math.Max(monitor.Bounds.Width,  1);
            int bH    = Math.Max(monitor.Bounds.Height, 1);
            fallbackX += bW;

            double rx = ox + (bLeft - minLeft) * scale;
            double ry = oy + (bTop  - minTop)  * scale;
            double rw = Math.Max(bW * scale, 40);
            double rh = Math.Max(bH * scale, 28);

            var pen  = new Pen(monitor.IsPrimary ? _accentBrush : _borderBrush,
                               monitor.IsPrimary ? 2 : 1);
            pen.Freeze();
            dc.DrawRoundedRectangle(_bgBrush, pen, new Rect(rx, ry, rw, rh), 2, 2);

            _nicknames.TryGetValue(monitor.DeviceId, out var nick);
            string label = !string.IsNullOrEmpty(nick) ? nick : $"Display {monitor.Index + 1}";

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _typeface, 10, _textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(rw - 6, 1),
                Trimming     = TextTrimming.CharacterEllipsis
            };

            double labelY = rh > 36 ? ry + (rh / 2 - ft.Height) - 2 : ry + (rh - ft.Height) / 2;
            dc.DrawText(ft, new Point(rx + (rw - ft.Width) / 2, labelY));

            if (rh > 36)
            {
                var sub = new FormattedText($"{monitor.Bounds.Width}×{monitor.Bounds.Height}",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, _typeface, 9, _subBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(sub, new Point(rx + (rw - sub.Width) / 2, labelY + ft.Height + 1));
            }
        }
    }
}
