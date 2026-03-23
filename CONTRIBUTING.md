# Contributing to NetSterm

Welcome, and thank you for considering contributing to **NetSterm**! Every contribution — whether it's a bug report, feature idea, documentation improvement, or code change — helps make this project better.

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before participating.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Clone & Build

```bash
git clone https://github.com/Paregov/WinSTerm.git
cd WinSTerm
dotnet build src/NetSterm/NetSterm.csproj
```

### Run

```bash
dotnet run --project src/NetSterm/NetSterm.csproj
```

## How to Contribute

### Bug Reports

Found a bug? [Open an issue](https://github.com/Paregov/WinSTerm/issues/new) with:

- **Steps to reproduce** — minimal, concrete steps
- **Expected behavior** — what you expected to happen
- **Actual behavior** — what actually happened
- **Environment** — OS, .NET version (`dotnet --version`), NetSterm version
- **Screenshots / logs** — if applicable (logs are in the `logs/` folder)

### Feature Requests

Have an idea? Open an issue with the **"Feature Request"** label. Describe the use case and why it would benefit users.

### Code Contributions

1. Check [open issues](https://github.com/Paregov/WinSTerm/issues) for something to work on, or propose your change in a new issue first.
2. Follow the development workflow below.

## Development Workflow

1. **Fork** the repository on GitHub.
2. **Clone** your fork locally.
3. **Create a branch** from `main` using the naming convention below.
4. **Implement** your changes.
5. **Test** that the application builds and runs correctly.
6. **Commit** using conventional commit messages.
7. **Push** your branch and open a **Pull Request** against `main`.

## Branch Naming

| Prefix | Purpose |
|--------|---------|
| `feature/` | New features — e.g. `feature/sftp-resume-transfer` |
| `fix/` | Bug fixes — e.g. `fix/tab-close-crash` |
| `docs/` | Documentation changes — e.g. `docs/update-readme` |

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add SFTP drag-and-drop upload
fix: prevent crash when closing last tab
docs: update build instructions in README
refactor: extract SSH connection logic into service
chore: update NuGet dependencies
```

Keep the subject line under 72 characters. Use the body for additional context when needed.

## Code Style

- **File-scoped namespaces** — use `namespace Foo;` instead of `namespace Foo { }`.
- **Private fields** — prefix with underscore and use camelCase: `_connectionService`.
- **MVVM pattern** — ViewModels use [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) source generators (`[ObservableProperty]`, `[RelayCommand]`).
- **Consistent formatting** — follow the existing code style in the repository.

## Architecture Notes

NetSterm follows an **MVVM** architecture:

| Layer | Technology | Location |
|-------|-----------|----------|
| **Views** | Avalonia AXAML | `src/NetSterm/Views/` |
| **ViewModels** | CommunityToolkit.Mvvm | `src/NetSterm/ViewModels/` |
| **Services** | Plain C# classes | `src/NetSterm/Services/` |
| **Models** | POCOs / data classes | `src/NetSterm/Models/` |

Key libraries:

- **SSH.NET** — SSH and SFTP connections
- **WebView.Avalonia + xterm.js** — terminal emulation
- **Avalonia Fluent theme** — UI styling

## Pull Request Process

1. **Describe your changes** clearly in the PR description.
2. **Reference related issues** — use `Closes #123` or `Fixes #123`.
3. **Ensure the project builds** — run `dotnet build src/NetSterm/NetSterm.csproj` before submitting.
4. **Keep PRs focused** — one logical change per PR.
5. A maintainer will review your PR and may request changes.

## Questions?

Open a [GitHub Discussion](https://github.com/Paregov/WinSTerm/issues) or issue — we're happy to help!

Thank you for contributing! 🚀
