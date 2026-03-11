using System.Windows;
using System.Windows.Controls;

namespace MonitorFusion.App.Views;

public partial class FadingSettingsView : UserControl
{
    private bool _isInitialized;

    public FadingSettingsView()
    {
        InitializeComponent();
        LoadSettings();
        _isInitialized = true;
    }

    private void LoadSettings()
    {
        var settings = App.SettingsService.Load().Fading;

        EnableFadingCheck.IsChecked = settings.Enabled;
        OpacitySlider.Value = settings.Opacity;
        OpacityValueText.Text = $"{(int)(settings.Opacity * 100)}%";

        foreach (ComboBoxItem item in ModeCombo.Items)
        {
            if (item.Tag.ToString() == settings.Mode)
            {
                ModeCombo.SelectedItem = item;
                break;
            }
        }
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;

        var fullSettings = App.SettingsService.Load();
        var settings = fullSettings.Fading;

        settings.Enabled = EnableFadingCheck.IsChecked ?? false;
        settings.Opacity = OpacitySlider.Value;
        
        if (ModeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            settings.Mode = selectedItem.Tag.ToString() ?? "InactiveMonitors";
        }

        OpacityValueText.Text = $"{(int)(settings.Opacity * 100)}%";

        App.SettingsService.Save(fullSettings);
        
        // Immediately reload the service so the user sees changes live
        App.FadingService.ReloadSettings();
    }
}
