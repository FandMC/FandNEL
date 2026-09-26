import { Archive, Copy, Download, FlaskConical, FolderOpen, PackageOpen, Plus, Trash2 } from "lucide-react";
import { ReactNode, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth, useGateway, useToasts } from "../../context/AppContext";
import {
  downloadComponent,
  getComponentsVersionByIds,
} from "../../lib/legacyPublicApi";
import { consumeGatewayMessages, findOutdatedPlugins, parseGatewayPayload, useGatewayList, writePluginUpdates } from "./gatewayData";
import type { GatewayMessage } from "../../types";

interface GameSession {
  id: string;
  name: string;
  guid: string;
  server_name: string;
  character_name: string;
  server_version: string;
  status_text: string;
  type: string;
  game_type: string;
  local_address: string;
  progress_value: number;
}

interface LaunchProgress {
  id: string;
  message: string;
  percent: number;
}

interface ModRecord {
  path: string;
}

interface InstalledPlugin {
  id: string;
  name: string;
  description: string;
  version: string;
  author: string;
  status: string;
  waiting_restart?: boolean;
}

interface InstallProgress {
  progress: number;
  status?: unknown;
}

function Intro({ title, description }: { title: string; description: string }) {
  return <section className="management-v253-intro"><h1>{title}</h1><p>{description}</p></section>;
}

function ManagementCard({ title, actions, children }: { title: string; actions?: ReactNode; children: ReactNode }) {
  return <section className="management-v253-card"><header><strong>{title}</strong>{actions ? <div>{actions}</div> : null}</header><div>{children}</div></section>;
}

export function LaunchersPage() {
  const { gateway, items, setItems, loading, error, refresh } = useGatewayList<GameSession>("query_game_session");
  const navigate = useNavigate();
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      if (message.type === "launch_progress") {
        const progress = parseGatewayPayload<LaunchProgress>(message.payload);
        setItems((current) => current.map((game) => game.id === progress.id ? { ...game, status_text: progress.message, progress_value: progress.percent } : game));
      } else if (message.type === "realm_mod_progress") {
        const progress = parseGatewayPayload<LaunchProgress & { sid?: string }>(message.payload);
        setItems((current) => current.map((game) => game.id === `realm-mod-${progress.id}` ? { ...game, status_text: progress.message, progress_value: progress.percent } : game));
      }
    }
  }, [gateway.messages, setItems]);

  return (
    <main className="workspace-page management-page management-v253-page">
      <Intro title="Game Management" description="Oversee and manage all currently active game server instances." />
      <ManagementCard title={`Active Sessions (${items.length})`}>
        {loading ? <div className="management-v253-loading">Fetching active sessions...</div> : error ? <div className="management-v253-empty" role="alert"><h3>Unable to load active sessions</h3><p>{error}</p><button className="management-outline-button" type="button" onClick={() => void refresh()}>Retry</button></div> : items.length ? items.map((game) => (
          <div
            className={game.game_type === "Java" && game.type === "Interceptor" ? "game-session-row configurable-game-session" : "game-session-row"}
            key={game.id}
            onClick={() => {
              if (game.game_type === "Java" && game.type === "Interceptor") navigate(`/user-center/launchers/configuration?id=${encodeURIComponent(game.name)}`);
            }}
          >
            <div className="game-session-main">
              <div><h3>{game.server_name}</h3><span>{game.server_version}</span></div>
              <small>{game.guid}</small>
              {game.status_text !== "Running" ? <div className="game-progress"><p><span>Launching...</span><span>{game.progress_value}%</span></p><i><b style={{ width: `${game.progress_value}%` }} /></i></div> : null}
              <em>{game.character_name}</em>
            </div>
            <div className="game-session-character"><small>Character</small><strong>{game.character_name}</strong></div>
            <div className="game-session-actions">
              <span className={game.status_text === "Running" ? "running" : "launching"}>{game.status_text}</span>
              <div>
                {game.status_text === "Running" && game.type === "Interceptor" ? <button type="button" title="Copy Address" onClick={(event) => { event.stopPropagation(); void navigator.clipboard.writeText(game.local_address); }}><Copy /></button> : null}
                {game.status_text === "Running" ? <button className="stop" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>Stop</button> : null}
                {game.type === "ModDownload" ? <button className="stop" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>Cancel</button> : null}
              </div>
            </div>
          </div>
        )) : <div className="management-v253-empty"><span><FlaskConical /></span><h3>No active sessions</h3><p>Start a game to see it listed here.</p></div>}
      </ManagementCard>
    </main>
  );
}

async function fileToBase64(file: File): Promise<string> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = "";
  bytes.forEach((byte) => { binary += String.fromCharCode(byte); });
  return btoa(binary);
}

export function ModsPage() {
  const { gateway, items, loading } = useGatewayList<ModRecord>("query_mods");
  const { notify } = useToasts();

  const addMod = async (files: FileList | null) => {
    if (!files?.length) return;
    try {
      const payload = await Promise.all(Array.from(files).map(async (file) => ({ file_name: file.name, base64: await fileToBase64(file) })));
      await gateway.send("add_mods", payload);
    } catch (error) {
      notify(error instanceof Error ? error.message : "Unable to add mod.", "error");
    }
  };

  return (
    <main className="workspace-page management-page management-v253-page">
      <Intro title="Mods Management" description="Install and manage game modifications (.jar files)." />
      <ManagementCard
        title={`Installed Mods (${items.length})`}
        actions={<><button className="management-outline-button" type="button" onClick={() => void gateway.send("open_folder", "resources:mods")}><FolderOpen />Open Folder</button><label className="management-primary-button" htmlFor="mod-file-upload"><Plus />Add Mod</label><input id="mod-file-upload" className="management-file-input" type="file" accept=".jar" multiple onChange={(event) => void addMod(event.target.files).finally(() => { event.target.value = ""; })} /></>}
      >
        {loading ? <div className="management-v253-loading">Loading mods list...</div> : items.length ? items.map((mod, index) => (
          <div className="mod-v253-row" key={`${mod.path}_${index}`}>
            <div><strong title={mod.path}>{mod.path.split(/[/\\]/).pop() || mod.path}</strong><small title={mod.path}>{mod.path}</small></div>
            <button type="button" title="Delete Mod" onClick={() => { if (window.confirm("Are you sure you want to delete this mod? This action cannot be undone.")) void gateway.send("delete_mod", mod.path); }}><Trash2 /></button>
          </div>
        )) : <div className="management-v253-empty"><span><PackageOpen /></span><h3>No mods installed</h3><p>Click &quot;Add Mod&quot; to upload .jar files.</p></div>}
      </ManagementCard>
    </main>
  );
}

export function InstalledPluginsPage() {
  const { gateway, items, loading, refresh } = useGatewayList<InstalledPlugin>("query_plugins");
  const { user, getAccessToken } = useAuth();
  const { notify } = useToasts();
  const [updateIds, setUpdateIds] = useState<string[]>([]);
  const [updatingId, setUpdatingId] = useState("");
  const [progress, setProgress] = useState(0);
  const updatingIdRef = useRef("");
  const completionRef = useRef<(() => void) | null>(null);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => {
    if (loading) return;
    if (!items.length) {
      setUpdateIds([]);
      writePluginUpdates([]);
      return;
    }
    let cancelled = false;
    void getAccessToken()
      .then((token) => getComponentsVersionByIds(items.map((plugin) => plugin.id), token))
      .then((response) => {
        if (cancelled) return;
        const outdated = findOutdatedPlugins(items, response.items);
        setUpdateIds(outdated.map((plugin) => plugin.id));
        writePluginUpdates(outdated);
      })
      .catch((error) => notify(error instanceof Error ? error.message : "Unable to check plugin versions.", "error"));
    return () => { cancelled = true; };
  }, [getAccessToken, items, loading, notify]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      if (message.type !== "report_install_plugin_progress") continue;
      const report = parseGatewayPayload<InstallProgress>(message.payload);
      setProgress(report.progress);
      if (!report.status) continue;
      completionRef.current?.();
      completionRef.current = null;
      updatingIdRef.current = "";
      setUpdatingId("");
      setProgress(0);
      void refresh();
    }
  }, [gateway.messages, refresh]);

  const canUpdate = (plugin: InstalledPlugin) => updateIds.some((id) => id === plugin.id) && !plugin.waiting_restart;

  const updatePlugin = async (plugin: InstalledPlugin): Promise<void> => {
    if (updatingIdRef.current && updatingIdRef.current !== plugin.id) throw new Error("Another update is already in progress.");
    if (updatingIdRef.current === plugin.id) return;
    updatingIdRef.current = plugin.id;
    setUpdatingId(plugin.id);
    setProgress(0);
    const completion = new Promise<void>((resolve) => { completionRef.current = resolve; });
    try {
      const token = await getAccessToken();
      await gateway.send("update", { id: user?.id || localStorage.getItem("userId") || "", token });
      const downloaded = await downloadComponent(plugin.id, token);
      await gateway.send("update_plugin", {
        id: plugin.id,
        old: plugin.version,
        info: JSON.stringify({ id: user?.id || localStorage.getItem("userId") || "", plugin: downloaded }),
      });
      return completion;
    } catch (error) {
      completionRef.current = null;
      updatingIdRef.current = "";
      setUpdatingId("");
      setProgress(0);
      throw error;
    }
  };

  const updateAll = async () => {
    if (updatingIdRef.current) return;
    for (const plugin of items.filter(canUpdate)) {
      try {
        await updatePlugin(plugin);
      } catch (error) {
        notify(error instanceof Error ? error.message : `Failed to update ${plugin.name}.`, "error");
      }
    }
  };

  const updateCount = items.filter(canUpdate).length;
  const updating = Boolean(updatingId);

  const pluginRows = items.map((plugin) => {
    const updatingThis = updatingId === plugin.id;
    const needsUpdate = canUpdate(plugin);
    const statusLabel = plugin.waiting_restart ? "Restart Pending" : needsUpdate ? "Update Available" : plugin.status;
    const statusClass = plugin.waiting_restart ? "restart" : needsUpdate ? "update" : plugin.status === "Online" ? "online" : "neutral";
    return (
      <div className="plugin-v253-row" key={plugin.id}>
        <div className="plugin-v253-main">
          <div><h3>{plugin.name}</h3><span>v{plugin.version}</span></div>
          <p>{plugin.description}</p>
          <em>By {plugin.author}</em>
          {updatingThis ? <div className="game-progress"><p><span>Updating...</span><span>{progress}%</span></p><i><b style={{ width: `${progress}%` }} /></i></div> : null}
        </div>
        <div className="plugin-v253-author"><small>Author</small><strong>{plugin.author}</strong></div>
        <div className="plugin-v253-actions">
          <span className={statusClass}>{statusLabel}</span>
          <div>
            {needsUpdate && !updatingThis ? <button className="update" type="button" disabled={updating} onClick={() => void updatePlugin(plugin).catch((error) => notify(error instanceof Error ? error.message : "Plugin update failed.", "error"))}>Update</button> : null}
            <button type="button" disabled={updating} onClick={() => void gateway.send("uninstall_plugin", plugin.id)} title="Uninstall"><Trash2 /></button>
          </div>
        </div>
      </div>
    );
  });

  return (
    <main className="workspace-page management-page management-v253-page">
      <Intro title="Manage Plugins" description="Enable, disable, or uninstall plugins to customize your Codexus Platform experience." />
      <ManagementCard title={`Installed Plugins (${items.length})`} actions={<>{updateCount > 0 ? <button className="management-primary-button compact" type="button" disabled={updating} onClick={() => void updateAll()}>Update All ({updateCount})</button> : null}<button className="management-outline-button compact" type="button" disabled={updating} onClick={() => void gateway.send("restart")}>Restart Service</button></>}>
        {loading ? <div className="management-v253-loading">Fetching installed plugins...</div> : items.length ? pluginRows : <div className="management-v253-empty"><span><Archive /></span><h3>No plugins installed</h3><p><Link to="/plugins">Browse the store</Link> to add functionality.</p></div>}
      </ManagementCard>
    </main>
  );
}

export function ConsolePage() {
  const gateway = useGateway();
  const outputRef = useRef<HTMLElement | null>(null);
  useEffect(() => {
    const output = outputRef.current;
    if (output) output.scrollTop = output.scrollHeight;
  }, [gateway.logs]);

  const download = () => {
    const content = gateway.logs.map((entry) => `[${new Date(entry.timestamp).toLocaleTimeString()}] ${logLabel(entry.type)} ${entry.content}`).join("\n");
    const url = URL.createObjectURL(new Blob([content], { type: "text/plain" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = "terminal_logs.txt";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return <div className="workspace-page console-page"><section className="console-header"><div><h1>Console</h1><p>System logs and terminal output</p></div><div><button type="button" onClick={gateway.clearLogs}><Trash2 />Clear</button><button type="button" onClick={download}><Download />Download</button></div></section><section className="console-output" ref={outputRef}>{gateway.logs.map((entry) => <div key={entry.id}><time>[{new Date(entry.timestamp).toLocaleTimeString()}]</time><b className={`log-${entry.type}`}>{logLabel(entry.type)}</b><span>{entry.content}</span></div>)}</section></div>;
}

function logLabel(type: string): string {
  if (type === "error") return "ERR!";
  if (type === "warning") return "WARN";
  if (type === "success") return "OK";
  if (type === "command") return "$";
  return "INFO";
}
