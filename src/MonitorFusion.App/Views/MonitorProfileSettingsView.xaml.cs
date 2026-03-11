using System.Windows;
using System.Windows.Controls;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Views;

public partial class MonitorProfileSettingsView : UserControl
{
    public MonitorProfileSettingsView()
    {
        InitializeComponent();
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        var settings = App.SettingsService.Load();
        ProfilesList.ItemsSource = settings.MonitorProfiles.ToList(); // ToList creates a fresh snapshot for UI binding
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string profileName = NewProfileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(profileName))
        {
            MessageBox.Show("Please enter a name for the profile.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            App.MonitorProfileService.SaveCurrentProfile(profileName);
            NewProfileNameBox.Text = "";
            LoadProfiles();
            
            MessageBox.Show($"Current layout saved as '{profileName}'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string profileName)
        {
            try
            {
                bool success = App.MonitorProfileService.ApplyProfile(profileName);
                if (success)
                {
                    MessageBox.Show($"Profile '{profileName}' applied successfully. Your monitors should now be reconfigured.", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to apply profile '{profileName}'. Check the logs.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply profile '{profileName}': {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string profileName)
        {
            var result = MessageBox.Show($"Are you sure you want to delete the profile '{profileName}'?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                App.MonitorProfileService.DeleteProfile(profileName);
                LoadProfiles();
            }
        }
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Currently no detail view, but we could expand this to show specific monitor resolutions in a side panel
    }
}
