# Security policy

## Supported versions

TADA is an early alpha. Only the newest build on the [releases page](https://github.com/tayzer/TADA/releases) receives fixes.

## Reporting a vulnerability

Please do not report security problems in public issues.

Report them privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**. Include the TADA version (`tada --version`), what you did, what happened and, if you can, a minimal reproduction.

You should get an acknowledgement within a week. Fixes ship as a new prerelease, and the advisory is published once a fix is available.

## What counts

TADA's results are **local advisory (L1)**. Anyone who controls the machine can forge approvals or evidence, and this is a documented limit, not a vulnerability. See [Limits](README.md#limits).

Reports we want to hear about include:

- `tada verify` reporting `PASS` for missing, skipped, stale, truncated or altered evidence, or for oracle files that differ from the approved baseline
- An agent or automated path being able to approve a baseline without the human running the approval command
- Path traversal, unsafe file handling or command injection in the CLI or plugin
- Release assets that do not match their published checksums or source commit
