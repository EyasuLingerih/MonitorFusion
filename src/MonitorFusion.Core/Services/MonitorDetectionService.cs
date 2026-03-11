using System.Runtime.InteropServices;
using MonitorFusion.Core.Models;

namespace MonitorFusion.Core.Services;

/// <summary>
/// Detects and enumerates all connected monitors using Win32 APIs.
/// This is the foundation service — everything else depends on knowing
/// which monitors are connected and their properties.
/// </summary>
public class MonitorDetectionService
{
    #region Win32 P/Invoke Declarations

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip,
        MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice, uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(
        string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName, ref DEVMODE lpDevMode,
        IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName, IntPtr lpDevMode,
        IntPtr hwnd, uint dwflags, IntPtr lParam);

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    // Constants
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
    
    // ChangeDisplaySettingsEx flags
    public const uint CDS_UPDATEREGISTRY = 0x01;
    public const uint CDS_TEST = 0x02;
    public const uint CDS_FULLSCREEN = 0x04;
    public const uint CDS_GLOBAL = 0x08;
    public const uint CDS_SET_PRIMARY = 0x10;
    public const uint CDS_NORESET = 0x10000000;

    // DEVMODE field flags
    public const int DM_POSITION = 0x00000020;
    public const int DM_DISPLAYORIENTATION = 0x00000080;
    public const int DM_BITSPERPEL = 0x00040000;
    public const int DM_PELSWIDTH = 0x00080000;
    public const int DM_PELSHEIGHT = 0x00100000;
    public const int DM_DISPLAYFREQUENCY = 0x00400000;

    #endregion

    #region Win32 Structures

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        // ... additional fields omitted for brevity
    }

    #endregion

    /// <summary>
    /// Gets all currently connected monitors with their properties.
    /// </summary>
    public List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        try
        {
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var monitorInfoEx = new MONITORINFOEX();
                monitorInfoEx.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (GetMonitorInfo(hMonitor, ref monitorInfoEx))
                {
                    var monitor = new MonitorInfo
                    {
                        DeviceId = monitorInfoEx.szDevice,
                        IsPrimary = (monitorInfoEx.dwFlags & 1) != 0, // MONITORINFOF_PRIMARY
                        Index = index++,
                        Bounds = new ScreenRect
                        {
                            Left = monitorInfoEx.rcMonitor.Left,
                            Top = monitorInfoEx.rcMonitor.Top,
                            Right = monitorInfoEx.rcMonitor.Right,
                            Bottom = monitorInfoEx.rcMonitor.Bottom
                        },
                        WorkArea = new ScreenRect
                        {
                            Left = monitorInfoEx.rcWork.Left,
                            Top = monitorInfoEx.rcWork.Top,
                            Right = monitorInfoEx.rcWork.Right,
                            Bottom = monitorInfoEx.rcWork.Bottom
                        }
                    };

                    // Get friendly name from display device
                    var adapterDevice = new DISPLAY_DEVICE();
                    adapterDevice.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                    // First pass: get the display adapter for this target device (e.g. \\.\DISPLAY1)
                    if (EnumDisplayDevices(monitorInfoEx.szDevice, 0, ref adapterDevice, 0))
                    {
                        var monitorDevice = new DISPLAY_DEVICE();
                        monitorDevice.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                        // Second pass: query the adapter's specific attached monitor (index 0)
                        if (EnumDisplayDevices(adapterDevice.DeviceName, 0, ref monitorDevice, EDD_GET_DEVICE_INTERFACE_NAME))
                        {
                            // The true hardware name is in DeviceString here
                            monitor.FriendlyName = string.IsNullOrWhiteSpace(monitorDevice.DeviceString) 
                                ? adapterDevice.DeviceString 
                                : monitorDevice.DeviceString;

                            // Clean up "Generic PnP Monitor" if a better name is available from EDID later,
                            // but this inner call usually reliably returns the real name.
                        }
                        else
                        {
                            // Fallback to adapter name (which might be "Generic PnP")
                            monitor.FriendlyName = adapterDevice.DeviceString;
                        }
                    }
                    else
                    {
                        monitor.FriendlyName = $"Display {index}";
                    }

                    // Get refresh rate and color depth from current display settings
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (EnumDisplaySettings(monitorInfoEx.szDevice, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        monitor.RefreshRate = devMode.dmDisplayFrequency;
                        monitor.BitsPerPixel = devMode.dmBitsPerPel;
                        monitor.Orientation = devMode.dmDisplayOrientation;
                    }

                    monitors.Add(monitor);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAllMonitors failed: {ex.Message}. Last Win32 Error: {Marshal.GetLastWin32Error()}");
        }

        return monitors.OrderBy(m => m.Index).ToList();
    }

    /// <summary>
    /// Gets available display modes (resolutions) for a specific monitor.
    /// Useful for the Monitor Profile settings UI.
    /// </summary>
    public List<DisplayMode> GetAvailableModes(string deviceId)
    {
        var modes = new List<DisplayMode>();
        var devMode = new DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

        int modeIndex = 0;
        while (EnumDisplaySettings(deviceId, modeIndex++, ref devMode))
        {
            var mode = new DisplayMode
            {
                Width = devMode.dmPelsWidth,
                Height = devMode.dmPelsHeight,
                RefreshRate = devMode.dmDisplayFrequency,
                BitsPerPixel = devMode.dmBitsPerPel
            };

            // Avoid duplicates
            if (!modes.Any(m => m.Width == mode.Width &&
                                m.Height == mode.Height &&
                                m.RefreshRate == mode.RefreshRate))
            {
                modes.Add(mode);
            }
        }

        return modes
            .OrderByDescending(m => m.Width)
            .ThenByDescending(m => m.Height)
            .ThenByDescending(m => m.RefreshRate)
            .ToList();
    }

    /// <summary>
    /// Finds which monitor contains the given screen coordinates.
    /// </summary>
    public MonitorInfo? GetMonitorAt(int x, int y)
    {
        return GetAllMonitors().FirstOrDefault(m => m.Bounds.Contains(x, y));
    }

    /// <summary>
    /// Gets the primary monitor.
    /// </summary>
    public MonitorInfo? GetPrimaryMonitor()
    {
        return GetAllMonitors().FirstOrDefault(m => m.IsPrimary);
    }

    /// <summary>
    /// Gets the current display configuration (DEVMODE) for a specific device.
    /// </summary>
    public bool GetCurrentDisplayConfig(string deviceName, out DEVMODE devMode)
    {
        devMode = new DEVMODE();
        devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        return EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode);
    }
}

/// <summary>
/// Represents an available display mode for a monitor.
/// </summary>
public class DisplayMode
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }

    public override string ToString()
        => $"{Width}x{Height} @ {RefreshRate}Hz ({BitsPerPixel}bpp)";
}
