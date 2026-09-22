# Team Codex Contract

This file is the shared operating contract for every developer and Codex session in this repository. The primary Codex agent is the orchestrator and remains responsible for scope, integration, validation, and the final result.

## Repository baseline

- The current integration and default branch is `main`. The requested `master` branch did not exist when this contract was created; do not create it implicitly.
- Never begin task work by editing `main` directly.
- Assume two other developers and their Codex sessions may be changing the repository at the same time.
- Treat `origin/main`, refreshed with `git fetch origin`, as the integration source of truth.

## Starting every task

1. Understand the request and inspect the relevant part of the repository before editing.
2. Run `git status`, preserve all user changes and unknown untracked files, and stop if safe isolation is unclear.
3. Run `git fetch origin` and inspect new upstream changes.
4. Start from the latest `origin/main`. If the current worktree is unsuitable, use a separate worktree rather than disturbing it.
5. Create a fresh branch named `codex/<github-user>/<task-slug>`. Never reuse one permanent branch for unrelated tasks.
6. Do not edit another developer's task branch or rewrite their commits.

Before the first commit, push, PR, or merge operation in a working session, run:

```text
gh auth status
git config user.name
git config user.email
```

Git authorship and GitHub authentication are separate identities; verify both. Every developer must push with their own GitHub credentials. Never copy tokens, passwords, SSH private keys, or another developer's credentials, and never store secrets in this repository. Do not silently change `user.name`, `user.email`, or the active GitHub account. If GitHub CLI authentication is missing, stop push and PR operations and ask the user to run `gh auth login`. If the wrong account is active, ask the user to run `gh auth switch --user <github-username>` and do not push until identity is correct.

## Using subagents

Use subagents for nontrivial work when specialization or independent investigation materially helps. Do not launch every role for a small edit. Prefer parallel reads and sequential writes.

For a complex feature, use this flow when applicable:

```text
explorer + architect (parallel, read-only)
                |
        orchestrator plan
                |
      implementer or gameplay
                |
              tester
                |
             reviewer
                |
   fix blocker/high findings
                |
    final validation and review
                |
       commit, push, PR, merge
```

- `explorer` maps the real code and dependency surface without editing.
- `architect` designs substantial changes without editing.
- `gameplay` owns engine-verified gameplay and VR implementation.
- `implementer` owns scoped production changes.
- `tester` validates behavior and may edit tests only when explicitly assigned.
- `reviewer` independently reviews the final diff and reports findings before any large rewrite.

Independent read-only work may run in parallel. Never allow multiple write agents to edit the same files concurrently. If parallel implementation is genuinely useful, divide ownership by independent files or subsystems, or use separate task branches/worktrees. Keep subagent tasks bounded; subagents must not create an uncontrolled delegation chain. The orchestrator integrates all results and resolves disagreements.

## Change discipline

- Make the smallest coherent change that satisfies the task.
- Follow existing project architecture and conventions. Do not perform unrelated refactors or repository-wide formatting.
- Verify engine versions, framework packages, and installed APIs from repository files before using them.
- Preserve teammate and user changes. Never delete unknown untracked files as cleanup.
- Treat scenes/maps, prefabs or equivalent serialized assets, project settings, dependency manifests, and binary assets as high-conflict files. Understand both sides of a conflict; never resolve one blindly with `ours` or `theirs`.
- Do not edit gameplay code merely to support tooling or configuration work.

## Validation and commits

Use build, test, lint, and static-analysis commands documented by the repository. If none exist, state that clearly and perform the safest focused verification available. Never claim an unrun check passed.

Before each logical commit:

1. Inspect `git status` and `git diff`.
2. Exclude secrets, credentials, generated junk, build caches, and local IDE files.
3. Run relevant checks.
4. Use a concise conventional message such as `feat: add VR grabbing system`, `fix: prevent duplicate object pickup`, `refactor: isolate locomotion input`, or `test: add interaction regression tests`.

Do not use vague messages such as `changes`, `update`, or `fix stuff`.

## Push, review, and merge

After local validation, push the task branch with `git push -u origin <task-branch>` and open a pull request targeting `main`. The PR must summarize the task, changed behavior, important architecture decisions, validations performed, and known limitations.

Before merge:

1. Run an independent `reviewer` subagent against the task branch diff relative to freshly fetched `origin/main`.
2. Do not merge with blocker or high-severity findings. Fix them, rerun relevant checks, and repeat review.
3. Run `git fetch origin` again. If `origin/main` changed, synchronize the task branch, resolve conflicts semantically, rerun relevant checks, push, and repeat review.
4. Confirm required GitHub checks are successful. Never bypass branch protection or required human approval.
5. Prefer squash merge when repository settings allow it. Delete the remote task branch after a successful merge when it is no longer needed.
6. Fetch once more and verify the merged change is present in `origin/main`.

Minor review suggestions may be addressed when useful, but avoid endless refactor loops.

## Prohibited Git operations

Without explicit human direction, never:

- run `git push --force` or `git push -f`;
- force-push to, delete, or rewrite the history of `main`;
- run `git reset --hard` when it could destroy user or teammate work;
- destructively reset, mass-revert, or overwrite changes you do not understand;
- force-push another developer's branch.

## Engine-specific rules

No engine or gameplay stack is committed yet, so engine-specific rules cannot be asserted safely. When an engine is introduced, update this section and `docs/ARCHITECTURE.md` in the same PR using facts from committed project/version/dependency files. At minimum, document generated directories, serialized/binary asset handling, metadata rules, lifecycle constraints, the XR/input framework, and verified build/test commands for the exact installed version.

## Definition of Done

A task is done only when its requirements are implemented, relevant validation passes, the reviewer has no blocker/high-severity findings, the diff contains no accidental changes, and the result is committed, pushed, reviewed in a PR, and merged into `main` when repository rules permit. If checks, branch protection, conflicts, authentication, or human approval block merge, leave the PR ready and report the exact blocker instead of bypassing it.
