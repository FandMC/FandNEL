import {
  ChevronLeft,
  ChevronRight,
  Heart,
  LockKeyhole,
  Search,
  Shield,
  Users,
} from "lucide-react";
import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useGateway, useToasts } from "../context/AppContext";
import type { GatewayMessage } from "../types";

type Resource = Record<string, unknown>;
export type ServerBrowserKind = "java-server" | "java-rental" | "bedrock-server" | "bedrock-rental" | "bedrock-realms";

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

const cardDefinitions: Record<Exclude<ServerBrowserKind, "bedrock-rental" | "bedrock-realms">, CardBrowserDefinition> = {
  "java-server": {
    title: "服务器",
    description: "浏览可用服务器",
    requestType: "net_games",
    searchType: "net_games_search",
    storageKey: "servers-store",
    rental: false,
    bedrock: false,
  },
  "java-rental": {
    title: "租赁服",
    description: "浏览可用租赁服",
    requestType: "rental_games",
    searchType: "rental_games_search",
    storageKey: "rental-servers-store",
    rental: true,
    bedrock: false,
  },
  "bedrock-server": {
    title: "基岩版服务器",
    description: "浏览可用的基岩版服务器",
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
  return (
    <div className="server-card-media">
      {src ? <img src={src} alt={`${name} 服务器图片`} /> : <div className="server-image-placeholder" />}
    </div>
  );
}

function PasswordDialog({
  name,
  errorMessage = "",
  submitting = false,
  onClose,
  onSubmit,
}: {
  name: string;
  errorMessage?: string;
  submitting?: boolean;
  onClose(): void;
  onSubmit(password: string): void;
}) {
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const submit = () => {
    if (!password.trim()) {
      setError("请输入密码");
      return;
    }
    onSubmit(password);
  };

  return (
    <div className="java-join-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="java-join-modal" role="dialog" aria-modal="true" aria-label="输入服务器密码" onMouseDown={(event) => event.stopPropagation()}>
        <header>
          <h2>输入服务器密码</h2>
          <button type="button" aria-label="关闭" onClick={onClose}>
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor" /></svg>
          </button>
        </header>
        <div className="java-password-body">
          <input autoFocus type="password" aria-label={`${name} 的密码`} placeholder="请输入密码" value={password} onChange={(event) => { setPassword(event.target.value); setError(""); }} onKeyDown={(event) => { if (event.key === "Enter") submit(); }} />
          {error || errorMessage ? <p>{error || errorMessage}</p> : null}
          <button type="button" disabled={submitting} onClick={submit}>{submitting ? "正在验证…" : "提交"}</button>
        </div>
      </section>
    </div>
  );
}

function CardServerBrowser({ kind }: { kind: Exclude<ServerBrowserKind, "bedrock-rental" | "bedrock-realms"> }) {
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
  const [passwordTarget, setPasswordTarget] = useState<{ id: string; name: string } | null>(null);
  const requestInFlight = useRef(false);
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
    const onScroll = () => {
      if (window.innerHeight + window.scrollY >= document.body.offsetHeight - 200) void loadNextPage();
    };
    window.addEventListener("scroll", onScroll);
    return () => window.removeEventListener("scroll", onScroll);
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
    if (definition.rental && text(server, "has_pwd") === "1") {
      setPasswordTarget({ id, name });
      return;
    }
    const route = definition.bedrock ? "/user-center/bedrock/launch" : definition.rental ? "/user-center/rentals/details" : "/user-center/servers/details";
    navigate(`${route}?id=${encodeURIComponent(id)}&name=${encodeURIComponent(name)}`);
  };

  return (
    <div className="workspace-page server-browser-page">
      <section className="server-browser-header">
        <div><h1>{definition.title}</h1><p>{definition.description}</p></div>
        <label className="server-search">
          <Search />
          <input type="text" placeholder="搜索服务器…" value={query} onKeyDown={(event) => { if (event.key === "Enter") void search(); }} onChange={(event) => { setQuery(event.target.value); setSearchResults(null); }} />
        </label>
      </section>
      <div className="server-card-grid">
        {servers.map((server, index) => {
          const id = text(server, definition.bedrock ? "item_id" : "entity_id");
          const name = text(server, definition.bedrock ? "res_name" : definition.rental ? "server_name" : "name");
          const description = text(server, definition.bedrock ? "brief" : definition.rental ? "name" : "brief_summary");
          const online = text(server, definition.bedrock ? "online_num" : definition.rental ? "player_count" : "online_count");
          const image = text(server, definition.rental ? "image_url" : "title_image_url");
          const hasPassword = definition.rental && text(server, "has_pwd") === "1";
          return (
            <button className="server-card" type="button" onClick={() => openServer(server)} key={id || `server-${index}`}>
              <ServerImage src={image} name={name} />
              <div className="server-card-body">
                <div className="server-card-heading"><h2>{name}</h2><span>{online} 人在线</span></div>
                {definition.rental ? <span className={`server-access ${hasPassword ? "protected" : "open"}`}>{hasPassword ? "需要密码" : "无密码"}</span> : null}
                <p>{description}</p>
              </div>
            </button>
          );
        })}
      </div>
      <div className="server-list-footer">
        {error ? <div className="server-list-error" role="alert">{error}<button type="button" onClick={reload}>重试</button></div> : null}
        {loading ? <div>正在加载更多服务器…</div> : null}
        {!error && !hasMore && !loading ? <><div>已显示全部服务器</div><button type="button" onClick={reload}>重新加载</button></> : null}
      </div>
      {passwordTarget ? (
        <PasswordDialog
          name={passwordTarget.name}
          onClose={() => setPasswordTarget(null)}
          onSubmit={(password) => navigate(`/user-center/rentals/details?id=${encodeURIComponent(passwordTarget.id)}&name=${encodeURIComponent(passwordTarget.name)}&password=${encodeURIComponent(password)}`)}
        />
      ) : null}
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

interface RealmServer {
  sid: string;
  name: string;
  user_name: string;
  user_id: string;
  status: number;
  is_mine: boolean;
  online_count: number;
  capacity: number;
}

function realmListFrom(payload: unknown): RealmServer[] {
  const value = payloadRecord(payload);
  const mine = resourceList(value.mine && typeof value.mine === "object" ? (value.mine as Record<string, unknown>).entities : []);
  const others = resourceList(value.others && typeof value.others === "object" ? (value.others as Record<string, unknown>).entities : []);
  return [
    ...mine.map((item) => ({ ...item, is_mine: true })),
    ...others.map((item) => ({ ...item, is_mine: false })),
  ].map((item) => ({
    sid: text(item, "sid"),
    name: text(item, "name"),
    user_name: text(item, "user_name"),
    user_id: text(item, "user_id"),
    status: number(item, "status"),
    is_mine: Boolean(item.is_mine),
    online_count: number(item, "online_count") || number(item, "player_count"),
    capacity: number(item, "capacity") || number(item, "current_capacity"),
  }));
}

const REALM_ORDER_KEY = "bedrock-realms-order";

const REALM_MODS_KEY = "bedrock-realms-mods";

function persistRealmModItemIds(sid: string, itemIds: string[]) {
  try {
    const all = JSON.parse(localStorage.getItem(REALM_MODS_KEY) ?? "{}") as Record<string, string[]>;
    all[sid] = itemIds;
    localStorage.setItem(REALM_MODS_KEY, JSON.stringify(all));
  } catch {
    // Ignore storage failures.
  }
}

function BedrockRealmsBrowser() {
  const navigate = useNavigate();
  const gateway = useGateway();
  const { notify } = useToasts();
  const [userId, setUserId] = useState("");
  const [realms, setRealms] = useState<RealmServer[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedSid, setSelectedSid] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [entering, setEntering] = useState(false);
  const lastHandled = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const userIdRef = useRef(userId);
  const selectedRef = useRef<RealmServer | null>(null);
  const pendingDetailRef = useRef<Set<string>>(new Set());

  useEffect(() => { userIdRef.current = userId; }, [userId]);

  const applyRealmOrder = useCallback((list: RealmServer[]): RealmServer[] => {
    try {
      const order = JSON.parse(localStorage.getItem(REALM_ORDER_KEY) ?? "null") as string[] | null;
      if (!order) return list;
      const bySid = new Map(list.map((realm) => [realm.sid, realm]));
      const ordered: RealmServer[] = [];
      for (const sid of order) {
        const realm = bySid.get(sid);
        if (realm) { ordered.push(realm); bySid.delete(sid); }
      }
      for (const realm of bySid.values()) ordered.push(realm);
      return ordered;
    } catch {
      return list;
    }
  }, []);

  const persistRealmOrder = useCallback((list: RealmServer[]) => {
    localStorage.setItem(REALM_ORDER_KEY, JSON.stringify(list.map((realm) => realm.sid)));
  }, []);

  const loadRealms = useCallback(() => {
    if (gateway.status !== "connected" || !userIdRef.current) return;
    setLoading(true);
    setError("");
    void gateway.send("realm_list", { id: userIdRef.current }).catch((requestError) => {
      setError(requestError instanceof Error ? requestError.message : "无法加载领域。");
      setLoading(false);
    });
  }, [gateway.send, gateway.status]);

  const requestDetails = useCallback((list: RealmServer[]) => {
    for (const realm of list) {
      if (pendingDetailRef.current.has(realm.sid)) continue;
      pendingDetailRef.current.add(realm.sid);
      void gateway.send("realm_detail", { id: userIdRef.current, sid: realm.sid }).catch(() => {});
    }
  }, [gateway.send]);

  useEffect(() => {
    if (gateway.status !== "connected") return;
    void gateway.send("get_accounts", "available-for-mobile").catch(() => {});
  }, [gateway.send, gateway.status]);

  useEffect(() => {
    if (!userId) return;
    loadRealms();
  }, [loadRealms, userId]);

  useEffect(() => {
    const pending = nextMessages(gateway.messages, lastHandled.current);
    for (const message of pending) {
      if (message.type === "get_accounts") {
        const value = parsePayload(message.payload);
        let accounts: unknown[] = [];
        if (Array.isArray(value)) accounts = value;
        else if (value && typeof value === "object") {
          const nested = (value as Record<string, unknown>).accounts;
          if (Array.isArray(nested)) accounts = nested;
          else if (Array.isArray((value as Record<string, unknown>).entities)) accounts = (value as Record<string, unknown>).entities as unknown[];
        }
        if (accounts.length > 0) {
          const account = accounts[0];
          if (typeof account === "string") {
            setUserId(account);
          } else if (account && typeof account === "object") {
            for (const key of ["id", "user_id", "entity_id", "username", "name", "email"]) {
              const idValue = (account as Record<string, unknown>)[key];
              if (idValue !== undefined && idValue !== null && idValue !== "") {
                setUserId(String(idValue));
                break;
              }
            }
          }
        }
      } else if (message.type === "realm_list") {
        const payload = payloadRecord(message.payload);
        const responseError = payloadError(message.payload);
        if (responseError) {
          setError(responseError);
        } else if (Number(payload.code) === 0 || payload.code === undefined) {
          setRealms((current) => applyRealmOrder(realmListFrom(message.payload)));
          requestDetails(realmListFrom(message.payload));
          setError("");
        }
        setLoading(false);
      } else if (message.type === "realm_detail") {
        const payload = payloadRecord(message.payload);
        const detail = payload.entity && typeof payload.entity === "object"
          ? payload.entity as Record<string, unknown>
          : payload as Record<string, unknown>;
        const sid = String(detail.sid ?? "");
        if (sid) {
          pendingDetailRef.current.delete(sid);
          setRealms((current) => current.map((realm) => {
            if (realm.sid !== sid) return realm;
            return {
              ...realm,
              status: number(detail, "status") || realm.status,
              online_count: number(detail, "online_count") || realm.online_count,
              capacity: number(detail, "current_capacity") || number(detail, "capacity") || realm.capacity,
              name: text(detail, "name") || realm.name,
              user_name: text(detail, "user_name") || realm.user_name,
            };
          }));
        }
      } else if (message.type === "realm_add") {
        const payload = payloadRecord(message.payload);
        if (Number(payload.code) === 0) {
          notify("添加成功", "success");
          setAddOpen(false);
          loadRealms();
        } else {
          notify(String(payload.message ?? "添加失败"), "error");
        }
      } else if (message.type === "realm_remove") {
        const payload = payloadRecord(message.payload);
        if (Number(payload.code) === 0) {
          notify("退出成功", "success");
          setSelectedSid(null);
          selectedRef.current = null;
          loadRealms();
        } else {
          notify(String(payload.message ?? "退出失败"), "error");
        }
      } else if (message.type === "realm_enter") {
        const payload = payloadRecord(message.payload);
        if (Number(payload.code) === 0 && payload.entity && typeof payload.entity === "object") {
          const entity = payload.entity as Record<string, unknown>;
          const host = String(entity.server_host ?? "");
          const port = Number(entity.server_port ?? 0);
          if (selectedRef.current && host) {
            const itemIds: string[] = [];
            const activeComponents = entity.active_components;
            if (activeComponents && typeof activeComponents === "object") {
              for (const value of Object.values(activeComponents as Record<string, unknown>)) {
                if (Array.isArray(value)) {
                  for (const id of value) {
                    const str = String(id);
                    if (str && !itemIds.includes(str)) itemIds.push(str);
                  }
                }
              }
            }
            persistRealmModItemIds(selectedRef.current.sid, itemIds);
            navigate(`/user-center/bedrock-realms/launch?id=${encodeURIComponent(selectedRef.current.sid)}&name=${encodeURIComponent(selectedRef.current.name)}&realm=1&host=${encodeURIComponent(host)}&port=${port}`);
          }
        } else {
          notify(String(payload.message ?? "进入失败"), "error");
        }
        setEntering(false);
      }
    }
    lastHandled.current = gateway.messages.at(-1) ?? null;
  }, [applyRealmOrder, gateway.messages, loadRealms, navigate, notify, requestDetails]);

  const openRealm = (realm: RealmServer) => {
    setSelectedSid(realm.sid);
    selectedRef.current = realm;
    setEntering(true);
    void gateway.send("realm_enter", { id: userId, sid: realm.sid }).catch(() => setEntering(false));
  };

  const selectRealm = (realm: RealmServer) => {
    setSelectedSid(realm.sid);
    selectedRef.current = realm;
  };

  const removeRealm = () => {
    if (!selectedSid) return;
    void gateway.send("realm_remove", { id: userId, sid: selectedSid }).catch(() => {});
  };

  const moveCard = (from: number, to: number) => {
    if (from === to) return;
    setRealms((current) => {
      const next = [...current];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      persistRealmOrder(next);
      return next;
    });
  };

  const dragStateRef = useRef<{ pointerId: number; fromIndex: number; startX: number; startY: number; moved: boolean } | null>(null);
  const suppressClickRef = useRef(false);
  const cardRefs = useRef(new Map<string, HTMLDivElement>());
  const animatingRef = useRef(new Set<string>());
  const dragOffsetRef = useRef({ x: 0, y: 0 });
  const dragIndexRef = useRef<number | null>(null);
  const dragSidRef = useRef<string | null>(null);
  const realmsRef = useRef(realms);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOffset, setDragOffset] = useState<{ x: number; y: number } | null>(null);

  useEffect(() => { realmsRef.current = realms; }, [realms]);

  const recordPositions = () => {
    const map = new Map<string, { left: number; top: number }>();
    cardRefs.current.forEach((el, sid) => {
      const rect = el.getBoundingClientRect();
      map.set(sid, { left: rect.left, top: rect.top });
    });
    return map;
  };

  const animateShift = (prev: Map<string, { left: number; top: number }>) => {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      cardRefs.current.forEach((el, sid) => {
        if (sid === dragSidRef.current) return;
        const old = prev.get(sid);
        if (!old) return;
        const rect = el.getBoundingClientRect();
        const dx = old.left - rect.left;
        const dy = old.top - rect.top;
        if (dx === 0 && dy === 0) return;
        const anim = el.animate(
          [
            { transform: `translate(${dx}px, ${dy}px)` },
            { transform: "translate(0px, 0px)" },
          ],
          { duration: 300, easing: "cubic-bezier(0.2, 0.8, 0.2, 1)" },
        );
        animatingRef.current.add(sid);
        anim.finished.then(() => {
          animatingRef.current.delete(sid);
        }).catch(() => {
          animatingRef.current.delete(sid);
        });
      });
    }));
  };

  const beginDrag = (event: ReactPointerEvent<HTMLElement>, index: number) => {
    if (event.button !== 0) return;
    dragStateRef.current = { pointerId: event.pointerId, fromIndex: index, startX: event.clientX, startY: event.clientY, moved: false };

    const onMove = (moveEvent: PointerEvent) => {
      const drag = dragStateRef.current;
      if (!drag || drag.pointerId !== moveEvent.pointerId) return;
      if (!drag.moved && Math.abs(moveEvent.clientX - drag.startX) + Math.abs(moveEvent.clientY - drag.startY) > 6) {
        drag.moved = true;
        dragIndexRef.current = drag.fromIndex;
        dragSidRef.current = realmsRef.current[drag.fromIndex]?.sid ?? null;
        setDragIndex(drag.fromIndex);
      }
      if (!drag.moved) return;

      const draggedEl = dragSidRef.current ? cardRefs.current.get(dragSidRef.current) : undefined;
      if (draggedEl) {
        const rect = draggedEl.getBoundingClientRect();
        const offset = {
          x: moveEvent.clientX - (rect.left - dragOffsetRef.current.x) - rect.width / 2,
          y: moveEvent.clientY - (rect.top - dragOffsetRef.current.y) - rect.height / 2,
        };
        dragOffsetRef.current = offset;
        setDragOffset(offset);
      }

      const hitEls = document.elementsFromPoint(moveEvent.clientX, moveEvent.clientY);
      let card: HTMLElement | null = null;
      for (const el of hitEls) {
        const c = el.closest<HTMLElement>("[data-realm-card]");
        if (!c) continue;
        const sid = c.dataset.realmSid;
        if (sid === dragSidRef.current) continue;
        if (sid && animatingRef.current.has(sid)) continue;
        card = c;
        break;
      }
      if (!card) return;
      const targetSid = card.dataset.realmSid;
      const targetIndex = realmsRef.current.findIndex((realm) => realm.sid === targetSid);
      const from = dragIndexRef.current;
      if (from === null || targetIndex === -1 || targetIndex === from) return;

      const prev = recordPositions();
      const next = [...realmsRef.current];
      const [moved] = next.splice(from, 1);
      next.splice(targetIndex, 0, moved);
      realmsRef.current = next;
      dragIndexRef.current = targetIndex;
      setDragIndex(targetIndex);
      setRealms(next);
      persistRealmOrder(next);
      animateShift(prev);
    };

    const onUp = (upEvent: PointerEvent) => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
      const drag = dragStateRef.current;
      if (!drag || drag.pointerId !== upEvent.pointerId) return;
      if (drag.moved) {
        suppressClickRef.current = true;
        const sid = dragSidRef.current;
        const offset = { ...dragOffsetRef.current };
        const el = sid ? cardRefs.current.get(sid) : undefined;
        if (el && (offset.x !== 0 || offset.y !== 0)) {
          el.style.transition = "none";
          const anim = el.animate(
            [
              { transform: `translate(${offset.x}px, ${offset.y}px)` },
              { transform: "translate(0px, 0px)" },
            ],
            { duration: 220, easing: "cubic-bezier(0.2, 0.8, 0.2, 1)" },
          );
          if (sid) {
            animatingRef.current.add(sid);
            anim.finished.then(() => {
              animatingRef.current.delete(sid);
              el.style.transition = "";
            }).catch(() => {
              animatingRef.current.delete(sid);
              el.style.transition = "";
            });
          }
        }
        dragStateRef.current = null;
        dragIndexRef.current = null;
        dragSidRef.current = null;
        dragOffsetRef.current = { x: 0, y: 0 };
        setDragIndex(null);
        setDragOffset(null);
      } else {
        dragStateRef.current = null;
      }
      window.setTimeout(() => { suppressClickRef.current = false; }, 50);
    };

    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
  };

  const selected = realms.find((realm) => realm.sid === selectedSid) ?? null;

  return (
    <div className="workspace-page bedrock-realms-browser" onMouseDown={(event) => {
      if (event.target === event.currentTarget) {
        setSelectedSid(null);
        selectedRef.current = null;
      }
    }}>
      <section className="server-browser-header">
        <div><h1>基岩版领域</h1><p>浏览可用的基岩版领域</p></div>
        <div className="bedrock-realms-actions">
          <button type="button" className="bedrock-realms-add" onClick={() => setAddOpen(true)}>添加</button>
          <button type="button" className="bedrock-realms-exit" disabled={!selected || selected.is_mine || entering} onClick={removeRealm}>{entering ? "正在进入…" : "退出"}</button>
        </div>
      </section>
      {loading ? <div className="bedrock-rental-loading">正在加载领域…</div> : error ? <div className="bedrock-rental-empty" role="alert"><h3>无法加载领域</h3><p>{error}</p><button type="button" onClick={loadRealms}>重试</button></div> : realms.length > 0 ? (
        <div className={`bedrock-realms-grid${dragIndex !== null ? " dragging" : ""}`}>
          {realms.map((realm, index) => (
            <div
              key={realm.sid}
              ref={(el) => { if (el) cardRefs.current.set(realm.sid, el); else cardRefs.current.delete(realm.sid); }}
              data-realm-card
              data-realm-sid={realm.sid}
              role="button"
              tabIndex={0}
              onPointerDown={(event) => beginDrag(event, index)}
              onClick={() => {
                if (suppressClickRef.current) return;
                selectRealm(realm);
              }}
              onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { selectRealm(realm); } }}
              style={dragIndex === index && dragOffset ? { transform: `translate(${dragOffset.x}px, ${dragOffset.y}px)`, zIndex: 10 } : undefined}
              className={`bedrock-realm-card ${selectedSid === realm.sid ? "selected" : ""} ${dragIndex === index ? "dragging" : ""}`}
            >
              <div className="bedrock-realm-card-body">
                <div className="bedrock-realm-card-heading">
                  <strong>{realm.name || "未命名领域"}</strong>
                  <span className={realm.is_mine ? "mine" : (realm.status === 1 ? "online" : "offline")}>{realm.is_mine ? "我的" : (realm.status === 1 ? "在线" : "离线")}</span>
                </div>
                <small className="bedrock-realm-card-owner">服主：{realm.user_name || "未知"}</small>
                <div className="bedrock-realm-card-players">
                  <span><Users />{realm.online_count}/{realm.capacity || "?"}</span>
                  <i><b style={{ width: `${realm.capacity > 0 ? Math.min((realm.online_count / realm.capacity) * 100, 100) : 0}%` }} /></i>
                </div>
              </div>
              <button
                type="button"
                className="bedrock-realm-open-btn"
                aria-label={`进入${realm.name || "领域"}`}
                onClick={(event) => { event.stopPropagation(); openRealm(realm); }}
              >
                <ChevronRight />
              </button>
            </div>
          ))}
        </div>
      ) : (
        <div className="bedrock-rental-empty"><Search /><h3>尚未加入领域</h3><p>点击“添加”，通过邀请链接或邀请码加入领域。</p></div>
      )}
      {addOpen ? (
        <RealmInviteDialog
          onClose={() => setAddOpen(false)}
          onSubmit={(code) => { setAddOpen(false); void gateway.send("realm_add", { id: userId, code }).catch(() => {}); }}
        />
      ) : null}
    </div>
  );
}

function RealmInviteDialog({ onClose, onSubmit }: { onClose(): void; onSubmit(code: string): void }) {
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const submit = () => {
    if (!code.trim()) {
      setError("请输入邀请链接或邀请码");
      return;
    }
    const trimmed = code.trim();
    const match = trimmed.match(/[?&]code=([^&]+)/);
    onSubmit(match ? decodeURIComponent(match[1]) : trimmed);
  };

  return (
    <div className="java-join-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="java-join-modal" role="dialog" aria-modal="true" aria-label="加入领域" onMouseDown={(event) => event.stopPropagation()}>
        <header>
          <h2>加入领域</h2>
          <button type="button" aria-label="关闭" onClick={onClose}>
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor" /></svg>
          </button>
        </header>
        <div className="java-password-body">
          <input autoFocus aria-label="邀请链接或邀请码" placeholder="请输入邀请链接或邀请码" value={code} onChange={(event) => { setCode(event.target.value); setError(""); }} onKeyDown={(event) => { if (event.key === "Enter") submit(); }} />
          {error ? <p>{error}</p> : null}
          <button type="button" onClick={submit}>加入</button>
        </div>
      </section>
    </div>
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
  const [passwordTarget, setPasswordTarget] = useState<{ id: string; name: string } | null>(null);
  const [passwordError, setPasswordError] = useState("");
  const [verifyingPassword, setVerifyingPassword] = useState(false);
  const lastHandled = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const passwordRequestRef = useRef<{ identify: string; password: string; target: { id: string; name: string } } | null>(null);
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
      } else if (passwordRequestRef.current && message.identify === passwordRequestRef.current.identify) {
        if (message.type === "g79_rental_game_address") {
          const request = passwordRequestRef.current;
          const address = payloadRecord(message.payload);
          const host = String(address.mcserver_host ?? "");
          const port = Number(address.mcserver_port ?? 0);
          const userId = String(address.user_id ?? "");
          if (!host || !Number.isInteger(port) || port <= 0 || port > 65535) {
            passwordRequestRef.current = null;
            setVerifyingPassword(false);
            setPasswordError("服务器地址无效，请重试");
            continue;
          }
          passwordRequestRef.current = null;
          setVerifyingPassword(false);
          navigate(`/user-center/rentals-for-bedrock/launch?id=${encodeURIComponent(request.target.id)}&name=${encodeURIComponent(request.target.name)}&password=${encodeURIComponent(request.password)}&host=${encodeURIComponent(host)}&port=${port}&user_id=${encodeURIComponent(userId)}`);
        } else if (["g79_rental_game_password", "error_notification", "error_notification_back", "error_notification_rt"].includes(message.type)) {
          passwordRequestRef.current = null;
          setVerifyingPassword(false);
          setPasswordError(typeof message.payload === "string" && message.payload ? message.payload : "密码错误");
        }
      }
    }
    lastHandled.current = gateway.messages.at(-1) ?? null;
  }, [gateway.messages, navigate]);

  const pageNumbers = Array.from({ length: Math.min(totalPages, 7) }, (_, index) => {
    if (totalPages <= 7 || page <= 4) return index + 1;
    if (page >= totalPages - 3) return totalPages - 6 + index;
    return page - 3 + index;
  });

  const openRental = (server: Resource) => {
    const id = text(server, "entity_id");
    const name = text(server, "server_name");
    if (hasPassword(server)) {
      setPasswordError("");
      setPasswordTarget({ id, name });
      return;
    }
    navigate(`/user-center/rentals-for-bedrock/launch?id=${encodeURIComponent(id)}&name=${encodeURIComponent(name)}`);
  };

  return (
    <div className="workspace-page bedrock-rental-browser">
      <section className={`server-browser-header ${totalPages < 1 ? "with-margin" : ""}`}>
        <div><h1>基岩版租赁服</h1><p>浏览可用的基岩版租赁服（共 {total.toLocaleString()} 个）</p></div>
        <label className="server-search"><Search /><input type="text" placeholder="搜索服务器…" value={query} onKeyDown={(event) => { if (event.key === "Enter") setLoading(true); }} onChange={(event) => setQuery(event.target.value)} /></label>
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
      {loading ? <div className="bedrock-rental-loading">正在加载服务器…</div> : error ? <div className="server-list-error" role="alert">{error}<button type="button" onClick={() => { setLoading(true); setError(""); void gateway.send("pe_rental_games", { offset: (page - 1) * 30 }).catch((requestError) => { setError(requestError instanceof Error ? requestError.message : "无法加载服务器。"); setLoading(false); }); }}>重试</button></div> : (
        <div className="bedrock-rental-table-wrap">
          <div className="bedrock-rental-table" role="table" aria-label="基岩版租赁服">
            <div className="bedrock-rental-row header" role="row"><span>服务器</span><span>玩家</span><span>版本</span><span>点赞</span><span>状态</span><span>操作</span></div>
            {visibleServers.map((server) => {
              const id = text(server, "entity_id");
              const playerCount = number(server, "player_count");
              const capacity = number(server, "capacity");
              return (
                <div className="bedrock-rental-row" role="row" key={id}>
                   <div className="bedrock-rental-name"><strong>{text(server, "server_name")}</strong><small>编号：{text(server, "name")}{hasPassword(server) ? <LockKeyhole aria-label="需要密码" /> : null}{Boolean(server.pvp) ? <Shield aria-label="已启用玩家对战" /> : null}{number(server, "min_level") > 0 ? <b>等级 {number(server, "min_level")}+</b> : null}</small></div>
                  <div className="bedrock-rental-players"><span><Users />{playerCount}/{capacity}</span><i><b style={{ width: `${capacity > 0 ? Math.min((playerCount / capacity) * 100, 100) : 0}%` }} /></i></div>
                   <div><strong>{text(server, "mc_version")}</strong><small>{text(server, "server_type")}</small></div>
                   <div className="bedrock-rental-likes"><Heart />{text(server, "like_num")}</div>
                   <div><StatusBadge status={number(server, "status")} /></div>
                    <button className="bedrock-rental-open" type="button" aria-label={`打开${text(server, "server_name")}`} onClick={() => openRental(server)}><ChevronRight /></button>
                </div>
              );
            })}
          </div>
        </div>
      )}
      {!loading && !error && visibleServers.length === 0 ? <div className="bedrock-rental-empty"><Search /><h3>未找到服务器</h3><p>{query ? `没有与“${query}”匹配的服务器，请尝试调整搜索条件。` : "当前没有可用服务器。"}</p></div> : null}
      {passwordTarget ? (
        <PasswordDialog
          name={passwordTarget.name}
          errorMessage={passwordError}
          submitting={verifyingPassword}
          onClose={() => { passwordRequestRef.current = null; setVerifyingPassword(false); setPasswordTarget(null); }}
          onSubmit={(password) => {
            setPasswordError("");
            setVerifyingPassword(true);
            const target = passwordTarget;
            void gateway.send("g79_rental_game_address", { game: target.id, password }).then((identify) => {
              passwordRequestRef.current = { identify, password, target };
            }).catch((error) => {
              setVerifyingPassword(false);
              setPasswordError(error instanceof Error ? error.message : "密码验证失败");
            });
          }}
        />
      ) : null}
    </div>
  );
}

export function ServerBrowserPage({ kind }: { kind: ServerBrowserKind }) {
  if (kind === "bedrock-rental") return <BedrockRentalBrowser />;
  if (kind === "bedrock-realms") return <BedrockRealmsBrowser />;
  return <CardServerBrowser kind={kind} />;
}
