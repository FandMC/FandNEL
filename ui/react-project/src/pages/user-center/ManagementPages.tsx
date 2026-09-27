import { Copy, Download, FlaskConical, FolderOpen, PackageOpen, Plus, Trash2 } from "lucide-react";
import { ReactNode, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useGateway, useToasts } from "../../context/AppContext";
import { consumeGatewayMessages, parseGatewayPayload, useGatewayList } from "./gatewayData";
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
