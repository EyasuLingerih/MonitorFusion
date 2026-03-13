# Contributing to MonitorFusion

Thanks for your interest in contributing! Here's everything you need to get started.

## Getting Started

### Prerequisites

- Windows 10 or 11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (with ".NET desktop development" workload) or VS Code

### Build and run locally

```bash
git clone https://github.com/EyasuLingerih/MonitorFusion.git
cd MonitorFusion
dotnet run --project src/MonitorFusion.App
```

## Project Structure

```
MonitorFusion.Core   — Platform services, Win32 interop, models (no UI dependency)
MonitorFusion.App    — WPF application, views, tray icon, zone overlay
```

Key services live in `src/MonitorFusion.Core/Services/` and are accessed statically via `App.*Service` in the WPF layer.

## Ways to Contribute

- **Bug reports** — Open an issue with steps to reproduce and your Windows version
- **Feature requests** — Open an issue describing the use case
- **Code** — Pick an open issue, comment that you're working on it, then open a PR
- **Docs / website** — The website is in `docs/` (plain HTML + CSS, no build step)

## Submitting a Pull Request

1. Fork the repository and create a branch from `master`
2. Make your changes — keep PRs focused on a single feature or fix
3. Test on at least one multi-monitor setup
4. Open a PR with a clear description of what changed and why

## Code Style

- Follow the existing C# conventions (file-scoped namespaces, `var` where type is obvious)
- Keep UI logic in code-behind; keep Win32/platform logic in `MonitorFusion.Core`
- No external UI frameworks — WPF only

## Reporting Bugs

Please include:
- Windows version
- Number of monitors and their configuration
- Steps to reproduce
- The crash log if applicable: `%AppData%\MonitorFusion\logs\crash.log`

## License

By contributing you agree that your contributions will be licensed under the [MIT License](LICENSE).
