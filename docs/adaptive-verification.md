# Adaptive initialisation and protected suites

TADA uses the repository's existing specifications and checks when they already express the agreed expectation. A layout preference guides future work. Human approval covers the executable oracle, and the enforcement ledger records which approved obligations are required for merge.

## Choose a project layout

| Project | Initialisation | Effect |
| --- | --- | --- |
| New project | `tada init` | Selects `recommended` and writes the workflow, context and project preference. A README or Git metadata alone is compatible with greenfield setup. |
| Established project | `tada init` | Reports discovered files and selects `existing` by default. Use the existing developer commands, test projects and specification paths. Register a relevant supported suite when its scope and authority are understood. |
| Established project adopting TADA conventions | `tada init --layout recommended` | Records a preference for new work alongside existing files, projects and CI. |
| Previously initialised project | `tada init` | Reuses the choice in `.tada/layout.json`. Legacy repositories with `tada.json` and no layout preference start with `existing`. |

`--layout existing` explicitly selects the existing convention. Discovery is a filesystem inventory of common project, test and specification files. Confirm ownership, existing execution commands and the authority of each artifact during discovery. Ask a focused question if multiple conventions would materially change the setup.

The recommended convention has these paths. Create directories and verification infrastructure as needed.

| Path | Purpose |
| --- | --- |
| `specifications/functional/` | Agreed behaviour, commonly Gherkin |
| `specifications/non-functional/` | Agreed quality attributes, thresholds and constraints |
| `specifications/contracts/` | Authoritative interfaces and interoperability expectations |
| `src/` | Project implementation |
| `tests/Acceptance/` | Business-facing verification |
| `tests/Contract/` | Producer, consumer and interface verification |
| `tests/Fitness/` | Performance, reliability, security, architecture and other fitness checks |
| `tests/Integration/` and `tests/Unit/` | Supporting engineering checks |
| `.tada/` and `tada.json` | Workflow state and, once selected, the functional baseline configuration |

For a new supported .NET acceptance harness, use `tada init --acceptance` after discovering the need. It generates the Reqnroll/MSTest harness, the functional oracle roots and the feature glob. Add agreed feature files and their bindings. Contract, fitness, integration and unit projects are created only when selected. In an existing repository with a vacant `tests/Acceptance/`, the same option can be combined with `--layout recommended`. Bring the repository's own stable .NET SDK policy and NuGet configuration to that setup. If an acceptance project already exists, register it manually in `tada.json`. Other stacks keep their existing execution tools.

Opting into the layout leaves `tada.json`, legacy `features/`, existing test projects and all retained records intact. To extend an existing harness to read both functional roots, add the new root to its project glob and to `oracleRoots`, retain the old root, then prepare, compare and obtain human approval for the replacement before using it. The layout preference itself is workflow guidance. Keep it outside oracle roots.

Run the [greenfield Echo walkthrough](../examples/Echo/README.md) to see lazy functional setup. The [brownfield protected-suite example](../examples/ProtectedSuites/README.md) starts with an existing contract and adds a fitness suite under the recommended layout.

## Agree the obligation where it lives

During discovery, check affected behaviour, interfaces and consumers, compatibility, performance and scale, resilience, data integrity, security, accessibility, architecture and operability. Use the effects and risk of the change to choose depth and scope. For a measurable threshold, agree the workload, measurement method and relevant environment. Record necessary human review or other evidence when meaningful automation is unavailable.

A feature file, OpenAPI document, executable pact, fitness assertion or tool-native threshold can be the authority. Reference the original wherever it already expresses the expectation. Select and protect the effective oracle, including how fixtures and tests interpret that expectation. Supporting implementation-level tests can change without individual approval. A directory or framework provides a discovery clue. Agreement determines which checks are protected.

## Run an independent protected suite

The current adapter supports dedicated MSTest 4.5.2 and MTP 2.5.2 projects on the supported Windows x64 and .NET 10 profile. Enable `EnableMicrosoftTestingExtensionsTrxReport` and use a locked restore. Write a suite definition like the [existing contract example](../examples/ProtectedSuites/contract.verification.json).

- Use a stable lowercase ID, a kind of `contract` or `fitness`, and the exact unique native test names reported by `--list-tests`. Discovery must match this set.
- Specify the runner project, assembly and TRX filename under `configuration.execution`. `configuration.oracleRoots` captures assertions, fixture code, mutable interpretation inputs and the original contract or threshold. Pin and capture imports, locks and shared fixture dependencies. Use `configuration.obligations` to label authoritative roots.
- Select application source under `configuration.implementationRoots`. A verifier project may reference an application project. Its own compiled assertions, imports and fixtures must be captured as oracle inputs.
- Prepare, review the exact captured files and obtain human approval. This suite can run with or without a Gherkin baseline.

```powershell
tada baseline prepare --file contract.verification.json
tada baseline review --baseline <digest>
tada baseline approve --review <review-id> --by <human-name>
tada run --baseline <digest>
tada verify --run <run-id>
```

Use `--against <old-digest>` during amendment review and `--replaces <old-digest>` at approval. The suite keeps its ID, kind and required test identities. Captured source edits invalidate live use of the old approval. Offline verification still checks an earlier run against its original snapshot, approval and evidence.

In a coordinated [verification plan](multi-level-verification.md), use the suite ID as the check ID, its kind as the check purpose and its digest as the baseline. The same plan can include functional acceptance and delegated engineering checks. Plan completion covers the selected checks. The merge gate independently checks the ledger's mandatory scope.

## Require a suite for merge

Use enforcement ledger schema 2 and list the approved native baselines in `protectedSuites`. Keep any existing functional `baseline`, `active` and `candidates` entries. With only native suites, set `baseline` to null and the scenario lists to empty arrays. Native suites enrolled in the ledger are mandatory. Candidate status still applies to functional scenarios.

```json
{
  "schemaVersion": 2,
  "baseline": null,
  "active": [],
  "candidates": [],
  "protectedSuites": ["<approved-contract-digest>", "<approved-fitness-digest>"]
}
```

Run the coordinated plan and pass its run ID to `tada gate --run <run-id> --base <target-branch>`. A single native suite can also use its direct run ID. The gate checks current oracle and implementation identities, retained evidence and coverage. Base comparison catches removed or reclassified suites and unapproved replacement lineage even if the plan itself passes. Teams must require independent review of protected changes, including first enrolment, through their repository controls.

Every check selected in a supplied plan contributes failures and gaps. This includes checks outside the ledger. Functional candidates in the ledger keep their existing enforcement policy. A gate run without `--base` reports `baseBaseline: "not-compared"` for local status. Required CI must supply the trusted target revision to establish continuity with previously approved obligations.

A failed supported MSTest assertion can establish FAIL. Missing, stale, skipped, incomplete or invalid evidence blocks PASS. The pinned TRX adapter is deliberately limited to unique names and recognised MSTest assertion failure reports. The local L1 SDK, package, build-task and runtime trust limits still apply. Dedicated external adapters, authorised team approval, and native-suite candidate and retirement workflows remain open.
