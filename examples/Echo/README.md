# Echo onboarding control

## Run the short demonstration

This sample contains one [feature file](echo.feature), a small [ASP.NET Core API](Api/Program.cs) and [HTTP step bindings](EchoBindings.cs). Its two scenarios cover message preservation and empty-message rejection. The short demonstration copies these sources into a disposable application and generates its harness with `tada init --acceptance`. It places the feature under `specifications/functional/`, the bindings under `tests/Acceptance/` and the implementation under `src/`.

From the TADA repository root, with Windows x64, PowerShell 7 and .NET SDK 10.0.403 installed, run these commands in order.

```powershell
dotnet restore src/Tada.Cli/Tada.Cli.csproj --locked-mode
dotnet build src/Tada.Cli/Tada.Cli.csproj --no-restore -c Release
pwsh -NoProfile -File scripts/Show-EchoWorkflow.ps1
```

The demonstration restores harness dependencies from NuGet and launches a loopback API. It records an explicitly labelled automated control approval in its isolated workspace. Business approval for your application requires your own review and decision.

The script checks these outcomes in order.

| Action | Expected result | Exit |
| --- | --- | --- |
| Run against a reachable endpoint returning HTTP 501 | `FAIL`, with `requirement-failure` | 1 |
| Implement the endpoint and run the same approved baseline | `PASS`, two passed cases | 0 |
| Verify the retained passing run | `PASS` | 0 |
| Replace the working status assertion with `Assert.IsTrue(true);`, then attempt a new run | `INDETERMINATE`, with `execution-invalid` explaining the changed oracle | 2 |
| Verify the earlier captured run while the working binding is edited | `PASS` for the captured run | 0 |
| Apply the assertion edit to the disposable captured snapshot, then verify | `INDETERMINATE`, with `integrity` | 2 |
| Restore the original binding bytes and verify again | `PASS` | 0 |

The working assertion edit blocks new execution against the approved baseline. Retained-evidence verification continues to assess the original captured run. Editing its captured assertion breaks that evidence's integrity and makes `verify` refuse PASS. A deliberate assertion change requires a new comparative baseline review and human approval.

Read the [captured transcript](evidence/transcript.txt), [failed result](evidence/fail.json), [passing result](evidence/verify-pass.json), [working assertion-edit result](evidence/oracle-edit.json) and [captured assertion-edit result](evidence/tamper.json). These are actual CLI outputs from the recorded source build, which used the historical `features/` layout. [Capture identity](evidence/demo.json) records the verifier hash, source commit, baseline and run IDs. [Control approval](evidence/approval.json) retains its explicit automated-control attribution.

Each invocation creates `.artifacts/echo-demo/<id>/` and prints its location. That directory retains complete local `.tada/` snapshots, reviews, approvals, run artifacts and failures, alongside `transcript.txt` and each command's JSON. The checked-in outputs are reading excerpts. Offline re-verification uses the complete local workspace. The script restores the working binding and captured snapshot in `finally` and preserves every run history.

Results remain local advisory L1. Team CI can require `tada gate --run <id> --base origin/main` and code-owner review of protected inputs. The independent approval adapter remains proposed work. [CI authority and merge policy](../../docs/ci-pr-workflows.md)

## Full onboarding control

These candidate examples exercise the installed TADA package in a newly initialised project. The [onboarding script](../../scripts/Test-Onboarding.ps1) installs the local package, runs `init` followed by `init --acceptance`, creates an unimplemented HTTP host and meaningful bindings, prepares/reviews an oracle and records an explicitly labelled automated control approval. It establishes assertion RED from HTTP 501, implements the endpoint, establishes GREEN under the same oracle, verifies retained evidence, and commits/reads back a lightweight maintenance/context handoff in a local control Git repository.

The candidate feature demonstrates whitespace preservation and rejection without privileged database observation. The script also extracts the endpoint response function as a behaviour-preserving refactor, reruns the same oracle and performs a warn-as-error build without adding scenarios. Automated control approval is not requester business acceptance. No TADA core changes are made for this project. Source templates are copied into a fresh working directory; ephemeral outputs stay outside the example sources.

The generated API uses a separate implementation library through a reviewed project reference. Bindings request `TadaRuntimeCheckpoint.Complete(api)` before process termination; the helper comes from `tada init --acceptance`. The application has no TADA project/package reference. Located managed assemblies and cooperative native snapshots are retained for each process, with missing final snapshots treated as evidence gaps.
