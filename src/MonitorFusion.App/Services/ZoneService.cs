using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MonitorFusion.App.Views;
using MonitorFusion.Core.Models;
using MonitorFusion.Core.Services;

namespace MonitorFusion.App.Services;

/// <summary>
/// Manages zone-based window snapping. Hooks window drag events, shows an overlay
/// highlighting available zones, and snaps the released window into the hovered zone.
/// Also manages optional per-zone taskbar windows.
/// </summary>
public class ZoneService : IDisposable
{
    private readonly MonitorDetectionService _monitorService;
    private readonly SettingsService _settingsService;
    private ZoneSettings _settings = new();

    // ── Win32 hooks ────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    private const uint EVENT_SYSTEM_MOVESIZEEND   = 0x000B;
    private const uint WINEVENT_OUTOFCONTEXT       = 0;
    private const uint SWP_NOZORDER    = 0x0004;
    private const uint SWP_NOACTIVATE  = 0x0010;
    private const uint SWP_SHOWWINDOW  = 0x0040;
    private const int  VK_SHIFT = 0x10;
    private const int  VK_CONTROL = 0x11;

    // ── State ──────────────────────────────────────────────────────────────────
    private IntPtr _hookMoveSize = IntPtr.Zero;
    private WinEventDelegate? _procMoveSize;
    private IntPtr _draggingHwnd;
    private ZoneDefinition? _hoveredZone;
    private MonitorInfo? _hoveredMonitor;

    // ── Overlay & taskbar windows ──────────────────────────────────────────────
    private readonly Dictionary<string, ZoneOverlayWindow>  _overlays  = new();
    private readonly Dictionary<string, ZoneTaskbarWindow>  _taskbars  = new();
    private DispatcherTimer? _cursorTimer;

    public ZoneService(MonitorDetectionService monitorService, SettingsService settingsService)
    {
        _monitorService  = monitorService;
        _settingsService = settingsService;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public void Start()
    {
        ReloadSettings();
        _procMoveSize = MoveSizeProc;
        _hookMoveSize = SetWinEventHook(
            EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _procMoveSize, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (_settings.ShowZoneTaskbars)
            RefreshTaskbars();
    }

    public void Stop()
    {
        if (_hookMoveSize != IntPtr.Zero)
        {
            UnhookWinEvent(_hookMoveSize);
            _hookMoveSize = IntPtr.Zero;
        }
        HideAllOverlays();
        foreach (var ov in _overlays.Values) ov.Close();
        _overlays.Clear();
        CloseAllTaskbars();
    }

    public void ReloadSettings()
    {
        _settings = _settingsService.Load().Zones;

        if (_hookMoveSize == IntPtr.Zero) return; // not yet started

        if (_settings.ShowZoneTaskbars)
            RefreshTaskbars();
        else
            CloseAllTaskbars();
    }

    public void Dispose() => Stop();

    // ── Hook callback ──────────────────────────────────────────────────────────

    private void MoveSizeProc(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || !_settings.Enabled) return;

        if (eventType == EVENT_SYSTEM_MOVESIZESTART)
        {
            _draggingHwnd = hwnd;

            if (_settings.ShowOverlayOnDrag && HasAnyLayout())
            {
                Application.Current?.Dispatcher.BeginInvoke(ShowOverlays);
            }
        }
        else if (eventType == EVENT_SYSTEM_MOVESIZEEND)
        {
            // Snap to hovered zone if trigger modifier is satisfied
            if (_hoveredZone != null && _hoveredMonitor != null && IsTriggerActive())
                SnapToZone(hwnd, _hoveredZone, _hoveredMonitor);

            _draggingHwnd  = IntPtr.Zero;
            _hoveredZone   = null;
            _hoveredMonitor = null;

            Application.Current?.Dispatcher.BeginInvoke(HideAllOverlays);
        }
    }

    // ── Overlay management ─────────────────────────────────────────────────────

    private void ShowOverlays()
    {
        _cursorTimer?.Stop();
        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cursorTimer.Tick += UpdateCursorHighlight;
        _cursorTimer.Start();

        foreach (var monitor in _monitorService.GetAllMonitors())
        {
            var layout = GetLayoutForMonitor(monitor.DeviceId);
            if (layout == null || layout.Zones.Count == 0) continue;

            if (!_overlays.TryGetValue(monitor.DeviceId, out var overlay))
            {
                overlay = new ZoneOverlayWindow();
                _overlays[monitor.DeviceId] = overlay;
            }
            overlay.ShowForMonitor(monitor, layout.Zones);
        }
    }

    private void HideAllOverlays()
    {
        _cursorTimer?.Stop();
        foreach (var ov in _overlays.Values) ov.Hide();
    }

    private void UpdateCursorHighlight(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var pt)) return;

        ZoneDefinition? newZone    = null;
        MonitorInfo?    newMonitor = null;

        foreach (var monitor in _monitorService.GetAllMonitors())
        {
            var layout = GetLayoutForMonitor(monitor.DeviceId);
            if (layout == null) continue;

            foreach (var zone in layout.Zones)
            {
                if (zone.HitTest(pt.X, pt.Y, monitor.Bounds))
                {
                    newZone    = zone;
                    newMonitor = monitor;
                    break;
                }
            }
            if (newZone != null) break;
        }

        // Only update if the hovered zone changed
        if (newZone?.Id != _hoveredZone?.Id)
        {
            _hoveredZone    = newZone;
            _hoveredMonitor = newMonitor;

            foreach (var ov in _overlays.Values)
                ov.HighlightZone(_hoveredZone?.Id);
        }
    }

    // ── Snap logic ─────────────────────────────────────────────────────────────

    private void SnapToZone(IntPtr hwnd, ZoneDefinition zone, MonitorInfo monitor)
    {
        var (left, top, width, height) = zone.ToPixels(monitor.Bounds);
        SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);
    }

    private bool IsTriggerActive()
    {
        return _settings.TriggerModifier switch
        {
            "Shift" => (GetAsyncKeyState(VK_SHIFT)   & 0x8000) != 0,
            "Ctrl"  => (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0,
            _       => true // "None" — always snap
        };
    }

    // ── Zone taskbar management ────────────────────────────────────────────────

    private void RefreshTaskbars()
    {
        CloseAllTaskbars();
        foreach (var monitor in _monitorService.GetAllMonitors())
        {
            var layout = GetLayoutForMonitor(monitor.DeviceId);
            if (layout == null || layout.Zones.Count == 0) continue;

            foreach (var zone in layout.Zones)
            {
                var taskbar = new ZoneTaskbarWindow(zone, monitor);
                _taskbars[$"{monitor.DeviceId}:{zone.Id}"] = taskbar;
                taskbar.Show();
            }
        }
    }

    private void CloseAllTaskbars()
    {
        foreach (var tb in _taskbars.Values) tb.Close();
        _taskbars.Clear();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private ZoneLayout? GetLayoutForMonitor(string deviceId)
        => _settings.Layouts.FirstOrDefault(l => l.MonitorDeviceId == deviceId);

    private bool HasAnyLayout()
        => _settings.Layouts.Any(l => l.Zones.Count > 0);
}
