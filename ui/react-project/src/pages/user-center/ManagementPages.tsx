import { Copy, FlaskConical, SlidersHorizontal } from "lucide-react";
import { ReactNode, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
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

function sessionStatusLabel(status: string): string {
  switch (status) {
    case "Running": return "运行中";
    case "Failed": return "失败";
    case "Stopping": return "正在停止";
    case "Stopped": return "已停止";
    default: return status;
  }
}

function Intro({ title, description, actions }: { title: string; description: string; actions?: ReactNode }) {
  return <header className="neo-management-header"><div><h1>{title}</h1><p>{description}</p></div>{actions ? <div className="neo-management-actions">{actions}</div> : null}</header>;
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
    <main className="workspace-page management-page neo-management-page">
      <Intro title="游戏会话" description={`查看和管理当前游戏会话（${items.length}）`} actions={<button className="neo-management-button" type="button" disabled={loading} onClick={() => void refresh()}>刷新</button>} />
      <section className="neo-session-list" aria-label="活动会话">
        {loading ? <div className="neo-management-empty">正在获取活动会话...</div> : error ? <div className="neo-management-empty" role="alert"><h3>无法加载活动会话</h3><p>{error}</p><button className="neo-management-button" type="button" onClick={() => void refresh()}>重试</button></div> : items.length ? items.map((game) => (
          <div
            className={game.game_type === "Java" && game.type === "Interceptor" ? "neo-session-card configurable-game-session" : "neo-session-card"}
            key={game.id}
            onClick={() => {
              if (game.game_type === "Java" && game.type === "Interceptor") navigate(`/user-center/launchers/configuration?id=${encodeURIComponent(game.name)}`);
            }}
          >
            <div className="neo-session-info">
              <h3>{game.server_name}</h3>
              <p className="neo-session-meta">{game.character_name} · {game.game_type} {game.server_version} · {game.type === "Launcher" ? "白端" : game.type === "Interceptor" ? "代理" : "模组下载"}</p>
              <small className="neo-session-identifier" title={game.guid}>{game.guid}</small>
              <p className={game.status_text === "Running" ? "neo-session-status running" : "neo-session-status"}>{sessionStatusLabel(game.status_text)}</p>
              {game.status_text !== "Running" ? <div className="neo-session-progress"><p><span>正在启动...</span><span>{game.progress_value}%</span></p><i><b style={{ width: `${game.progress_value}%` }} /></i></div> : null}
            </div>
            <div className="neo-session-actions">
              {game.game_type === "Java" && game.type === "Interceptor" ? <button className="neo-management-button" type="button" onClick={(event) => { event.stopPropagation(); navigate(`/user-center/launchers/configuration?id=${encodeURIComponent(game.name)}`); }}><SlidersHorizontal />配置</button> : null}
              {game.status_text === "Running" && game.type === "Interceptor" ? <button className="neo-management-button" type="button" onClick={(event) => { event.stopPropagation(); void navigator.clipboard.writeText(game.local_address); }}><Copy />复制地址</button> : null}
              {game.status_text === "Running" ? <button className="neo-management-button danger" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>停止</button> : null}
              {game.type === "ModDownload" ? <button className="neo-management-button danger" type="button" onClick={(event) => { event.stopPropagation(); void gateway.send("cancel_game_session", [game.guid]); }}>取消</button> : null}
            </div>
          </div>
        )) : <div className="neo-management-empty"><FlaskConical /><h3>暂无活动会话</h3><p>启动游戏后，会话将显示在此处。</p></div>}
      </section>
    </main>
  );
}
