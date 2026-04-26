// Dabbler Session Sets — VS Code tree-view extension.
//
// Reads docs/session-sets/<slug>/{spec.md,activity-log.json,change-log.md}
// and renders a three-group tree: In Flight, Not Started, Done. State is
// derived from file presence, mirroring ai_router.find_active_session_set
// and ai_router.print_session_set_status. The extension never writes —
// it only watches and re-reads.

const vscode = require("vscode");
const fs = require("fs");
const path = require("path");

const SESSION_SETS_REL = path.join("docs", "session-sets");

function workspaceRoot() {
  const folders = vscode.workspace.workspaceFolders;
  if (!folders || folders.length === 0) return null;
  return folders[0].uri.fsPath;
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

    let state;
    if (fs.existsSync(changeLogPath)) state = "done";
    else if (fs.existsSync(activityPath)) state = "in-flight";
    else state = "not-started";

    let totalSessions = null;
    let sessionsCompleted = 0;
    let lastTouched = null;

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

    sets.push({
      name: entry.name,
      dir,
      specPath,
      activityPath,
      changeLogPath,
      state,
      totalSessions,
      sessionsCompleted,
      lastTouched,
    });
  }
  return sets;
}

function progressText(set) {
  if (set.totalSessions && set.totalSessions > 0) {
    return `${set.sessionsCompleted}/${set.totalSessions}`;
  }
  if (set.sessionsCompleted > 0) {
    return `${set.sessionsCompleted} done`;
  }
  return "";
}

function touchedDate(set) {
  if (!set.lastTouched) return "";
  return set.lastTouched.slice(0, 10);
}

class SessionSetsProvider {
  constructor() {
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
    const root = workspaceRoot();
    if (!root) return [];

    if (!this._cache) {
      this._cache = readSessionSets(root);
    }
    const all = this._cache;

    if (!element) {
      // Top level: three group nodes.
      const inFlight = all.filter((s) => s.state === "in-flight");
      const notStarted = all.filter((s) => s.state === "not-started");
      const done = all.filter((s) => s.state === "done");

      const groups = [];
      groups.push(
        makeGroup("In Flight", "in-flight", inFlight.length, "loading~spin"),
      );
      groups.push(
        makeGroup(
          "Not Started",
          "not-started",
          notStarted.length,
          "circle-large-outline",
        ),
      );
      groups.push(makeGroup("Done", "done", done.length, "pass-filled"));
      return groups;
    }

    if (element.contextValue === "group") {
      let subset = all.filter((s) => s.state === element.groupKey);
      if (element.groupKey === "in-flight" || element.groupKey === "done") {
        subset.sort((a, b) =>
          (b.lastTouched || "").localeCompare(a.lastTouched || ""),
        );
      } else {
        subset.sort((a, b) => a.name.localeCompare(b.name));
      }
      return subset.map(makeSetItem);
    }

    return [];
  }
}

function makeGroup(label, groupKey, count, icon) {
  const item = new vscode.TreeItem(
    `${label}  (${count})`,
    count > 0
      ? vscode.TreeItemCollapsibleState.Expanded
      : vscode.TreeItemCollapsibleState.Collapsed,
  );
  item.iconPath = new vscode.ThemeIcon(icon);
  item.contextValue = "group";
  item.groupKey = groupKey;
  return item;
}

function makeSetItem(set) {
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
      `Folder: \`${path.relative(workspaceRoot(), set.dir)}\``,
    ]
      .filter(Boolean)
      .join("\n\n"),
  );
  item.contextValue = `sessionSet:${set.state}`;
  item.set = set;
  if (set.state === "done") {
    item.iconPath = new vscode.ThemeIcon(
      "pass-filled",
      new vscode.ThemeColor("testing.iconPassed"),
    );
  } else if (set.state === "in-flight") {
    item.iconPath = new vscode.ThemeIcon(
      "loading~spin",
      new vscode.ThemeColor("testing.runAction"),
    );
  } else {
    item.iconPath = new vscode.ThemeIcon("circle-large-outline");
  }
  // Default click → open spec.md.
  item.command = {
    command: "dabblerSessionSets.openSpec",
    title: "Open Spec",
    arguments: [item],
  };
  return item;
}

function activate(context) {
  const root = workspaceRoot();
  if (!root) {
    return;
  }

  const provider = new SessionSetsProvider();
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider("dabblerSessionSets", provider),
  );

  // Watch the session-sets tree for any state-relevant change.
  const sessionSetsAbs = path.join(root, SESSION_SETS_REL);
  if (fs.existsSync(sessionSetsAbs)) {
    const pattern = new vscode.RelativePattern(
      sessionSetsAbs,
      "**/{spec.md,activity-log.json,change-log.md}",
    );
    const watcher = vscode.workspace.createFileSystemWatcher(pattern);
    const refresh = () => provider.refresh();
    watcher.onDidCreate(refresh);
    watcher.onDidDelete(refresh);
    watcher.onDidChange(refresh);
    context.subscriptions.push(watcher);
  }

  context.subscriptions.push(
    vscode.commands.registerCommand("dabblerSessionSets.refresh", () =>
      provider.refresh(),
    ),
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

  const startCommandPresets = {
    "default": (slug) =>
      `Please start the next session of \`${slug}\`.`,
    "maxoutClaude": (slug) =>
      `Please start the next session of \`${slug}\` — maxout Claude (use Opus 4.7 as orchestrator at effort=max, drop max_tier caps on routed calls, push complexity_hint up so eligible calls route to Opus/max).`,
    "maxoutCodex": (slug) =>
      `Please start the next session of \`${slug}\` — maxout Codex (use GPT-5.5 or GPT-5.4 as orchestrator at reasoning_effort=high, push routed calls to GPT-5.4/high).`,
    "opusHigh": (slug) =>
      `Please start the next session of \`${slug}\` with Claude Opus @ effort=high as the orchestrator.`,
    "opusMedium": (slug) =>
      `Please start the next session of \`${slug}\` with Claude Opus @ effort=medium as the orchestrator.`,
    "sonnetMedium": (slug) =>
      `Please start the next session of \`${slug}\` with Claude Sonnet @ effort=medium as the orchestrator.`,
    "sonnetLow": (slug) =>
      `Please start the next session of \`${slug}\` with Claude Sonnet @ effort=low as the orchestrator.`,
  };

  const presetLabels = {
    "default": "default start command",
    "maxoutClaude": "maxout Claude",
    "maxoutCodex": "maxout Codex",
    "opusHigh": "Opus @ effort=high",
    "opusMedium": "Opus @ effort=medium",
    "sonnetMedium": "Sonnet @ effort=medium",
    "sonnetLow": "Sonnet @ effort=low",
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
