namespace MonitorFusion.Core.Models;

/// <summary>
/// Represents a physical or virtual monitor connected to the system.
/// </summary>
public class MonitorInfo
{
    /// <summary>Unique device identifier (e.g., \\.\DISPLAY1)</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Human-friendly name (e.g., "Dell U2723QE")</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>Whether this is the primary monitor</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Monitor bounds in virtual screen coordinates</summary>
    public ScreenRect Bounds { get; set; } = new();

    /// <summary>Working area (excludes taskbar)</summary>
    public ScreenRect WorkArea { get; set; } = new();

    /// <summary>Current resolution width in pixels</summary>
    public int Width { get => Bounds.Width; set { } }

    /// <summary>Current resolution height in pixels</summary>
    public int Height { get => Bounds.Height; set { } }

    /// <summary>Current refresh rate in Hz</summary>
    public int RefreshRate { get; set; }

    /// <summary>Bits per pixel (color depth)</summary>
    public int BitsPerPixel { get; set; }

    /// <summary>Display orientation (0=default, 1=90°, 2=180°, 3=270°)</summary>
    public int Orientation { get; set; }

    /// <summary>Index for display ordering</summary>
    public int Index { get; set; }

    /// <summary>DPI scale percent (100 = 100%, 125 = 125%, 0 = not detected)</summary>
    public double DpiScale { get; set; } = 0;

    public override string ToString()
        => $"{FriendlyName} ({Width}x{Height} @ {RefreshRate}Hz) {(IsPrimary ? "[Primary]" : "")}";
}

/// <summary>
/// Simple rectangle in screen coordinates.
/// </summary>
public class ScreenRect
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool Contains(int x, int y)
        => x >= Left && x < Right && y >= Top && y < Bottom;

    public override string ToString()
        => $"({Left},{Top})-({Right},{Bottom}) [{Width}x{Height}]";
}
