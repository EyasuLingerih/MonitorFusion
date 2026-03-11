using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

public partial class MonitorProfileSettingsView : UserControl
{
    private List<MonitorInfo> _monitors = new();
    private bool _arrangementPending;

    public MonitorProfileSettingsView()
    {
        InitializeComponent();
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
        RenderArrangement(_monitors, settings.MonitorNicknames);
        LoadProfiles();
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
            Width = 265
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

        // ── Stats ───────────────────────────────────────────────────────────
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
                   "Refresh Rate",  $"{monitor.RefreshRate} Hz");
        AddStatRow("DPI Scale", dpiText,
                   "Orientation", orientationText);

        stack.Children.Add(statsPanel);

        // ── Identify button ─────────────────────────────────────────────────
        var identifyBtn = new Button
        {
            Tag = monitor.Index,
            Padding = new Thickness(12, 6, 12, 6),
            Content = "Identify Monitor"
        };
        identifyBtn.SetResourceReference(StyleProperty, "ModernButton");
        identifyBtn.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x3F));
        identifyBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED));
        identifyBtn.Click += IdentifyMonitor_Click;
        stack.Children.Add(identifyBtn);

        card.Child = stack;
        return card;
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
        RenderArrangement(_monitors, settings.MonitorNicknames);
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

    // ─── Arrangement Canvas ───────────────────────────────────────────────────

    private void ArrangementCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_arrangementPending && _monitors.Count > 0)
        {
            var settings = App.SettingsService.Load();
            RenderArrangement(_monitors, settings.MonitorNicknames);
        }
    }

    private void RenderArrangement(List<MonitorInfo> monitors, Dictionary<string, string> nicknames)
    {
        ArrangementCanvas.Children.Clear();
        if (monitors.Count == 0) return;

        double canvasW = ArrangementCanvas.ActualWidth;
        double canvasH = ArrangementCanvas.ActualHeight;

        if (canvasW < 10 || canvasH < 10)
        {
            _arrangementPending = true;
            return;
        }
        _arrangementPending = false;

        int minLeft = monitors.Min(m => m.Bounds.Left);
        int minTop  = monitors.Min(m => m.Bounds.Top);
        int totalW  = monitors.Max(m => m.Bounds.Right)  - minLeft;
        int totalH  = monitors.Max(m => m.Bounds.Bottom) - minTop;
        if (totalW == 0 || totalH == 0) return;

        const double pad = 12;
        double scale = Math.Min((canvasW - pad * 2) / totalW, (canvasH - pad * 2) / totalH);
        double ox = pad + (canvasW - pad * 2 - totalW * scale) / 2;
        double oy = pad + (canvasH - pad * 2 - totalH * scale) / 2;

        foreach (var monitor in monitors)
        {
            double x = ox + (monitor.Bounds.Left - minLeft) * scale;
            double y = oy + (monitor.Bounds.Top  - minTop)  * scale;
            double w = Math.Max(monitor.Bounds.Width  * scale, 32);
            double h = Math.Max(monitor.Bounds.Height * scale, 22);

            nicknames.TryGetValue(monitor.DeviceId, out var nick);
            string label = !string.IsNullOrEmpty(nick) ? nick : $"Display {monitor.Index + 1}";

            var rect = new Border
            {
                Width  = w, Height = h,
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x3F)),
                BorderBrush = monitor.IsPrimary
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xA2, 0xED))
                    : new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x6A)),
                BorderThickness = new Thickness(monitor.IsPrimary ? 2 : 1),
                CornerRadius = new CornerRadius(2)
            };

            var inner = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            inner.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = w - 6
            });
            if (h > 36)
            {
                inner.Children.Add(new TextBlock
                {
                    Text = $"{monitor.Width}×{monitor.Height}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            rect.Child = inner;

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            ArrangementCanvas.Children.Add(rect);
        }
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
