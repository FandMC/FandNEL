import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth, useGateway, useToasts } from "../context/AppContext";
import { generateRandomNickname } from "../lib/randomNickname";
import { readGatewaySettings, writeGatewaySettings, type GatewaySettings } from "../lib/settingsStorage";
import type { GatewayMessage } from "../types";

export type JavaGameKind = "net_game" | "rental_game";
export type JavaGameDetails = Record<string, unknown>;

type Account = Record<string, unknown>;
type Role = Record<string, unknown>;

const debugAccounts: Account[] = [{ id: "debug-java-account", alias: "Debug Java Account" }];
const debugRoles: Role[] = [{ name: "Debug Java Role", delete_ts: 0, expire_time: 0 }];

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

function records(payload: unknown): Record<string, unknown>[] {
  const value = parsePayload(payload);
  return Array.isArray(value)
    ? value.filter((item): item is Record<string, unknown> => Boolean(item && typeof item === "object"))
    : [];
}

function value(item: Record<string, unknown> | null | undefined, key: string): string {
  const result = item?.[key];
  return result === undefined || result === null ? "" : String(result);
}

function firstVersion(details: JavaGameDetails): Record<string, unknown> {
  const versions = details.mc_version_list;
  return Array.isArray(versions) && versions[0] && typeof versions[0] === "object"
    ? versions[0] as Record<string, unknown>
    : {};
}

function accountLabel(account: Account): string {
  return value(account, "alias") || value(account, "id");
}

function roleUnavailable(role: Role, kind: JavaGameKind): boolean {
  return Number(role[kind === "rental_game" ? "delete_ts" : "expire_time"] ?? 0) !== 0;
}

function messagesAfter(messages: GatewayMessage[], lastHandled: GatewayMessage | null): GatewayMessage[] {
  if (!lastHandled) return messages;
  const index = messages.lastIndexOf(lastHandled);
  return messages.slice(index >= 0 ? index + 1 : Math.max(messages.length - 1, 0));
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange(checked: boolean): void }) {
  return (
    <div className="java-join-toggle-row">
      <span>{label}</span>
      <button type="button" role="switch" aria-checked={checked} aria-label={label} className={checked ? "active" : ""} onClick={() => onChange(!checked)}>
        <i />
      </button>
    </div>
  );
}

export function SelectMenu({
  title,
  items,
  selectedIndex,
  onChange,
  itemLabel,
  itemDisabled,
  action,
}: {
  title: string;
  items: Record<string, unknown>[];
  selectedIndex: number;
  onChange(index: number): void;
  itemLabel(item: Record<string, unknown>): string;
  itemDisabled?(item: Record<string, unknown>): boolean;
  action?: { label: string; onClick(): void };
}) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const selected = items[selectedIndex];

  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  return (
    <div className="java-join-select" ref={rootRef}>
      <button type="button" aria-haspopup="listbox" aria-expanded={open} onClick={() => setOpen((current) => !current)}>
        <span><small>{title}</small><strong>{selected ? itemLabel(selected) : "Select an option"}</strong></span>
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M6 9L12 15L18 9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </button>
      {open ? (
        <div className="java-join-options">
          <ul role="listbox" aria-label={title}>
            {items.map((item, index) => {
              const disabled = itemDisabled?.(item) ?? false;
              return (
                <li key={`${itemLabel(item)}-${index}`}>
                  <button type="button" role="option" aria-selected={index === selectedIndex} disabled={disabled} className={index === selectedIndex ? "selected" : ""} onClick={() => { onChange(index); setOpen(false); }}>
                    {itemLabel(item)}{disabled ? " (删除中)" : ""}
                  </button>
                </li>
              );
            })}
          </ul>
          {action ? <div><button type="button" onClick={() => { action.onClick(); setOpen(false); }}>{action.label}</button></div> : null}
        </div>
      ) : null}
    </div>
  );
}

export function CreateRoleDialog({ accountId, gameId, kind, onClose }: { accountId: string; gameId: string; kind: JavaGameKind; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const lastHandled = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);

  useEffect(() => {
    const pending = messagesAfter(gateway.messages, lastHandled.current);
    for (const message of pending) {
      if (message.type !== "create_role") continue;
      setSubmitting(false);
      onClose();
    }
    lastHandled.current = gateway.messages.at(-1) ?? null;
  }, [gateway.messages, notify, onClose]);

  const submit = async () => {
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await gateway.send("create_role", { id: accountId, name, game: gameId, type: kind });
    } catch (error) {
      setSubmitting(false);
      notify(error instanceof Error ? error.message : "Unable to create role.", "error");
    }
  };

  return (
    <div className="java-join-backdrop nested" role="presentation">
      <section className="java-join-modal" role="dialog" aria-modal="true" aria-label="Create Role" onMouseDown={(event) => event.stopPropagation()}>
        <header><h2>Create Role</h2><button type="button" aria-label="Close" onClick={onClose}><CloseIcon /></button></header>
        <div className="java-create-role-body">
          <label><span>Role Name</span><input type="text" placeholder="Role name" value={name} disabled={submitting} onChange={(event) => setName(event.target.value)} /></label>
        </div>
        <footer className="java-create-role-actions">
          <button type="button" onClick={() => setName(generateRandomNickname())}>Random Name</button>
          <button type="button" className="primary" disabled={!name.trim() || submitting} onClick={() => void submit()}>{submitting ? "Processing..." : "Request to LocalServer"}</button>
        </footer>
      </section>
    </div>
  );
}

function CloseIcon() {
  return <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor" /></svg>;
}

export function JavaJoinGameModal({
  kind,
  gameId,
  gameName,
  details,
  debugPreview,
  onClose,
}: {
  kind: JavaGameKind;
  gameId: string;
  gameName: string;
  details: JavaGameDetails;
  debugPreview: boolean;
  onClose(): void;
}) {
  const gateway = useGateway();
  const navigate = useNavigate();
  const { user, getAccessToken } = useAuth();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<Account[]>(debugPreview ? debugAccounts : []);
  const [roles, setRoles] = useState<Role[]>(debugPreview ? debugRoles : []);
  const [accountIndex, setAccountIndex] = useState(() => Number(sessionStorage.getItem("X-CURRENT-USER-INDEX") ?? "0"));
  const [roleIndex, setRoleIndex] = useState(0);
  const [loadingRoles, setLoadingRoles] = useState(!debugPreview);
  const [launching, setLaunching] = useState(false);
  const [joining, setJoining] = useState(false);
  const [createRoleOpen, setCreateRoleOpen] = useState(false);
  const [settings, setSettings] = useState<GatewaySettings>(readGatewaySettings());
  const handledMessageRef = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const joinRequestIdentifyRef = useRef<string | null>(null);
  const selectedAccount = accounts[accountIndex] ?? null;
  const selectedRole = roles[roleIndex] ?? null;
  const socks5 = settings.enableSocks5;

  const updateSettings = <Key extends keyof GatewaySettings>(key: Key, nextValue: GatewaySettings[Key]) => {
    setSettings((current) => {
      const next = { ...current, [key]: nextValue };
      void writeGatewaySettings(next).catch(() => {});
      return next;
    });
  };

  const requestRoles = useCallback(async (account: Account) => {
    setLoadingRoles(true);
    await gateway.send("get_roles", { id: value(account, "id"), game: gameId, type: kind });
  }, [gameId, gateway.send, kind]);

  useEffect(() => {
    document.body.style.overflow = "hidden";
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      if (createRoleOpen) setCreateRoleOpen(false);
      else onClose();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      document.body.style.overflow = "auto";
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [createRoleOpen, onClose]);

  useEffect(() => {
    if (debugPreview || gateway.status !== "connected") return;
    void gateway.send("get_accounts", "available").catch((error) => notify(error instanceof Error ? error.message : "Unable to load Java accounts.", "error"));
  }, [debugPreview, gateway.send, gateway.status, notify]);

  useEffect(() => {
    const previous = handledMessageRef.current;
    const previousIndex = previous ? gateway.messages.lastIndexOf(previous) : -1;
    const pending = gateway.messages.slice(previous ? Math.max(previousIndex + 1, 0) : 0);
    for (const message of pending) {
      if (message.type === "get_accounts") {
        const nextAccounts = records(message.payload);
        const nextIndex = Math.min(accountIndex, Math.max(nextAccounts.length - 1, 0));
        setAccounts(nextAccounts);
        setAccountIndex(nextIndex);
        if (nextAccounts[nextIndex]) void requestRoles(nextAccounts[nextIndex]);
        else setLoadingRoles(false);
      } else if (message.type === "get_roles") {
        const nextRoles = records(message.payload);
        setRoles(nextRoles);
        const firstAvailable = nextRoles.findIndex((role) => !roleUnavailable(role, kind));
        setRoleIndex(firstAvailable >= 0 ? firstAvailable : 0);
        setLoadingRoles(false);
      } else if (message.type === "join_game/success") {
        joinRequestIdentifyRef.current = null;
        setJoining(false);
        onClose();
        navigate(`/user-center/launchers/configuration?id=${encodeURIComponent(String(parsePayload(message.payload)))}`);
      } else if (message.type === "error_notification" && message.identify === joinRequestIdentifyRef.current) {
        joinRequestIdentifyRef.current = null;
        setJoining(false);
      }
    }
    handledMessageRef.current = gateway.messages.at(-1) ?? null;
  }, [accountIndex, gateway.messages, kind, navigate, onClose, requestRoles]);

  const version = useMemo(() => {
    if (kind === "rental_game") return { id: 0x45b6bb0c, name: value(details, "mc_version") };
    const item = firstVersion(details);
    return { id: Number(item.mcversionid ?? 0), name: value(item, "name") };
  }, [details, kind]);
  const address = kind === "rental_game" ? value(details, "server_ip") : value(details, "server_address");
  const port = Number(details.server_port ?? 0);

  const socks5Payload = () => {
    const proxyParts = settings.socks5Address.split(":");
    const host = proxyParts[0] ?? "127.0.0.1";
    return {
      enabled: socks5,
      address: host,
      port: proxyParts.length >= 2 ? Number(proxyParts[1]) : 1080,
      username: settings.socks5Username || null,
      password: settings.socks5Password || null,
    };
  };

  const launchGame = async () => {
    if (!selectedAccount || !selectedRole) return;
    setLaunching(true);
    let accessToken: string;
    try {
      accessToken = await getAccessToken();
    } catch {
      setLaunching(false);
      navigate("/gateway");
      return;
    }
    try {
      await gateway.send("launch_game", {
        user_id: value(selectedAccount, "id"),
        game_name: gameName,
        game_id: gameId,
        role_name: value(selectedRole, "name"),
        client_type: 1,
        game_type: kind === "rental_game" ? 8 : 2,
        game_version_id: version.id,
        game_version: version.name,
        server_ip: address,
        server_port: port,
        access_token: accessToken,
        max_game_memory: Number(settings.jvmMaxMemory),
        load_core_mods: settings.loadCoreModules,
      });
      onClose();
    } catch (error) {
      notify(error instanceof Error ? error.message : "Unable to launch Java game.", "error");
    } finally {
      setLaunching(false);
    }
  };

  const launchInterceptor = async () => {
    if (!selectedAccount || !selectedRole) return;
    setJoining(true);
    let accessToken: string;
    try {
      accessToken = await getAccessToken();
    } catch {
      setJoining(false);
      navigate("/gateway");
      return;
    }
    try {
      joinRequestIdentifyRef.current = await gateway.send("join_game", {
        id: value(selectedAccount, "id"),
        name: gameName,
        game: gameId,
        role: value(selectedRole, "name"),
        vid: version.id,
        version: version.name,
        ip: address,
        port,
        nid: user?.id ?? "",
        token: accessToken,
        socks5: socks5Payload(),
      });
    } catch (error) {
      joinRequestIdentifyRef.current = null;
      setJoining(false);
      notify(error instanceof Error ? error.message : "Unable to launch Java interceptor.", "error");
    }
  };

  const chooseAccount = (index: number) => {
    setAccountIndex(index);
    setRoleIndex(0);
    sessionStorage.setItem("X-CURRENT-USER-INDEX", String(index));
    sessionStorage.setItem("X-CURRENT-USER-ID", value(accounts[index], "id"));
    if (!debugPreview && accounts[index]) void requestRoles(accounts[index]).catch((error) => notify(error instanceof Error ? error.message : "Unable to load roles.", "error"));
  };

  return (
    <div className="java-join-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="java-join-modal" role="dialog" aria-modal="true" aria-label="Join Game" onMouseDown={(event) => event.stopPropagation()}>
        <header><h2>Join Game</h2><button type="button" aria-label="Close" onClick={onClose}><CloseIcon /></button></header>
        <div className="java-join-body">
          <SelectMenu title="Account" items={accounts} selectedIndex={accountIndex} onChange={chooseAccount} itemLabel={accountLabel} />
          <SelectMenu
            title="Role"
            items={roles}
            selectedIndex={roleIndex}
            onChange={setRoleIndex}
            itemLabel={(role) => value(role, "name")}
            itemDisabled={(role) => roleUnavailable(role, kind)}
            action={{ label: "Create New Role", onClick: () => setCreateRoleOpen(true) }}
          />
          <Toggle label="Socks5" checked={socks5} onChange={(checked) => updateSettings("enableSocks5", checked)} />
          {socks5 ? (
            <div className="java-join-proxy">
              <p className="java-join-proxy-hint">Use the manually configured SOCKS5 address and credentials.</p>
            </div>
          ) : null}
        </div>
        <footer className="java-join-actions">
          <button type="button" disabled={launching} onClick={() => void launchGame()}>{launching ? "Processing..." : "Launch Game"}</button>
          <button type="button" className="primary" disabled={joining || loadingRoles} onClick={() => void launchInterceptor()}>{joining ? "Processing..." : "Launch Interceptor"}</button>
        </footer>
      </section>
      {createRoleOpen ? <CreateRoleDialog accountId={value(selectedAccount, "id")} gameId={gameId} kind={kind} onClose={() => setCreateRoleOpen(false)} /> : null}
    </div>
  );
}
