# MonitorFusion

**Multi-Monitor Management Made Easy** — A free & open-source Windows utility for power users with multiple displays.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://github.com/EyasuLingerih/MonitorFusion)
[![Release](https://img.shields.io/github/v/release/EyasuLingerih/MonitorFusion?logo=github)](https://github.com/EyasuLingerih/MonitorFusion/releases/latest)

> Zone layouts, per-monitor wallpapers, hotkey window movement, magnetic snapping, multi-monitor taskbar — all free and open source.

**[Website](https://EyasuLingerih.github.io/MonitorFusion) · [Download Installer](https://github.com/EyasuLingerih/MonitorFusion/releases/latest) · [Report Bug](https://github.com/EyasuLingerih/MonitorFusion/issues)**

---

## Install

Download **MonitorFusion-Setup-x.x.x.exe** from the [latest release](https://github.com/EyasuLingerih/MonitorFusion/releases/latest) and run it.

- No prerequisites — .NET is bundled inside (~30 MB)
- Works on Windows 10 and 11 (64-bit)
- No admin rights required
- Optional: Start with Windows, desktop shortcut, desktop right-click menu entry

---

## Features

| Feature | Description |
|---------|-------------|
| **Zone Layouts** | Divide any monitor into custom zones. Drag windows to snap into zones. Per-zone taskbar. |
| **Per-Monitor Wallpapers** | Different wallpaper per display, named profiles, thumbnail preview, auto-rotation. |
| **Hotkey Window Movement** | Move/center/maximize/span windows across monitors with global shortcuts. |
| **Magnetic Snapping** | Windows snap to monitor edges and other windows as you drag. |
| **Monitor Profiles** | Save and restore full display configurations. |
| **Window Layout Profiles** | Save all window positions and restore them with one hotkey. |
| **Multi-Monitor Taskbar** | Custom taskbar on secondary monitors. Per-zone taskbars. |
| **Focus Mode** | Dim inactive monitors. Configurable opacity. |

---

## Build from Source

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** with ".NET desktop development" workload (optional, VS Code works too)

### Run

```bash
git clone https://github.com/EyasuLingerih/MonitorFusion.git
cd MonitorFusion
dotnet run --project src/MonitorFusion.App
```

### Build installer

Requires [InnoSetup 6](https://jrsoftware.org/isinfo.php) installed.

```bat
scripts\build-installer.bat
```

Output: `installer\output\MonitorFusion-Setup-x.x.x.exe`

---

## Architecture

```
MonitorFusion.Core   — Platform services, Win32 interop, models (no UI dependency)
MonitorFusion.App    — WPF application, views, tray icon, zone overlay
```

### Key Services

| Service | Purpose |
|---------|---------|
| `MonitorDetectionService` | Enumerate monitors, detect plug/unplug via WM_DISPLAYCHANGE |
| `WallpaperService` | Per-monitor wallpaper via IDesktopWallpaper COM |
| `WindowManagementService` | Move/snap/span windows, save/restore positions |
| `ZoneService` | Zone layout engine — drag-to-snap with real-time overlay |
| `HotkeyService` | Global keyboard shortcuts via RegisterHotKey |
| `TaskbarService` | Per-monitor custom taskbar windows |
| `FadingService` | Focus mode — dim inactive monitors |
| `SettingsService` | JSON settings in %APPDATA%\MonitorFusion |

---

## Development Roadmap

- [x] Project structure & architecture
- [x] Phase 1: Monitor detection + basic wallpaper
- [x] Phase 2: Wallpaper profiles & rotation
- [x] Phase 3: Window management & snapping
- [x] Phase 4: Monitor profiles
- [x] Phase 5: Multi-monitor taskbar & Focus mode
- [x] Phase 6: Zone layout engine
- [x] Phase 7: Installer & shell integration
- [ ] Phase 8: Auto-update & release pipeline
- [ ] Phase 9: Zone app launcher

---

## License

MIT License — see [LICENSE](LICENSE) for details.
