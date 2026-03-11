# MonitorFusion

**Multi-Monitor Management Made Easy** — A free & open-source Windows utility for power users with multiple displays.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://github.com/eling/MonitorFusion)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)

> Set different wallpapers per monitor, move windows with hotkeys, snap windows to monitor edges, and more — all for free.

**[Website](https://eling.github.io/MonitorFusion) · [Download](https://github.com/eling/MonitorFusion/releases) · [Report Bug](https://github.com/eling/MonitorFusion/issues)**

---

## Getting Started

### Prerequisites

1. **Windows 10/11** (64-bit)
2. **Visual Studio 2022** (Community edition is free)
   - Install with ".NET desktop development" workload
3. **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)

### Setup

```bash
# Clone the repository
git clone https://github.com/eling/MonitorFusion.git
cd MonitorFusion

# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run --project src/MonitorFusion.App
```

### Or in Visual Studio

1. Open `MonitorFusion.sln`
2. Set `MonitorFusion.App` as startup project
3. Press F5 to run

---

## Architecture

```
MonitorFusion.Core     — Platform services, Win32 interop, models (no UI)
MonitorFusion.App      — WPF application, views, tray icon
MonitorFusion.Taskbar  — Separate process for multi-monitor taskbar (Phase 5)
```

### Key Services

| Service | Purpose |
|---------|---------|
| `MonitorDetectionService` | Enumerate monitors, get resolutions, detect changes |
| `WallpaperService` | Per-monitor wallpaper via IDesktopWallpaper COM |
| `WindowManagementService` | Move/snap/span windows, save/restore positions |
| `HotkeyService` | Global keyboard shortcuts via RegisterHotKey |
| `SettingsService` | JSON-based settings persistence |

---

## Development Roadmap

- [x] Project structure & architecture
- [ ] Phase 1: Monitor detection + basic wallpaper
- [ ] Phase 2: Wallpaper profiles & rotation
- [ ] Phase 3: Window management & snapping
- [ ] Phase 4: Monitor profiles
- [ ] Phase 5: Multi-monitor taskbar
- [ ] Phase 6: Installer, licensing & polish

See `PROJECT_ROADMAP.md` for detailed plans.

---

## License

MIT License — see [LICENSE](LICENSE) for details.
