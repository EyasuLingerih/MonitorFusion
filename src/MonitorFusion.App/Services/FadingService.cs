using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using MonitorFusion.App.Views;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Services;

/// <summary>
/// Service responsible for intercepting foreground window changes and applying transparent
/// dimming overlays to inactive monitors.
/// </summary>
public class FadingService
{
    private readonly MonitorDetectionService _monitorService;
    private readonly SettingsService _settingsService;
    private readonly Dictionary<string, FadingWindow> _fadingWindows = new();
    private bool _isRunning;

    // --- Win32 Interop ---

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

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
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    private WinEventDelegate? _winEventDelegate;
    private IntPtr _hHook;

    public FadingService(MonitorDetectionService monitorService, SettingsService settingsService)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        
        SystemEvents.DisplaySettingsChanged += (s, e) => {
            if (_isRunning)
            {
                Application.Current.Dispatcher.Invoke(RefreshWindows);
            }
        };
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        
        RefreshWindows();

        // Hook the OS foreground window change event (when user switches apps)
        _winEventDelegate = new WinEventDelegate(WinEventProc);
        _hHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        // Release the hook immediately so we don't leak OS resources
        if (_hHook != IntPtr.Zero)
        {
            UnhookWinEvent(_hHook);
            _hHook = IntPtr.Zero;
        }

        ClearWindows();
    }

    public void ReloadSettings()
    {
        Stop();
        
        var settings = _settingsService.Load();
        if (settings.Fading.Enabled)
        {
            Start();
        }
    }

    private void ClearWindows()
    {
        foreach (var window in _fadingWindows.Values)
        {
            window.Close();
        }
        _fadingWindows.Clear();
    }

    private void RefreshWindows()
    {
        var settings = _settingsService.Load().Fading;
        if (!settings.Enabled)
        {
            Stop();
            return;
        }

        var monitors = _monitorService.GetAllMonitors();

        // Update existing windows opacity or create new ones
        foreach (var monitor in monitors)
        {
            string key = $"{monitor.Bounds.Left}_{monitor.Bounds.Top}_{monitor.Bounds.Width}_{monitor.Bounds.Height}";

            if (!_fadingWindows.ContainsKey(key))
            {
                var fader = new FadingWindow(monitor, settings);
                fader.Show();
                fader.Visibility = Visibility.Hidden; // start hidden to avoid flashing
                _fadingWindows[key] = fader;
            }
            else
            {
                var fader = _fadingWindows[key];
                fader.Opacity = settings.Opacity;
                
                // Dimensions already match by definition of the key, just ensure active
                fader.Left = monitor.Bounds.Left;
                fader.Top = monitor.Bounds.Top;
                fader.Width = monitor.Bounds.Width;
                fader.Height = monitor.Bounds.Height;
            }
        }

        // Remove old monitors that disconnected
        var currentKeys = monitors.Select(m => $"{m.Bounds.Left}_{m.Bounds.Top}_{m.Bounds.Width}_{m.Bounds.Height}").ToHashSet();
        var toRemove = _fadingWindows.Keys.Where(k => !currentKeys.Contains(k)).ToList();
        foreach (var key in toRemove)
        {
            _fadingWindows[key].Close();
            _fadingWindows.Remove(key);
        }

        // Apply immediately to current state
        UpdateFadingState(GetForegroundWindow());
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            UpdateFadingState(hwnd);
        }
    }

    private void UpdateFadingState(IntPtr foregroundHwnd)
    {
        var settings = _settingsService.Load().Fading;
        if (!settings.Enabled || !_isRunning) return;

        // Find which monitor bounds currently contain the active foreground application
        string activeBoundsKey = GetMonitorBoundsKeyFromWindow(foregroundHwnd);

        foreach (var kvp in _fadingWindows)
        {
            var boundsKey = kvp.Key;
            var window = kvp.Value;

            if (settings.Mode == "InactiveMonitors" || settings.Mode == "AllExceptActiveWindow")
            {
                if (boundsKey != activeBoundsKey && !string.IsNullOrEmpty(activeBoundsKey))
                {
                    window.Visibility = Visibility.Visible;
                }
                else
                {
                    window.Visibility = Visibility.Hidden;
                }
            }
        }
    }

    private string GetMonitorBoundsKeyFromWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;

        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (hMonitor != IntPtr.Zero)
        {
            MONITORINFOEX mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                int width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                int height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                return $"{mi.rcMonitor.Left}_{mi.rcMonitor.Top}_{width}_{height}";
            }
        }
        return string.Empty;
    }
}
