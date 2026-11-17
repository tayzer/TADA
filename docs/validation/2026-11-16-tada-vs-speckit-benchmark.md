# TADA vs GitHub Spec Kit: two checkout fixes on eShopOnWeb

**Status:** demonstration, one run per workflow per task (n=1). Treat these results as a worked example, not as
statistical evidence. The disclosures below are part of the result; please read them before quoting any number.

**Date:** 2026-11-16 · **Harness:** [tayzer/tada-speckit-benchmark @ `f679348`](https://github.com/tayzer/tada-speckit-benchmark/tree/f67934890ee344d29ca3458fd8802d8534406d8f)

## Disclosure

- The benchmark was designed and run by the author of TADA. The harness was built with the explicit rule that
  it must not be engineered to favour TADA. Agent settings, permissions, model, effort, the task text and the
  hidden acceptance suites are identical for both workflows and are checked by automated integrity checks.
- During the first task, a TADA defect surfaced on this application (see [TADA fixed mid-benchmark](#tada-fixed-mid-benchmark)).
  TADA was fixed and re-run; Spec Kit was not re-run. That favours TADA.
- The second task was chosen after the first task's results were known.

## Setup

| | |
|---|---|
| Application | eShopOnWeb (ASP.NET Core reference shop), pinned at `f21d6233` |
| Agent | Claude Code CLI 2.1.325, `claude-opus-5-5`, effort `high`, interactive sessions |
| TADA | `v0.1.0-alpha.13.107` (published prerelease, plugin zip SHA-256 `50dbe79d…69e5`), loaded with `--plugin-dir` |
| Spec Kit | `v1.4.0` (`specify-cli 1.4.0`, `0019fa14`) |
| Isolation | `--setting-sources project,local --strict-mcp-config`; same per-run permission policy for both arms; no user-level plugins, hooks or MCP servers |
| Business decisions | A shared register of ten decisions (D1–D10), used to answer any agent question the same way in both arms |
| Correctness | Hidden behavioural xUnit suite run against SQL Server LocalDB with fault injection. The agents never see it |
| Engineering quality | Blinded AI review: Opus 5.5 and Sonnet 5.5, two passes each with the A/B order swapped, scoring a six-dimension rubric (1–5) |

**How each workflow was driven.**
- **Spec Kit:** the task was sent as `/speckit-specify <task>`. The operator then ran `/speckit-plan`,
  `/speckit-tasks`, `/speckit-analyze` and `/speckit-implement`.
- **TADA:** the task was sent as a plain message, and the TADA skill drove the session. The operator answered
  the agent's questions and approved protected baselines with `tada baseline approve`.

## Task 1: duplicate orders on checkout retry

> Customers have reported duplicate orders when checkout is retried after a timeout. Please fix this while
> ensuring customers can still make legitimate subsequent purchases.

### Results

| | Spec Kit | TADA |
|---|---|---|
| Hidden suite (13 tests, 5 critical) | 12/13 | 12/13 |
| Critical requirements failed | 0 | 0 |
| Existing tests broken | 1 of 59 (intended contract change) | 1 of 59 (intended contract change) |
| Agent execution time | 34.2 min | 33.0 min |
| Time the agent waited on the human | 24.2 min | 5.9 min |
| Human inputs | 5 slash commands | 2 messages, 1 answer, 5 shell commands (4 baseline approvals, 1 gate) |
| Cost (Claude Code estimate) | $7.84 | $7.56 |
| Output tokens | 130k | 97k |
| Lines added (all files) | +2,387 | +14,205 |

- **Both arms failed the same test, HT08.** When the checkout page is reloaded while the first payment is
  still in flight, one order is placed correctly, but one of the two submissions returns HTTP 500 (decision
  D7). Neither workflow's own tests covered that case.
- **HT08 was tightened after the runs.** Its no-server-error assertion was added after the AI review pointed
  out the gap, and it applies equally to both arms.
- **Total session time is not comparable.** The Spec Kit session was left open overnight (20 h, against
  73 min for TADA), so only agent time and human waiting time are compared.

### Engineering quality (blinded AI review)

Each range covers both reviewers and both passes.

| Dimension | Spec Kit | TADA |
|---|---|---|
| Maintainability | 3–4 | 3 |
| Unnecessary complexity | 3 | 1–2 |
| Architectural consistency | 3–4 | 3 |
| Error handling | 3–4 | 3–4 |
| Test quality | 4–5 | 2–3 |
| Security and reliability | 4 | 3–4 |

**Spec Kit scored higher on task 1.** The reviewers' main criticisms of TADA's change:

- **Build and tooling changes the task did not need.** TADA's one-time setup enabled repository-wide NuGet
  lock files (about 8,000 generated lines), added a `NuGet.Config` that clears package sources, changed
  bundling in `Web.csproj`, and added a dotnet tool manifest.
- **A new acceptance project that breaks repository conventions.** It uses Reqnroll and MSTest instead of
  xUnit, opts out of central package management, and needs LocalDB.
- **Thin in-repository tests.** The xUnit tests mock the transaction, so the unique index and the race are
  covered only by the acceptance scenarios.

Spec Kit's change was judged more conventional, with stronger xUnit coverage, including SQLite integration
tests.

## Task 2: error page when checkout is reloaded (continuation)

Each workflow continued **its own** task-1 result, so TADA's setup and Spec Kit's `specs/001` were already in
place. Integrity checks confirm that each arm started from its own pinned task-1 run.

> Customers have reported getting an error page at checkout when payment is slow and they reload the checkout
> page and pay again, or pay from two browser tabs. Only one order is placed, but the error page worries them.
> Please fix this.

Before the runs, both starting points failed the same three of the four new hidden tests, in the same way. Both
starting points scored 12/13 on the task-1 suite.

### Results

| | Spec Kit | TADA |
|---|---|---|
| New hidden suite (RL01–RL04) | 4/4 | 4/4 |
| Task-1 suite re-run | 13/13 (HT08 now passes) | 13/13 (HT08 now passes) |
| Existing tests failing in the arm's own final code | 1 of 110 | 0 of 66 |
| Agent execution time | 26.6 min | 12.1 min |
| Time the agent waited on the human | 4.4 min | 6.2 min |
| Human inputs | 5 slash commands | 1 message, 1 answer, 1 baseline approval |
| Cost (Claude Code estimate) | $6.71 | $2.57 |
| Output tokens | 112k | 39k |

**About the existing-test row.** The report also gives Spec Kit "8 of 59" regressions. That count comes from
running the *starting version's* test files, unchanged, against the new code. Spec Kit made a new form field
mandatory and updated those tests to send it. Its updated tests pass, except one
(`IndexTest.OnPostUpdateTo50Successfully`), which also fails in its own final code. The other seven reflect a
deliberate contract change, not broken checkout.

### Engineering quality (blinded AI review)

| Dimension | Spec Kit | TADA |
|---|---|---|
| Maintainability | 3 | 4 |
| Unnecessary complexity | 2–3 | 4–5 |
| Architectural consistency | 4 | 4–5 |
| Error handling | 4 | 3–4 |
| Test quality | 2–4 | 3–4 |
| Security and reliability | 3 | 4 |

**TADA scored higher on task 2.**

- **TADA's change** was small and followed the existing design: one helper, a unique filtered index for one
  order per basket, and two acceptance scenarios against SQL Server. Its main criticism was that the second
  tab lands on *My Orders* rather than the order's confirmation page, which D2 prefers. The operator chose that
  option when TADA asked.
- **Spec Kit's change** was broader: a "basket changed" notice, duplicate detection spread across five paths,
  and a non-unique index, so one order per basket relies on the basket delete failing inside the transaction.
  Its process review noted a hand-written migration that was never applied or tested on SQL Server.
- **Caveat on Spec Kit's test-quality score.** The reviewers saw the "8 regressions" described above without
  being told that the changed tests pass. Treat Spec Kit's test-quality score and part of its reliability
  evidence with caution.

## What this suggests

With n=1 per task, these are observations, not findings.

1. **Correctness was the same.** Both workflows produced working fixes on both tasks. Both missed the same
   edge case (D7) in task 1, and both fixed it in task 2.
2. **TADA's cost is front-loaded.** On the first task, TADA's setup added build and tooling changes that
   reviewers penalised heavily, and its agent time and cost were about the same as Spec Kit's. On the second
   task, with setup already done, TADA used about 45% of Spec Kit's agent time and 38% of its cost, and
   produced the change reviewers preferred.
3. **The human's effort differs in kind.** Spec Kit needs the operator to step through its phases. TADA asks
   business questions and needs explicit baseline approvals. Waiting time was lower for TADA on task 1 and
   similar on task 2.
4. **Findings for TADA itself.**
   - Initial setup makes repository-wide build changes (lock files, `NuGet.Config`, bundling) that reviewers
     regard as out of scope for a bug fix.
   - The acceptance project ignores the host repository's test framework and central package management.
   - A verifier defect on Blazor static web assets surfaced and was fixed (below).
   - The baseline-approval command the agent printed contained a `<your name>` placeholder, which was run
     literally.

## Threats to validity

- **One run per workflow per task.** Variation between runs of the same workflow is unknown and could exceed
  the differences reported here.
- **Author involvement.** The benchmark was designed and operated by the author of TADA, who also answered
  the agents' business questions. The hidden suites and the decision register were approved before the runs
  they scored, except for the HT08 tightening noted above.
- **TADA fixed mid-benchmark.** See below.
- **Task 2 was chosen after task 1.** It was chosen because both arms shared the defect, and both starting
  points were verified to fail it in the same way, but the choice was not blind.
- **Continuation starting points differ.** Task 2 compares each workflow's second change, not two changes to
  the same code.
- **Same-family reviewers.** The AI reviewers are Claude models, the same family as the agent. Blinding was
  imperfect: TADA's file and type names, and Spec Kit's `SC-001` comments, were recognised. Reviewers were told
  to ignore such clues.
- **The operator departed from the register once.** In task 2, TADA's question about the second tab's
  destination was answered with *My Orders*. Decision D2 prefers the confirmation page. The hidden suite
  accepts either, but the reviewers counted it against TADA.
- **Narrow scope.** One application, one stack (.NET 10, Windows, SQL Server LocalDB) and one problem domain
  (checkout idempotency).

### TADA fixed mid-benchmark

A TADA run on `alpha.13.105` hit a verifier defect on eShopOnWeb: *Build input changed during build*, caused
by Blazor static web assets and SDK transitive project references. The fix shipped in `alpha.13.107`
(`BuildInputs.cs`, `RunEngine.cs`; nothing task-specific), and TADA was re-run. The Spec Kit run was kept. TADA
therefore received a fix informed by the benchmark application, and Spec Kit had no equivalent opportunity.
The failed run is kept as evidence. The investigation is recorded in
[2026-11-16-build-input-transforms.md](2026-11-16-build-input-transforms.md).

## Reproducing

The harness, experiment configurations, hidden suites, decision register, AI-review prompts and rubric are in
[tayzer/tada-speckit-benchmark](https://github.com/tayzer/tada-speckit-benchmark/tree/f67934890ee344d29ca3458fd8802d8534406d8f) (commit `f679348`). Run evidence (transcripts, telemetry,
evaluations) is kept outside the repository and is not published.

```powershell
.\bench.ps1 run speckit --experiment checkout-retry
.\bench.ps1 run tada    --experiment checkout-retry
.\bench.ps1 compare     --experiment checkout-retry
.\bench.ps1 ai-review   --experiment checkout-retry
# task 2 continues the task-1 runs pinned in experiments/checkout-reload/experiment.json
.\bench.ps1 run speckit --experiment checkout-reload
.\bench.ps1 run tada    --experiment checkout-reload
.\bench.ps1 compare     --experiment checkout-reload
.\bench.ps1 ai-review   --experiment checkout-reload
```

| Evidence | Task 1 | Task 2 |
|---|---|---|
| Spec Kit run | `20261115-124537-speckit-d26ba9` | `20261116-195523-speckit-6d8add` |
| TADA run | `20261116-134151-tada-485b83` | `20261116-203659-tada-a1d2af` |
| Hidden suite SHA-256 | `044aafa9…4896` | `2b1d070f…d5a8` |
| AI review set | `20261116-164536-7295ce` (protocol `9d9e045a`) | `20261116-212457-13bde5` (protocol `109b71fe`) |
| AI review cost | $4.45 | $3.92 |
