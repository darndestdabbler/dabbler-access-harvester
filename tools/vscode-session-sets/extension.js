// Dabbler Session Sets — VS Code tree-view extension.
//
// Reads docs/session-sets/<slug>/{spec.md, session-state.json,
// activity-log.json, change-log.md} and renders a three-group tree: In
// Progress, Not Started, Done. State is derived from file presence,
// mirroring ai_router.find_active_session_set and
// ai_router.print_session_set_status. session-state.json is the earliest
// in-progress signal — it is written at the start of session 1 (before any
// activity-log entry exists), so a freshly-started set shows as in-progress
// without waiting for the first commit. The extension never writes — it
// only watches and re-reads.
//
// Worktree auto-discovery: for every workspace folder that is a git repo,
// the extension also scans the `docs/session-sets/` tree of every other
// worktree of that repo (discovered via `git worktree list --porcelain`).
// This surfaces in-progress sessions running in sibling worktrees without
// requiring the user to add each worktree as a workspace folder. New
// worktrees are picked up on (a) workspace-folder changes, (b) the
// title-bar refresh button, or (c) a 30-second background poll.
//
// Multi-root precedence: when the same slug exists in multiple discovered
// roots (e.g., the main repo carries the spec while a worktree carries
// the live activity-log.json), the higher-state entry wins (done >
// in-progress > not-started); ties break on most-recent `lastTouched`.

const vscode = require("vscode");
const cp = require("child_process");
const fs = require("fs");
const path = require("path");

const SESSION_SETS_REL = path.join("docs", "session-sets");

function workspaceFolders() {
  return vscode.workspace.workspaceFolders || [];
}

function primaryRoot() {
  const folders = workspaceFolders();
  return folders.length ? folders[0].uri.fsPath : null;
}

// Run `git worktree list --porcelain` in `cwd` and return the absolute
// paths of every worktree git knows about. Silent fallback to [] when
// git is missing, the directory is not a repo, or the command errors —
// the extension is still useful with workspace folders alone.
function listGitWorktrees(cwd) {
  let out;
  try {
    out = cp.execFileSync("git", ["worktree", "list", "--porcelain"], {
      cwd,
      encoding: "utf8",
      // Suppress shell windows on Windows; cap output to keep a runaway
      // git invocation from blocking the UI thread.
      windowsHide: true,
      maxBuffer: 1024 * 1024,
      timeout: 5000,
    });
  } catch (_) {
    return [];
  }
  const paths = [];
  for (const line of out.split(/\r?\n/)) {
    if (line.startsWith("worktree ")) {
      const wt = line.slice("worktree ".length).trim();
      if (wt) paths.push(path.resolve(wt));
    }
  }
  return paths;
}

// Workspace folders + every worktree git knows about for any of them,
// deduped by case-insensitive absolute path (Windows-friendly).
function discoverRoots() {
  const seen = new Map();   // lower-cased path -> canonical path
  const order = [];
  const add = (p) => {
    if (!p) return;
    const canonical = path.resolve(p);
    const key = canonical.toLowerCase();
    if (seen.has(key)) return;
    if (!fs.existsSync(canonical)) return;
    seen.set(key, canonical);
    order.push(canonical);
  };
  for (const folder of workspaceFolders()) {
    add(folder.uri.fsPath);
  }
  for (const folder of workspaceFolders()) {
    for (const wt of listGitWorktrees(folder.uri.fsPath)) {
      add(wt);
    }
  }
  return order;
}

function readSessionSets(root) {
  const sessionSetsDir = path.join(root, SESSION_SETS_REL);
  if (!fs.existsSync(sessionSetsDir)) return [];
  const entries = fs.readdirSync(sessionSetsDir, { withFileTypes: true });
  const sets = [];
  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    const dir = path.join(sessionSetsDir, entry.name);
    const specPath = path.join(dir, "spec.md");
    if (!fs.existsSync(specPath)) continue;

    const activityPath = path.join(dir, "activity-log.json");
    const changeLogPath = path.join(dir, "change-log.md");
    const statePath = path.join(dir, "session-state.json");

    let state;
    if (fs.existsSync(changeLogPath)) state = "done";
    else if (fs.existsSync(activityPath) || fs.existsSync(statePath)) state = "in-progress";
    else state = "not-started";

    let totalSessions = null;
    let sessionsCompleted = 0;
    let lastTouched = null;
    let liveSession = null;  // populated from session-state.json when present

    if (fs.existsSync(activityPath)) {
      try {
        const data = JSON.parse(fs.readFileSync(activityPath, "utf8"));
        if (typeof data.totalSessions === "number") {
          totalSessions = data.totalSessions;
        }
        const completedSet = new Set();
        for (const e of data.entries || []) {
          if (typeof e.sessionNumber === "number") {
            completedSet.add(e.sessionNumber);
          }
          if (e.dateTime && (!lastTouched || e.dateTime > lastTouched)) {
            lastTouched = e.dateTime;
          }
        }
        sessionsCompleted = completedSet.size;
      } catch (err) {
        // ignore — show what we have
      }
    }

    if (fs.existsSync(statePath)) {
      try {
        const sd = JSON.parse(fs.readFileSync(statePath, "utf8"));
        if (totalSessions === null && typeof sd.totalSessions === "number") {
          totalSessions = sd.totalSessions;
        }
        const stateTouched = sd.completedAt || sd.startedAt;
        if (stateTouched && (!lastTouched || stateTouched > lastTouched)) {
          lastTouched = stateTouched;
        }
        liveSession = {
          currentSession: sd.currentSession,
          status: sd.status,
          orchestrator: sd.orchestrator || null,
          startedAt: sd.startedAt || null,
          completedAt: sd.completedAt || null,
          verificationVerdict: sd.verificationVerdict || null,
        };
      } catch (err) {
        // ignore — show what we have
      }
    }

    sets.push({
      name: entry.name,
      dir,
      specPath,
      activityPath,
      changeLogPath,
      statePath,
      state,
      totalSessions,
      sessionsCompleted,
      lastTouched,
      liveSession,
      root,
    });
  }
  return sets;
}

// Scan every workspace folder's docs/session-sets/ tree and merge the
// results, keeping one entry per slug. State precedence (done >
// in-progress > not-started) decides which root wins when the same slug
// exists in multiple folders; ties break on most-recent `lastTouched`.
const STATE_RANK = { "done": 2, "in-progress": 1, "not-started": 0 };

function readAllSessionSets() {
  const merged = new Map();
  for (const root of discoverRoots()) {
    for (const set of readSessionSets(root)) {
      const prior = merged.get(set.name);
      if (!prior) {
        merged.set(set.name, set);
        continue;
      }
      const newRank = STATE_RANK[set.state] ?? -1;
      const priorRank = STATE_RANK[prior.state] ?? -1;
      if (newRank > priorRank) {
        merged.set(set.name, set);
      } else if (newRank === priorRank) {
        const newer = (set.lastTouched || "") > (prior.lastTouched || "");
        if (newer) merged.set(set.name, set);
      }
    }
  }
  return Array.from(merged.values());
}

function progressText(set) {
  if (set.totalSessions && set.totalSessions > 0) {
    return `${set.sessionsCompleted}/${set.totalSessions}`;
  }
  // Done sets: completed == total by definition. Render as N/N even when
  // totalSessions is missing/zero in the activity-log (some historical
  // logs never had it populated). This keeps the Done subtree visually
  // consistent rather than mixing "N/N" and "N done" rows.
  if (set.state === "done" && set.sessionsCompleted > 0) {
    return `${set.sessionsCompleted}/${set.sessionsCompleted}`;
  }
  if (set.sessionsCompleted > 0) {
    return `${set.sessionsCompleted} done`;
  }
  return "";
}

function touchedDate(set) {
  if (!set.lastTouched) return "";
  return new Date(set.lastTouched).toLocaleDateString("en-CA");
}

// Custom SVG icons live under media/. Same icon is used for the group
// header AND each row in that group, so the visual mapping is one-to-one.
const ICON_FILES = {
  "done": "done.svg",
  "in-progress": "in-progress.svg",
  "not-started": "not-started.svg",
};

function iconUriFor(extensionUri, state) {
  const file = ICON_FILES[state];
  if (!file) return undefined;
  return vscode.Uri.joinPath(extensionUri, "media", file);
}

class SessionSetsProvider {
  constructor(extensionUri) {
    this.extensionUri = extensionUri;
    this._onDidChangeTreeData = new vscode.EventEmitter();
    this.onDidChangeTreeData = this._onDidChangeTreeData.event;
    this._cache = null;
  }

  refresh() {
    this._cache = null;
    this._onDidChangeTreeData.fire();
  }

  getTreeItem(element) {
    return element;
  }

  getChildren(element) {
    if (workspaceFolders().length === 0) return [];

    if (!this._cache) {
      this._cache = readAllSessionSets();
    }
    const all = this._cache;

    if (!element) {
      // Top level: three group nodes.
      const inProgress = all.filter((s) => s.state === "in-progress");
      const notStarted = all.filter((s) => s.state === "not-started");
      const done = all.filter((s) => s.state === "done");

      const groups = [];
      groups.push(
        makeGroup(this.extensionUri, "In Progress", "in-progress", inProgress.length),
      );
      groups.push(
        makeGroup(this.extensionUri, "Not Started", "not-started", notStarted.length),
      );
      groups.push(
        makeGroup(this.extensionUri, "Done", "done", done.length),
      );
      return groups;
    }

    if (element.contextValue === "group") {
      let subset = all.filter((s) => s.state === element.groupKey);
      if (element.groupKey === "in-progress" || element.groupKey === "done") {
        subset.sort((a, b) =>
          (b.lastTouched || "").localeCompare(a.lastTouched || ""),
        );
      } else {
        subset.sort((a, b) => a.name.localeCompare(b.name));
      }
      return subset.map((s) => makeSetItem(this.extensionUri, s));
    }

    return [];
  }
}

function makeGroup(extensionUri, label, groupKey, count) {
  const item = new vscode.TreeItem(
    `${label}  (${count})`,
    count > 0
      ? vscode.TreeItemCollapsibleState.Expanded
      : vscode.TreeItemCollapsibleState.Collapsed,
  );
  // Group header reuses the per-state custom SVG so the header visually
  // matches the rows it contains.
  item.iconPath = iconUriFor(extensionUri, groupKey);
  item.contextValue = "group";
  item.groupKey = groupKey;
  return item;
}

function liveSessionTooltipLines(set) {
  if (!set.liveSession) return [];
  const ls = set.liveSession;
  const lines = [];
  if (typeof ls.currentSession === "number") {
    const total = set.totalSessions ? `/${set.totalSessions}` : "";
    const status = ls.status ? ` (${ls.status})` : "";
    lines.push(`Session: ${ls.currentSession}${total}${status}`);
  }
  if (ls.orchestrator) {
    const o = ls.orchestrator;
    const parts = [o.engine, o.model].filter(Boolean).join(" · ");
    const effort = o.effort && o.effort !== "unknown" ? ` @ effort=${o.effort}` : "";
    if (parts) lines.push(`Orchestrator: ${parts}${effort}`);
  }
  if (ls.verificationVerdict) {
    lines.push(`Verifier: ${ls.verificationVerdict}`);
  }
  return lines;
}

function folderTooltip(set) {
  // For sets discovered via `git worktree list`, name the worktree
  // directory so the user can tell at a glance whether a row is from
  // the main repo or a sibling worktree. With a single discovered root
  // this collapses to the familiar `docs/session-sets/<slug>` form.
  const roots = discoverRoots();
  const rel = path.relative(set.root, set.dir);
  if (roots.length > 1) {
    return `${path.basename(set.root)} / ${rel}`;
  }
  return rel;
}

function makeSetItem(extensionUri, set) {
  const item = new vscode.TreeItem(
    set.name,
    vscode.TreeItemCollapsibleState.None,
  );
  const bits = [progressText(set), touchedDate(set)].filter(Boolean);
  item.description = bits.join("  ·  ");
  item.tooltip = new vscode.MarkdownString(
    [
      `**${set.name}**`,
      `State: ${set.state}`,
      bits.length ? `Progress: ${bits.join(" · ")}` : null,
      ...liveSessionTooltipLines(set),
      `Folder: \`${folderTooltip(set)}\``,
    ]
      .filter(Boolean)
      .join("\n\n"),
  );
  item.contextValue = `sessionSet:${set.state}`;
  item.set = set;
  // Per-state custom SVGs from media/. Same icon as the group header.
  item.iconPath = iconUriFor(extensionUri, set.state);
  // Default click → open spec.md.
  item.command = {
    command: "dabblerSessionSets.openSpec",
    title: "Open Spec",
    arguments: [item],
  };
  return item;
}

function activate(context) {
  if (workspaceFolders().length === 0) {
    return;
  }

  const provider = new SessionSetsProvider(context.extensionUri);
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider("dabblerSessionSets", provider),
  );

  // Per-root file watchers — one per discovered root (workspace folders +
  // sibling worktrees). Rebound on workspace-folder changes, on the
  // 30-second background poll, and on every manual refresh, so newly-
  // created worktrees pick up watchers without a VS Code restart.
  let watcherSubs = [];
  let boundRoots = new Set();

  function bindWatchers() {
    const roots = discoverRoots();
    const want = new Set(roots.map((r) => r.toLowerCase()));
    if (
      want.size === boundRoots.size &&
      [...want].every((r) => boundRoots.has(r))
    ) {
      return;  // no change — keep existing watchers
    }
    for (const sub of watcherSubs) sub.dispose();
    watcherSubs = [];
    boundRoots = want;
    for (const root of roots) {
      const sessionSetsAbs = path.join(root, SESSION_SETS_REL);
      if (!fs.existsSync(sessionSetsAbs)) continue;
      const pattern = new vscode.RelativePattern(
        sessionSetsAbs,
        "**/{spec.md,session-state.json,activity-log.json,change-log.md}",
      );
      const watcher = vscode.workspace.createFileSystemWatcher(pattern);
      const onEvent = () => provider.refresh();
      watcher.onDidCreate(onEvent);
      watcher.onDidDelete(onEvent);
      watcher.onDidChange(onEvent);
      watcherSubs.push(watcher);
      context.subscriptions.push(watcher);
    }
  }

  const refreshAll = () => {
    bindWatchers();
    provider.refresh();
  };

  bindWatchers();

  context.subscriptions.push(
    vscode.workspace.onDidChangeWorkspaceFolders(refreshAll),
  );

  // Background poll picks up worktrees created mid-session (`git
  // worktree add ../<repo>-<slug>`) without requiring the user to hit
  // refresh. 30s is well under typical session length and the cost is
  // one `git worktree list` per workspace folder.
  const pollHandle = setInterval(refreshAll, 30000);
  context.subscriptions.push({ dispose: () => clearInterval(pollHandle) });

  context.subscriptions.push(
    vscode.commands.registerCommand("dabblerSessionSets.refresh", refreshAll),
  );

  const openIfExists = (p) => {
    if (!p || !fs.existsSync(p)) {
      vscode.window.showInformationMessage(
        `File does not exist yet: ${p ? path.basename(p) : "<unknown>"}`,
      );
      return;
    }
    vscode.commands.executeCommand("vscode.open", vscode.Uri.file(p));
  };

  context.subscriptions.push(
    vscode.commands.registerCommand("dabblerSessionSets.openSpec", (item) =>
      openIfExists(item && item.set && item.set.specPath),
    ),
    vscode.commands.registerCommand(
      "dabblerSessionSets.openActivityLog",
      (item) => openIfExists(item && item.set && item.set.activityPath),
    ),
    vscode.commands.registerCommand(
      "dabblerSessionSets.openChangeLog",
      (item) => openIfExists(item && item.set && item.set.changeLogPath),
    ),
    vscode.commands.registerCommand(
      "dabblerSessionSets.openFolder",
      (item) => {
        if (!item || !item.set) return;
        vscode.commands.executeCommand(
          "revealInExplorer",
          vscode.Uri.file(item.set.dir),
        );
      },
    ),
  );

  // --- Clipboard helpers for "start next session" prompts ---

  const copy = async (text, label) => {
    await vscode.env.clipboard.writeText(text);
    vscode.window.setStatusBarMessage(`Copied: ${label}`, 4000);
  };

  // The clipboard text intentionally stays terse. What each phrase
  // actually means to the orchestrator is documented in
  // docs/ai-led-session-workflow.md → Start-Session Trigger Phrases,
  // including the optional `— maxout Claude` / `— maxout Codex` suffix
  // the human can append when draining a token-window allotment.
  const startCommandPresets = {
    default: (slug) => `Start the next session of \`${slug}\`.`,
    parallel: (slug) => `Start the next parallel session of \`${slug}\`.`,
  };

  const presetLabels = {
    default: "start next session",
    parallel: "start next parallel session",
  };

  for (const [key, builder] of Object.entries(startCommandPresets)) {
    context.subscriptions.push(
      vscode.commands.registerCommand(
        `dabblerSessionSets.copyStartCommand.${key}`,
        async (item) => {
          if (!item || !item.set) return;
          await copy(builder(item.set.name), presetLabels[key]);
        },
      ),
    );
  }

  context.subscriptions.push(
    vscode.commands.registerCommand("dabblerSessionSets.copySlug", async (item) => {
      if (!item || !item.set) return;
      await copy(item.set.name, "slug");
    }),
  );
}

function deactivate() {}

module.exports = { activate, deactivate };
