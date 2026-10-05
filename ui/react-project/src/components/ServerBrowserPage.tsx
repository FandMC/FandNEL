import {
  ChevronLeft,
  ChevronRight,
  Heart,
  LockKeyhole,
  RefreshCw,
  Search,
  Shield,
} from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useGateway } from "../context/AppContext";
import type { GatewayMessage } from "../types";

type Resource = Record<string, unknown>;
export type ServerBrowserKind = "java-server" | "java-rental" | "bedrock-server" | "bedrock-rental";

interface CardBrowserDefinition {
  title: string;
  description: string;
  requestType: "net_games" | "rental_games" | "pe_net_games";
  searchType?: "net_games_search" | "rental_games_search";
  storageKey: "servers-store" | "rental-servers-store" | "bedrock-servers-store";
  rental: boolean;
  bedrock: boolean;
}

interface PersistedCardBrowserState {
  savedServers: Resource[];
  hasMore: boolean;
  page: number;
}

const cardDefinitions: Record<Exclude<ServerBrowserKind, "bedrock-rental">, CardBrowserDefinition> = {
  "java-server": {
    title: "网络服务器",
    description: "浏览和加入网络服务器",
    requestType: "net_games",
    searchType: "net_games_search",
    storageKey: "servers-store",
    rental: false,
    bedrock: false,
  },
  "java-rental": {
    title: "租赁服务器",
    description: "浏览和加入租赁服务器",
    requestType: "rental_games",
    searchType: "rental_games_search",
    storageKey: "rental-servers-store",
    rental: true,
    bedrock: false,
  },
  "bedrock-server": {
    title: "网络服务器(PE)",
    description: "浏览和加入基岩版网络服务器",
    requestType: "pe_net_games",
    storageKey: "bedrock-servers-store",
    rental: false,
    bedrock: true,
  },
};

function parsePayload(payload: unknown): unknown {
  let value = payload;
  for (let index = 0; index < 2 && typeof value === "string"; index += 1) {
    try {
      value = JSON.parse(value);
    } catch {
      break;
    }
  }
  return value;
}

function payloadRecord(payload: unknown): Resource {
  const value = parsePayload(payload);
  return value && typeof value === "object" && !Array.isArray(value) ? value as Resource : {};
}

function payloadError(payload: unknown): string {
  const value = payloadRecord(payload);
  return value.code !== undefined && Number(value.code) !== 0
    ? String(value.message ?? "无法加载服务器。")
    : "";
}

function resourceList(value: unknown): Resource[] {
  return Array.isArray(value)
    ? value.filter((item): item is Resource => Boolean(item && typeof item === "object"))
    : [];
}

function text(item: Resource, key: string): string {
  const value = item[key];
  return value === undefined || value === null ? "" : String(value);
}

function hasPassword(item: Resource): boolean {
  const value = ["has_pwd", "has_password", "hasPassword", "password_required"]
    .map((key) => text(item, key).trim().toLowerCase())
    .find((entry) => entry !== "") ?? "";
  return value !== "" && !["0", "false", "no", "none", "null"].includes(value);
}

function number(item: Resource, key: string): number {
  const value = Number(item[key]);
  return Number.isFinite(value) ? value : 0;
}

function loadCardState(storageKey: string): PersistedCardBrowserState {
  try {
    const stored = JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as { state?: Partial<PersistedCardBrowserState> } | null;
    const savedServers = resourceList(stored?.state?.savedServers);
    return {
      savedServers,
      // Empty states written by an old failed request must be fetched again.
      hasMore: savedServers.length === 0 || stored?.state?.hasMore !== false,
      page: Math.max(1, Number(stored?.state?.page) || 1),
    };
  } catch {
    return { savedServers: [], hasMore: true, page: 1 };
  }
}

function saveCardState(storageKey: string, state: PersistedCardBrowserState): void {
  sessionStorage.setItem(storageKey, JSON.stringify({ state, version: 0 }));
}

function nextMessages(messages: GatewayMessage[], lastHandled: GatewayMessage | null): GatewayMessage[] {
  if (!lastHandled) return messages;
  const index = messages.lastIndexOf(lastHandled);
  return messages.slice(index >= 0 ? index + 1 : Math.max(messages.length - 1, 0));
}

function ServerImage({ src, name }: { src: string; name: string }) {
  const [failedSource, setFailedSource] = useState("");
  return (
    <>
      {src && failedSource !== src ? <img className="server-card-img" src={src} alt={`${name} 服务器图片`} loading="lazy" onError={() => setFailedSource(src)} /> : <div className="server-card-placeholder" />}
      <div className="server-card-gradient" />
    </>
  );
}

function CardServerBrowser({ kind }: { kind: Exclude<ServerBrowserKind, "bedrock-rental"> }) {
  const definition = cardDefinitions[kind];
  const navigate = useNavigate();
  const gateway = useGateway();
  const initialState = useRef(loadCardState(definition.storageKey));
  const [savedServers, setSavedServers] = useState(initialState.current.savedServers);
  const [hasMore, setHasMore] = useState(initialState.current.hasMore);
  const [page, setPage] = useState(initialState.current.page);
  const [query, setQuery] = useState("");
  const [searchResults, setSearchResults] = useState<Resource[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const requestInFlight = useRef(false);
  const browserRef = useRef<HTMLDivElement>(null);
  const lastHandled = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const savedServersRef = useRef(savedServers);
  const pageRef = useRef(page);
  const hasMoreRef = useRef(hasMore);

  useEffect(() => { savedServersRef.current = savedServers; }, [savedServers]);
  useEffect(() => { pageRef.current = page; }, [page]);
  useEffect(() => { hasMoreRef.current = hasMore; }, [hasMore]);

  const persist = useCallback((next: PersistedCardBrowserState) => {
    savedServersRef.current = next.savedServers;
    pageRef.current = next.page;
    hasMoreRef.current = next.hasMore;
    setSavedServers(next.savedServers);
    setPage(next.page);
    setHasMore(next.hasMore);
    saveCardState(definition.storageKey, next);
  }, [definition.storageKey]);

  const loadNextPage = useCallback(async () => {
    if (gateway.status !== "connected" || requestInFlight.current || !hasMoreRef.current) return;
    requestInFlight.current = true;
    setLoading(true);
    setError("");
    const payload = {
      offset: (pageRef.current - 1) * 20,
      ...(definition.rental ? {} : { length: 20 }),
    };
    try {
      await gateway.send(definition.requestType, payload);
    } catch (requestError) {
      requestInFlight.current = false;
      setLoading(false);
      setError(requestError instanceof Error ? requestError.message : "无法加载服务器。");
    }
  }, [definition.requestType, definition.rental, gateway.send, gateway.status]);

  useEffect(() => {
    if (gateway.status === "connected" && hasMoreRef.current && savedServersRef.current.length === 0) void loadNextPage();
  }, [gateway.status, loadNextPage]);

  useEffect(() => {
    const pending = nextMessages(gateway.messages, lastHandled.current);
    for (const message of pending) {
      if (message.type === definition.requestType) {
        const responseError = payloadError(message.payload);
        if (responseError) {
          setError(responseError);
          requestInFlight.current = false;
          setLoading(false);
          continue;
        }
        const payload = payloadRecord(message.payload);
        const entities = resourceList(payload.entities);
        const combined = [...savedServersRef.current, ...entities];
        const total = Number(payload.total) || 0;
        persist({
          savedServers: combined,
          hasMore: total > combined.length,
          page: total > combined.length ? pageRef.current + 1 : pageRef.current,
        });
        requestInFlight.current = false;
        setLoading(false);
        setError("");
      } else if (definition.searchType && message.type === definition.searchType) {
        const responseError = payloadError(message.payload);
        if (responseError) {
          setError(responseError);
          requestInFlight.current = false;
          setLoading(false);
          continue;
        }
        setSearchResults(resourceList(payloadRecord(message.payload).entities));
        requestInFlight.current = false;
        setLoading(false);
        setError("");
      }
    }
    lastHandled.current = gateway.messages.at(-1) ?? null;
  }, [definition.requestType, definition.searchType, gateway.messages, persist]);

  useEffect(() => {
    const container = browserRef.current?.closest<HTMLElement>(".content");
    if (!container) return;
    const onScroll = () => {
      if (container.scrollTop + container.clientHeight >= container.scrollHeight - 200) void loadNextPage();
    };
    container.addEventListener("scroll", onScroll);
    return () => container.removeEventListener("scroll", onScroll);
  }, [loadNextPage]);

  const search = async () => {
    if (!definition.searchType || gateway.status !== "connected" || requestInFlight.current) return;
    requestInFlight.current = true;
    setLoading(true);
    setError("");
    try {
      await gateway.send(definition.searchType, query);
    } catch (requestError) {
      requestInFlight.current = false;
      setLoading(false);
      setError(requestError instanceof Error ? requestError.message : "无法搜索服务器。");
    }
  };

  const reload = () => {
    setError("");
    persist({ savedServers: [], hasMore: true, page: 1 });
    setSearchResults(null);
    void loadNextPage();
  };
  const servers = searchResults ?? savedServers;

  const openServer = (server: Resource) => {
    const id = text(server, definition.bedrock ? "item_id" : "entity_id");
    const name = text(server, definition.bedrock ? "res_name" : definition.rental ? "server_name" : "name");
    const route = definition.bedrock ? "/user-center/bedrock/launch" : definition.rental ? "/user-center/rentals/details" : "/user-center/servers/details";
    navigate(`${route}?id=${encodeURIComponent(id)}&name=${encodeURIComponent(name)}`, definition.bedrock || definition.rental ? { state: { server } } : undefined);
  };

  return (
    <div className="workspace-page network-page" ref={browserRef}>
      <section className="network-header">
        <div><h2>{definition.title}</h2><p className="page-desc">{definition.description}</p></div>
        <div className="network-actions">
          <button className="btn-secondary" type="button" disabled={loading} onClick={reload}><RefreshCw size={16} />刷新</button>
          <input className="network-search" type="text" placeholder="搜索服务器..." value={query} onKeyDown={(event) => { if (event.key === "Enter") void search(); }} onChange={(event) => { setQuery(event.target.value); setSearchResults(null); }} />
        </div>
      </section>
      <div className="server-grid">
        {servers.map((server, index) => {
          const id = text(server, definition.bedrock ? "item_id" : "entity_id");
          const name = text(server, definition.bedrock ? "res_name" : definition.rental ? "server_name" : "name");
          const online = text(server, definition.bedrock ? "online_num" : definition.rental ? "player_count" : "online_count");
          const image = text(server, definition.rental ? "image_url" : "title_image_url");
          const hasPassword = definition.rental && text(server, "has_pwd") === "1";
          return (
            <button className="server-card" type="button" onClick={() => openServer(server)} key={id || `server-${index}`}>
              <ServerImage src={image} name={name} />
              <div className="server-card-body">
                <h2 className="server-card-name">{name}</h2>
                <div className="server-card-online">在线：{online || "0"}</div>
                <div className="server-card-id">{id}</div>
                {hasPassword ? <span className="rental-lock"><LockKeyhole size={13} />需要密码</span> : null}
              </div>
            </button>
          );
        })}
      </div>
      {!error && !loading && servers.length === 0 ? <div className="network-empty">{query ? "未找到匹配的服务器" : "暂无服务器"}</div> : null}
      <div className="network-load-more">
        {error ? <div className="server-list-error" role="alert">{error}<button type="button" onClick={reload}>重试</button></div> : null}
        {loading ? <div>正在加载更多服务器…</div> : null}
        {!error && !hasMore && !loading ? <><div>已显示全部服务器</div><button type="button" onClick={reload}>重新加载</button></> : null}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: number }) {
  return (
    <span className={`bedrock-rental-status ${status === 1 ? "online" : status === 0 ? "offline" : "unknown"}`}>
      <i />{status === 1 ? "在线" : status === 0 ? "离线" : "未知"}
    </span>
  );
}

function BedrockRentalBrowser() {
  const navigate = useNavigate();
  const gateway = useGateway();
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [servers, setServers] = useState<Resource[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const lastHandled = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const totalPages = total > 0 ? Math.ceil(total / 30) : 0;
  const start = (page - 1) * 30;
  const visibleServers = servers.filter((server) =>
    text(server, "server_name").toLowerCase().includes(query.toLowerCase()) ||
    text(server, "name").toLowerCase().includes(query.toLowerCase()),
  );

  useEffect(() => { setPage(1); }, [query]);

  useEffect(() => {
    if (gateway.status !== "connected") return;
    setLoading(true);
    setError("");
    void gateway.send("pe_rental_games", { offset: (page - 1) * 30 }).catch((requestError) => {
      setError(requestError instanceof Error ? requestError.message : "无法加载服务器。");
      setLoading(false);
    });
  }, [gateway.send, gateway.status, page]);

  useEffect(() => {
    const pending = nextMessages(gateway.messages, lastHandled.current);
    for (const message of pending) {
      if (message.type === "pe_rental_games") {
        const payload = payloadRecord(message.payload);
        const responseError = payloadError(message.payload);
        if (responseError) {
          setError(responseError);
        } else if (Number(payload.code) === 0 || payload.code === undefined) {
          setServers(resourceList(payload.entities));
          setTotal(Number.parseInt(String(payload.total), 10) || 0);
          setError("");
        }
        setLoading(false);
      }
    }
    lastHandled.current = gateway.messages.at(-1) ?? null;
  }, [gateway.messages]);

  const pageNumbers = Array.from({ length: Math.min(totalPages, 7) }, (_, index) => {
    if (totalPages <= 7 || page <= 4) return index + 1;
    if (page >= totalPages - 3) return totalPages - 6 + index;
    return page - 3 + index;
  });

  const openRental = (server: Resource) => {
    const id = text(server, "entity_id");
    const name = text(server, "server_name");
    navigate(`/user-center/rentals-for-bedrock/launch?id=${encodeURIComponent(id)}&name=${encodeURIComponent(name)}`, { state: { server } });
  };

  return (
    <div className="workspace-page network-page bedrock-rental-browser">
      <section className={`network-header ${totalPages < 1 ? "with-margin" : ""}`}>
        <div><h2>租赁服务器(PE)</h2><p className="page-desc">浏览和加入基岩版租赁服务器（共 {total.toLocaleString()} 个）</p></div>
        <input className="network-search" type="text" placeholder="搜索服务器..." value={query} onChange={(event) => setQuery(event.target.value)} />
      </section>
      {totalPages > 1 ? (
        <div className="bedrock-rental-pagination">
          <span>显示第 {start + 1} 至 {Math.min(start + 30, total)} 个，共 {total} 个服务器</span>
          <div>
            <button type="button" disabled={page === 1} onClick={() => setPage((current) => Math.max(current - 1, 1))}><ChevronLeft />上一页</button>
            {pageNumbers.map((pageNumber) => <button type="button" className={page === pageNumber ? "active" : ""} onClick={() => setPage(pageNumber)} key={pageNumber}>{pageNumber}</button>)}
            <button type="button" disabled={page === totalPages} onClick={() => setPage((current) => Math.min(current + 1, totalPages))}>下一页<ChevronRight /></button>
          </div>
        </div>
      ) : null}
      {loading ? <div className="bedrock-rental-loading">正在加载服务器…</div> : error ? <div className="server-list-error" role="alert">{error}<button className="btn-secondary btn-sm" type="button" onClick={() => { setLoading(true); setError(""); void gateway.send("pe_rental_games", { offset: (page - 1) * 30 }).catch((requestError) => { setError(requestError instanceof Error ? requestError.message : "无法加载服务器。"); setLoading(false); }); }}>重试</button></div> : (
        <div className="server-grid">
            {visibleServers.map((server) => {
              const id = text(server, "entity_id");
              const playerCount = number(server, "player_count");
              const capacity = number(server, "capacity");
              return (
                <button className="server-card" type="button" aria-label={`打开${text(server, "server_name")}`} onClick={() => openRental(server)} key={id}>
                  <ServerImage src={text(server, "image_url")} name={text(server, "server_name")} />
                  <div className="server-card-body">
                    <h2 className="server-card-name">{text(server, "server_name")}</h2>
                    <div className="server-card-online">在线：{playerCount}/{capacity} · {text(server, "mc_version")}</div>
                    <div className="server-card-id">{text(server, "name") || id}</div>
                    <div className="server-card-tags">
                      <StatusBadge status={number(server, "status")} />
                      {hasPassword(server) ? <span className="rental-lock"><LockKeyhole size={13} />需要密码</span> : null}
                      {Boolean(server.pvp) ? <span><Shield />PvP</span> : null}
                      {number(server, "min_level") > 0 ? <span>等级 {number(server, "min_level")}+</span> : null}
                      <span><Heart />{text(server, "like_num") || "0"}</span>
                    </div>
                    {text(server, "server_type") ? <div className="server-card-id">{text(server, "server_type")}</div> : null}
                  </div>
                </button>
              );
            })}
        </div>
      )}
      {!loading && !error && visibleServers.length === 0 ? <div className="bedrock-rental-empty"><Search /><h3>未找到服务器</h3><p>{query ? `没有与“${query}”匹配的服务器，请尝试调整搜索条件。` : "当前没有可用服务器。"}</p></div> : null}
    </div>
  );
}

export function ServerBrowserPage({ kind }: { kind: ServerBrowserKind }) {
  if (kind === "bedrock-rental") return <BedrockRentalBrowser />;
  return <CardServerBrowser kind={kind} />;
}
