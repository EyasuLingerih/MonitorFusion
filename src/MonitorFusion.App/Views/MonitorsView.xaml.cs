using System.Windows;
using System.Windows.Controls;

namespace MonitorFusion.App.Views;

public partial class MonitorsView : UserControl
{
    public MonitorsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    public void Refresh()
    {
        MonitorList.ItemsSource = App.MonitorService.GetAllMonitors();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void IdentifyMonitors_Click(object sender, RoutedEventArgs e)
    {
        var monitors = App.MonitorService.GetAllMonitors();
        foreach (var monitor in monitors)
        {
            var label = new TextBlock
            {
                Text = (monitor.Index + 1).ToString(),
                FontSize = 96,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var subLabel = new TextBlock
            {
                Text = monitor.FriendlyName,
                FontSize = 18,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 200, 200, 200)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(label);
            stack.Children.Add(subLabel);

            var overlay = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(210, 15, 15, 30)),
                Left = monitor.Bounds.Left,
                Top = monitor.Bounds.Top,
                Width = monitor.Width,
                Height = monitor.Height,
                Content = stack
            };
            overlay.Show();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, _) => { overlay.Close(); timer.Stop(); };
            timer.Start();
        }
    }

    private static MainWindow? GetMainWindow() =>
        Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

    private void SavePositions_Click(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.SavePositions_Click(sender, e);

    private void RestorePositions_Click(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.RestorePositions_Click(sender, e);

    private void NextWallpaper_Click(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.NextWallpaper_Click(sender, e);
}
