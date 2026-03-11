using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MonitorFusion.Core.Models;

namespace MonitorFusion.App.Views;

/// <summary>
/// A secondary taskbar window that docks to the edge of a monitor.
/// Uses the Win32 SHAppBarMessage API to reserve screen real estate
/// so maximized windows don't overlap it.
/// </summary>
public partial class TaskbarWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly TaskbarSettings _settings;
    private readonly DispatcherTimer _clockTimer;
    private int _appBarMessageId;

    public TaskbarWindow(MonitorInfo monitor, TaskbarSettings settings)
    {
        InitializeComponent();
        _monitor = monitor;
        _settings = settings;

        // Ensure this doesn't show up in the primary Taskbar or Alt-Tab
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        AllowsTransparency = true;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += ClockTimer_Tick;

        Loaded += TaskbarWindow_Loaded;
        Closing += TaskbarWindow_Closing;
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        if (_settings.ShowClock)
        {
            ClockText.Text = DateTime.Now.ToShortTimeString();
        }
    }

    private void TaskbarWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Start the clock
        if (_settings.ShowClock)
        {
            _clockTimer.Start();
            ClockText.Visibility = Visibility.Visible;
            ClockText.Text = DateTime.Now.ToShortTimeString();
        }
        else
        {
            ClockText.Visibility = Visibility.Collapsed;
        }

        // Register the AppBar to reserve space
        RegisterAppBar();

        // Hook into the WndProc to handle AppBar resize messages
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private void TaskbarWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop();
        UnregisterAppBar();
    }

    #region Win32 AppBar Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern int RegisterWindowMessage(string lpString);

    // Constants
    private const int ABM_NEW = 0x00000000;
    private const int ABM_REMOVE = 0x00000001;
    private const int ABM_QUERYPOS = 0x00000002;
    private const int ABM_SETPOS = 0x00000003;
    
    private const int ABE_LEFT = 0;
    private const int ABE_TOP = 1;
    private const int ABE_RIGHT = 2;
    private const int ABE_BOTTOM = 3;

    private void RegisterAppBar()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _appBarMessageId = RegisterWindowMessage("AppBarMessage");

        var abd = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
            hWnd = hwnd,
            uCallbackMessage = _appBarMessageId
        };

        // 1. Register
        SHAppBarMessage(ABM_NEW, ref abd);

        // 2. Determine Position based on settings & monitor
        int edge = _settings.Position.ToLower() switch
        {
            "top" => ABE_TOP,
            "left" => ABE_LEFT,
            "right" => ABE_RIGHT,
            _ => ABE_BOTTOM // Default
        };

        abd.uEdge = edge;
        abd.rc = new RECT
        {
            left = _monitor.Bounds.Left,
            top = _monitor.Bounds.Top,
            right = _monitor.Bounds.Right,
            bottom = _monitor.Bounds.Bottom
        };

        // Propose our size constraints
        if (edge == ABE_TOP) abd.rc.bottom = abd.rc.top + _settings.Height;
        else if (edge == ABE_BOTTOM) abd.rc.top = abd.rc.bottom - _settings.Height;
        else if (edge == ABE_LEFT) abd.rc.right = abd.rc.left + _settings.Height;
        else if (edge == ABE_RIGHT) abd.rc.left = abd.rc.right - _settings.Height;

        // 3. Query OS to see if this position is ok, it might adjust abd.rc
        SHAppBarMessage(ABM_QUERYPOS, ref abd);

        // Enforce our exact height/width constraint again in case Windows moved the rect
        if (edge == ABE_TOP) abd.rc.bottom = abd.rc.top + _settings.Height;
        else if (edge == ABE_BOTTOM) abd.rc.top = abd.rc.bottom - _settings.Height;
        else if (edge == ABE_LEFT) abd.rc.right = abd.rc.left + _settings.Height;
        else if (edge == ABE_RIGHT) abd.rc.left = abd.rc.right - _settings.Height;

        // 4. Set the final position which subtracts the space from the work area
        SHAppBarMessage(ABM_SETPOS, ref abd);

        // 5. Actually move the WPF window to match the reserved rect visually
        // Need to use Win32 SetWindowPos because WPF Left/Top are DPI-aware and 
        // Monitor Bounds are raw physical pixels, which causes a mismatch and overlap on high DPI screens
        SetWindowPos(hwnd, new IntPtr(-1), // HWND_TOPMOST
            abd.rc.left, abd.rc.top,
            abd.rc.right - abd.rc.left, abd.rc.bottom - abd.rc.top,
            0x0040); // SWP_SHOWWINDOW
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public void UnregisterAppBar()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        var abd = new APPBARDATA
        {
            cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
            hWnd = hwnd
        };
        SHAppBarMessage(ABM_REMOVE, ref abd);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Handle custom AppBar message
        if (msg == _appBarMessageId)
        {
            // ABE_POSCHANGED = 1, ABE_STATECHANGE = 2, ABE_WINDOWARRANGE = 3
            if (wParam.ToInt32() == 1) // ABE_POSCHANGED (Usually 1)
            {
                // Another appbar moved, we need to recalculate our position
                RegisterAppBar();
            }
        }
        // WM_WINDOWPOSCHANGING = 0x0046
        else if (msg == 0x0046)
        {
            // Prevent Windows from unexpectedly moving this window
            // When building custom appbars, Windows sometimes tries to snap them back to primary monitor
        }

        return IntPtr.Zero;
    }

    #endregion
}
