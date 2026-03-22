# NetSterm - Cross-Platform SSH Terminal

A modern cross-platform SSH terminal application inspired by MobaXterm, built with Avalonia and .NET 10. Runs on Windows, Linux, and macOS.

![Dark Theme](https://img.shields.io/badge/theme-dark-1e1e1e) ![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4) ![Avalonia](https://img.shields.io/badge/UI-Avalonia-8b44ac) ![Cross-Platform](https://img.shields.io/badge/platform-Win%20%7C%20Linux%20%7C%20macOS-green)

## Features

- **Multi-tab SSH sessions** — Connect to multiple servers simultaneously with tabbed interface
- **Session management** — Organize connections in folders, save credentials securely (AES-256 encryption)
- **xterm.js terminal** — Full VT100/xterm-256color emulation via embedded WebView + xterm.js
- **SFTP file browser** — Dual-pane local/remote file browser with upload/download
- **Quick connect** — Toolbar for instant SSH connections
- **Dark theme** — Professional dark UI with Avalonia Fluent theme
- **Key authentication** — Support for password and private key (PEM, PPK) authentication
- **Cross-platform** — Runs natively on Windows, Linux, and macOS

## Tech Stack

| Component | Library |
|-----------|---------|
| SSH/SFTP | [SSH.NET](https://github.com/sshnet/SSH.NET) |
| Terminal | [xterm.js](https://xtermjs.org/) via WebView |
| UI Framework | [Avalonia](https://avaloniaui.net/) with Fluent theme |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| Icons | [Material.Icons.Avalonia](https://github.com/SKProCH/Material.Icons) |

## Building

```bash
dotnet build src/NetSterm/NetSterm.csproj
```

## Running

```bash
dotnet run --project src/NetSterm/NetSterm.csproj
```

## Requirements

- .NET 10 SDK
- No platform-specific runtime dependencies

## Installer (Windows)

A Windows installer can be built with [Inno Setup](https://jrsoftware.org/isinfo.php):

```powershell
./build-installer.ps1
```

This creates either an Inno Setup installer or a portable ZIP archive.

## License

See [LICENSE](LICENSE) for details.
