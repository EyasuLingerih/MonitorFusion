using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MonitorFusion.Core.Models;

namespace MonitorFusion.Core.Services;

/// <summary>
/// Handles window movement, snapping, and position management across monitors.
/// This is the core of Phase 3 functionality.
/// </summary>
public class WindowManagementService
{
    private readonly MonitorDetectionService _monitorService;

    public WindowManagementService(MonitorDetectionService monitorService)
    {
        _monitorService = monitorService;
    }

    #region Win32 P/Invoke

    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private static string GetExecutablePathRobust(uint processId)
    {
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                uint capacity = 1024;
                var sb = new StringBuilder((int)capacity);
                if (QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        return string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

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

    // Constants
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_VISIBLE = 0x10000000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const int VK_SHIFT = 0x10;

    #endregion

    private IntPtr _winEventHook;
    private WinEventDelegate? _winEventProc;
    private SnappingSettings _settings = new();

    /// <summary>
    /// Starts the background window management loops/hooks.
    /// Call this from App startup or MainWindow loaded.
    /// </summary>
    public void StartBackgroundServices(SnappingSettings settings)
    {
        ReloadSettings(settings);

        // Prevent garbage collection of the delegate
        _winEventProc = new WinEventDelegate(WinEventProc);
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _winEventProc,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void StopBackgroundServices()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    public void ReloadSettings(SnappingSettings settings)
    {
        if (settings != null)
        {
            _settings = settings;
        }
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_settings.Enabled || hwnd == IntPtr.Zero) return;

        // Check if Shift is held down and Bypass is enabled
        if (_settings.BypassWithShift)
        {
            short shiftState = GetAsyncKeyState(VK_SHIFT);
            if ((shiftState & 0x8000) != 0) return; // Shift is down
        }

        // Check ignored processes
        if (_settings.IgnoredProcesses.Count > 0)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                var process = Process.GetProcessById((int)pid);
                if (_settings.IgnoredProcesses.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch { /* Process might have exited */ }
        }

        GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        int newLeft = rect.Left;
        int newTop = rect.Top;
        bool snapped = false;

        // Snapping to monitor edges
        if (_settings.SnapToMonitorEdges)
        {
            var monitors = _monitorService.GetAllMonitors();
            int snapDist = _settings.SnapDistance;

            foreach (var monitor in monitors)
            {
                // Left edge snap
                if (Math.Abs(rect.Left - monitor.Bounds.Left) < snapDist) { newLeft = monitor.Bounds.Left; snapped = true; }
                else if (Math.Abs(rect.Right - monitor.Bounds.Left) < snapDist) { newLeft = monitor.Bounds.Left - width; snapped = true; }
                
                // Right edge snap
                if (Math.Abs(rect.Right - monitor.Bounds.Right) < snapDist) { newLeft = monitor.Bounds.Right - width; snapped = true; }
                else if (Math.Abs(rect.Left - monitor.Bounds.Right) < snapDist) { newLeft = monitor.Bounds.Right; snapped = true; }
                
                // Top edge snap
                if (Math.Abs(rect.Top - monitor.Bounds.Top) < snapDist) { newTop = monitor.Bounds.Top; snapped = true; }
                else if (Math.Abs(rect.Bottom - monitor.Bounds.Top) < snapDist) { newTop = monitor.Bounds.Top - height; snapped = true; }
                
                // Bottom edge snap
                if (Math.Abs(rect.Bottom - monitor.Bounds.Bottom) < snapDist) { newTop = monitor.Bounds.Bottom - height; snapped = true; }
                else if (Math.Abs(rect.Top - monitor.Bounds.Bottom) < snapDist) { newTop = monitor.Bounds.Bottom; snapped = true; }
            }
        }

        // Apply snapping
        if (snapped)
        {
            SetWindowPos(hwnd, IntPtr.Zero, newLeft, newTop, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }


    /// <summary>
    /// Moves the currently focused window to the next monitor.
    /// </summary>
    public void MoveActiveWindowToNextMonitor()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var monitors = _monitorService.GetAllMonitors();
        if (monitors.Count < 2) return;

        GetWindowRect(hwnd, out var windowRect);
        var currentMonitor = _monitorService.GetMonitorAt(
            (windowRect.Left + windowRect.Right) / 2,
            (windowRect.Top + windowRect.Bottom) / 2);

        if (currentMonitor == null) return;

        int nextIndex = (currentMonitor.Index + 1) % monitors.Count;
        var nextMonitor = monitors[nextIndex];

        MoveWindowToMonitor(hwnd, windowRect, currentMonitor, nextMonitor);
    }

    /// <summary>
    /// Moves the currently focused window to the previous monitor.
    /// </summary>
    public void MoveActiveWindowToPreviousMonitor()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var monitors = _monitorService.GetAllMonitors();
        if (monitors.Count < 2) return;

        GetWindowRect(hwnd, out var windowRect);
        var currentMonitor = _monitorService.GetMonitorAt(
            (windowRect.Left + windowRect.Right) / 2,
            (windowRect.Top + windowRect.Bottom) / 2);

        if (currentMonitor == null) return;

        int prevIndex = (currentMonitor.Index - 1 + monitors.Count) % monitors.Count;
        var prevMonitor = monitors[prevIndex];

        MoveWindowToMonitor(hwnd, windowRect, currentMonitor, prevMonitor);
    }

    /// <summary>
    /// Moves a window from one monitor to another, maintaining relative position.
    /// </summary>
    private void MoveWindowToMonitor(IntPtr hwnd, RECT windowRect,
        MonitorInfo fromMonitor, MonitorInfo toMonitor)
    {
        int windowWidth = windowRect.Right - windowRect.Left;
        int windowHeight = windowRect.Bottom - windowRect.Top;

        // Calculate relative position on source monitor
        float relX = (float)(windowRect.Left - fromMonitor.Bounds.Left) / fromMonitor.Width;
        float relY = (float)(windowRect.Top - fromMonitor.Bounds.Top) / fromMonitor.Height;

        // Apply relative position to target monitor
        int newX = toMonitor.Bounds.Left + (int)(relX * toMonitor.Width);
        int newY = toMonitor.Bounds.Top + (int)(relY * toMonitor.Height);

        // Ensure window fits within target monitor
        newX = Math.Max(toMonitor.Bounds.Left,
            Math.Min(newX, toMonitor.Bounds.Right - windowWidth));
        newY = Math.Max(toMonitor.Bounds.Top,
            Math.Min(newY, toMonitor.Bounds.Bottom - windowHeight));

        SetWindowPos(hwnd, IntPtr.Zero, newX, newY, windowWidth, windowHeight,
            SWP_NOZORDER | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// Centers the active window on its current monitor.
    /// </summary>
    public void CenterActiveWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        var monitor = _monitorService.GetMonitorAt(
            (rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        if (monitor == null) return;

        int x = monitor.WorkArea.Left + (monitor.WorkArea.Width - width) / 2;
        int y = monitor.WorkArea.Top + (monitor.WorkArea.Height - height) / 2;

        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER);
    }

    /// <summary>
    /// Spans the active window across all monitors.
    /// </summary>
    public void SpanActiveWindowAcrossMonitors()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var monitors = _monitorService.GetAllMonitors();
        if (monitors.Count == 0) return;

        int left = monitors.Min(m => m.Bounds.Left);
        int top = monitors.Min(m => m.Bounds.Top);
        int right = monitors.Max(m => m.Bounds.Right);
        int bottom = monitors.Max(m => m.Bounds.Bottom);

        // Restore if maximized first
        ShowWindow(hwnd, SW_RESTORE);

        SetWindowPos(hwnd, IntPtr.Zero, left, top,
            right - left, bottom - top, SWP_NOZORDER | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Gets all visible application windows (for taskbar display).
    /// </summary>
    public List<WindowInfo> GetAllVisibleWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            int style = GetWindowLong(hwnd, GWL_STYLE);
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Skip tool windows and windows without app window style
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

            if (isToolWindow && !isAppWindow) return true;

            var title = new StringBuilder(256);
            GetWindowText(hwnd, title, 256);
            if (string.IsNullOrWhiteSpace(title.ToString())) return true;

            var className = new StringBuilder(256);
            GetClassName(hwnd, className, 256);

            GetWindowThreadProcessId(hwnd, out uint processId);
            GetWindowRect(hwnd, out var rect);

            string processName = "";
            string executablePath = GetExecutablePathRobust(processId);
            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
                
                // Fallback if QueryFullProcessImageName failed
                if (string.IsNullOrEmpty(executablePath))
                {
                    try
                    {
                        executablePath = process.MainModule?.FileName ?? "";
                    }
                    catch { /* Some 64-bit or protected processes throw AccessDenied here */ }
                }
            }
            catch { /* Process vanished or access denied */ }

            // Skip common system processes that shouldn't be saved/relaunched
            if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("SystemSettings", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var monitor = _monitorService.GetMonitorAt(
                (rect.Left + rect.Right) / 2,
                (rect.Top + rect.Bottom) / 2);

            windows.Add(new WindowInfo
            {
                Handle = hwnd,
                Title = title.ToString(),
                ClassName = className.ToString(),
                ProcessName = processName,
                ExecutablePath = executablePath,
                ProcessId = processId,
                Bounds = new ScreenRect
                {
                    Left = rect.Left, Top = rect.Top,
                    Right = rect.Right, Bottom = rect.Bottom
                },
                MonitorDeviceId = monitor?.DeviceId ?? ""
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Saves current positions of all visible windows.
    /// </summary>
    public WindowPositionProfile SaveCurrentPositions(string profileName)
    {
        var profile = new WindowPositionProfile { Name = profileName };
        var windows = GetAllVisibleWindows();

        foreach (var win in windows)
        {
            profile.Windows.Add(new SavedWindowPosition
            {
                ProcessName = win.ProcessName,
                ExecutablePath = win.ExecutablePath,
                WindowTitle = win.Title,
                ClassName = win.ClassName,
                Left = win.Bounds.Left,
                Top = win.Bounds.Top,
                Width = win.Bounds.Width,
                Height = win.Bounds.Height,
                MonitorDeviceId = win.MonitorDeviceId
            });
        }

        return profile;
    }

    /// <summary>
    /// Restores window positions from a saved profile, optionally launching missing applications.
    /// </summary>
    public async Task RestorePositionsAsync(WindowPositionProfile profile)
    {
        var currentWindows = GetAllVisibleWindows();
        bool anyLaunched = false;

        // Pass 1: Launch missing apps
        foreach (var saved in profile.Windows)
        {
            if (string.IsNullOrWhiteSpace(saved.ExecutablePath)) continue;

            // Check if it's already running (by exact exe path or process name)
            bool isRunning = currentWindows.Any(w => w.ExecutablePath.Equals(saved.ExecutablePath, StringComparison.OrdinalIgnoreCase) || 
                                                     w.ProcessName.Equals(saved.ProcessName, StringComparison.OrdinalIgnoreCase));

            if (!isRunning)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = saved.ExecutablePath,
                        UseShellExecute = true
                    });
                    anyLaunched = true;
                }
                catch { /* Access denied or file missing */ }
            }
        }

        // Wait for apps to launch
        if (anyLaunched)
        {
            await Task.Delay(3000);
            currentWindows = GetAllVisibleWindows(); // Refresh the list
        }

        // Pass 2: Position all apps
        foreach (var saved in profile.Windows)
        {
            // Find matching window by process name and class
            var match = currentWindows.FirstOrDefault(w =>
                w.ProcessName == saved.ProcessName &&
                w.ClassName == saved.ClassName);

            // Fallback to process name only if the main window class changed
            if (match == null)
            {
                match = currentWindows.FirstOrDefault(w => w.ProcessName == saved.ProcessName);
            }

            if (match != null)
            {
                // Unminimize if it was minimized by the system
                ShowWindow(match.Handle, SW_RESTORE);

                SetWindowPos(match.Handle, IntPtr.Zero,
                    saved.Left, saved.Top, saved.Width, saved.Height,
                    SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }
    }
}

/// <summary>
/// Information about a visible window.
/// </summary>
public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public uint ProcessId { get; set; }
    public ScreenRect Bounds { get; set; } = new();
    public string MonitorDeviceId { get; set; } = string.Empty;
}
