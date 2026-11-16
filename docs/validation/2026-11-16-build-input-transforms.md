# Build-input transformation investigation

Investigation date 2026-11-16. Validation used Windows x64 and .NET SDK 10.0.403. The reported benchmark used Tada.Cli 0.1.0-alpha.13.105 in workspace `20261116-090523-tada-f5df92`.

## Confirmed verifier defects

`BuildInputs.CheckStable` required every non-generated pre-build input to remain in the post-build evaluated item lists. The benchmark's retained BlazorAdmin reports show `wwwroot/appsettings*.json` under `Content` before build and absent afterward. The SDK's `ResolveProjectStaticWebAssets` target removes consumed assets from `Content`. The verifier treated that item-list transformation as a file change.

Post-build capture now also hashes non-generated paths observed before build. Their existing uses and input classification remain subject to the normal selection, approval, link and byte-integrity checks. Disappearing files and changed bytes still fail. The retained post-build inventory also supplies those inputs to execution-time checks and offline verification.

A disposable benchmark run with that correction exposed a second assumption. `IncludeTransitiveProjectReferences` adds BlazorShared to Infrastructure's project references during build. The verifier rejected the expanded dependency list. Pre-build evaluation now runs this SDK target through the observed task-command path, before dependency capture and dependency-order rebuilding. Post-build dependency equality and offline report checks still apply.

## Benchmark result

Validation used a disposable application copy under `.artifacts/blazor-input-investigation/benchmark-copy`, with approved baseline `sha256:0363540ad2a6efa390b258132e5a1d61558cbb9d1c541df5b43e637f41565c21`.

- Run `5a1a62e02c7d4753a474e8c78a78f9cb` passed the Blazor input check and exposed the transitive-reference check.
- Run `1e811730fef048f6a74f52066e7adbb3` passed both corrected checks and reached the Web project. It remained INDETERMINATE because static-web-asset resolution could not find `wwwroot/css/site.min.css` after bundle cleaning.
- Plain MSBuild reproduced the remaining failure. A Release rebuild starting with absent bundle outputs succeeded and generated them. Repeating the same rebuild failed after `BundleMinifyClean` deleted those previously evaluated assets. `BundleMinify` runs before compilation, after the failing static-asset resolution. The application needs its bundle-generation and asset-discovery ordering reconciled.

The benchmark's checkout scenarios have no recorded TADA PASS from this investigation. Acceptance and enforcement promotion still depend on a successful recorded run after the application build issue is resolved.

## Validation

- The new unchanged-content regression failed before the fix with the reported `Build input changed during build` exception.
- The workspace Release suite passed all 146 tests after both verifier corrections. The local CLI package also built successfully.
- The cached `origin/main` source snapshot at `7985294aad02cc9532ba2e8b0387c934d36cdaa5` passed all 217 Release tests after both verifier corrections. Restore used locked graphs and the build reported zero warnings and errors.
- Unit controls cover consumed content, changed and deleted files, post-capture mutation, and fresh hashing of removed toolchain inputs.
- The complete updated Orders workflow passed all 48 controls. It includes a three-project graph and a target that removes content from the final item list, with checks for real recorded execution, offline verification, dependency capture and the retained content digest. Its report is under `current-main/.artifacts/orders-workflow/64c05b8eb3954945ad83b146a8f78958/validation.json` within the investigation artifacts.
- Fresh-cache installation and the complete onboarding workflow passed all 12 controls with the locally packed CLI. Controls included recorded RED/GREEN, native Debug and Release execution, a behaviour-preserving refactor, unchanged-rerun identities, failure-history preservation, maintenance compilation and a Git-backed context handoff. The report is `.artifacts/onboarding/9beaba3a6b1a40698ce9283fca84ea11/validation.json`.

Detailed command output and disposable run evidence are under `.artifacts/blazor-input-investigation/`. `current-main-fix.patch` provides the corresponding changes against the cached main snapshot. An elevated read-only remote check identified current main as `bd11c12a55ec8079cbde1f50255bd0a078fdc7aa`. Its source, test and build-configuration inputs match the tested snapshot apart from this fix. The patch passed forward and reverse applicability checks, including a forward check against a disposable clone of current remote main.

All verification claims remain local advisory L1. Automated control approvals identify isolated validation fixtures.
