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
    /// Applies a saved MonitorProfile using Windows display APIs.
    /// Returns (true, "") on success or (false, user-friendly message) on failure.
    /// </summary>
    public (bool Success, string Message) ApplyProfile(string profileName)
    {
        var settings = _settingsService.Load();
        var profile = settings.MonitorProfiles.FirstOrDefault(p => p.Name == profileName);
        if (profile == null)
            return (false, $"Profile '{profileName}' was not found.");

        string errorMessage = string.Empty;
        bool allSuccessful = true;

        foreach (var display in profile.Displays)
        {
            if (!_monitorService.GetCurrentDisplayConfig(display.DeviceId, out var devMode))
            {
                errorMessage = $"Monitor '{display.DeviceId}' is not currently connected. Make sure all monitors from this profile are plugged in.";
                allSuccessful = false;
                break;
            }

            devMode.dmPelsWidth          = display.Width;
            devMode.dmPelsHeight         = display.Height;
            devMode.dmDisplayFrequency   = display.RefreshRate;
            devMode.dmBitsPerPel         = display.BitsPerPixel;
            devMode.dmDisplayOrientation = display.Orientation;
            devMode.dmPositionX          = display.PositionX;
            devMode.dmPositionY          = display.PositionY;
            devMode.dmFields = MonitorDetectionService.DM_PELSWIDTH |
                               MonitorDetectionService.DM_PELSHEIGHT |
                               MonitorDetectionService.DM_DISPLAYFREQUENCY |
                               MonitorDetectionService.DM_BITSPERPEL |
                               MonitorDetectionService.DM_DISPLAYORIENTATION |
                               MonitorDetectionService.DM_POSITION;

            uint flags = MonitorDetectionService.CDS_UPDATEREGISTRY | MonitorDetectionService.CDS_NORESET;
            if (display.IsPrimary) flags |= MonitorDetectionService.CDS_SET_PRIMARY;

            int result = MonitorDetectionService.ChangeDisplaySettingsEx(
                display.DeviceId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);

            if (result != 0)
            {
                allSuccessful = false;
                errorMessage = result switch
                {
                     1 => $"A system restart is required to apply settings for '{display.DeviceId}'.",
                    -1 => $"Windows rejected the settings for '{display.DeviceId}'. Try a different resolution.",
                    -2 => $"{display.Width}×{display.Height} @ {display.RefreshRate} Hz is not supported by this monitor.",
                    -4 => $"Settings could not be saved to the registry for '{display.DeviceId}'.",
                    _  => $"Unexpected error (code {result}) applying settings for '{display.DeviceId}'."
                };
                System.Diagnostics.Debug.WriteLine($"[MonitorProfile] {errorMessage}");
            }
        }

        if (allSuccessful)
        {
            int commitResult = MonitorDetectionService.ChangeDisplaySettingsEx(
                null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (commitResult != 0)
                return commitResult == 1
                    ? (false, "Settings applied but a restart is required to take full effect.")
                    : (false, $"Settings queued but could not be committed (error {commitResult}).");

            settings.ActiveMonitorProfile = profileName;
            _settingsService.Save(settings);
        }

        return allSuccessful ? (true, string.Empty) : (false, errorMessage);
    }

    /// <summary>
    /// Applies display settings for a single monitor immediately.
    /// Returns (true, "") on success or (false, user-friendly message) on failure.
    /// </summary>
    public (bool Success, string Message) ApplyMonitorSettings(
        string deviceId, int width, int height, int refreshRate, int orientation, bool setPrimary)
    {
        if (!_monitorService.GetCurrentDisplayConfig(deviceId, out var devMode))
            return (false, $"Monitor '{deviceId}' is not currently connected.");

        devMode.dmPelsWidth          = width;
        devMode.dmPelsHeight         = height;
        devMode.dmDisplayFrequency   = refreshRate;
        devMode.dmDisplayOrientation = orientation;
        devMode.dmFields = MonitorDetectionService.DM_PELSWIDTH   |
                           MonitorDetectionService.DM_PELSHEIGHT  |
                           MonitorDetectionService.DM_DISPLAYFREQUENCY |
                           MonitorDetectionService.DM_DISPLAYORIENTATION |
                           MonitorDetectionService.DM_POSITION;

        uint flags = MonitorDetectionService.CDS_UPDATEREGISTRY | MonitorDetectionService.CDS_NORESET;
        if (setPrimary) flags |= MonitorDetectionService.CDS_SET_PRIMARY;

        int result = MonitorDetectionService.ChangeDisplaySettingsEx(
            deviceId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);

        if (result != 0)
        {
            string errorMessage = result switch
            {
                 1 => "A system restart is required to apply these settings.",
                -1 => "Windows rejected these settings. Try a different resolution or refresh rate.",
                -2 => $"{width}×{height} @ {refreshRate} Hz is not supported by this monitor.",
                -4 => "Settings could not be saved to the registry.",
                _  => $"Unexpected error (code {result}) applying settings."
            };
            return (false, errorMessage);
        }

        // Commit the change
        int commitResult = MonitorDetectionService.ChangeDisplaySettingsEx(
            null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        if (commitResult != 0)
            return commitResult == 1
                ? (false, "Settings applied but a restart is required to take full effect.")
                : (false, $"Settings queued but could not be committed (error {commitResult}).");

        return (true, string.Empty);
    }

    /// <summary>
    /// Repositions monitors without changing any other settings.
    /// Positions are in virtual desktop coordinates; the caller should
    /// normalise so the minimum X and Y are 0.
    /// </summary>
    public (bool Success, string Message) ApplyMonitorPositions(
        List<(string DeviceId, int X, int Y)> positions)
    {
        foreach (var (deviceId, x, y) in positions)
        {
            if (!_monitorService.GetCurrentDisplayConfig(deviceId, out var devMode))
                return (false, $"Monitor '{deviceId}' is not currently connected.");

            devMode.dmPositionX = x;
            devMode.dmPositionY = y;
            devMode.dmFields    = MonitorDetectionService.DM_POSITION;

            uint flags = MonitorDetectionService.CDS_UPDATEREGISTRY |
                         MonitorDetectionService.CDS_NORESET;

            int result = MonitorDetectionService.ChangeDisplaySettingsEx(
                deviceId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);

            if (result != 0 && result != 1)
                return (false, $"Failed to reposition monitor (error {result}).");
        }

        int commit = MonitorDetectionService.ChangeDisplaySettingsEx(
            null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);

        return commit == 0 || commit == 1
            ? (true, string.Empty)
            : (false, $"Could not commit layout (error {commit}).");
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
