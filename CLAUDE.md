# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Publish single-file release (Windows-only, .NET 8 required)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Run via the published exe
bin\Release\net8.0-windows\win-x64\publish\TrafficLight.exe

# Or build & run in one step
dotnet run -c Release
```

Output is a single `TrafficLight.exe` in the publish folder.

## Architecture Overview

A single-file Windows Forms app (`Program.cs`) that lives in the system tray and changes its icon based on HTTP status requests.

### Core Components (all in `Program.cs`)

- **`TrafficLightContext`** — inherits `ApplicationContext`; owns the `NotifyIcon`, `Timer`, and `HttpListener`
- **HTTP Listener** — runs on a background thread on `localhost:9876`; accepts `?status=<value>` query params, calls `SetStatus()` which stores the value with a timestamp under a lock
- **500ms Tick Timer** — UI-thread timer that reads the status (with timeout check — 60s auto-idle) and switches the tray icon accordingly
- **Icon Rendering** — embedded 32×32 PNGs loaded from `images/` as `EmbeddedResource`, converted to `System.Drawing.Icon` via handcrafted DIB-in-ICO format (32bpp ARGB) for proper transparency

### Status → Icon Mapping

| HTTP status value(s) | Icon | Tray text |
|---|---|---|
| `idle` (default) | ⚪ gray | 空闲 |
| `waiting`, `confirm` | 🔴 red (flashing every 500ms) | 需确认 |
| `thinking`, `processing` | 🟡 yellow | 执行中 |
| `completed`, `done` | 🟢 green | 已完成 |

### Key Behaviors

- **Red flash**: alternates between red and gray icons every 500ms while in a waiting/confirm state; uses direct icon swap (no visibility toggle) during flash ticks to avoid flicker
- **Timeout**: any non-idle status auto-resets to idle after 60s of no updates
- **Double-click**: resets status to idle immediately
- **Debug logging**: appends to `%TEMP%\TrafficLight-debug.log` with millisecond timestamps

### Claude Code Integration

The `claude-settings/settings.json` file registers Claude Code hooks that `curl` status updates to `http://localhost:9876/` when events fire:
- `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PostToolBatch`, `TaskCreated`, `SubagentStart` → `?status=processing`
- `PermissionRequest` → `?status=confirm`
- `TaskCompleted`, `Stop` → `?status=completed`
- `SessionEnd` → `?status=idle`

All hooks are fire-and-forget (`"async": true`) to avoid blocking Claude Code.

## Development Notes

- **Windows-only**: requires `UseWindowsForms` (WinForms) and `System.Drawing.Common`
- **.csproj embeds icons**: `<EmbeddedResource Include="images\*.png" />` — new PNGs in `images/` are auto-embedded
- **Icon format**: PNGs are 32×32; loaded as 32bpp ARGB and packed into a `.ico` container manually (not via `Icon.Save` which strips alpha). If adding new icons, replicate the same loading pipeline
- **No external dependencies**: only uses BCL + WinForms
- **Thread safety**: status is shared between HTTP thread and UI timer via a `_statusLock` + `_lastUpdate` timestamp pattern

## Project Files

| Path | Role |
|---|---|
| `Program.cs` | All source code (entry point, tray, HTTP, icon loading, logging, disposal) |
| `TrafficLight.csproj` | .NET 8 Windows project — WinForms, single-file publish, embedded resources |
| `images/*.png` | 32×32 icon source files (idle, red, yellow, green) |
| `claude-settings/settings.json` | Claude Code hook configuration (reference/copy into Claude Code's settings) |
| `claude-settings/install-hooks.bat` / `.ps1` | Scripts to install hooks into Claude Code settings |
| `README.md` | Full documentation (integration guide, testing, customization) |
