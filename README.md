# MultiRoblox

Run multiple Roblox clients on one PC, with optional anti-AFK. Tray app for Windows.

## Features

- Multi-instance via Roblox singleton mutex
- Anti-AFK for all open clients
- System tray (minimize / close goes to tray)
- Start with Windows

## Requirements

- Windows
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Run `publish/MultiRoblox.exe`.

## Usage

1. Close all Roblox clients
2. Start MultiRoblox and enable multi-instance
3. Launch as many Roblox clients as you want
4. Keep MultiRoblox running in the tray

Enable anti-AFK from the window or tray if you want clients to stay active.
