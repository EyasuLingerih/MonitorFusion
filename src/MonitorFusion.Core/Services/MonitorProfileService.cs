using MonitorFusion.Core.Models;

namespace MonitorFusion.Core.Services;

public class MonitorProfileService
{
    private readonly MonitorDetectionService _monitorService;
    private readonly SettingsService _settingsService;

    public MonitorProfileService(MonitorDetectionService monitorService, SettingsService settingsService)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Reads the current physical setup and saves it as a MonitorProfile.
    /// </summary>
    public MonitorProfile SaveCurrentProfile(string profileName)
    {
        var profile = new MonitorProfile { Name = profileName };
        var monitors = _monitorService.GetAllMonitors();

        foreach (var monitor in monitors)
        {
            if (_monitorService.GetCurrentDisplayConfig(monitor.DeviceId, out var devMode))
            {
                var config = new MonitorDisplayConfig
                {
                    DeviceId = monitor.DeviceId,
                    IsPrimary = monitor.IsPrimary,
                    Width = devMode.dmPelsWidth,
                    Height = devMode.dmPelsHeight,
                    RefreshRate = devMode.dmDisplayFrequency,
                    BitsPerPixel = devMode.dmBitsPerPel,
                    Orientation = devMode.dmDisplayOrientation,
                    PositionX = devMode.dmPositionX,
                    PositionY = devMode.dmPositionY
                };
                profile.Displays.Add(config);
            }
        }

        var settings = _settingsService.Load();
        
        var existingIndex = settings.MonitorProfiles.FindIndex(p => p.Name == profileName);
        if (existingIndex >= 0)
        {
            settings.MonitorProfiles[existingIndex] = profile;
        }
        else
        {
            settings.MonitorProfiles.Add(profile);
        }

        settings.ActiveMonitorProfile = profileName;
        _settingsService.Save(settings);

        return profile;
    }

    /// <summary>
    /// Applies a saved MonitorProfile using Windows display APIs to adjust resolution, position, etc.
    /// </summary>
    public bool ApplyProfile(string profileName)
    {
        var settings = _settingsService.Load();
        var profile = settings.MonitorProfiles.FirstOrDefault(p => p.Name == profileName);
        if (profile == null) return false;

        bool allSuccessful = true;

        foreach (var display in profile.Displays)
        {
            // Get current DEVMODE struct shape to populate safely
            if (_monitorService.GetCurrentDisplayConfig(display.DeviceId, out var devMode))
            {
                devMode.dmPelsWidth = display.Width;
                devMode.dmPelsHeight = display.Height;
                devMode.dmDisplayFrequency = display.RefreshRate;
                devMode.dmBitsPerPel = display.BitsPerPixel;
                devMode.dmDisplayOrientation = display.Orientation;
                devMode.dmPositionX = display.PositionX;
                devMode.dmPositionY = display.PositionY;

                // Tell the API which fields are actually valid
                devMode.dmFields = MonitorDetectionService.DM_PELSWIDTH | 
                                   MonitorDetectionService.DM_PELSHEIGHT |
                                   MonitorDetectionService.DM_DISPLAYFREQUENCY |
                                   MonitorDetectionService.DM_BITSPERPEL |
                                   MonitorDetectionService.DM_DISPLAYORIENTATION |
                                   MonitorDetectionService.DM_POSITION;

                // Queue changes into registry but do not broadcast yet (multimonitor safe)
                uint flags = MonitorDetectionService.CDS_UPDATEREGISTRY | MonitorDetectionService.CDS_NORESET;
                if (display.IsPrimary)
                {
                    flags |= MonitorDetectionService.CDS_SET_PRIMARY;
                }

                int result = MonitorDetectionService.ChangeDisplaySettingsEx(display.DeviceId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
                
                if (result != 0) // DISP_CHANGE_SUCCESSFUL is 0
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to queue apply display settings for {display.DeviceId}. Result: {result}");
                    allSuccessful = false;
                }
            }
        }

        if (allSuccessful)
        {
            // Broadcast all queued changes simultaneously to Windows
            int commitResult = MonitorDetectionService.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (commitResult != 0)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to commit display settings. Result: {commitResult}");
                allSuccessful = false;
            }
            else
            {
                settings.ActiveMonitorProfile = profileName;
                _settingsService.Save(settings);
            }
        }

        return allSuccessful;
    }

    public void DeleteProfile(string profileName)
    {
        var settings = _settingsService.Load();
        var profile = settings.MonitorProfiles.FirstOrDefault(p => p.Name == profileName);
        if (profile != null)
        {
            settings.MonitorProfiles.Remove(profile);
            _settingsService.Save(settings);
        }
    }
}
