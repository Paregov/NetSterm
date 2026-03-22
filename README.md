# WinSTerm - Windows SSH Terminal

A modern Windows SSH terminal application inspired by MobaXterm, built with WPF and .NET 10.

![Dark Theme](https://img.shields.io/badge/theme-dark-1e1e1e) ![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4) ![WPF](https://img.shields.io/badge/UI-WPF-blue)

## Features

- **Multi-tab SSH sessions** — Connect to multiple servers simultaneously with tabbed interface
- **Session management** — Organize connections in folders, save credentials securely (DPAPI)
- **xterm.js terminal** — Full VT100/xterm-256color emulation via WebView2 + xterm.js
- **SFTP file browser** — Dual-pane local/remote file browser with upload/download
- **Quick connect** — Toolbar for instant SSH connections
- **Dark theme** — Professional MobaXterm-inspired dark UI with MahApps.Metro
- **Key authentication** — Support for password and private key (PEM, PPK) authentication

## Tech Stack

| Component | Library |
|-----------|---------|
| SSH/SFTP | [SSH.NET](https://github.com/sshnet/SSH.NET) |
| Terminal | [xterm.js](https://xtermjs.org/) via WebView2 |
| UI Framework | WPF + [MahApps.Metro](https://mahapps.com/) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| Icons | MahApps.Metro.IconPacks.Material |

## Building

```bash
dotnet build src\WinSTerm\WinSTerm.csproj
```

## Running

```bash
dotnet run --project src\WinSTerm\WinSTerm.csproj
```

## Requirements

- Windows 10/11
- .NET 10 SDK
- WebView2 Runtime (included with modern Windows)

## License

See [LICENSE](LICENSE) for details.
Windows SSH Terminal
