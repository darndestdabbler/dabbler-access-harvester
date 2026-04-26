# Session Set Explorer — VS Code extension

A small workspace VS Code extension that adds a "Session Set Explorer" view
to its own Activity Bar container — the leftmost icon strip in VS Code; look
for the checklist icon. Surfaces every session set under
`docs/session-sets/` grouped by state (In Progress / Not Started / Done) with
progress and last-touched date. State is derived from file presence the same
way `ai_router.find_active_session_set` and
`ai_router.print_session_set_status` do — `change-log.md` → done,
`session-state.json` OR `activity-log.json` without it → in-progress, only
`spec.md` → not-started.

`session-state.json` is the earliest in-progress signal: it is written at
the start of each session by the orchestrator (Step 0 of
`docs/ai-led-session-workflow.md`), before any activity-log entry exists.
This means a freshly-started session set flips to In Progress as soon as
the orchestrator finishes its bootstrap — no longer waiting for the first
session's commit. The tooltip on each in-progress set surfaces the
current session number, the orchestrator (engine + model + effort), and
the latest verification verdict when present.

The extension never writes. It only watches `docs/session-sets/**/{spec.md,
session-state.json,activity-log.json,change-log.md}` and re-reads when those
change, so the tree auto-refreshes whenever a session is registered, logged,
or completed.

## Multi-root workspaces (per-set worktrees)

The extension scans `docs/session-sets/` under **every** workspace folder,
not just the first. When you run a session set in an isolated git
worktree (the per-set worktree convention in
[`docs/planning/project-guidance.md`](../../docs/planning/project-guidance.md)),
add the worktree to the workspace via **File → Add Folder to Workspace…**
and the extension picks up its in-progress signals in real time —
`session-state.json` and `activity-log.json` updates land while the
session is still running, before the worktree merges back to main.

When the same slug exists in multiple roots (e.g., the main repo holds
the spec while a worktree carries the live activity log), the
higher-state entry wins (done > in-progress > not-started); ties break
on most-recent `lastTouched`. The tooltip's `Folder:` line shows which
workspace folder owns the displayed entry, so you can tell at a glance
whether a row reflects the main repo or a sibling worktree.

The watchers and the cache rebuild automatically when workspace folders
are added or removed, so adding the worktree mid-session is enough.

## Tree-item interactions

- **Click a session set** → opens its `spec.md`.
- **Right-click → Open Activity Log** / **Open Change Log** / **Reveal Folder**.
- **Right-click → Copy: Start next session** — places
  ``Start the next session of `<slug>`.`` on the clipboard. Paste into
  Claude Code / Codex / Gemini chat to kick off the next session on the
  current working tree.
- **Right-click → Copy: Start next parallel session** — places
  ``Start the next parallel session of `<slug>`.`` on the clipboard.
  Use when another session set is already in progress on the same
  worktree; the orchestrator then runs the session inside an isolated
  git worktree.

  Both presets are only shown for in-progress and not-started sets —
  done sets do not have a "next session". What each phrase means to an
  orchestrator (and the optional `— maxout Claude` / `— maxout Codex`
  suffix the human can append manually) is documented in
  [`docs/ai-led-session-workflow.md`](../../docs/ai-led-session-workflow.md)
  → Start-Session Trigger Phrases. The status-bar shows a brief
  confirmation (`Copied: <preset>`) when a copy succeeds.
- **Right-click → Copy: Slug only** — places just the bare slug on the
  clipboard, for ad-hoc references.
- **Title-bar refresh button** → manual re-scan (useful if you suspect a
  filesystem watcher missed an update).

## Two ways to install

### A. Dev mode (zero-build, restart-on-change)

The fastest path. No `npm install`, no packaging.

1. Open this folder (`tools/vscode-session-sets/`) in a separate VS Code
   window.
2. Press **F5**. VS Code launches a second window — the
   "Extension Development Host" — with the repo root opened and this
   extension loaded. The "Session Set Explorer" icon appears in the
   Activity Bar; click it to open the tree view.
3. Edits to `extension.js` take effect after **Cmd/Ctrl+R** in the dev
   host window (or pressing F5 again from the source window).

The pre-wired `launch.json` in this folder points the dev host at the repo
root automatically (`${workspaceFolder}/../..`).

### B. Package + install as VSIX (persistent, no dev host)

Use this when you want the extension to load every time you open the repo
in regular VS Code.

```bash
# One-time toolchain install:
npm install -g @vscode/vsce

# From this folder:
cd tools/vscode-session-sets
vsce package           # produces dabbler-session-sets-0.6.0.vsix
code --install-extension dabbler-session-sets-0.6.0.vsix
```

To uninstall: `code --uninstall-extension darndestdabbler.dabbler-session-sets`.

If `vsce package` complains about missing `LICENSE` or `repository`, add the
fields to `package.json` or pass `--allow-missing-repository`. The VSIX is
not committed to the repo — rebuild it after pulling extension updates.

## What the extension does NOT do

- Does not modify any session set file.
- Does not call the AI router or spend any API budget.
- Does not require Node dependencies (uses only `fs`, `path`, and the
  built-in `vscode` API).

## Internals

`SessionSetsProvider` implements `vscode.TreeDataProvider`. On
`getChildren(undefined)` it returns three group nodes; on
`getChildren(<group>)` it returns the matching session-set rows sorted by
last-touched (in-progress, done) or by name (not-started). A
`vscode.workspace.createFileSystemWatcher` invalidates the cache and fires
`_onDidChangeTreeData` whenever a state-relevant file is created, deleted,
or modified.
