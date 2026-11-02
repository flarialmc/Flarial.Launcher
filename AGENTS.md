# AGENTS.md

Rules for AI agents working in `flarialmc/Flarial.Launcher`.

## Workflow

1. Work on a task branch, in a separate worktree when possible.
2. Keep changes to the requested scope. Leave unrelated files, formatting, and other contributors' changes as they are.
3. Submit every change as a pull request for maintainer review.
4. Stop after you report the PR link and validation results. Maintainers decide what happens next.

## Maintainer-only actions

Do these only when a maintainer explicitly requests that specific action:

- Commit or push to `main`, the default branch, or any other protected branch.
- Merge a PR or enable auto-merge.
- Publish releases, tags, or build artifacts.
- Deploy anything.
- Change repository settings, branch protection, workflows, or permissions.

A request to implement, fix, or add something authorizes preparing a PR. It does not authorize merging or releasing it.
