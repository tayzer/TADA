# Contributing to TADA

Thanks for your interest. TADA is an early alpha with a single maintainer, so issues, feedback from real use and focused pull requests are all welcome.

## Before you start

- **Bugs and ideas:** open an [issue](https://github.com/tayzer/TADA/issues/new/choose) first. Feedback from using TADA on a real project is especially useful, including when the agent asked the wrong questions or the workflow got in your way.
- **Larger changes:** please open an issue to discuss the approach before writing a large pull request. Some behaviour is deliberate, and the [decision records](docs/decisions/) explain why.
- **Security problems:** do not open a public issue. See [SECURITY.md](SECURITY.md).

## Development setup

You need:

- Windows x64
- The .NET SDK version pinned in [`global.json`](global.json)
- PowerShell 7
- Git for Windows, with long paths enabled in your clone: `git config core.longpaths true`

Build and run the focused and replay tests:

```powershell
dotnet build tests/Tada.Core.Tests/Tada.Core.Tests.csproj -c Release
dotnet tests/Tada.Core.Tests/bin/Release/net10.0/Tada.Core.Tests.dll --progress off
```

Pack the CLI:

```powershell
dotnet pack src/Tada.Cli/Tada.Cli.csproj -o .artifacts/packages
```

End-to-end controls run against real HTTP APIs and SQLite in disposable copies. Run the ones relevant to your change:

| Script | What it covers |
| --- | --- |
| `scripts/Test-OrdersWorkflow.ps1` | Business, integrity and invalid-evidence controls |
| `scripts/Test-EnforcementWorkflow.ps1` | The candidate/active enforcement lifecycle |
| `scripts/Test-MultiServiceWorkflow.ps1` | Acceptance runs across several services |
| `scripts/Test-Onboarding.ps1` | Fresh-cache installation, `init`, maintenance checks and context handoff (needs NuGet network access) |
| `scripts/Test-TadaPlugin.ps1` | Plugin packaging and setup |
| `scripts/Test-ReleaseBundle.ps1` | Download bundle installation and corruption rejection |

Run each with `pwsh -File <script>`. Some restricted sandboxes block Reqnroll task-host startup or atomic directory renames. If a control fails for that reason, it is an environment problem, not a behavioural failure; rerun it with the required permissions rather than weakening the check.

## Rules the code must keep

These are the product's core promises. Changes that weaken them will not be accepted.

- **Only a human approves expectations.** Code, scripts and agents must never approve business Gherkin on a person's behalf. Test fixtures may create approvals only when they are clearly labelled as automated controls in isolated test projects.
- **Evidence is exact.** Oracle files are hashed as exact bytes. Baseline, review and approval records are immutable, and failures and gaps are retained rather than dropped.
- **Verdicts stay distinct.** `PASS`, `FAIL` and `INDETERMINATE` mean different things. A green native test runner on its own is not a TADA `PASS`.
- **Proportionate checks.** Chores and refactors use existing checks; do not invent business scenarios for them.

## Dependencies

Restores are locked. Keep dependencies minimal. If you add or update a package, commit the changed `packages.lock.json` files deliberately, and update [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when redistributed content changes.

## Decision records

Accepted records in [`docs/decisions/`](docs/decisions/) are historical. Do not rewrite them. To change a decision, add a new record that supersedes the old one.

## Pull requests

- Branch from `main` and keep each pull request to one concern.
- Use a conventional prefix in commit messages and pull request titles: `feat:`, `fix:`, `docs:`, `test:`, `ci:` or `chore:`.
- Describe what you changed, why, and which tests or controls you ran.
- Update the README or docs when user-visible behaviour changes.
- CI must pass. Every validated merge to `main` publishes a new prerelease automatically, so the maintainer decides when a pull request is merged.

## Licence

By contributing, you agree that your contributions are licensed under the [MIT licence](LICENSE).
