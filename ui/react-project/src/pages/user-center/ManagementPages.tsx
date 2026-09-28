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

function sessionStatusLabel(status: string): string {
  switch (status) {
    case "Running": return "运行中";
    case "Failed": return "失败";
    case "Stopping": return "正在停止";
    case "Stopped": return "已停止";
    default: return status;
  }
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
      <Intro title="游戏管理" description="查看并管理当前所有活动游戏会话。" />
      <ManagementCard title={`活动会话（${items.length}）`}>
        {loading ? <div className="management-v253-loading">正在获取活动会话...</div> : error ? <div className="management-v253-empty" role="alert"><h3>无法加载活动会话</h3><p>{error}</p><button className="management-outline-button" type="button" onClick={() => void refresh()}>重试</button></div> : items.length ? items.map((game) => (
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
              {game.status_text !== "Running" ? <div className="game-progress"><p><span>正在启动...</span><span>{game.progress_value}%</span></p><i><b style={{ width: `${game.progress_value}%` }} /></i></div> : null}
              <em>{game.character_name}</em>
            </div>
            <div className="game-session-character"><small>角色</small><strong>{game.character_name}</strong></div>
            <div className="game-session-actions">
              <span className={game.status_text === "Running" ? "running" : "launching"}>{sessionStatusLabel(game.status_text)}</span>
              <div>
                {game.status_text === "Running" && game.type === "Interceptor" ? <button type="button" title="复制地址" onClick={(event) => { event.stopPropagation(); void navigator.clipboard.writeText(game.local_address); }}><Copy /></button> : null}
                {game.status_text === "Running" ? <button className="stop" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>停止</button> : null}
                {game.type === "ModDownload" ? <button className="stop" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>取消</button> : null}
              </div>
            </div>
          </div>
        )) : <div className="management-v253-empty"><span><FlaskConical /></span><h3>暂无活动会话</h3><p>启动游戏后，会话将显示在此处。</p></div>}
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
      notify(error instanceof Error ? error.message : "无法添加模组。", "error");
    }
  };

  return (
    <main className="workspace-page management-page management-v253-page">
      <Intro title="模组管理" description="安装并管理游戏模组（.jar 文件）。" />
      <ManagementCard
        title={`已安装模组（${items.length}）`}
        actions={<><button className="management-outline-button" type="button" onClick={() => void gateway.send("open_folder", "resources:mods")}><FolderOpen />打开文件夹</button><label className="management-primary-button" htmlFor="mod-file-upload"><Plus />添加模组</label><input id="mod-file-upload" className="management-file-input" type="file" accept=".jar" multiple onChange={(event) => void addMod(event.target.files).finally(() => { event.target.value = ""; })} /></>}
      >
        {loading ? <div className="management-v253-loading">正在加载模组列表...</div> : items.length ? items.map((mod, index) => (
          <div className="mod-v253-row" key={`${mod.path}_${index}`}>
            <div><strong title={mod.path}>{mod.path.split(/[/\\]/).pop() || mod.path}</strong><small title={mod.path}>{mod.path}</small></div>
            <button type="button" title="删除模组" onClick={() => { if (window.confirm("确定要删除此模组吗？此操作无法撤销。")) void gateway.send("delete_mod", mod.path); }}><Trash2 /></button>
          </div>
        )) : <div className="management-v253-empty"><span><PackageOpen /></span><h3>尚未安装模组</h3><p>点击“添加模组”上传 .jar 文件。</p></div>}
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

  return <div className="workspace-page console-page"><section className="console-header"><div><h1>控制台</h1><p>系统日志与终端输出</p></div><div><button type="button" onClick={gateway.clearLogs}><Trash2 />清空</button><button type="button" onClick={download}><Download />下载</button></div></section><section className="console-output" ref={outputRef}>{gateway.logs.map((entry) => <div key={entry.id}><time>[{new Date(entry.timestamp).toLocaleTimeString()}]</time><b className={`log-${entry.type}`}>{logLabel(entry.type)}</b><span>{entry.content}</span></div>)}</section></div>;
}

function logLabel(type: string): string {
  if (type === "error") return "错误";
  if (type === "warning") return "警告";
  if (type === "success") return "成功";
  if (type === "command") return "$";
  return "信息";
}
