# Contributing

## Workflow

`main` is protected — direct push is blocked, every change goes through a Pull Request.

```bash
# Start from main
git checkout main && git pull

# Branch
git checkout -b feat/your-change

# Hack
# ... edit, dotnet test, dotnet build ...

# Push
git push -u origin feat/your-change

# Open PR
gh pr create --fill
```

CI runs on every PR (`dotnet build -p:TreatWarningsAsErrors=true` + `dotnet test` minus the Npcap-dependent ones).

Merge style is **squash only** — the PR title and body become the squashed commit message. Branches are auto-deleted on merge.

## Branch naming

| Prefix | For |
|---|---|
| `feat/` | New feature |
| `fix/` | Bug fix |
| `refactor/` | Restructuring without behavior change |
| `docs/` | Documentation only |
| `chore/` | Tooling, build, CI |
| `test/` | Test-only changes |

## Releases

Push a tag matching `v*` (e.g. `v0.2.0`). The Release workflow:

1. Runs `scripts/publish.ps1 -SkipTests` on a Windows runner
2. Zips the three exes per-component
3. Creates a GitHub Release with auto-generated notes and all artifacts attached (3 zips + 2 nupkg + 2 snupkg)

Update `Directory.Build.props` `<Version>` first.

## Local development

Required for the full test suite:

- **.NET 10 SDK** — <https://dotnet.microsoft.com/download/dotnet/10.0>
- **[Npcap](https://npcap.com/)** with WinPcap API-compatible Mode (only needed for `PcapWriterTests`)

```bash
dotnet test                                        # full suite
dotnet test --filter "FullyQualifiedName!~PcapWriter"   # without Npcap
.\scripts\publish.ps1                              # local release build
```

## Style

- File-scoped namespaces
- `sealed` on classes by default
- `readonly record struct` for small value types, `sealed record` for reference types
- No comments unless the WHY is non-obvious — clean names + tests over prose
- `TreatWarningsAsErrors=true` everywhere; CI fails on warnings
