using System.Runtime.InteropServices;
using MonitorFusion.Core.Models;

namespace MonitorFusion.Core.Services;

/// <summary>
/// Registers and manages global hotkeys using Win32 RegisterHotKey API.
/// Global hotkeys work even when your app is not focused.
/// 
/// IMPORTANT: Must be called from a thread with a message pump (WPF UI thread).
/// In WPF, use the Window's HwndSource to receive WM_HOTKEY messages.
/// </summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier keys
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // WM_HOTKEY message constant
    public const int WM_HOTKEY = 0x0312;

    private readonly Dictionary<int, Action> _registeredHotkeys = new();
    private readonly IntPtr _windowHandle;
    private int _nextId = 9000; // Start IDs high to avoid conflicts

    /// <summary>
    /// Creates a HotkeyService bound to a specific window handle.
    /// In WPF: pass the HWND from HwndSource.
    /// </summary>
    public HotkeyService(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="modifiers">Combination of MOD_CONTROL, MOD_ALT, MOD_SHIFT, MOD_WIN</param>
    /// <param name="key">Virtual key code (use System.Windows.Forms.Keys enum values)</param>
    /// <param name="callback">Action to execute when hotkey is pressed</param>
    /// <returns>Hotkey ID (needed for unregistration)</returns>
    public int Register(uint modifiers, uint key, Action callback)
    {
        int id = _nextId++;

        if (!RegisterHotKey(_windowHandle, id, modifiers | MOD_NOREPEAT, key))
        {
            throw new InvalidOperationException(
                $"Failed to register hotkey. The key combination may already be in use. " +
                $"Error code: {Marshal.GetLastWin32Error()}");
        }

        System.IO.File.AppendAllText("hotkey_test.log", $"Successfully registered hotkey ID {id} with Modifiers {modifiers} and Key {key}\n");
        _registeredHotkeys[id] = callback;
        return id;
    }

    /// <summary>
    /// Registers a hotkey from a HotkeyBinding configuration.
    /// </summary>
    public int Register(HotkeyBinding binding, Action callback)
    {
        uint modifiers = ParseModifiers(binding.Modifiers);
        uint key = ParseKey(binding.Key);
        return Register(modifiers, key, callback);
    }

    /// <summary>
    /// Unregisters a previously registered hotkey.
    /// </summary>
    public void Unregister(int id)
    {
        UnregisterHotKey(_windowHandle, id);
        _registeredHotkeys.Remove(id);
    }

    /// <summary>
    /// Call this from your WndProc when receiving WM_HOTKEY messages.
    /// In WPF, hook into HwndSource.AddHook.
    /// </summary>
    /// <example>
    /// // In your WPF Window:
    /// var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
    /// source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
    /// {
    ///     if (msg == HotkeyService.WM_HOTKEY)
    ///     {
    ///         _hotkeyService.ProcessHotkeyMessage(wParam.ToInt32());
    ///         handled = true;
    ///     }
    ///     return IntPtr.Zero;
    /// });
    /// </example>
    public void ProcessHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var callback))
        {
            try
            {
                callback.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hotkey callback error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Parses modifier string like "Ctrl+Alt+Shift+Win" to flags.
    /// </summary>
    public static uint ParseModifiers(string modifierString)
    {
        uint result = 0;
        var parts = modifierString.Split('+', StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            result |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => MOD_CONTROL,
                "alt" => MOD_ALT,
                "shift" => MOD_SHIFT,
                "win" or "windows" or "super" => MOD_WIN,
                _ => 0
            };
        }

        return result;
    }

    /// <summary>
    /// Parses a key name to a virtual key code.
    /// Uses common key names — extend as needed.
    /// </summary>
    public static uint ParseKey(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            // Arrow keys
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,

            // Function keys
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,

            // Special keys
            "SPACE" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "TAB" => 0x09,
            "ESCAPE" or "ESC" => 0x1B,
            "DELETE" or "DEL" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,

            // Letters (A-Z = 0x41-0x5A)
            var k when k.Length == 1 && char.IsLetter(k[0])
                => (uint)char.ToUpper(k[0]),

            // Numbers (0-9 = 0x30-0x39)
            var k when k.Length == 1 && char.IsDigit(k[0])
                => (uint)k[0],

            _ => throw new ArgumentException($"Unknown key: {keyName}")
        };
    }

    public void Dispose()
    {
        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            Unregister(id);
        }
    }
}
