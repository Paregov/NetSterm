# AGENTS.md — AI Agent Instructions

## Project Overview

NetSterm is a cross-platform SSH terminal application inspired by MobaXterm, built with .NET 10 and Avalonia UI. It provides multi-tab SSH sessions, SFTP file browsing, command snippets, and session management with encrypted credentials.

## Build & Run

- **Build**: `dotnet build src/NetSterm/NetSterm.csproj`
- **Run**: `dotnet run --project src/NetSterm/NetSterm.csproj`
- **Solution**: `NetSterm.slnx` (VS 2024 slnx format)
- **Installer**: `build-installer.ps1` (wraps Inno Setup via `installer/netsterm-setup.iss`)
- **Target**: .NET 10 (`net10.0`), cross-platform

## Project Structure

```
src/NetSterm/
├── Assets/               # netsterm.ico, terminal.html (xterm.js v5)
├── Converters/           # XAML value converters (8 converters)
├── Models/               # Data models (ConnectionInfo, CommandSnippet, SftpFileItem, etc.)
├── Services/             # Business logic (SSH, SFTP, Encryption, Storage)
├── ViewModels/           # MVVM ViewModels (CommunityToolkit.Mvvm source generators)
├── Views/                # Avalonia AXAML views, controls, and dialogs
├── Properties/           # PublishProfiles (NetSterm-Release.pubxml)
├── App.axaml(.cs)        # Application entry, themes, global resources
├── MainWindow.axaml(.cs) # Main shell window (~800 XAML, ~1170 code-behind)
├── Program.cs            # Entry point with Serilog bootstrap
└── app.manifest          # Windows supportedOS manifest (required by WebView)
```

## Architecture

- **UI Framework**: Avalonia 11.x with FluentTheme (Dark variant)
- **MVVM**: CommunityToolkit.Mvvm 8.4.1 with source-generated `[ObservableProperty]` and `[RelayCommand]`
- **SSH/SFTP**: SSH.NET library — `SshConnectionService` manages ShellStream, `SftpService` manages file operations
- **Terminal**: WebView.Avalonia hosting xterm.js v5 via `Assets/terminal.html`
- **Encryption**: AES-256-CBC + PBKDF2 (100K iterations, SHA256) with fixed salt — cross-platform replacement for DPAPI
- **Icons**: Material.Icons.Avalonia (requires explicit `StyleInclude` in App.axaml)
- **Logging**: Serilog with File + Debug sinks, rolling daily (7 day retention)

## Key Conventions

- **Namespaces**: File-scoped (`namespace NetSterm.Services;`)
- **Private fields**: `_camelCase` (required by CommunityToolkit.Mvvm `[ObservableProperty]`)
- **Commit messages**: Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `chore:`)
- **XAML**: 2-space indentation
- **C#**: 4-space indentation, `var` when type is apparent
- **Nullable**: Enabled globally
- **Implicit usings**: Enabled globally

## Avalonia-Specific Gotchas

- **AttachedToVisualTree fires ONLY ONCE** — when toggling `IsVisible`, use `Dispatcher.UIThread.Post` with `DispatcherPriority.Background` instead
- **StaticResource forward-reference** — resources must be defined BEFORE use in XAML (same as WPF)
- **NativeControlHost requires app.manifest** — WebView.Avalonia needs `supportedOS` entries or crashes on Windows
- **Material.Icons.Avalonia** — requires explicit `StyleInclude`; does NOT auto-register
- **Resource paths are case-sensitive** — exact casing matters on Linux
- **ContentControl+DataTemplate reuse** — for persistent state (WebView terminals), use `ItemsControl` + `IsVisible` toggle instead
- **Unloaded fires on IsVisible change** — guard with `GetVisualRoot() == null`

## Storage Locations

All under `%AppData%/NetSterm/`:

| File | Purpose |
|------|---------|
| `connections.json` | Saved SSH connections (passwords encrypted via `EncryptionService`) |
| `settings.json` | Application settings |
| `snippets.json` | Command snippet library |
| `logs/NetSterm-YYYYMMDD.log` | Daily rolling logs (7 day retention) |

## Testing Notes

- No unit tests yet — validate changes by building successfully (`dotnet build` must pass with 0 errors)
- Manual testing required for SSH connections and UI interactions
- Build must pass with 0 errors before committing
