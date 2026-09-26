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

const debugConfig: InterceptorConfig = {
  game_id: "debug-java-server",
  server_name: "Debug Java Server",
  user_id: "debug-java-account",
  nickname: "Debug Java Role",
  server_version: "1.21.4",
  local_address: "127.0.0.1",
  local_port: 25565,
  forward_address: "play.example.net",
  forward_port: 25565,
  mod_info: JSON.stringify({ mods: [{ id: "codexus-interceptor", md5: "d41d8cd98f00b204e9800998ecf8427e" }] }),
};

const debugDetails: NetGameDetails = {
  entity_id: "debug-java-server",
  name: "Debug Java Server",
  brief_image_urls: ["https://api.mcsrvstat.us/icon/play.hypixel.net"],
  server_address: "play.example.net",
  server_port: 25565,
  mc_version_list: [{ mcversionid: "debug-version", name: "1.21.4" }],
};

const debugServer: NetGameSummary = {
  entity_id: "debug-java-server-2",
  name: "Alternative Java Server",
  brief_summary: "A Java Edition server available as the new interceptor upstream.",
  online_count: 42,
  title_image_url: "https://api.mcsrvstat.us/icon/mc.hypixel.net",
};

const debugAccount: GatewayAccount = { id: "debug-java-account", alias: "Debug Java Account" };
const debugRole: GatewayRole = { name: "Debug Java Role", expire_time: 0 };

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
  const debugPreview = import.meta.env.DEV && searchParams.get("debug") === "1";
  const [config, setConfig] = useState<InterceptorConfig | null>(debugPreview ? debugConfig : null);
  const [details, setDetails] = useState<NetGameDetails | null>(debugPreview ? debugDetails : null);
  const configRef = useRef<InterceptorConfig | null>(debugPreview ? debugConfig : null);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => {
    configRef.current = config;
  }, [config]);

  useEffect(() => {
    if (!id || debugPreview || gateway.status !== "connected") return;
    void gateway.send("java_edition/network/session/config", id).catch((error) => {
      notify(error instanceof Error ? error.message : "Unable to load interceptor configuration.", "error");
    });
  }, [debugPreview, gateway.send, gateway.status, id, notify]);

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
        notify(error instanceof Error ? error.message : "Invalid interceptor configuration response.", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify]);

  if (!id) return null;
  const query = `?${searchParams.toString()}`;

  return (
    <InterceptorConfigContext.Provider value={{ id, config, details }}>
      <section className="interceptor-configuration">
        <header className="interceptor-heading">
          <h1>Configuration</h1>
          <p>Here, you can change the upstream server for this interceptor without stopping the proxy itself, and without affecting any established connections.</p>
          <nav aria-label="Interceptor configuration">
            <NavLink end to={`/user-center/launchers/configuration${query}`}>Config</NavLink>
            <NavLink to={`/user-center/launchers/configuration/active-channels${query}`}>Active Channels</NavLink>
            <NavLink to={`/user-center/launchers/configuration/packet-monitor${query}`}>Packet Monitor</NavLink>
            <NavLink to={`/user-center/launchers/configuration/actions${query}`}>Actions</NavLink>
          </nav>
        </header>
        <div className="interceptor-divider" />
        <div className="interceptor-content"><Outlet /></div>
      </section>
    </InterceptorConfigContext.Provider>
  );
}

function ServerChangerModal({ sessionId, debugPreview, onClose }: { sessionId: string; debugPreview: boolean; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [servers, setServers] = useState<NetGameSummary[]>(debugPreview ? [debugServer] : []);
  const [searchResults, setSearchResults] = useState<NetGameSummary[]>([]);
  const [query, setQuery] = useState("");
  const [offset, setOffset] = useState(0);
  const [hasMore, setHasMore] = useState(!debugPreview);
  const [loading, setLoading] = useState(false);
  const [phase, setPhase] = useState<"selecting" | "switching" | "finished">("selecting");
  const listRef = useRef<HTMLDivElement>(null);
  const requestInFlight = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const debounceTimer = useRef<number | null>(null);

  const loadServers = useCallback(async (nextOffset: number) => {
    if (debugPreview || gateway.status !== "connected" || requestInFlight.current) return;
    requestInFlight.current = true;
    setLoading(true);
    try {
      await gateway.send("net_games", { offset: nextOffset, length: 20 });
    } catch (error) {
      requestInFlight.current = false;
      setLoading(false);
      notify(error instanceof Error ? error.message : "Unable to load servers.", "error");
    }
  }, [debugPreview, gateway.send, gateway.status, notify]);

  useEffect(() => {
    if (!debugPreview) void loadServers(0);
  }, [debugPreview, loadServers]);

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
          if (!version) throw new Error("Selected server has no compatible Java version.");
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
        notify(error instanceof Error ? error.message : "Invalid server response.", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify, offset, onClose, sessionId]);

  useEffect(() => {
    if (debounceTimer.current !== null) window.clearTimeout(debounceTimer.current);
    if (!query.trim()) {
      setSearchResults([]);
      return;
    }
    if (debugPreview) {
      setSearchResults([debugServer].filter((server) => server.name.toLowerCase().includes(query.toLowerCase())));
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
  }, [debugPreview, gateway.send, query]);

  const selectServer = (server: NetGameSummary) => {
    setPhase("switching");
    if (debugPreview) {
      window.setTimeout(() => {
        setPhase("finished");
        window.setTimeout(onClose, 500);
      }, 700);
      return;
    }
    void gateway.send("net_games_detail", server.entity_id, "server_changer_modal").catch((error) => {
      setPhase("selecting");
      notify(error instanceof Error ? error.message : "Unable to select server.", "error");
    });
  };

  const visibleServers = query ? searchResults : servers;

  return (
    <div className="legacy-modal-backdrop interceptor-modal-backdrop" role="presentation" onMouseDown={onClose}>
      {phase === "selecting" ? (
        <section className="server-changer-modal" role="dialog" aria-modal="true" aria-label="Servers" onMouseDown={(event) => event.stopPropagation()}>
          <header>
            <h2>Servers</h2>
            <div><label><Search /><input type="text" value={query} placeholder="Search servers..." onChange={(event) => setQuery(event.target.value)} />{query ? <button type="button" aria-label="Clear search" onClick={() => setQuery("")}><X /></button> : null}</label><button type="button" aria-label="Close" onClick={onClose}><X /></button></div>
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
                <span><strong>{server.name}</strong><small>{server.brief_summary}</small><em>Online: <b>{server.online_count}</b></em></span>
              </button>
            ))}
            {loading ? <><div className="server-changer-skeleton" /><div className="server-changer-skeleton" /><div className="server-changer-skeleton" /></> : null}
            {!loading && visibleServers.length === 0 ? <p className="server-changer-empty">{query ? "No servers found" : "No servers available"}</p> : null}
            {!loading && !query && !hasMore && servers.length ? <p className="server-changer-empty compact">No more servers</p> : null}
          </div>
        </section>
      ) : (
        <section className="legacy-modal interceptor-state-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
          <OperationState state={phase} switchingTitle="Switching Servers" switchingDescription="This should only take a moment..." finishedTitle="Successfully Switched" finishedDescription="Your server has been updated and is ready to use." />
        </section>
      )}
    </div>
  );
}

function RoleChangerModal({ sessionId, details, debugPreview, onClose }: { sessionId: string; details: NetGameDetails; debugPreview: boolean; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<GatewayAccount[]>(debugPreview ? [debugAccount] : []);
  const [roles, setRoles] = useState<GatewayRole[]>(debugPreview ? [debugRole] : []);
  const [accountIndex, setAccountIndex] = useState(0);
  const [roleIndex, setRoleIndex] = useState(0);
  const [loadingRoles, setLoadingRoles] = useState(false);
  const [phase, setPhase] = useState<"selecting" | "switching" | "finished">("selecting");
  const [loginOpen, setLoginOpen] = useState(false);
  const [createRoleOpen, setCreateRoleOpen] = useState(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  const requestRoles = useCallback((accountId: string) => {
    if (debugPreview) return;
    setLoadingRoles(true);
    void gateway.send("get_roles", { id: accountId, game: details.entity_id, type: "net_game" }).catch((error) => {
      setLoadingRoles(false);
      notify(error instanceof Error ? error.message : "Unable to load roles.", "error");
    });
  }, [debugPreview, details.entity_id, gateway.send, notify]);

  useEffect(() => {
    if (!debugPreview) void gateway.send("get_accounts", "available", "role_changer_modal");
  }, [debugPreview, gateway.send]);

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
        notify(error instanceof Error ? error.message : "Invalid role response.", "error");
      }
    }
  }, [gateway.messages, gateway.send, notify, onClose, requestRoles, sessionId]);

  const switchRole = () => {
    const account = accounts[accountIndex];
    const role = roles[roleIndex];
    if (!account || !role) return;
    setPhase("switching");
    if (debugPreview) {
      window.setTimeout(() => {
        setPhase("finished");
        window.setTimeout(onClose, 500);
      }, 700);
      return;
    }
    void gateway.send("java_edition/session/switch_role", { id: sessionId, user_id: account.id, role: role.name }).catch((error) => {
      setPhase("selecting");
      notify(error instanceof Error ? error.message : "Unable to switch role.", "error");
    });
  };

  const selectedAccount = accounts[accountIndex];

  return (
    <div className="legacy-modal-backdrop interceptor-modal-backdrop" role="presentation" onMouseDown={onClose}>
      {phase === "selecting" ? (
        <section className="legacy-modal role-changer-modal" role="dialog" aria-modal="true" aria-label="Switch Role" onMouseDown={(event) => event.stopPropagation()}>
          <header className="legacy-modal-header"><h2>Switch Role</h2><button type="button" aria-label="Close" onClick={onClose}><X /></button></header>
          <div className="role-changer-body">
            <SelectMenu
              title="Target Account"
              items={accounts}
              selectedIndex={accountIndex}
              itemLabel={(account) => accountLabel(account as GatewayAccount)}
              onChange={(index) => {
                setAccountIndex(index);
                setRoleIndex(0);
                if (accounts[index]) requestRoles(accounts[index].id);
              }}
              action={{ label: "Add Account", onClick: () => setLoginOpen(true) }}
            />
            <SelectMenu
              title="Target Role"
              items={roles}
              selectedIndex={roleIndex}
              itemLabel={(role) => `${(role as GatewayRole).name}${Number((role as GatewayRole).expire_time ?? 0) !== 0 ? " (删除中)" : ""}`}
              itemDisabled={(role) => Number((role as GatewayRole).expire_time ?? 0) !== 0}
              onChange={setRoleIndex}
              action={{ label: "Create New Role", onClick: () => setCreateRoleOpen(true) }}
            />
          </div>
          <footer className="role-changer-actions"><button type="button" onClick={onClose}>Cancel</button><button className="primary" type="button" disabled={loadingRoles || roles.length === 0} onClick={switchRole}>{loadingRoles ? <><LoaderCircle className="spin" />Processing...</> : "Confirm Change"}</button></footer>
          {loginOpen ? <AccountLoginModal onClose={() => setLoginOpen(false)} /> : null}
          {createRoleOpen && selectedAccount ? <CreateRoleDialog accountId={selectedAccount.id} gameId={details.entity_id} kind="net_game" onClose={() => { setCreateRoleOpen(false); requestRoles(selectedAccount.id); }} /> : null}
        </section>
      ) : (
        <section className="legacy-modal interceptor-state-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
          <OperationState state={phase} switchingTitle="Switching Role" switchingDescription="This should only take a moment..." finishedTitle="Successfully Switched Role" finishedDescription="Your role has been updated and is ready to use." />
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
  const debugPreview = import.meta.env.DEV && searchParams.get("debug") === "1";
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
    ["Game ID", config.game_id],
    ["Server Version", config.server_version],
    ["Local Endpoint", `${config.local_address}:${config.local_port}`],
    ["Forward Endpoint", `${config.forward_address}:${config.forward_port}`],
  ];

  return (
    <>
      <section className="interceptor-upstream-card">
        <div>
          <span>Upstream</span>
          <h2>{config.server_name}</h2>
          <p>This is the currently intercepted server. Click the button below to instantly switch to another available server.</p>
          <button type="button" onClick={() => setServerChangerOpen(true)}>Reselect</button>
        </div>
        <div>{details.brief_image_urls?.[0] ? <img src={details.brief_image_urls[0]} alt="Server View" /> : null}</div>
      </section>
      <section className="interceptor-role-card">
        <div><span>Role <small>{config.user_id}</small></span><strong>{config.nickname}</strong></div>
        <button type="button" onClick={() => setRoleChangerOpen(true)}>Switch Role</button>
      </section>
      <section className="interceptor-detail-card">
        <h2>Details</h2>
        <dl>{detailsRows.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
      </section>
      <section className="interceptor-mods-card">
        <h2>Mods ({mods.length})</h2>
        {mods.length ? <div>{mods.map((mod, index) => <article key={mod.id || index}><span><small>Mod Identifier</small><strong>{mod.id}</strong></span><span><small>MD5</small><strong>{mod.md5}</strong></span></article>)}</div> : <p>No modifications detected.</p>}
      </section>
      {serverChangerOpen ? <ServerChangerModal sessionId={id} debugPreview={debugPreview} onClose={() => setServerChangerOpen(false)} /> : null}
      {roleChangerOpen ? <RoleChangerModal sessionId={id} details={details} debugPreview={debugPreview} onClose={() => setRoleChangerOpen(false)} /> : null}
    </>
  );
}

export function InterceptorPlaceholderPage({ section }: { section: "Actions" | "Active Channels" | "Packet Monitor" }) {
  return <section className="interceptor-placeholder"><h1>Work In Progress</h1><p>This section of the <strong>{section}</strong> is currently being refined. New configuration modules will be deployed shortly.</p></section>;
}
