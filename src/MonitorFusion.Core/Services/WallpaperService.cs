using System.Runtime.InteropServices;
using MonitorFusion.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MonitorFusion.Core.Services;

/// <summary>
/// Manages desktop wallpaper using the IDesktopWallpaper COM interface (Windows 8+).
/// This is the modern, reliable way to set per-monitor wallpaper.
/// 
/// Fallback for older systems: stitch images into one large bitmap
/// spanning the virtual desktop, then use SystemParametersInfo.
/// </summary>
public class WallpaperService
{
    private readonly MonitorDetectionService _monitorService;
    private System.Timers.Timer? _rotationTimer;
    private readonly Random _random = new();

    public WallpaperService(MonitorDetectionService monitorService)
    {
        _monitorService = monitorService;
        CleanupTempCache();
    }

    private void CleanupTempCache()
    {
        try
        {
            var folder = GetTempFolder();
            if (!Directory.Exists(folder)) return;

            // Delete processing cache files older than 3 days
            var files = Directory.GetFiles(folder, "wp_*");
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if ((DateTime.Now - info.LastAccessTime).TotalDays > 3)
                {
                    try { File.Delete(file); } catch { /* Ignore locked files */ }
                }
            }
        }
        catch { }
    }

    #region IDesktopWallpaper COM Interface

    // The IDesktopWallpaper interface provides per-monitor wallpaper control.
    // Available on Windows 8 and later.

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID,
                          [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        void GetMonitorRECT(
            [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
            [Out] out RECT displayRect);

        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();

        void SetPosition(DESKTOP_WALLPAPER_POSITION position);
        DESKTOP_WALLPAPER_POSITION GetPosition();

        void AdvanceSlideshow(
            [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
            DESKTOP_SLIDESHOW_DIRECTION direction);

        // ... additional methods available but not needed initially
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass { }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private enum DESKTOP_WALLPAPER_POSITION
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    private enum DESKTOP_SLIDESHOW_DIRECTION
    {
        Forward = 0,
        Backward = 1
    }

    #endregion

    /// <summary>
    /// Sets wallpaper on a specific monitor using the modern COM interface.
    /// </summary>
    /// <param name="monitorIndex">Zero-based monitor index</param>
    /// <param name="imagePath">Full path to the image file</param>
    /// <param name="sizing">How to size the image</param>
    public void SetWallpaper(int monitorIndex, string imagePath, WallpaperSizing sizing = WallpaperSizing.Fill)
    {
        try
        {
            var targetMonitor = _monitorService.GetAllMonitors().FirstOrDefault(m => m.Index == monitorIndex);
            if (targetMonitor == null)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperService] Target monitor with index {monitorIndex} not found.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[WallpaperService] SetWallpaper called for Monitor {monitorIndex} with Image: {imagePath}");
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();

            string? targetMonitorId = null;
            uint count = wallpaper.GetMonitorDevicePathCount();
            
            for (uint i = 0; i < count; i++)
            {
                string id = wallpaper.GetMonitorDevicePathAt(i);
                wallpaper.GetMonitorRECT(id, out var rect);
                
                if (rect.Left == targetMonitor.Bounds.Left && 
                    rect.Top == targetMonitor.Bounds.Top && 
                    rect.Right == targetMonitor.Bounds.Right && 
                    rect.Bottom == targetMonitor.Bounds.Bottom)
                {
                    targetMonitorId = id;
                    break;
                }
            }

            if (targetMonitorId == null)
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperService] Failed to map monitor {monitorIndex} via RECT.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Monitor ID discovered via RECT mapping: {targetMonitorId}");

            // Set the position/sizing mode
            var position = SizingToPosition(sizing);
            wallpaper.SetPosition(position);
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Position set to: {position}");

            // Enforce absolute path (COM API requires this)
            string absoluteImagePath = Path.GetFullPath(imagePath);
            if (!File.Exists(absoluteImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperService] Aborting: Image file not found: {absoluteImagePath}");
                return;
            }

            // Set the wallpaper image
            wallpaper.SetWallpaper(targetMonitorId, absoluteImagePath);
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] SetWallpaper COM call executed successfully for {absoluteImagePath}.");
        }
        catch (COMException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] COM Exception on monitor {monitorIndex}: {ex.Message}\n{ex.StackTrace}");
            // Don't crash the whole app just for a wallpaper failure, but log it.
            // Some Windows editions or locked files can cause opaque E_FAIL errors.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Generic Exception on monitor {monitorIndex}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Sets wallpaper on all monitors from a WallpaperProfile.
    /// </summary>
    public void ApplyProfile(WallpaperProfile profile)
    {
        var monitors = _monitorService.GetAllMonitors();

        foreach (var monitor in monitors)
        {
            if (profile.Monitors.TryGetValue(monitor.DeviceId, out var config))
            {
                if (File.Exists(config.Source))
                {
                    // TODO: Apply image adjustments (brightness, grayscale, etc.)
                    // For now, set the image directly
                    string processedPath = ProcessImage(config.Source, config.Adjustments, monitor);
                    SetWallpaper(monitor.Index, processedPath, config.Sizing);
                }
            }
        }
    }

    /// <summary>
    /// Gets the current wallpaper path for a specific monitor.
    /// </summary>
    public string GetCurrentWallpaper(int monitorIndex)
    {
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            string monitorId = wallpaper.GetMonitorDevicePathAt((uint)monitorIndex);
            return wallpaper.GetWallpaper(monitorId);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets all monitor device paths known to the wallpaper system.
    /// </summary>
    public List<string> GetWallpaperMonitorIds()
    {
        var ids = new List<string>();
        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            uint count = wallpaper.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++)
            {
                ids.Add(wallpaper.GetMonitorDevicePathAt(i));
            }
        }
        catch { }
        return ids;
    }

    /// <summary>
    /// Sets a random wallpaper from the specified folders.
    /// </summary>
    public void SetRandomWallpaper(List<string> folders, bool includeSubfolders = true)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        var searchOption = includeSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var allImages = folders
            .Where(Directory.Exists)
            .SelectMany(f => Directory.GetFiles(f, "*.*", searchOption))
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (allImages.Count == 0) return;

        var monitors = _monitorService.GetAllMonitors();
        foreach (var monitor in monitors)
        {
            var randomImage = allImages[_random.Next(allImages.Count)];
            SetWallpaper(monitor.Index, randomImage);
        }
    }

    /// <summary>
    /// Starts automatic wallpaper rotation.
    /// </summary>
    public void StartRotation(WallpaperRotation settings)
    {
        StopRotation();

        if (!settings.Enabled || settings.Folders.Count == 0) return;

        _rotationTimer = new System.Timers.Timer(settings.IntervalMinutes * 60 * 1000);
        _rotationTimer.Elapsed += (s, e) =>
        {
            SetRandomWallpaper(settings.Folders, settings.IncludeSubfolders);
        };
        _rotationTimer.AutoReset = true;
        _rotationTimer.Start();
    }

    /// <summary>
    /// Stops automatic wallpaper rotation.
    /// </summary>
    public void StopRotation()
    {
        _rotationTimer?.Stop();
        _rotationTimer?.Dispose();
        _rotationTimer = null;
    }

    /// <summary>
    /// Processes an image with the given adjustments (brightness, grayscale, etc.).
    /// Returns the path to the processed image (may be a temp file).
    /// </summary>
    private string ProcessImage(string sourcePath, ImageAdjustments adjustments, MonitorInfo monitor)
    {
        bool hasAdjustments =
            adjustments.Brightness != 0 ||
            adjustments.Contrast != 0 ||
            adjustments.Grayscale ||
            adjustments.Sepia ||
            adjustments.Invert ||
            adjustments.Blur > 0 ||
            adjustments.Rotation != 0 ||
            adjustments.FlipHorizontal ||
            adjustments.FlipVertical;

        System.Diagnostics.Debug.WriteLine($"[WallpaperService] ProcessImage called for '{sourcePath}'. HasAdjustments: {hasAdjustments}");

        if (!hasAdjustments)
            return sourcePath;

        try
        {
            var tempFolder = GetTempFolder();
            
            // Create a unique filename based on source and adjustment hash so we don't re-process identical settings unnecessarily
            int hash1 = HashCode.Combine(sourcePath, adjustments.Brightness, adjustments.Contrast, adjustments.Grayscale, adjustments.Sepia, adjustments.Invert);
            int hash2 = HashCode.Combine(adjustments.Blur, adjustments.Rotation, adjustments.FlipHorizontal, adjustments.FlipVertical);
            int hash = HashCode.Combine(hash1, hash2);
            
            string ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            
            string tempPath = Path.Combine(tempFolder, $"wp_{monitor.DeviceId.GetHashCode()}_{Math.Abs(hash)}{ext}");
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Generated temp path: {tempPath}");
            
            // If we already generated this exact combo, reuse it
            if (File.Exists(tempPath))
            {
                System.Diagnostics.Debug.WriteLine($"[WallpaperService] Reusing existing cached image.");
                return tempPath;
            }

            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Processing image via ImageSharp...");
            using (var image = SixLabors.ImageSharp.Image.Load(sourcePath))
            {
                image.Mutate(x =>
                {
                    if (adjustments.Brightness != 0)
                        x.Brightness(1f + adjustments.Brightness);

                    if (adjustments.Contrast != 0)
                        x.Contrast(1f + adjustments.Contrast);

                    if (adjustments.Grayscale)
                        x.Grayscale();

                    if (adjustments.Sepia)
                        x.Sepia();

                    if (adjustments.Invert)
                        x.Invert();

                    if (adjustments.Blur > 0)
                        x.GaussianBlur(adjustments.Blur);

                    if (adjustments.Rotation != 0)
                        x.Rotate(adjustments.Rotation);

                    if (adjustments.FlipHorizontal)
                        x.Flip(SixLabors.ImageSharp.Processing.FlipMode.Horizontal);

                    if (adjustments.FlipVertical)
                        x.Flip(SixLabors.ImageSharp.Processing.FlipMode.Vertical);
                });

                image.Save(tempPath);
                System.Diagnostics.Debug.WriteLine($"[WallpaperService] Successfully saved processed image to cache.");
            }

            return tempPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WallpaperService] Image processing failed: {ex.Message}\n{ex.StackTrace}");
            return sourcePath; // Fallback to original image if processing fails
        }
    }

    private static DESKTOP_WALLPAPER_POSITION SizingToPosition(WallpaperSizing sizing) => sizing switch
    {
        WallpaperSizing.Fill => DESKTOP_WALLPAPER_POSITION.Fill,
        WallpaperSizing.Fit => DESKTOP_WALLPAPER_POSITION.Fit,
        WallpaperSizing.Stretch => DESKTOP_WALLPAPER_POSITION.Stretch,
        WallpaperSizing.Tile => DESKTOP_WALLPAPER_POSITION.Tile,
        WallpaperSizing.Center => DESKTOP_WALLPAPER_POSITION.Center,
        WallpaperSizing.Span => DESKTOP_WALLPAPER_POSITION.Span,
        _ => DESKTOP_WALLPAPER_POSITION.Fill
    };

    private string GetTempFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonitorFusion", "WallpaperCache");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
