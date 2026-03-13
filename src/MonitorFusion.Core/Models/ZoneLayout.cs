namespace MonitorFusion.Core.Models;

/// <summary>
/// A collection of zones defined for one specific monitor.
/// Each monitor can have one active layout; layouts survive resolution changes
/// because zones are stored as fractions (0.0–1.0) of the monitor's dimensions.
/// </summary>
public class ZoneLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Custom Layout";
    public string MonitorDeviceId { get; set; } = string.Empty;
    public List<ZoneDefinition> Zones { get; set; } = new();
}

/// <summary>
/// A single rectangular zone defined as fractions of its monitor's size.
/// </summary>
public class ZoneDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>Left edge as a fraction of monitor width (0.0 = left, 1.0 = right)</summary>
    public double LeftPct { get; set; }

    /// <summary>Top edge as a fraction of monitor height (0.0 = top, 1.0 = bottom)</summary>
    public double TopPct { get; set; }

    /// <summary>Width as a fraction of monitor width</summary>
    public double WidthPct { get; set; }

    /// <summary>Height as a fraction of monitor height</summary>
    public double HeightPct { get; set; }

    /// <summary>
    /// Converts this zone to absolute screen pixel coordinates for the given monitor bounds.
    /// </summary>
    public (int Left, int Top, int Width, int Height) ToPixels(ScreenRect monitorBounds)
    {
        int left   = monitorBounds.Left + (int)(LeftPct  * monitorBounds.Width);
        int top    = monitorBounds.Top  + (int)(TopPct   * monitorBounds.Height);
        int width  = (int)(WidthPct  * monitorBounds.Width);
        int height = (int)(HeightPct * monitorBounds.Height);
        return (left, top, width, height);
    }

    /// <summary>
    /// Returns true if the given screen point is inside this zone on the specified monitor.
    /// </summary>
    public bool HitTest(int screenX, int screenY, ScreenRect monitorBounds)
    {
        var (left, top, width, height) = ToPixels(monitorBounds);
        return screenX >= left && screenX < left + width
            && screenY >= top  && screenY < top  + height;
    }
}

/// <summary>
/// Global settings for the zone layout feature.
/// </summary>
public class ZoneSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Show the zone highlight overlay whenever a window is being dragged.</summary>
    public bool ShowOverlayOnDrag { get; set; } = true;

    /// <summary>Show a slim per-zone taskbar at the bottom of each zone.</summary>
    public bool ShowZoneTaskbars { get; set; } = false;

    /// <summary>
    /// Modifier key that must be held to activate zone snapping.
    /// "None" = always active, "Shift" = hold Shift, "Ctrl" = hold Ctrl.
    /// </summary>
    public string TriggerModifier { get; set; } = "None";

    /// <summary>All zone layouts, one per monitor.</summary>
    public List<ZoneLayout> Layouts { get; set; } = new();
}
