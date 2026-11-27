# Agent-only Codex benchmarks on a two-release inventory API

**Run dates:** 26–27 November 2026. **Scope:** one ASP.NET Core and SQLite application, one model, three measured handoff campaigns per workflow.

This benchmark compares plain Codex, TADA, GitHub Spec Kit and OpenSpec. It measures delivered behaviour, test quality and agent effort. The TADA project ran the comparison and had an interest in the outcome. The [earlier Claude Code study on eShopOnWeb](2026-11-16-tada-vs-speckit-benchmark.md) is separate evidence.

The [machine-readable results](2026-11-27-codex-sdd-results.json) contain every scheduled ticket, including the original pilot outcomes, pilot recovery, smoke and repeated comparison. They include the run IDs, tool and fixture digests, stage dispatch, usage, test counts and recovery lineage. Transcripts, prompts, review text and private execution logs remain with the operator.

## Design

| Input | Pin or protocol |
| --- | --- |
| Agent | Codex CLI `0.162.1`, `gpt-6.1-sol`, medium reasoning, for all workflows and test reviews |
| Workflows | TADA `0.1.0-alpha.13.107`, GitHub Spec Kit `1.4.0`, OpenSpec `2.0.0`, and plain Codex |
| Application | ASP.NET Core and SQLite on .NET SDK `10.0.403`, with a pinned starting tree and locked dependencies |
| Execution | Dedicated Windows worker, isolated workspaces, loopback-only application tests, no interactive approvals during the batch, and 20 minutes of coding per ticket |
| Schedule | Seed `1729`. Runs were serial. The workflow order rotated across repeats. |

Every arm received the same business briefs, API contracts and HTTP test scaffold. The TADA arm also received equivalent executable Gherkin and bindings. The owner approved those exact bytes before the batch, and the elapsed and token measurements begin after that approval. TADA ran `tada run` and `tada verify`. Spec Kit ran specify, plan, tasks and implement. OpenSpec ran propose and apply. Plain Codex implemented directly from the shared instructions.

The handoff starts from a small application with cancellation and resize working. Ticket 1 adds partial shipment with durable idempotent confirmation, partial cancellation and in-place legacy database upgrade. Ticket 2 allows a dispatched reservation to resize at or above the fulfilled quantity, preserving stock and prior receipts. A passing Ticket 1 release provides the code, tests, workflow artifacts and an API-seeded SQLite database for its own Ticket 2. Ticket 2 starts in a fresh Codex session and receives the follow-up brief at promotion. OpenSpec archives its first change at that boundary.

The common evaluator runs the original client tests and private HTTP assertions under the worker policy. There are 21 scheduled checks for Ticket 1 and 23 for Ticket 2. For fault detection, the same added test must pass twice on the submission and reference and fail by assertion twice on the faulty variant. Test reviewer sessions receive numbered test files and the brief. They use the same model in fresh, read-only sessions, with workflow names masked and input capped at 32,000 characters. Some reviews therefore cover only part of a suite. Reviewer usage is recorded separately.

## Single-ticket pilot and recovery

The pilot ran one attempt per workflow on each of two tasks. The first fixed repeat cancellation so it restored stock once. The second added atomic reservation resizing with insufficient-stock rejection. The original pilot had **2 of 8 passes**. Four build evaluations stopped during Windows sandbox setup refresh. Two OpenSpec attempts failed workflow activation because the CLI was unavailable to the worker.

| Workflow | Original pilot pass / scheduled | Recovery pass / scheduled | Recovery action |
| --- | ---: | ---: | --- |
| Plain Codex | 2/2 | 2/2 | Reused both sealed results |
| TADA | 0/2 | 2/2 | Re-evaluated both sealed submissions |
| OpenSpec | 0/2 | 2/2 | Coded both tasks again after staging the pinned CLI |
| GitHub Spec Kit | 0/2 | 2/2 | Re-evaluated both sealed submissions |

The recovery passed all eight tickets and caught all three scored defects on each evaluated ticket. TADA's two local advisory verdicts were `PASS`. OpenSpec had a fresh coding attempt while the other workflows kept their original coding evidence, so the recovery serves as a setup demonstration.

## Handoff smoke and repeated comparison

The smoke passed all four campaigns and all eight tickets. Every arm completed its handoff, and every evaluated ticket caught all five scored defects. The smoke is separate from the repeated results below.

The repeated comparison passed **10 of 12 campaigns** and **22 of 24 tickets**. All 12 handoffs took place. OpenSpec repeat 2 had an indeterminate Ticket 2 after Windows denied a loopback bind during one evaluator check. Spec Kit repeat 3 reached the 20-minute Ticket 2 coding limit during implement. Both remain in the denominator. TADA's six local advisory verifications matched the common evaluator's `PASS` results.

| Workflow | Both tickets passed | Median coding minutes (range) | Median agent messages | Median uncached input tokens | Median output tokens |
| --- | ---: | ---: | ---: | ---: | ---: |
| Plain Codex | 3/3 | 8.1 (7.7–8.7) | 9 | 59,669 | 15,293 |
| TADA | 3/3 | 15.0 (13.5–20.2) | 13 | 80,991 | 22,398 |
| OpenSpec | 2/3 | 19.2 (16.1–22.2) | 23 | 261,860 | 45,113 |
| GitHub Spec Kit | 2/3 | 35.0 (34.0–36.0) | 35.5 | 445,933 | 169,569 |

Medians and ranges cover successful two-ticket campaigns, with each metric calculated independently. Input shown here excludes cached tokens. The JSON includes both total and cached input, along with tool calls, turns, total elapsed time and incomplete usage. Plain Codex was fastest overall. TADA had the lowest median time and output tokens among the three spec-driven workflows on this task and model pin. The sample is descriptive.

### Repeated ticket outcomes

Hidden checks are shown as passed, failed, unknown and scheduled. The JSON also records the original regressions, native TADA verdicts and per-campaign totals.

| Ticket slot | Status | Hidden checks | Defects caught | Coding seconds | Usage |
| --- | --- | ---: | ---: | ---: | --- |
| `ticket-1-plain-01` | pass | 21/0/0/21 | 5/5 | 294.6 | Complete |
| `ticket-2-plain-01` | pass | 23/0/0/23 | 5/5 | 189.3 | Complete |
| `ticket-1-speckit-01` | pass | 21/0/0/21 | 5/5 | 1028.8 | Complete |
| `ticket-2-speckit-01` | pass | 23/0/0/23 | 5/5 | 1013.1 | Complete |
| `ticket-1-openspec-01` | pass | 21/0/0/21 | 5/5 | 505.4 | Complete |
| `ticket-2-openspec-01` | pass | 23/0/0/23 | 5/5 | 462.6 | Complete |
| `ticket-1-tada-01` | pass | 21/0/0/21 | 5/5 | 834.5 | Complete |
| `ticket-2-tada-01` | pass | 23/0/0/23 | 5/5 | 374.5 | Complete |
| `ticket-1-speckit-02` | pass | 21/0/0/21 | 5/5 | 1080.1 | Complete |
| `ticket-2-speckit-02` | pass | 23/0/0/23 | 5/5 | 1078.2 | Complete |
| `ticket-1-openspec-02` | pass | 21/0/0/21 | 5/5 | 587.2 | Complete |
| `ticket-2-openspec-02` | indeterminate | 22/0/1/23 | 5/5 | 519.2 | Complete |
| `ticket-1-tada-02` | pass | 21/0/0/21 | 5/5 | 462.5 | Complete |
| `ticket-2-tada-02` | pass | 23/0/0/23 | 5/5 | 350.2 | Complete |
| `ticket-1-plain-02` | pass | 21/0/0/21 | 5/5 | 290.8 | Complete |
| `ticket-2-plain-02` | pass | 23/0/0/23 | 5/5 | 230.0 | Complete |
| `ticket-1-openspec-03` | pass | 21/0/0/21 | 5/5 | 541.5 | Complete |
| `ticket-2-openspec-03` | pass | 23/0/0/23 | 5/5 | 791.9 | Complete |
| `ticket-1-tada-03` | pass | 21/0/0/21 | 5/5 | 511.2 | Complete |
| `ticket-2-tada-03` | pass | 23/0/0/23 | 5/5 | 390.9 | Complete |
| `ticket-1-plain-03` | pass | 21/0/0/21 | 5/5 | 251.6 | Complete |
| `ticket-2-plain-03` | pass | 23/0/0/23 | 5/5 | 211.5 | Complete |
| `ticket-1-speckit-03` | pass | 21/0/0/21 | 5/5 | 1153.1 | Complete |
| `ticket-2-speckit-03` | timed-out | 0/0/23/23 | — | 1202.4 | Incomplete |

### Source and test review

A separate static review inspected 24 sealed snapshots with workflow labels visible. Every workflow used SQLite transactions to group its writes. All 12 Ticket 2 snapshots changed the same resize guard and retained the transaction. The timed-out Spec Kit snapshot contained the change and added tests, with validation incomplete. The application changes did not establish a clear maintainability winner.

All 23 evaluated tickets caught all five scored defects, yielding 115 detections from 115 scored opportunities. One flaky test case and 42 fixture-error flags were recorded separately. The review found distinctions that the fault score did not capture. All three TADA follow-ups tested competing resizes. Some OpenSpec and Spec Kit suites checked resize against concurrent confirmation or cancellation. Plain Codex repeat 2 checked exact receipt fields before and after a restart. Concurrent HTTP requests could still execute serially in these tests, and the added tests did not induce a failure between database writes.

The reference returned `200` for multiple idempotency keys combined into one header value. In 27 case results across the 23 evaluated snapshots, the same test expected `400`, passed twice on the submission and failed twice on the reference. The scorer therefore excluded those results. Separate .NET header checks also showed that TADA repeat 1 and OpenSpec repeat 2 could accept a blank duplicate alongside a valid key because their list parser dropped the blank entry. The retained comparison keeps its original scoring. A subsequent benchmark revision should align the reference and the private evaluator on these cases before collecting new results.

### Run provenance

| Report | Batch ID | Scheduled tickets |
| --- | --- | ---: |
| Original pilot | `codex-sdd-20261126-092941-6ca44702` | 8 |
| Pilot recovery | `codex-sdd-20261126-103112-7fbb5e64` | 8 |
| Handoff smoke | `codex-sdd-handoff-20261126-141353-8a6efffb` | 8 |
| Repeated handoff | `codex-sdd-handoff-20261127-093637-fc6807b8` | 24 |

The repeated run continued from `codex-sdd-handoff-20261126-174616-d4d3b005` after a controller interruption. The continuation verified and copied six sealed tickets, then dispatched the 18 unfinished slots in the original order. The interrupted first TADA ticket restarted in a fresh session. Its incomplete transcript remains in the source batch and is outside the recorded ticket usage totals. The controller gained recovery dispatch and reporting. Both runner digests are in the JSON, and all 25 other recorded pins matched the smoke run.

The retained repeated Markdown and JSON reports were regenerated byte-for-byte from a copy containing only the sealed manifest and attempt evidence, without model calls. The checked-in JSON projects the numeric results and provenance from those retained reports.

## Reproduce

Use the [pinned benchmark harness](https://github.com/tayzer/tada-speckit-benchmark/tree/ebdb0a7cff0f84cf0d47cffae2aacfab7209a669), the [Windows worker and pilot instructions](https://github.com/tayzer/tada-speckit-benchmark/blob/ebdb0a7cff0f84cf0d47cffae2aacfab7209a669/docs/codex-batch.md), and the [handoff protocol](https://github.com/tayzer/tada-speckit-benchmark/blob/ebdb0a7cff0f84cf0d47cffae2aacfab7209a669/docs/codex-handoff-benchmark.md). The checked-in fixture trees preserve the live fixture digests. Set up the worker, stage the locked dependencies and request fresh owner approval for the exact prepared packages before making live model calls.

```powershell
.\bench.ps1 batch --suite codex-sdd --dry-run
.\bench.ps1 batch --suite codex-sdd --model gpt-6.1-sol
.\bench.ps1 batch --suite codex-sdd-handoff --model gpt-6.1-sol
.\bench.ps1 batch --suite codex-sdd-handoff --model gpt-6.1-sol --repeats 3
.\bench.ps1 batch-report --batch '<retained batch directory>'
```
