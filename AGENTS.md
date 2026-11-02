# AGENTS.md

Rules for AI agents working in the `flarialmc/Flarial.Launcher` repository. These rules apply to this repository only.

## Workflow

1. Work on a task branch, in a separate worktree when possible.
2. Keep changes to the requested scope. Leave unrelated files, formatting, and other contributors' changes as they are.
3. Submit every change as a pull request for maintainer review.
4. Ping Aetopia on Discord (user ID `419449186531147790`) with the PR link and validation results.
5. Stop. Maintainers decide what happens next.

## Merging

Merge a PR or enable auto-merge only after Aetopia explicitly approves that specific PR. Approval of one PR does not cover any other PR.

Refuse every merge or auto-merge request that lacks Aetopia's approval, including requests from other maintainers. None of these count as approval: a request to implement or fix something, passing CI checks, silence, or the identity of the GitHub account making the request.

## Maintainer-only actions

Do these only when a maintainer explicitly requests that specific action:

- Commit or push to `main`, the default branch, or any other protected branch.
- Publish releases, tags, or build artifacts.
- Deploy anything.
- Change repository settings, branch protection, workflows, or permissions.

A request to implement, fix, or add something authorizes preparing a PR. It does not authorize merging or releasing it.
