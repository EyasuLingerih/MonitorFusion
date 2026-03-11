using System.Text.Json;
using System.Text.Json.Serialization;
using MonitorFusion.Core.Models;

namespace MonitorFusion.Core.Services;

/// <summary>
/// Manages loading, saving, and migrating application settings.
/// Settings are stored as JSON in %APPDATA%/MonitorFusion/settings.json
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsDir;
    private readonly string _settingsPath;
    private AppSettings? _cached;

    public SettingsService()
    {
        _settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MonitorFusion");
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    /// <summary>
    /// Loads settings from disk, or creates defaults if not found.
    /// </summary>
    public AppSettings Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _cached = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                          ?? new AppSettings();
            }
            else
            {
                _cached = CreateDefaults();
                Save(_cached);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            _cached = CreateDefaults();
        }

        return _cached;
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
            _cached = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports settings to a file for backup/transfer.
    /// </summary>
    public void Export(string exportPath)
    {
        var settings = Load();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(exportPath, json);
    }

    /// <summary>
    /// Imports settings from a backup file.
    /// </summary>
    public AppSettings Import(string importPath)
    {
        var json = File.ReadAllText(importPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                       ?? new AppSettings();
        Save(settings);
        return settings;
    }

    /// <summary>
    /// Resets to default settings.
    /// </summary>
    public AppSettings Reset()
    {
        var defaults = CreateDefaults();
        Save(defaults);
        return defaults;
    }

    /// <summary>
    /// Forces a reload from disk (clears cache).
    /// </summary>
    public AppSettings Reload()
    {
        _cached = null;
        return Load();
    }

    private AppSettings CreateDefaults()
    {
        return new AppSettings
        {
            General = new GeneralSettings(),
            Snapping = new SnappingSettings(),
            Hotkeys = new HotkeySettings(),
            Taskbar = new TaskbarSettings { Enabled = false },
            WallpaperProfiles = new List<WallpaperProfile>
            {
                new() { Name = "Default" }
            },
            MonitorProfiles = new List<MonitorProfile>
            {
                new() { Name = "Default" }
            },
            WindowProfiles = new List<WindowPositionProfile>()
        };
    }
}
