using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class HotkeySettingsView : UserControl
{
    private HotkeySettings _settings = new();
    private List<HotkeyViewModel> _viewModels = new();
    private HotkeyViewModel? _selectedHotkey;

    // We use this to track what keys are physically pressed down during recording
    private readonly HashSet<Key> _pressedKeys = new();

    public HotkeySettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var appSettings = App.SettingsService.Load();
        _settings = appSettings.Hotkeys ?? new HotkeySettings();
        
        // Build ViewModels to make display nice
        _viewModels = _settings.Bindings.Select(b => new HotkeyViewModel(b)).ToList();
        HotkeysList.ItemsSource = _viewModels;
    }

    private void HotkeysList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedHotkey = HotkeysList.SelectedItem as HotkeyViewModel;
        
        if (_selectedHotkey != null)
        {
            EditPanel.Visibility = Visibility.Visible;
            ListeningBox.Text = "Click here to record new shortcut...";
            ErrorText.Visibility = Visibility.Collapsed;
        }
        else
        {
            EditPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void HotkeyEnable_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is string actionId)
        {
            var binding = _settings.Bindings.FirstOrDefault(b => b.Action == actionId);
            if (binding != null)
            {
                binding.Enabled = cb.IsChecked == true;
                SaveAndApplySettings();
            }
        }
    }

    private void ListeningBox_GotFocus(object sender, RoutedEventArgs e)
    {
        ListeningBox.Text = "Listening... Press keys now";
        ListeningBox.BorderBrush = System.Windows.Media.Brushes.Yellow;
        _pressedKeys.Clear();
    }

    private void ListeningBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ListeningBox.BorderBrush = Application.Current.Resources["AccentBrush"] as System.Windows.Media.Brush;
        _pressedKeys.Clear();
    }

    private void ListeningBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
        
        // Ignore standalone modifiers
        if (key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // We got a real key. Look at modifiers.
        var modifiers = Keyboard.Modifiers;
        var modifierStr = new StringBuilder();
        
        if (modifiers.HasFlag(ModifierKeys.Control)) modifierStr.Append("Ctrl+");
        if (modifiers.HasFlag(ModifierKeys.Alt)) modifierStr.Append("Alt+");
        if (modifiers.HasFlag(ModifierKeys.Shift)) modifierStr.Append("Shift+");
        if (modifiers.HasFlag(ModifierKeys.Windows)) modifierStr.Append("Win+");

        string keyStr = key.ToString();
        // Friendly corrections
        if (key >= Key.D0 && key <= Key.D9) keyStr = keyStr.Replace("D", "");
        
        string fullShortcut = modifierStr.ToString() + keyStr;
        
        if (modifierStr.Length == 0)
        {
            ErrorText.Text = "Hotkeys must include at least one modifier (Ctrl, Alt, Shift, Win).";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ListeningBox.Text = fullShortcut;
        ErrorText.Visibility = Visibility.Collapsed;

        if (_selectedHotkey != null)
        {
            _selectedHotkey.Binding.Modifiers = modifierStr.ToString().TrimEnd('+');
            _selectedHotkey.Binding.Key = keyStr;
        }
        
        // Move focus out to stop listening immediately after one combo
        Apply_Click(null, null);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedHotkey != null)
        {
            _selectedHotkey.Binding.Modifiers = "";
            _selectedHotkey.Binding.Key = "";
            SaveAndApplySettings();
            
            EditPanel.Visibility = Visibility.Collapsed;
            HotkeysList.SelectedItem = null;
        }
    }

    private void Apply_Click(object? sender, RoutedEventArgs? e)
    {
        if (_selectedHotkey != null && !string.IsNullOrEmpty(_selectedHotkey.Binding.Key))
        {
            SaveAndApplySettings();
            EditPanel.Visibility = Visibility.Collapsed;
            HotkeysList.SelectedItem = null;
        }
    }

    private void SaveAndApplySettings()
    {
        HotkeysList.Items.Refresh(); // Update the UI list

        var appSettings = App.SettingsService.Load();
        appSettings.Hotkeys = _settings;
        App.SettingsService.Save(appSettings);

        // Re-register hotkeys immediately — no restart needed
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.ReloadHotkeys();
    }
}

public class HotkeyViewModel
{
    public HotkeyBinding Binding { get; }

    public HotkeyViewModel(HotkeyBinding binding)
    {
        Binding = binding;
    }

    public string ActionId => Binding.Action;
    
    public string ActionName
    {
        get
        {
            return Binding.Action switch
            {
                "MoveWindowNextMonitor" => "Move Window to Next Monitor",
                "MoveWindowPrevMonitor" => "Move Window to Prev Monitor",
                "MaximizeWindow" => "Maximize Window",
                "MinimizeWindow" => "Minimize Window",
                "SpanWindow" => "Span Window Across Monitors",
                "CenterWindow" => "Center Window on Monitor",
                "SaveWindowPositions" => "Quick Save Window Positions",
                "RestoreWindowPositions" => "Quick Restore Window Positions",
                "NextWallpaper" => "Next Wallpaper (Rotation)",
                "ToggleFocusMode" => "Toggle Focus Mode",
                _ => Binding.Action
            };
        }
    }

    public string DisplayString
    {
        get
        {
            if (string.IsNullOrEmpty(Binding.Key)) return "Not Bound";
            if (string.IsNullOrEmpty(Binding.Modifiers)) return Binding.Key;
            return $"{Binding.Modifiers}+{Binding.Key}";
        }
    }

    public bool IsEnabled
    {
        get => Binding.Enabled;
        set => Binding.Enabled = value;
    }
}
