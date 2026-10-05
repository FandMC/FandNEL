import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useNavigate } from "react-router-dom";
import { useGateway, useToasts } from "../context/AppContext";
import { generateRandomNickname } from "../lib/randomNickname";
import { readGatewaySettings } from "../lib/settingsStorage";
import type { GatewayMessage } from "../types";

export type JavaGameKind = "net_game" | "rental_game";
export type JavaGameDetails = Record<string, unknown>;

type Account = Record<string, unknown>;
type Role = Record<string, unknown>;


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

function roleEntityId(role: Role): string {
  return value(role, "entity_id") || value(role, "entityId") || value(role, "id");
}

function messagesAfter(messages: GatewayMessage[], lastHandled: GatewayMessage | null): GatewayMessage[] {
  if (!lastHandled) return messages;
  const index = messages.lastIndexOf(lastHandled);
  return messages.slice(index >= 0 ? index + 1 : Math.max(messages.length - 1, 0));
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
        <span><small>{title}</small><strong>{selected ? itemLabel(selected) : "请选择"}</strong></span>
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
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

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
      notify(error instanceof Error ? error.message : "无法创建角色。", "error");
    }
  };

  return createPortal(
    <div className="dialog-overlay" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="dialog-box java-role-dialog" role="dialog" aria-modal="true" aria-label="添加角色" onMouseDown={(event) => event.stopPropagation()}>
        <header className="dialog-header"><h3>添加角色</h3><button className="dialog-close" type="button" aria-label="关闭" onClick={onClose}>&times;</button></header>
        <div className="dialog-body">
          <div className="form-group"><input type="text" aria-label="角色名称" placeholder="输入角色名称" maxLength={8} value={name} disabled={submitting} onChange={(event) => setName(event.target.value)} /></div>
          <div className="role-random-actions">
            <button className="btn-random-name" type="button" onClick={() => setName(generateRandomNickname().slice(0, 8))}>随机名字</button>
            <button className="btn-random-name" type="button" onClick={() => setName(generateRandomNickname(true).slice(0, 8))}>随机中文名</button>
          </div>
        </div>
        <footer className="dialog-footer">
          <button className="btn-secondary" type="button" onClick={onClose}>取消</button>
          <button className="btn-accent" type="button" disabled={!name.trim() || submitting} onClick={() => void submit()}>{submitting ? "处理中..." : "添加"}</button>
        </footer>
      </section>
    </div>,
    document.body,
  );
}

type LaunchAction = "proxy" | "game";
interface PendingLaunch {
  action: LaunchAction;
  account: Account;
  role: Role;
  phase: "password" | "launch";
  identify?: string;
}

export function JavaGameLaunchControls({
  kind,
  gameId,
  gameName,
  details,
  initialPassword = "",
}: {
  kind: JavaGameKind;
  gameId: string;
  gameName: string;
  details: JavaGameDetails;
  initialPassword?: string;
}) {
  const gateway = useGateway();
  const navigate = useNavigate();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [accountIndex, setAccountIndex] = useState(() => Number(sessionStorage.getItem("X-CURRENT-USER-INDEX") ?? "0"));
  const [loadingRoles, setLoadingRoles] = useState(true);
  const [activeLaunch, setActiveLaunch] = useState<{ action: LaunchAction; role: string } | null>(null);
  const [accountOpen, setAccountOpen] = useState(false);
  const [password, setPassword] = useState(initialPassword);
  const [passwordError, setPasswordError] = useState("");
  const [createRoleOpen, setCreateRoleOpen] = useState(false);
  const [deletingRole, setDeletingRole] = useState<string | null>(null);
  const handledMessageRef = useRef<GatewayMessage | null>(gateway.messages.at(-1) ?? null);
  const pendingLaunchRef = useRef<PendingLaunch | null>(null);
  const pendingRoleActionRef = useRef<{ identify: string; entityId: string; accountId: string } | null>(null);
  const accountMenuRef = useRef<HTMLDivElement>(null);
  const selectedAccount = accounts[accountIndex] ?? null;
  const requiresPassword = kind === "rental_game" && value(details, "has_pwd") === "1";

  const requestRoles = useCallback(async (account: Account) => {
    setLoadingRoles(true);
    setRoles([]);
    await gateway.send("get_roles", { id: value(account, "id"), game: gameId, type: kind });
  }, [gameId, gateway.send, kind]);

  useEffect(() => {
    const closeAccountMenu = (event: MouseEvent) => {
      if (!accountMenuRef.current?.contains(event.target as Node)) setAccountOpen(false);
    };
    document.addEventListener("mousedown", closeAccountMenu);
    return () => document.removeEventListener("mousedown", closeAccountMenu);
  }, []);

  useEffect(() => {
    if (gateway.status !== "connected") return;
    void gateway.send("get_accounts", "available").catch((error) => notify(error instanceof Error ? error.message : "无法加载 Java 账号。", "error"));
  }, [gateway.send, gateway.status, notify]);

  const version = useMemo(() => {
    if (kind === "rental_game") return { id: 0x45b6bb0c, name: value(details, "mc_version") };
    const item = firstVersion(details);
    return { id: Number(item.mcversionid ?? 0), name: value(item, "name") };
  }, [details, kind]);
  const sendLaunch = useCallback(async (request: PendingLaunch, serverDetails: JavaGameDetails) => {
    const settings = readGatewaySettings();
    const address = kind === "rental_game" ? value(serverDetails, "server_ip") : value(serverDetails, "server_address");
    const port = Number(serverDetails.server_port ?? 0);
    request.phase = "launch";
    const proxyParts = settings.socks5Address.split(":");
    const identify = request.action === "game"
      ? await gateway.send("launch_game", {
        user_id: value(request.account, "id"),
        game_name: gameName,
        game_id: gameId,
        role_name: value(request.role, "name"),
        client_type: 1,
        game_type: kind === "rental_game" ? 8 : 2,
        game_version_id: version.id,
        game_version: version.name,
        server_ip: address,
        server_port: port,
        max_game_memory: Number(settings.jvmMaxMemory),
        load_core_mods: settings.loadCoreModules,
      })
      : await gateway.send("join_game", {
        id: value(request.account, "id"),
        name: gameName,
        game: gameId,
        role: value(request.role, "name"),
        vid: version.id,
        version: version.name,
        ip: address,
        port,
        socks5: {
          enabled: settings.enableSocks5,
          address: proxyParts[0] || "127.0.0.1",
          port: proxyParts.length >= 2 ? Number(proxyParts[1]) : 1080,
          username: settings.socks5Username || null,
          password: settings.socks5Password || null,
        },
      });
    if (pendingLaunchRef.current === request) request.identify = identify;
  }, [gameId, gameName, gateway.send, kind, version]);

  const launchRole = async (action: LaunchAction, role: Role) => {
    if (!selectedAccount || roleUnavailable(role, kind) || pendingLaunchRef.current) return;
    if (requiresPassword && !password.trim()) {
      setPasswordError("请输入服务器密码");
      return;
    }
    const request: PendingLaunch = { action, account: selectedAccount, role, phase: requiresPassword ? "password" : "launch" };
    pendingLaunchRef.current = request;
    setActiveLaunch({ action, role: value(role, "name") });
    setPasswordError("");
    try {
      if (requiresPassword) {
        request.identify = await gateway.send("rental_games_detail", `${gameId}:${password.trim()}`);
      } else {
        await sendLaunch(request, details);
      }
    } catch (error) {
      pendingLaunchRef.current = null;
      setActiveLaunch(null);
      notify(error instanceof Error ? error.message : "无法启动 Java 游戏。", "error");
    }
  };

  const deleteRole = async (role: Role) => {
    if (!selectedAccount || activeLaunch || deletingRole) return;
    const entityId = roleEntityId(role);
    if (!entityId) {
      notify("角色缺少 EntityID，无法操作。", "error");
      return;
    }
    const pendingNetworkDelete = kind === "net_game" && roleUnavailable(role, kind);
    if (kind === "rental_game" && roleUnavailable(role, kind)) return;
    const prompt = pendingNetworkDelete
      ? `确定取消角色“${value(role, "name")}”的删除吗？`
      : kind === "rental_game"
        ? `确定删除租赁服务器角色“${value(role, "name")}”吗？`
        : `确定删除网络服务器角色“${value(role, "name")}”吗？删除后将进入等待期。`;
    if (!window.confirm(prompt)) return;
    setDeletingRole(entityId);
    try {
      const identify = await gateway.send(pendingNetworkDelete ? "cancel_delete_role" : "delete_role", {
        id: value(selectedAccount, "id"),
        game: gameId,
        type: kind,
        entity_id: entityId,
      });
      pendingRoleActionRef.current = { identify, entityId, accountId: value(selectedAccount, "id") };
    } catch (error) {
      setDeletingRole(null);
      notify(error instanceof Error ? error.message : "无法操作角色。", "error");
    }
  };

  useEffect(() => {
    const pending = messagesAfter(gateway.messages, handledMessageRef.current);
    for (const message of pending) {
      if (message.type === "get_accounts") {
        const nextAccounts = records(message.payload);
        const nextIndex = Math.min(accountIndex, Math.max(nextAccounts.length - 1, 0));
        setAccounts(nextAccounts);
        setAccountIndex(nextIndex);
        if (nextAccounts[nextIndex]) void requestRoles(nextAccounts[nextIndex]).catch((error) => { setLoadingRoles(false); notify(error instanceof Error ? error.message : "无法加载角色。", "error"); });
        else { setRoles([]); setLoadingRoles(false); }
      } else if (message.type === "get_roles") {
        setRoles(records(message.payload));
        setLoadingRoles(false);
      }
      const roleAction = pendingRoleActionRef.current;
      if (roleAction && message.type === "error_notification" && message.identify === roleAction.identify) {
        setDeletingRole(null);
        pendingRoleActionRef.current = null;
        notify(typeof message.payload === "string" ? message.payload : "角色操作失败。", "error");
      } else if (roleAction && (message.type === "delete_role" || message.type === "cancel_delete_role") && message.identify === roleAction.identify) {
        const response = parsePayload(message.payload);
        const record = response && typeof response === "object" && !Array.isArray(response) ? response as Record<string, unknown> : null;
        if (record?.code !== undefined && Number(record.code) !== 0) {
          setDeletingRole(null);
          pendingRoleActionRef.current = null;
          notify(value(record, "message") || "角色操作失败。", "error");
        } else if (selectedAccount && value(selectedAccount, "id") === roleAction.accountId) {
          void requestRoles(selectedAccount).catch((error) => {
            setDeletingRole(null);
            notify(error instanceof Error ? error.message : "无法刷新角色列表。", "error");
          });
          setDeletingRole(null);
          pendingRoleActionRef.current = null;
        } else {
          setDeletingRole(null);
          pendingRoleActionRef.current = null;
        }
      }
      const request = pendingLaunchRef.current;
      if (!request?.identify || message.identify !== request.identify) continue;
      if (message.type === "rental_games_detail" && request.phase === "password") {
        const parsed = parsePayload(message.payload);
        const record = parsed && typeof parsed === "object" && !Array.isArray(parsed) ? parsed as JavaGameDetails : null;
        if (!record || (record.code !== undefined && Number(record.code) !== 0)) {
          setPasswordError(value(record, "message") || "密码验证失败，请重试");
          pendingLaunchRef.current = null;
          setActiveLaunch(null);
          continue;
        }
        const serverDetails = record.entity && typeof record.entity === "object" ? record.entity as JavaGameDetails : record;
        void sendLaunch(request, serverDetails).catch((error) => { pendingLaunchRef.current = null; setActiveLaunch(null); notify(error instanceof Error ? error.message : "无法启动 Java 游戏。", "error"); });
      } else if (message.type === "join_game/success" && request.action === "proxy") {
        pendingLaunchRef.current = null;
        setActiveLaunch(null);
        navigate(`/user-center/launchers/configuration?id=${encodeURIComponent(String(parsePayload(message.payload)))}`);
      } else if (message.type === "launch_game" && request.action === "game") {
        pendingLaunchRef.current = null;
        setActiveLaunch(null);
      } else if (message.type === "error_notification") {
        if (request.phase === "password") setPasswordError(typeof message.payload === "string" ? message.payload : "密码验证失败，请重试");
        pendingLaunchRef.current = null;
        setActiveLaunch(null);
      }
    }
    handledMessageRef.current = gateway.messages.at(-1) ?? null;
  }, [accountIndex, gateway.messages, navigate, notify, requestRoles, selectedAccount, sendLaunch]);

  const chooseAccount = (index: number) => {
    setAccountIndex(index);
    setAccountOpen(false);
    sessionStorage.setItem("X-CURRENT-USER-INDEX", String(index));
    sessionStorage.setItem("X-CURRENT-USER-ID", value(accounts[index], "id"));
    if (accounts[index]) void requestRoles(accounts[index]).catch((error) => { setLoadingRoles(false); notify(error instanceof Error ? error.message : "无法加载角色。", "error"); });
  };

  return (
    <>
      {requiresPassword ? <div className="detail-password-group"><label htmlFor="java-server-password">服务器密码</label><input id="java-server-password" type="password" placeholder="输入服务器密码" value={password} disabled={Boolean(activeLaunch)} onChange={(event) => { setPassword(event.target.value); setPasswordError(""); }} />{passwordError ? <div className="dialog-error" role="alert">{passwordError}</div> : null}</div> : null}
      <div className="server-detail-section">
        <div className="server-detail-section-title">账号</div>
        <div className={`custom-select${accountOpen ? " open" : ""}`} ref={accountMenuRef}>
          <button className="custom-select-trigger" type="button" aria-haspopup="listbox" aria-expanded={accountOpen} disabled={Boolean(activeLaunch)} onClick={() => setAccountOpen((open) => !open)}>{selectedAccount ? accountLabel(selectedAccount) : "选择账号"}</button>
          <svg className="custom-select-arrow" width="10" height="6" viewBox="0 0 10 6" aria-hidden="true"><path d="M0 0l5 6 5-6z" fill="currentColor" /></svg>
          <div className="custom-select-dropdown" role="listbox" aria-label="账号" hidden={!accountOpen}>
            {accounts.length === 0 ? <div className="custom-select-empty">没有已登录的游戏账号</div> : accounts.map((account, index) => <button className={`custom-select-option${index === accountIndex ? " selected" : ""}`} type="button" role="option" aria-selected={index === accountIndex} key={value(account, "id")} onClick={() => chooseAccount(index)}>{accountLabel(account)}</button>)}
          </div>
        </div>
      </div>
      <div className="server-detail-section">
        <div className="server-detail-section-title">角色列表</div>
        <div className="server-detail-roles">
          {loadingRoles ? <div className="detail-no-roles">加载中...</div> : roles.length === 0 ? <div className="detail-no-roles">暂无角色，请添加</div> : roles.map((role, index) => {
            const unavailable = roleUnavailable(role, kind);
            const roleName = value(role, "name");
            const entityId = roleEntityId(role);
            const networkDeletePending = kind === "net_game" && unavailable;
            const actionBusy = deletingRole === entityId;
            return (
              <div className={`role-row${unavailable ? " role-row-unavailable" : ""}`} key={`${entityId || roleName}-${index}`}>
                <div className="role-row-left"><div className="role-row-name">{roleName}</div>{unavailable ? <div className="role-ban-info">删除中</div> : null}</div>
                <div className="role-row-actions">
                  <button className={`role-launch-btn${activeLaunch?.action === "proxy" && activeLaunch.role === roleName ? " btn-loading" : ""}`} type="button" disabled={unavailable || Boolean(activeLaunch) || !selectedAccount} onClick={() => void launchRole("proxy", role)}>启动</button>
                  <button className={`role-white-btn${activeLaunch?.action === "game" && activeLaunch.role === roleName ? " btn-loading" : ""}`} type="button" disabled={unavailable || Boolean(activeLaunch) || !selectedAccount} onClick={() => void launchRole("game", role)}>白端</button>
                  <button className={`role-delete-btn${networkDeletePending ? " role-cancel-btn" : ""}`} type="button" disabled={!selectedAccount || Boolean(activeLaunch) || actionBusy || !entityId || (kind === "rental_game" && unavailable)} onClick={() => void deleteRole(role)}>{actionBusy ? "处理中..." : networkDeletePending ? "取消" : "删除"}</button>
                </div>
              </div>
            );
          })}
        </div>
        <button className="detail-add-role-btn" type="button" disabled={!selectedAccount || Boolean(activeLaunch)} onClick={() => setCreateRoleOpen(true)} style={{ marginTop: 8 }}>+ 添加角色</button>
      </div>
      {createRoleOpen ? <CreateRoleDialog accountId={value(selectedAccount, "id")} gameId={gameId} kind={kind} onClose={() => setCreateRoleOpen(false)} /> : null}
    </>
  );
}
