import { Check, LoaderCircle, Search, X } from "lucide-react";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { NavLink, Outlet, useSearchParams } from "react-router-dom";
import { CreateRoleDialog, SelectMenu } from "../../components/JavaJoinGameModal";
import { useGateway, useToasts } from "../../context/AppContext";
import type { GatewayMessage } from "../../types";
import { AccountLoginModal } from "./DashboardPage";
import { consumeGatewayMessages, parseGatewayPayload } from "./gatewayData";

interface InterceptorConfig {
  game_id: string;
  is_rental?: boolean;
  server_name: string;
  user_id: string;
  nickname: string;
  server_version: string;
  local_address: string;
  local_port: string | number;
  forward_address: string;
  forward_port: string | number;
  mod_info?: string;
}

interface GameVersion extends Record<string, unknown> {
  mcversionid: string | number;
  name: string;
}

interface NetGameDetails extends Record<string, unknown> {
  entity_id: string;
  name: string;
  brief_image_urls?: string[];
  server_address: string;
  server_port: string | number;
  mc_version_list?: GameVersion[];
}

interface NetGameSummary extends Record<string, unknown> {
  entity_id: string;
  name: string;
  brief_summary: string;
  online_count: string | number;
  title_image_url: string;
}

interface GatewayAccount extends Record<string, unknown> {
  id: string;
  alias?: string;
}

interface GatewayRole extends Record<string, unknown> {
  name: string;
  expire_time?: number;
}

interface InterceptorConfigContextValue {
  id: string;
  config: InterceptorConfig | null;
  details: NetGameDetails | null;
}

const InterceptorConfigContext = createContext<InterceptorConfigContextValue | null>(null);

function useInterceptorConfig(): InterceptorConfigContextValue {
  const value = useContext(InterceptorConfigContext);
  if (!value) throw new Error("useInterceptorConfig must be used inside InterceptorConfigurationLayout");
  return value;
}

function accountLabel(account: GatewayAccount): string {
  return account.alias || account.id;
}

function OperationState({
  state,
  switchingTitle,
  switchingDescription,
  finishedTitle,
  finishedDescription,
}: {
  state: "switching" | "finished";
  switchingTitle: string;
  switchingDescription: string;
  finishedTitle: string;
  finishedDescription: string;
}) {
  return (
    <div className="operation-state">
      {state === "switching" ? <LoaderCircle className="spin" /> : <span><Check /></span>}
      <div>
        <h3>{state === "switching" ? switchingTitle : finishedTitle}</h3>
        <p>{state === "switching" ? switchingDescription : finishedDescription}</p>
      </div>
    </div>
  );
}

export function InterceptorConfigurationLayout() {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [searchParams] = useSearchParams();
  const id = searchParams.get("id") ?? "";
  const [config, setConfig] = useState<InterceptorConfig | null>(null);
  const [details, setDetails] = useState<NetGameDetails | null>(null);
  const configRef = useRef<InterceptorConfig | null>(null);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => {
    configRef.current = config;
  }, [config]);

  useEffect(() => {
    if (!id || gateway.status !== "connected") return;
    void gateway.send("java_edition/network/session/config", id).catch((error) => {
      notify(error instanceof Error ? error.message : "无法加载代理通道配置。", "error");
    });
  }, [gateway.send, gateway.status, id, notify]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      try {
        if (message.type === "java_edition/network/session/config") {
          const nextConfig = parseGatewayPayload<InterceptorConfig>(message.payload);
          configRef.current = nextConfig;
          setConfig(nextConfig);
          void gateway.send(nextConfig.is_rental ? "rental_games_detail" : "net_games_detail", nextConfig.game_id);
        } else if ((message.type === "net_games_detail" || message.type === "rental_games_detail") && message.identify !== "server_changer_modal") {
          const nextDetails = parseGatewayPayload<NetGameDetails>(message.payload);
          if (nextDetails.entity_id === configRef.current?.game_id) setDetails(nextDetails);
        }
      } catch (error) {
        notify(error instanceof Error ? error.message : "代理通道配置响应无效。", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify]);

  if (!id) return null;
  const query = `?${searchParams.toString()}`;

  return (
    <InterceptorConfigContext.Provider value={{ id, config, details }}>
      <section className="interceptor-configuration">
        <header className="interceptor-heading">
          <h1>代理通道配置</h1>
          <p>在此切换代理通道的上游服务器，无需停止代理，也不会影响已建立的连接。</p>
          <nav aria-label="代理通道配置">
            <NavLink end to={`/user-center/launchers/configuration${query}`}>配置</NavLink>
            <NavLink to={`/user-center/launchers/configuration/active-channels${query}`}>活动连接</NavLink>
            <NavLink to={`/user-center/launchers/configuration/packet-monitor${query}`}>数据包监控</NavLink>
            <NavLink to={`/user-center/launchers/configuration/actions${query}`}>操作</NavLink>
          </nav>
        </header>
        <div className="interceptor-divider" />
        <div className="interceptor-content"><Outlet /></div>
      </section>
    </InterceptorConfigContext.Provider>
  );
}

function ServerChangerModal({ sessionId, onClose }: { sessionId: string; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [servers, setServers] = useState<NetGameSummary[]>([]);
  const [searchResults, setSearchResults] = useState<NetGameSummary[]>([]);
  const [query, setQuery] = useState("");
  const [offset, setOffset] = useState(0);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [phase, setPhase] = useState<"selecting" | "switching" | "finished">("selecting");
  const listRef = useRef<HTMLDivElement>(null);
  const requestInFlight = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const debounceTimer = useRef<number | null>(null);

  const loadServers = useCallback(async (nextOffset: number) => {
    if (gateway.status !== "connected" || requestInFlight.current) return;
    requestInFlight.current = true;
    setLoading(true);
    try {
      await gateway.send("net_games", { offset: nextOffset, length: 20 });
    } catch (error) {
      requestInFlight.current = false;
      setLoading(false);
      notify(error instanceof Error ? error.message : "无法加载服务器列表。", "error");
    }
  }, [gateway.send, gateway.status, notify]);

  useEffect(() => {
    void loadServers(0);
  }, [loadServers]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      try {
        if (message.type === "net_games") {
          const response = parseGatewayPayload<{ entities: NetGameSummary[]; total: number }>(message.payload);
          setServers((current) => offset === 0 ? response.entities : [...current, ...response.entities]);
          setHasMore(offset + response.entities.length < response.total);
          requestInFlight.current = false;
          setLoading(false);
        } else if (message.type === "net_games_search") {
          const response = parseGatewayPayload<{ entities: NetGameSummary[] }>(message.payload);
          setSearchResults(response.entities);
          requestInFlight.current = false;
          setLoading(false);
        } else if (message.type === "net_games_detail" && message.identify === "server_changer_modal") {
          const detail = parseGatewayPayload<NetGameDetails>(message.payload);
          const version = detail.mc_version_list?.[0];
          if (!version) throw new Error("所选服务器没有兼容的 Java 版本。");
          void gateway.send("java_edition/session/reselect_server", {
            id: sessionId,
            game_version_id: version.mcversionid,
            game_version: version.name,
            game_name: detail.name,
            game_id: detail.entity_id,
            address: detail.server_address,
            port: detail.server_port,
          });
        } else if (message.type === "java_edition/session/reselect_server") {
          void gateway.send("java_edition/network/session/config", sessionId);
          setPhase("finished");
          window.setTimeout(onClose, 500);
        }
      } catch (error) {
        requestInFlight.current = false;
        setLoading(false);
        setPhase("selecting");
        notify(error instanceof Error ? error.message : "服务器响应无效。", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify, offset, onClose, sessionId]);

  useEffect(() => {
    if (debounceTimer.current !== null) window.clearTimeout(debounceTimer.current);
    if (!query.trim()) {
      setSearchResults([]);
      return;
    }
    debounceTimer.current = window.setTimeout(() => {
      if (requestInFlight.current) return;
      requestInFlight.current = true;
      setLoading(true);
      void gateway.send("net_games_search", query.trim()).catch(() => {
        requestInFlight.current = false;
        setLoading(false);
      });
    }, 300);
    return () => {
      if (debounceTimer.current !== null) window.clearTimeout(debounceTimer.current);
    };
  }, [gateway.send, query]);

  const selectServer = (server: NetGameSummary) => {
    setPhase("switching");
    void gateway.send("net_games_detail", server.entity_id, "server_changer_modal").catch((error) => {
      setPhase("selecting");
      notify(error instanceof Error ? error.message : "无法选择服务器。", "error");
    });
  };

  const visibleServers = query ? searchResults : servers;

  return (
    <div className="legacy-modal-backdrop interceptor-modal-backdrop" role="presentation" onMouseDown={onClose}>
      {phase === "selecting" ? (
        <section className="server-changer-modal" role="dialog" aria-modal="true" aria-label="服务器" onMouseDown={(event) => event.stopPropagation()}>
          <header>
            <h2>服务器</h2>
            <div><label><Search /><input type="text" value={query} placeholder="搜索服务器..." onChange={(event) => setQuery(event.target.value)} />{query ? <button type="button" aria-label="清空搜索" onClick={() => setQuery("")}><X /></button> : null}</label><button type="button" aria-label="关闭" onClick={onClose}><X /></button></div>
          </header>
          <div
            className="server-changer-list"
            ref={listRef}
            onScroll={() => {
              const list = listRef.current;
              if (!list || query || loading || !hasMore || list.scrollHeight - list.scrollTop - list.clientHeight >= 200) return;
              const nextOffset = offset + 20;
              setOffset(nextOffset);
              void loadServers(nextOffset);
            }}
          >
            {visibleServers.map((server, index) => (
              <button type="button" className="server-changer-row" onClick={() => selectServer(server)} key={`${index}-${server.entity_id}`}>
                <span>{server.title_image_url ? <img src={server.title_image_url} alt={server.name} /> : null}</span>
                <span><strong>{server.name}</strong><small>{server.brief_summary}</small><em>在线人数：<b>{server.online_count}</b></em></span>
              </button>
            ))}
            {loading ? <><div className="server-changer-skeleton" /><div className="server-changer-skeleton" /><div className="server-changer-skeleton" /></> : null}
            {!loading && visibleServers.length === 0 ? <p className="server-changer-empty">{query ? "未找到服务器" : "暂无可用服务器"}</p> : null}
            {!loading && !query && !hasMore && servers.length ? <p className="server-changer-empty compact">没有更多服务器了</p> : null}
          </div>
        </section>
      ) : (
        <section className="legacy-modal interceptor-state-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
          <OperationState state={phase} switchingTitle="正在切换服务器" switchingDescription="请稍候..." finishedTitle="切换成功" finishedDescription="服务器已切换，可以继续使用。" />
        </section>
      )}
    </div>
  );
}

function RoleChangerModal({ sessionId, details, onClose }: { sessionId: string; details: NetGameDetails; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<GatewayAccount[]>([]);
  const [roles, setRoles] = useState<GatewayRole[]>([]);
  const [accountIndex, setAccountIndex] = useState(0);
  const [roleIndex, setRoleIndex] = useState(0);
  const [loadingRoles, setLoadingRoles] = useState(false);
  const [phase, setPhase] = useState<"selecting" | "switching" | "finished">("selecting");
  const [loginOpen, setLoginOpen] = useState(false);
  const [createRoleOpen, setCreateRoleOpen] = useState(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  const requestRoles = useCallback((accountId: string) => {
    setLoadingRoles(true);
    void gateway.send("get_roles", { id: accountId, game: details.entity_id, type: "net_game" }).catch((error) => {
      setLoadingRoles(false);
      notify(error instanceof Error ? error.message : "无法加载角色列表。", "error");
    });
  }, [details.entity_id, gateway.send, notify]);

  useEffect(() => {
    void gateway.send("get_accounts", "available", "role_changer_modal");
  }, [gateway.send]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      try {
        if (message.type === "get_accounts") {
          const nextAccounts = parseGatewayPayload<GatewayAccount[]>(message.payload);
          setAccounts(nextAccounts);
          setAccountIndex(0);
          setRoleIndex(0);
          if (nextAccounts[0]) requestRoles(nextAccounts[0].id);
        } else if (message.type === "get_roles") {
          setRoles(parseGatewayPayload<GatewayRole[]>(message.payload));
          setRoleIndex(0);
          setLoadingRoles(false);
        } else if (message.type === "java_edition/session/switch_role") {
          void gateway.send("java_edition/network/session/config", sessionId);
          setPhase("finished");
          window.setTimeout(onClose, 500);
        }
      } catch (error) {
        setLoadingRoles(false);
        setPhase("selecting");
        notify(error instanceof Error ? error.message : "角色响应无效。", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify, onClose, requestRoles, sessionId]);

  const switchRole = () => {
    const account = accounts[accountIndex];
    const role = roles[roleIndex];
    if (!account || !role) return;
    setPhase("switching");
    void gateway.send("java_edition/session/switch_role", { id: sessionId, user_id: account.id, role: role.name }).catch((error) => {
      setPhase("selecting");
      notify(error instanceof Error ? error.message : "无法切换角色。", "error");
    });
  };

  const selectedAccount = accounts[accountIndex];

  return (
    <div className="legacy-modal-backdrop interceptor-modal-backdrop" role="presentation" onMouseDown={onClose}>
      {phase === "selecting" ? (
        <section className="legacy-modal role-changer-modal" role="dialog" aria-modal="true" aria-label="切换角色" onMouseDown={(event) => event.stopPropagation()}>
          <header className="legacy-modal-header"><h2>切换角色</h2><button type="button" aria-label="关闭" onClick={onClose}><X /></button></header>
          <div className="role-changer-body">
            <SelectMenu
              title="目标账号"
              items={accounts}
              selectedIndex={accountIndex}
              itemLabel={(account) => accountLabel(account as GatewayAccount)}
              onChange={(index) => {
                setAccountIndex(index);
                setRoleIndex(0);
                if (accounts[index]) requestRoles(accounts[index].id);
              }}
              action={{ label: "添加账号", onClick: () => setLoginOpen(true) }}
            />
            <SelectMenu
              title="目标角色"
              items={roles}
              selectedIndex={roleIndex}
              itemLabel={(role) => `${(role as GatewayRole).name}${Number((role as GatewayRole).expire_time ?? 0) !== 0 ? " (删除中)" : ""}`}
              itemDisabled={(role) => Number((role as GatewayRole).expire_time ?? 0) !== 0}
              onChange={setRoleIndex}
              action={{ label: "创建角色", onClick: () => setCreateRoleOpen(true) }}
            />
          </div>
          <footer className="role-changer-actions"><button type="button" onClick={onClose}>取消</button><button className="primary" type="button" disabled={loadingRoles || roles.length === 0} onClick={switchRole}>{loadingRoles ? <><LoaderCircle className="spin" />正在处理...</> : "确认切换"}</button></footer>
          {loginOpen ? <AccountLoginModal onClose={() => setLoginOpen(false)} /> : null}
          {createRoleOpen && selectedAccount ? <CreateRoleDialog accountId={selectedAccount.id} gameId={details.entity_id} kind="net_game" onClose={() => { setCreateRoleOpen(false); requestRoles(selectedAccount.id); }} /> : null}
        </section>
      ) : (
        <section className="legacy-modal interceptor-state-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
          <OperationState state={phase} switchingTitle="正在切换角色" switchingDescription="请稍候..." finishedTitle="角色切换成功" finishedDescription="角色已切换，可以继续使用。" />
        </section>
      )}
    </div>
  );
}

function ConfigSkeleton() {
  return <div className="interceptor-skeleton"><span /><span /><span /><span /></div>;
}

export function InterceptorConfigPage() {
  const { id, config, details } = useInterceptorConfig();
  const [searchParams] = useSearchParams();
  const [serverChangerOpen, setServerChangerOpen] = useState(false);
  const [roleChangerOpen, setRoleChangerOpen] = useState(false);
  const mods = useMemo(() => {
    if (!config?.mod_info) return [] as Array<{ id: string; md5: string }>;
    try {
      const parsed = JSON.parse(config.mod_info) as { mods?: Array<{ id: string; md5: string }> };
      return parsed.mods ?? [];
    } catch {
      return [];
    }
  }, [config?.mod_info]);

  if (!config || !details) return <ConfigSkeleton />;
  const detailsRows = [
    ["游戏 ID", config.game_id],
    ["服务器版本", config.server_version],
    ["本地地址", `${config.local_address}:${config.local_port}`],
    ["转发地址", `${config.forward_address}:${config.forward_port}`],
  ];

  return (
    <>
      <section className="interceptor-upstream-card">
        <div>
          <span>上游服务器</span>
          <h2>{config.server_name}</h2>
          <p>这是当前代理的服务器。点击下方按钮可立即切换到其他可用服务器。</p>
          <button type="button" onClick={() => setServerChangerOpen(true)}>重新选择</button>
        </div>
        <div>{details.brief_image_urls?.[0] ? <img src={details.brief_image_urls[0]} alt="服务器预览" /> : null}</div>
      </section>
      <section className="interceptor-role-card">
        <div><span>角色 <small>{config.user_id}</small></span><strong>{config.nickname}</strong></div>
        <button type="button" onClick={() => setRoleChangerOpen(true)}>切换角色</button>
      </section>
      <section className="interceptor-detail-card">
        <h2>详情</h2>
        <dl>{detailsRows.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
      </section>
      <section className="interceptor-mods-card">
        <h2>模组（{mods.length}）</h2>
        {mods.length ? <div>{mods.map((mod, index) => <article key={mod.id || index}><span><small>模组标识</small><strong>{mod.id}</strong></span><span><small>MD5</small><strong>{mod.md5}</strong></span></article>)}</div> : <p>未检测到模组。</p>}
      </section>
      {serverChangerOpen ? <ServerChangerModal sessionId={id} onClose={() => setServerChangerOpen(false)} /> : null}
      {roleChangerOpen ? <RoleChangerModal sessionId={id} details={details} onClose={() => setRoleChangerOpen(false)} /> : null}
    </>
  );
}

export function InterceptorPlaceholderPage({ section }: { section: "操作" | "活动连接" | "数据包监控" }) {
  return <section className="interceptor-placeholder"><h1>开发中</h1><p><strong>{section}</strong>功能正在完善中，后续将提供新的配置模块。</p></section>;
}
