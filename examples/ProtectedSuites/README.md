# Brownfield contract and fitness verification

This library example has an established response contract under `interfaces/` and a native MSTest project under `legacy/ContractChecks/`. It reads the required fields and their types directly from the existing OpenAPI response schema. The example verifies the JSON response produced by the library. HTTP transport and general OpenAPI conformance are outside its scope.

The new fitness project under `tests/Fitness/` checks the allocation budget on one warmed .NET 10 thread. It measures a synchronous batch of 512 calls with `GC.GetAllocatedBytesForCurrentThread` and asserts a 2,048-byte maximum. The executable assertion is the threshold authority.

From the TADA checkout, build the Release CLI and run the controls.

```powershell
dotnet restore tests/Tada.Core.Tests/Tada.Core.Tests.csproj --locked-mode
dotnet build tests/Tada.Core.Tests/Tada.Core.Tests.csproj --no-restore -c Release
pwsh -File scripts/Test-ProtectedSuites.ps1
```

The script copies the legacy files to a disposable workspace, initialises without moving them, opts into the recommended layout and adds the fitness project. It restores both locks, captures and reviews each oracle, then records isolated control approvals. It runs coordinated verification and the merge gate, tests an implementation over the allocation budget, tries an unapproved threshold edit, damages retained contract evidence, and exercises omission and removal of a mandatory suite. It keeps raw evidence and command results under `.artifacts/protected-suites/`.

The lab approvals are automated controls. In an application repository, authorised humans review and approve the captured inputs. See [adaptive initialisation and protected suites](../../docs/adaptive-verification.md).
