# MonitorFusion — Multi-Monitor Management for Windows
## Your Open-Source DisplayFusion Alternative

---

## Reality Check & Timeline

**DisplayFusion** has been developed for 15+ years by a dedicated team. Building a comparable product solo while learning Windows system programming is a **2-3 year journey minimum**. Here's the honest breakdown:

| Phase | Duration | What You Ship |
|-------|----------|---------------|
| Phase 1: Foundation | 2-3 months | System tray app, monitor detection, basic wallpaper per monitor |
| Phase 2: Wallpaper Pro | 1-2 months | Wallpaper profiles, spanning, random rotation, online sources |
| Phase 3: Window Management | 2-3 months | Snapping, hotkey moves, drag between monitors |
| Phase 4: Monitor Profiles | 1-2 months | Save/load resolution configs, linked wallpaper profiles |
| Phase 5: Multi-Monitor Taskbar | 3-4 months | Custom taskbar on secondary monitors (HARDEST PART) |
| Phase 6: Polish & Ship | 2-3 months | Installer, settings UI, auto-update, licensing |

**Total estimate: 12-18 months** for a sellable MVP

---

## Tech Stack

| Component | Technology | Why |
|-----------|-----------|-----|
| Language | C# (.NET 8+) | Best Win32 interop, WPF for UI |
| UI Framework | WPF (Windows Presentation Foundation) | Native Windows look, hardware accelerated |
| System Tray | WPF NotifyIcon (Hardcodet.NotifyIcon.Wpf) | Industry standard for tray apps |
| Win32 Interop | P/Invoke + CsWin32 | Auto-generates Win32 API bindings |
| Settings Storage | JSON (System.Text.Json) | Simple, portable, human-readable |
| Installer | InnoSetup or WiX | Free, professional installers |
| Auto-Update | Squirrel.Windows or NetSparkle | Open-source update frameworks |
| Licensing | Custom or Keygen.sh | For when you're ready to sell |

---

## Phase 1: Foundation (Start Here!)

### What You're Building
A system tray application that detects all connected monitors and sets different wallpapers on each one.

### Key Windows APIs to Learn

```
Monitor Detection:
  - EnumDisplayMonitors()     → List all monitors
  - GetMonitorInfo()          → Get monitor name, resolution, position
  - EnumDisplayDevices()      → Get device details
  - EnumDisplaySettings()     → Get available resolutions

Wallpaper:
  - SystemParametersInfo()    → Set wallpaper (SPI_SETDESKWALLPAPER)
  - IDesktopWallpaper (COM)   → Per-monitor wallpaper (Windows 8+)

Window Management (Phase 3):
  - SetWindowPos()            → Move/resize windows
  - EnumWindows()             → List all windows
  - GetWindowRect()           → Get window position
  - SetWinEventHook()         → Listen for window events
  - RegisterHotKey()          → Global hotkeys

Shell (Phase 5):
  - Shell_NotifyIcon()        → System tray
  - SHAppBarMessage()         → Taskbar-like app bars
```

### Learning Resources (Priority Order)

1. **C# & .NET Basics** → Microsoft Learn (free)
2. **WPF** → Microsoft Learn WPF tutorials
3. **P/Invoke** → pinvoke.net (Win32 API signatures for C#)
4. **CsWin32** → GitHub: microsoft/CsWin32 (auto-generates P/Invoke)
5. **Multi-Monitor APIs** → MSDN "Multiple Display Monitors"

---

## Phase 2: Wallpaper Management (Details)

### Features to Implement

1. **Per-Monitor Wallpaper**
   - Use `IDesktopWallpaper` COM interface (modern, reliable)
   - Fallback: Stitch images into one large bitmap spanning virtual desktop

2. **Wallpaper Profiles**
   - Save/load named configurations as JSON
   - Each profile stores: monitor ID → image path + sizing mode

3. **Image Sizing Modes**
   - Fill, Fit, Stretch, Tile, Center, Span, Crop-to-Fill

4. **Random Rotation**
   - Timer-based wallpaper change from folder
   - Configurable interval (minutes/hours)

5. **Online Sources** (later)
   - Unsplash API (free, high quality)
   - Reddit /r/wallpapers API
   - Bing daily wallpaper

### Data Model

```json
{
  "profiles": {
    "Work Setup": {
      "monitors": {
        "MONITOR-ID-1": {
          "source": "C:\\Wallpapers\\code.jpg",
          "sizing": "Fill",
          "adjustments": { "brightness": 0, "grayscale": false }
        },
        "MONITOR-ID-2": {
          "source": "C:\\Wallpapers\\nature.jpg",
          "sizing": "Fill"
        }
      }
    }
  },
  "rotation": {
    "enabled": true,
    "intervalMinutes": 30,
    "folders": ["C:\\Wallpapers\\Collection"]
  }
}
```

---

## Phase 3: Window Management

### Core Features

1. **Window Snapping**
   - Monitor edge snapping (detect proximity during drag)
   - Window-to-window snapping
   - Use `SetWinEventHook` for EVENT_OBJECT_LOCATIONCHANGE

2. **Hotkey Window Movement**
   - Move window to next/previous monitor
   - Maximize/minimize on specific monitor
   - Span window across monitors
   - Use `RegisterHotKey()` for global shortcuts

3. **Window Position Profiles**
   - Save all window positions
   - Restore on profile load or monitor reconnect

### Implementation Approach

```
Low-Level Hook Pipeline:
  1. SetWinEventHook → Detect window move/resize events
  2. GetWindowRect → Read current position
  3. Calculate snap targets (monitor edges, other windows)
  4. SetWindowPos → Snap to target if within threshold
```

---

## Phase 4: Monitor Profiles

### Features

1. **Profile Management**
   - Save current monitor layout (resolution, position, refresh rate, orientation)
   - Load profiles via hotkey, tray menu, or titlebar button
   - Auto-detect profile based on connected monitors

2. **Linked Profiles**
   - Link wallpaper profile to monitor profile
   - Link desktop icon layout to monitor profile
   - Auto-switch everything when display config changes

### Windows APIs

```
ChangeDisplaySettingsEx()  → Apply resolution/refresh rate
EnumDisplaySettings()      → List available modes
WM_DISPLAYCHANGE          → Detect display changes (via message hook)
```

---

## Phase 5: Multi-Monitor Taskbar (Advanced)

**This is by far the hardest feature.** DisplayFusion's taskbar is essentially a custom shell extension.

### Approach Options

| Option | Difficulty | Result |
|--------|-----------|--------|
| WPF AppBar Window | Medium | Good enough for most users |
| Custom Shell Extension | Expert | True taskbar replacement |
| Transparent Overlay | Easy | Limited functionality |

### Recommended: WPF AppBar

1. Create a WPF window positioned as an AppBar (`SHAppBarMessage`)
2. Reserve screen space so windows don't overlap
3. Enumerate windows per monitor using `EnumWindows`
4. Show window buttons with thumbnails (DWM Thumbnail API)
5. Support click-to-activate, right-click context menu
6. Add clock, system tray mirror, pinned shortcuts

### Key APIs

```
SHAppBarMessage()              → Register as application bar
DwmRegisterThumbnail()         → Live window thumbnails
EnumWindows() + filter         → List windows per monitor
SetWinEventHook()              → Track window create/destroy/move
Shell_NotifyIcon()             → System tray integration
```

---

## Project Structure

```
MonitorFusion/
├── MonitorFusion.sln
├── src/
│   ├── MonitorFusion.Core/           # Core logic (no UI dependency)
│   │   ├── Models/
│   │   │   ├── MonitorInfo.cs
│   │   │   ├── WallpaperProfile.cs
│   │   │   ├── MonitorProfile.cs
│   │   │   └── WindowLayout.cs
│   │   ├── Services/
│   │   │   ├── MonitorDetectionService.cs
│   │   │   ├── WallpaperService.cs
│   │   │   ├── WindowManagementService.cs
│   │   │   ├── HotkeyService.cs
│   │   │   ├── SnappingService.cs
│   │   │   └── ProfileService.cs
│   │   ├── Native/
│   │   │   ├── User32.cs             # P/Invoke declarations
│   │   │   ├── Shell32.cs
│   │   │   ├── Dwmapi.cs
│   │   │   └── NativeStructs.cs
│   │   └── MonitorFusion.Core.csproj
│   │
│   ├── MonitorFusion.App/            # WPF Application
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml       # Settings window
│   │   │   ├── WallpaperSettings.xaml
│   │   │   ├── MonitorSettings.xaml
│   │   │   └── HotkeySettings.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   └── WallpaperViewModel.cs
│   │   ├── Controls/
│   │   │   ├── MonitorPreview.xaml    # Visual monitor layout
│   │   │   └── TaskbarControl.xaml
│   │   ├── TrayIcon/
│   │   │   └── TrayIconManager.cs
│   │   └── MonitorFusion.App.csproj
│   │
│   └── MonitorFusion.Taskbar/        # Separate process for taskbar (Phase 5)
│       └── MonitorFusion.Taskbar.csproj
│
├── tests/
│   └── MonitorFusion.Tests/
│
├── installer/
│   └── setup.iss                     # InnoSetup script
│
└── assets/
    ├── icons/
    └── screenshots/
```

---

## Monetization Strategy

### Free Tier (Always Free)
- Per-monitor wallpaper (basic)
- Window move to next monitor (1 hotkey)
- Monitor detection & info display
- Basic window snapping to monitor edges

### Pro Tier ($19-29 USD one-time)
- Wallpaper profiles & rotation
- Wallpaper from online sources
- All hotkey functions
- Window-to-window snapping
- Window position profiles
- Monitor profiles
- Monitor fading/dimming

### Pro+ / Enterprise ($49-99 USD)
- Multi-monitor taskbar
- Scripted functions (C# macros)
- Event triggers
- Remote control
- Silent installer / ADMX
- Priority support

### Revenue Targets
- Start selling at Phase 3 completion (window management works)
- Target: 100 sales in first 6 months at $25 = $2,500
- Growth: Word-of-mouth + Reddit + multi-monitor communities
- Long-term: Compete on price (DisplayFusion Pro = $34)

---

## Marketing Channels

1. **Reddit** — r/multimonitor, r/ultrawidemasterrace, r/battlestations
2. **GitHub** — Open-source the free tier for community trust
3. **YouTube** — Demo videos showing setup and features
4. **Product Hunt** — Launch when you have a polished beta
5. **Stack Overflow** — Answer multi-monitor questions, link to tool
6. **Windows community forums** — TenForums, ElevenForum

---

## Competitive Advantages to Target

1. **Modern UI** — DisplayFusion looks dated (Win7-era design)
2. **Price** — Undercut at $19-25 vs $34
3. **Lightweight** — Focus on performance, small memory footprint
4. **Open-source free tier** — Build community trust
5. **Modern .NET** — Faster startup, smaller footprint than legacy apps
6. **Ethiopian market** — Localize for Amharic (unique selling point for regional market)
