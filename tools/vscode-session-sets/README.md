# Dabbler Session Sets — VS Code extension

A small workspace VS Code extension that surfaces every session set under
`docs/session-sets/` as a tree view in the Explorer panel, grouped by state
(In Flight / Not Started / Done) with progress and last-touched date. State
is derived from file presence the same way `ai_router.find_active_session_set`
and `ai_router.print_session_set_status` do — `change-log.md` → done,
`activity-log.json` without it → in-flight, only `spec.md` → not-started.

The extension never writes. It only watches `docs/session-sets/**/{spec.md,
activity-log.json,change-log.md}` and re-reads when those change, so the tree
auto-refreshes whenever a session is logged or completed.

## Tree-item interactions

- **Click a session set** → opens its `spec.md`.
- **Right-click → Open Activity Log** / **Open Change Log** / **Reveal Folder**.
- **Title-bar refresh button** → manual re-scan (useful if you suspect a
  filesystem watcher missed an update).

## Two ways to install

### A. Dev mode (zero-build, restart-on-change)

The fastest path. No `npm install`, no packaging.

1. Open this folder (`tools/vscode-session-sets/`) in a separate VS Code
   window.
2. Press **F5**. VS Code launches a second window — the
   "Extension Development Host" — with the repo root opened and this
   extension loaded. The "Session Sets" view appears under Explorer.
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
vsce package           # produces dabbler-session-sets-0.1.0.vsix
code --install-extension dabbler-session-sets-0.1.0.vsix
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
last-touched (in-flight, done) or by name (not-started). A
`vscode.workspace.createFileSystemWatcher` invalidates the cache and fires
`_onDidChangeTreeData` whenever a state-relevant file is created, deleted,
or modified.
