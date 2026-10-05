import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { SkinViewer, WalkingAnimation } from "skinview3d";
import { useGateway, useToasts } from "../context/AppContext";
import { loadGatewaySettings } from "../lib/settingsStorage";
import { generateRandomNickname } from "../lib/randomNickname";
import type { GatewayMessage } from "../types";

type Account = string | Record<string, unknown>;
type LaunchKind = "netserver" | "realms" | "realm";


function parsePayload(payload: unknown): unknown {
  if (typeof payload !== "string") return payload;
  try {
    return JSON.parse(payload);
  } catch {
    return payload;
  }
}

function recordPayload(payload: unknown): Record<string, unknown> {
  const value = parsePayload(payload);
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function gatewayErrorMessage(payload: unknown): string {
  const value = parsePayload(payload);
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    if (record.code !== undefined && Number(record.code) !== 0) {
      return String(record.message ?? "网关请求失败。");
    }
  }
  return "";
}

function accountList(payload: unknown): Account[] {
  const value = parsePayload(payload);
  if (Array.isArray(value)) {
    const accounts = value.filter((account): account is Account => typeof account === "string" || Boolean(account && typeof account === "object"));
    return [...new Map(accounts.map((account) => [accountId(account), account])).values()].filter((account) => accountId(account));
  }
  if (value && typeof value === "object") {
    const accounts = (value as Record<string, unknown>).accounts;
    if (Array.isArray(accounts)) return accountList(accounts);
  }
  return [];
}

function accountText(account: Account): string {
  if (typeof account === "string") return account;
  for (const key of ["username", "name", "email", "id", "user_id"]) {
    const value = account[key];
    if (value !== undefined && value !== null && value !== "") return String(value);
  }
  return "未知账号";
}

function accountId(account: Account): string {
  if (typeof account === "string") return account;
  for (const key of ["id", "user_id", "username", "name", "email"]) {
    const value = account[key];
    if (value !== undefined && value !== null && value !== "") return String(value);
  }
  return "";
}

function decodeSkin(payload: unknown): number[] {
  if (Array.isArray(payload)) return payload.filter((value): value is number => typeof value === "number");
  if (typeof payload !== "string" || !payload) return [];
  try {
    const decoded = atob(payload);
    return Array.from(decoded, (character) => character.charCodeAt(0));
  } catch {
    return [];
  }
}

function BackIcon() {
  return <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true"><path d="M10 12L6 8l4-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function SkinPreview({ skinBytes, fallbackSkinUrl }: { skinBytes: number[]; fallbackSkinUrl?: string }) {
  const hostRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [skinUrl, setSkinUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!skinBytes.length) {
      setSkinUrl(fallbackSkinUrl ?? null);
      return;
    }
    const url = URL.createObjectURL(new Blob([new Uint8Array(skinBytes)], { type: "image/png" }));
    setSkinUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [fallbackSkinUrl, skinBytes]);

  useEffect(() => {
    if (!hostRef.current) return;
    const observer = new ResizeObserver(([entry]) => {
      const width = Math.round(entry.contentRect.width);
      const height = Math.round(entry.contentRect.height);
      setSize((current) => current.width === width && current.height === height ? current : { width, height });
    });
    observer.observe(hostRef.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!canvasRef.current || !skinUrl || !size.width || !size.height) return;
    const viewer = new SkinViewer({ canvas: canvasRef.current, width: size.width, height: size.height, skin: skinUrl });
    const animation = new WalkingAnimation();
    animation.headBobbing = false;
    animation.speed = 0.5;
    viewer.animation = animation;
    viewer.fov = 40;
    viewer.zoom = 0.86;
    viewer.globalLight.intensity = 2;
    viewer.cameraLight.intensity = 3000;
    return () => viewer.dispose();
  }, [size, skinUrl]);

  return <div className="game-skin-canvas" ref={hostRef}><canvas ref={canvasRef} /></div>;
}

function SkinThumbnail({ skin, selected, onSelect, skinUrl }: { skin: readonly [string, string]; selected: boolean; onSelect(): void; skinUrl?: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [imageUrl, setImageUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!canvasRef.current) return;
    setImageUrl(null);
    const viewer = new SkinViewer({
      canvas: canvasRef.current,
      width: 150,
      height: 200,
    });
    viewer.playerObject.rotation.y = Math.PI / 8;
    viewer.playerObject.rotation.x = Math.PI / 8;
    viewer.fov = 40;
    viewer.zoom = 0.86;
    viewer.globalLight.intensity = 2;
    viewer.cameraLight.intensity = 3000;

    let disposed = false;
    void viewer.loadSkin(skinUrl ?? "").then(() => {
      if (disposed || !canvasRef.current) return;
      viewer.render();
      setImageUrl(canvasRef.current.toDataURL());
      viewer.dispose();
    }).catch(() => viewer.dispose());

    return () => {
      disposed = true;
      viewer.dispose();
    };
  }, [skinUrl]);

  return (
    <button type="button" className={`skin-card ${selected ? "selected" : ""}`} onClick={onSelect}>
      <span className="skin-card-preview">
        <canvas ref={canvasRef} aria-hidden="true" />
        {imageUrl ? <img src={imageUrl} alt={skin[0]} /> : <span className="skin-card-spinner" aria-label={`正在加载 ${skin[0]} 皮肤`} />}
        <span className="skin-card-overlay"><strong>{skin[0]}</strong></span>
      </span>
      {selected ? (
        <i className="skin-card-check" aria-hidden="true">
          <svg viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        </i>
      ) : null}
    </button>
  );
}

function AccountSelector({ accounts, selected, onChange }: { accounts: Account[]; selected: Account | null; onChange(account: Account): void }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  return (
    <div className={`custom-select${open ? " open" : ""}`} ref={rootRef}>
      <button type="button" className="custom-select-trigger" aria-haspopup="listbox" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
        {selected ? accountText(selected) : "选择账号"}
      </button>
      <svg className="custom-select-arrow" width="10" height="6" viewBox="0 0 10 6" aria-hidden="true"><path d="M0 0l5 6 5-6z" fill="currentColor" /></svg>
      <div className="custom-select-dropdown" role="listbox" aria-label="选择账号">
        {accounts.length === 0 ? <div className="custom-select-empty">没有已登录的游戏账号</div> : accounts.map((account) => (
          <button type="button" key={accountId(account)} role="option" aria-selected={accountId(account) === accountId(selected ?? "")} className={`custom-select-option${accountId(account) === accountId(selected ?? "") ? " selected" : ""}`} onClick={() => { onChange(account); setOpen(false); }}>{accountText(account)}</button>
        ))}
      </div>
    </div>
  );
}

function SkinPicker({ onClose, localSkins, onSelect, currentSkin }: { onClose(): void; localSkins: string[]; onSelect(skin: string): void; currentSkin: string }) {
  const [localSelected, setLocalSelected] = useState<string | null>(currentSkin || null);

  return (
    <div className="dialog-overlay neo-bedrock-skin-overlay" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="dialog-box neo-bedrock-skin-dialog" role="dialog" aria-modal="true" aria-label="皮肤预览">
        <header className="dialog-header"><h3>皮肤预览</h3><button className="dialog-close" type="button" aria-label="关闭" onClick={onClose}>&times;</button></header>
        <div className="dialog-body">{localSkins.length > 0 ? <div className="skin-picker-grid">{localSkins.map((skin) => <SkinThumbnail key={skin} skin={[skin, skin]} selected={localSelected === skin} onSelect={() => setLocalSelected(skin)} skinUrl={skin.endsWith(".zip") ? `/api/skins/thumbnail?file=${encodeURIComponent(skin)}` : `/skins/${skin}`} />)}</div> : <p className="detail-no-roles">未在 resources/skins 中找到皮肤</p>}</div>
        <footer className="dialog-footer"><button className="btn-secondary" type="button" onClick={onClose}>取消</button>{localSelected ? <button className="btn-accent" type="button" onClick={() => { onSelect(localSelected); onClose(); }}>选择</button> : null}</footer>
      </section>
    </div>
  );
}

function readRealmModItemIds(sid: string): string[] {
  try {
    const all = JSON.parse(localStorage.getItem("bedrock-realms-mods") ?? "{}") as Record<string, string[]>;
    return Array.isArray(all[sid]) ? all[sid] : [];
  } catch {
    return [];
  }
}

export function GameLaunchPage({ kind }: { kind: LaunchKind }) {
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { status, send, messages } = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selectedAccount, setSelectedAccount] = useState<Account | null>(null);
  const [nickname, setNickname] = useState("");
  const [nicknameLoading, setNicknameLoading] = useState(false);
  const [roleSettingsOpen, setRoleSettingsOpen] = useState(false);
  const [nicknameDraft, setNicknameDraft] = useState("");
  const [nicknameError, setNicknameError] = useState("");
  const [nicknameSaving, setNicknameSaving] = useState(false);
  const nicknameRequestRef = useRef<{ userId: string; nickname: string; identify: string } | null>(null);
  const nicknameLoadRef = useRef<{ userId: string; identify: string } | null>(null);
  const pendingAddressLaunchRef = useRef<{ interceptor: boolean; identify: string; endpoint?: { host: string; port: number; userId: string } } | null>(null);
  const [nicknameRequestVersion, setNicknameRequestVersion] = useState(0);
  const [failedImage, setFailedImage] = useState("");
  const [address, setAddress] = useState({ host: "", port: 0 });
  const [skinBytes, setSkinBytes] = useState<number[]>([]);
  const [launching, setLaunching] = useState(false);
  const [joining, setJoining] = useState(false);
  const [rentalPassword, setRentalPassword] = useState("");
  const [passwordPromptOpen, setPasswordPromptOpen] = useState(false);
  const [passwordError, setPasswordError] = useState("");
  const [skinPickerOpen, setSkinPickerOpen] = useState(false);
  const [localSkins, setLocalSkins] = useState<string[]>([]);
  const [selectedSkin, setSelectedSkin] = useState<string>(() => localStorage.getItem("selected-pe-skin") ?? "");
  const [modTask, setModTask] = useState<{ percent: number; message: string; modName: string; done: number; total: number } | null>(null);
  const modDismissedRef = useRef(false);
  const pendingLaunchRef = useRef<(() => void) | null>(null);
  const handledMessageRef = useRef<GatewayMessage | null>(messages.at(-1) ?? null);
  const addressUserIdRef = useRef("");
  const accountsRef = useRef<Account[]>([]);
  const id = searchParams.get("id") ?? "";
  const name = searchParams.get("name") ?? "游戏服务器";
  const realms = kind === "realms";
  const domainRealm = kind === "realm";
  const userId = selectedAccount ? accountId(selectedAccount) : "";
  const server = recordPayload(recordPayload(location.state).server);
  const images = Array.isArray(server.pic_url_list) ? server.pic_url_list.map(String) : [];
  const image = images.find((value) => value.trim()) ?? String(server.title_image_url ?? server.image_url ?? "");
  const description = String(server.brief ?? server.description ?? server.brief_summary ?? "").replace(/<img[^>]*>/gi, "");
  const passwordFlag = [server.has_pwd, server.has_password, server.hasPassword, server.password_required].find((value) => value !== undefined && value !== null);
  const requiresPassword = realms && (searchParams.has("password") || (passwordFlag !== undefined && !["", "0", "false", "no", "none", "null"].includes(String(passwordFlag).toLowerCase())));

  const showError = useCallback((error: unknown, fallback: string) => {
    notify(error instanceof Error ? error.message : fallback, "error");
  }, [notify]);

  useEffect(() => {
    handledMessageRef.current = messages.at(-1) ?? null;
    accountsRef.current = [];
    setAccounts(accountsRef.current);
    setSelectedAccount(null);
    setNickname("");
    setNicknameLoading(false);
    setRoleSettingsOpen(false);
    setNicknameDraft("");
    setNicknameError("");
    setNicknameSaving(false);
    nicknameRequestRef.current = null;
    nicknameLoadRef.current = null;
    pendingAddressLaunchRef.current = null;
    setAddress({ host: "", port: 0 });
    setSkinBytes([]);
    setLaunching(false);
    setJoining(false);
    setRentalPassword(searchParams.get("password") ?? "");
    setPasswordPromptOpen(false);
    setPasswordError("");
    addressUserIdRef.current = "";
  }, [id, realms, searchParams]);

  useEffect(() => {
    if (status !== "connected") return;
    fetch("/api/skins").then(r => r.json()).then((skins: string[]) => {
      setLocalSkins(skins);
      if (selectedSkin && skins.includes(selectedSkin)) {
        loadSkinPreview(selectedSkin);
      }
    }).catch(() => {});
    if (domainRealm) {
      const host = searchParams.get("host") ?? "";
      const port = Number(searchParams.get("port") ?? 0);
      if (host) setAddress({ host, port });
      void send("get_accounts", "available-for-mobile").catch(() => {});
    } else if (realms && searchParams.get("host")) {
      const host = searchParams.get("host") ?? "";
      const port = Number(searchParams.get("port") ?? 0);
      addressUserIdRef.current = searchParams.get("user_id") ?? "";
      setAddress({ host, port });
      void send("get_accounts", "available-for-mobile").catch(() => {});
    } else if (requiresPassword) {
      void send("get_accounts", "available-for-mobile").catch((error) => showError(error, "无法加载启动设置。"));
    } else {
      const addressType = realms ? "g79_rental_game_address" : "g79_net_game_address";
      const addressPayload = realms ? { game: id, password: searchParams.get("password") ?? "" } : { game: id };
      void Promise.all([
        send("get_accounts", "available-for-mobile"),
        send(addressType, addressPayload),
      ]).catch((error) => showError(error, "无法加载启动设置。"));
    }
  }, [domainRealm, id, realms, requiresPassword, searchParams, send, showError, status]);

  const requestRentalAddress = useCallback(async (password: string) => {
    await send("g79_rental_game_address", { game: id, password });
  }, [id, send]);

  useEffect(() => {
    if (!userId || status !== "connected") return;
    setNickname("");
    setNicknameLoading(true);
    const identify = crypto.randomUUID();
    nicknameLoadRef.current = { userId, identify };
    void send("g79_get_nickname", userId, identify).catch((error) => {
      setNicknameLoading(false);
      setLaunching(false);
      setJoining(false);
      pendingAddressLaunchRef.current = null;
      showError(error, "无法加载昵称。");
    });
  }, [nicknameRequestVersion, send, showError, status, userId]);

  useEffect(() => {
    const previous = handledMessageRef.current;
    const previousIndex = previous ? messages.lastIndexOf(previous) : -1;
    const startIndex = previous ? (previousIndex >= 0 ? previousIndex + 1 : Math.max(messages.length - 1, 0)) : 0;
    const pending = messages.slice(startIndex);
    for (const message of pending) {
      if (message.type === "get_accounts") {
        const nextAccounts = accountList(message.payload);
        accountsRef.current = nextAccounts;
        setAccounts(nextAccounts);
        setSelectedAccount(nextAccounts.find((account) => accountId(account) === addressUserIdRef.current) ?? nextAccounts[0] ?? null);
      } else if (message.type === "g79_get_nickname") {
        if (message.identify !== nicknameLoadRef.current?.identify) continue;
        nicknameLoadRef.current = null;
        setNicknameLoading(false);
        const error = gatewayErrorMessage(message.payload);
        const nicknamePayload = parsePayload(message.payload);
        let nextNickname = "";
        if (error) {
          pendingAddressLaunchRef.current = null;
          setLaunching(false);
          setJoining(false);
          showError(new Error(error), "无法加载昵称。");
        } else if (typeof nicknamePayload === "string") nextNickname = nicknamePayload;
        else {
          const payload = recordPayload(nicknamePayload);
          const nicknameValue = payload.nickname ?? payload.name ?? payload.role_name;
          if (nicknameValue !== undefined) nextNickname = String(nicknameValue);
        }
        if (!error) setNickname(nextNickname);
        const pendingLaunch = pendingAddressLaunchRef.current;
        if (!error && pendingLaunch?.endpoint) {
          pendingAddressLaunchRef.current = null;
          void sendLaunch(pendingLaunch.interceptor, pendingLaunch.endpoint, nextNickname);
        }
      } else if (message.type === "g79_set_nickname" && nicknameRequestRef.current) {
        const request = nicknameRequestRef.current;
        if (message.identify !== request.identify) continue;
        nicknameRequestRef.current = null;
        setNicknameSaving(false);
        const error = gatewayErrorMessage(message.payload);
        if (error) setNicknameError(error);
        else if (request.userId === userId) {
          setNickname(request.nickname);
          setRoleSettingsOpen(false);
        }
      } else if (realms && (
        message.type === "g79_rental_game_password"
        || (searchParams.has("password") && ["error_notification", "error_notification_back", "error_notification_rt"].includes(message.type))
      )) {
        const error = gatewayErrorMessage(message.payload)
          || (typeof message.payload === "string" ? message.payload : "请输入租赁服密码。");
        setPasswordError(error);
        pendingAddressLaunchRef.current = null;
        setLaunching(false);
        setJoining(false);
        setPasswordPromptOpen(true);
      } else if (message.type === (realms ? "g79_rental_game_address" : "g79_net_game_address")) {
        const responseError = gatewayErrorMessage(message.payload);
        if (responseError) {
          pendingAddressLaunchRef.current = null;
          setLaunching(false);
          setJoining(false);
          if (realms) setPasswordError(responseError);
          showError(new Error(responseError), "无法加载服务器地址。");
          continue;
        }
        const payload = recordPayload(message.payload);
        const addressUserId = String(payload.user_id ?? "");
        if (addressUserId) {
          addressUserIdRef.current = addressUserId;
          setSelectedAccount((current) => accountId(current ?? "") === addressUserId
            ? current
            : accountsRef.current.find((account) => accountId(account) === addressUserId) ?? current);
        }
        const endpoint = {
          host: String(realms ? payload.mcserver_host ?? "" : payload.host ?? ""),
          port: Number(realms ? payload.mcserver_port ?? 0 : payload.port ?? 0),
        };
        setAddress(endpoint);
        const pendingLaunch = pendingAddressLaunchRef.current;
        if (pendingLaunch && pendingLaunch.identify === message.identify) {
          pendingLaunch.endpoint = { ...endpoint, userId: addressUserId || userId };
          setNicknameRequestVersion((value) => value + 1);
        }
      } else if (message.type === "g79_launch_game" || message.type === "g79_join_game") {
        const error = gatewayErrorMessage(message.payload);
        if (error) {
          setLaunching(false);
          setJoining(false);
          showError(new Error(error), "无法启动基岩版游戏。");
        }
      } else if (message.type === "g79_query_skin" || message.type === "g79_modify_skin") {
        const error = gatewayErrorMessage(message.payload);
        if (error) showError(new Error(error), "无法修改基岩版皮肤。");
        else setSkinBytes(decodeSkin(message.payload));
      } else if (message.type === "realm_mod_download") {
        const result = recordPayload(message.payload);
        modDismissedRef.current = false;
        if (Number(result.code) === 0 && (result.need_download === false || result.need_download === "false")) {
          pendingLaunchRef.current?.();
          pendingLaunchRef.current = null;
        }
      } else if (message.type === "realm_mod_progress") {
        if (modDismissedRef.current) return;
        const progress = recordPayload(message.payload);
        const percent = Number(progress.percent ?? 0);
        setModTask({
          percent,
          message: String(progress.message ?? "下载中..."),
          modName: String(progress.mod_name ?? ""),
          done: Number(progress.done ?? 0),
          total: Number(progress.total ?? 0),
        });
      } else if (message.type === "realm_mod_done") {
        const done = recordPayload(message.payload);
        setModTask(null);
        modDismissedRef.current = false;
        const success = Number(done.success) === 1 || done.success === true;
        if (success) {
          pendingLaunchRef.current?.();
          pendingLaunchRef.current = null;
        } else {
          showError(new Error(String(done.message ?? "模组下载失败")), "模组下载失败");
        }
      } else if (message.type === "g79_launch_game_done" && (String(message.payload) === id || String(message.payload) === `${id}:DomainGame`)) {
        setLaunching(false);
      } else if (message.type === "g79_join_game_done" && String(message.payload) === id) {
        setJoining(false);
      }
    }
    handledMessageRef.current = messages.at(-1) ?? null;
  }, [id, messages, realms, searchParams, showError, userId]);

  const submitRentalPassword = async () => {
    if (!rentalPassword.trim()) {
      setPasswordError("请输入服务器密码。");
      return;
    }
    setPasswordError("");
    setPasswordPromptOpen(false);
    try {
      await requestRentalAddress(rentalPassword);
    } catch (error) {
      setPasswordPromptOpen(true);
      showError(error, "无法验证租赁服密码。");
    }
  };

  const setServerNickname = async () => {
    const nextNickname = nicknameDraft.trim();
    if (!userId || !nextNickname) {
      setNicknameError(userId ? "请输入角色名称。" : "请先选择账号。");
      return;
    }
    setNicknameError("");
    setNicknameSaving(true);
    const identify = crypto.randomUUID();
    nicknameRequestRef.current = { userId, nickname: nextNickname, identify };
    try {
      await send("g79_set_nickname", { id: userId, new: nextNickname }, identify);
    } catch (error) {
      nicknameRequestRef.current = null;
      setNicknameSaving(false);
      showError(error, "无法更新昵称。");
    }
  };

	const sendLaunch = async (interceptor: boolean, endpoint = { ...address, userId }, roleName = nickname) => {
		interceptor ? setJoining(true) : setLaunching(true);
		try {
			if (!endpoint.host.trim() || !Number.isInteger(endpoint.port) || endpoint.port <= 0 || endpoint.port > 65535) {
				throw new Error("服务器尚未提供有效的连接地址，请稍后重试。");
			}
      if (!endpoint.userId || !roleName.trim()) throw new Error("请选择账号并设置角色名称。");
      const gatewaySettings = await loadGatewaySettings();
      const pePath = gatewaySettings.peLaunchPath;
      const hasCustomPath = pePath.trim().length > 0;
      const itemIds = domainRealm ? readRealmModItemIds(id) : [];
      const payload: Record<string, unknown> = {
        game_name: name,
        game_id: id,
        role_name: roleName,
        user_id: endpoint.userId,
        ...(interceptor ? {} : { client_type: 2, launch_type: hasCustomPath ? 0 : 1, ...(hasCustomPath ? { launch_path: pePath } : {}) }),
        game_type: realms || domainRealm ? 8 : 2,
        ...(domainRealm ? { is_domain_game: true } : {}),
        server_ip: endpoint.host,
        server_port: endpoint.port,
        skin_path: "",
        ...(itemIds.length > 0 ? { item_ids: itemIds } : {}),
      };
      const doLaunch = () => {
        void send(interceptor ? "g79_join_game" : "g79_launch_game", payload).catch((error) => {
          interceptor ? setJoining(false) : setLaunching(false);
          showError(error, interceptor ? "无法启动代理通道。" : "无法启动游戏。");
        });
      };
      if (domainRealm && itemIds.length > 0) {
        pendingLaunchRef.current = doLaunch;
        await send("realm_mod_download", { id: userId, sid: id, item_ids: itemIds });
        return;
      }
      doLaunch();
    } catch (error) {
      interceptor ? setJoining(false) : setLaunching(false);
      pendingLaunchRef.current = null;
      showError(error, interceptor ? "无法启动代理通道。" : "无法启动游戏。");
    }
  };

  const prepareLaunch = async (interceptor: boolean) => {
    if (!realms) {
      await sendLaunch(interceptor);
      return;
    }
    if (requiresPassword && !rentalPassword.trim()) {
      setPasswordError("请输入服务器密码。");
      return;
    }
    setPasswordError("");
    interceptor ? setJoining(true) : setLaunching(true);
    const identify = crypto.randomUUID();
    pendingAddressLaunchRef.current = { interceptor, identify };
    try {
      await send("g79_rental_game_address", { game: id, password: rentalPassword }, identify);
    } catch (error) {
      pendingAddressLaunchRef.current = null;
      setLaunching(false);
      setJoining(false);
      showError(error, "无法验证租赁服密码。");
    }
  };

  const loadSkinPreview = useCallback(async (filename: string) => {
    const response = await fetch(`/skins/${filename}`);
    const blob = await response.blob();
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = String(reader.result ?? "").split(",")[1];
      if (base64) {
        setSkinBytes(Array.from(atob(base64), (c) => c.charCodeAt(0)));
      }
    };
    reader.readAsDataURL(blob);
  }, []);

  const selectLocalSkin = useCallback(async (filename: string) => {
    setSelectedSkin(filename);
    localStorage.setItem("selected-pe-skin", filename);
    const response = await fetch(`/skins/${filename}`);
    const blob = await response.blob();
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = String(reader.result ?? "").split(",")[1];
      if (base64) {
        setSkinBytes(Array.from(atob(base64), (c) => c.charCodeAt(0)));
        void send("g79_modify_skin", base64).catch(() => {});
      }
    };
    reader.readAsDataURL(blob);
  }, [send]);

  const openRoleSettings = () => {
    setNicknameDraft(nickname);
    setNicknameError("");
    setRoleSettingsOpen(true);
  };

  const selectAccount = (account: Account) => {
    if (accountId(account) === userId) return;
    pendingAddressLaunchRef.current = null;
    nicknameLoadRef.current = null;
    setLaunching(false);
    setJoining(false);
    setSelectedAccount(account);
  };

  return (
    <div className="server-detail neo-bedrock-detail">
      <div className="server-detail-header">
        <button className="server-detail-back" type="button" aria-label="返回" onClick={() => navigate(-1)}><BackIcon /></button>
        <div className="server-detail-title">{name}</div>
      </div>
      {image ? <div className="server-detail-images">{failedImage === image ? <div className="detail-img-placeholder" /> : <img src={image} alt="服务器预览图片" onError={() => setFailedImage(image)} />}</div> : null}
      {description ? <div className="server-detail-desc" dangerouslySetInnerHTML={{ __html: description }} /> : null}
      {requiresPassword ? <div className="detail-password-group"><label htmlFor="bedrock-detail-password">服务器密码</label><input id="bedrock-detail-password" type="password" placeholder="输入服务器密码" autoComplete="off" value={rentalPassword} onChange={(event) => { setRentalPassword(event.target.value); setPasswordError(""); }} />{passwordError ? <div className="dialog-error" role="alert">{passwordError}</div> : null}</div> : null}
      <div className="server-detail-section">
        <div className="server-detail-section-title">账号</div>
        <AccountSelector accounts={accounts} selected={selectedAccount} onChange={selectAccount} />
      </div>
      <div className="server-detail-section">
        <div className="server-detail-section-title">角色列表</div>
        <div className="server-detail-roles">
          {nicknameLoading ? <div className="detail-no-roles">加载中...</div> : nickname ? (
            <div className="role-row">
              <div className="role-row-left"><div className="role-row-name">{nickname}</div></div>
              <div className="role-row-actions"><button className={`role-white-btn${launching ? " btn-loading" : ""}`} type="button" disabled={launching || !userId} onClick={() => void prepareLaunch(false)}>白端</button></div>
            </div>
          ) : <div className="detail-no-roles">{selectedAccount ? "暂无角色，请添加" : "请选择已登录的游戏账号"}</div>}
        </div>
        <button className="detail-add-role-btn" type="button" disabled={!userId || nicknameLoading} style={{ marginTop: 8 }} onClick={openRoleSettings}>{nickname ? "角色设置" : "+ 添加角色"}</button>
      </div>
      {roleSettingsOpen ? createPortal(
        <div className="dialog-overlay" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget && !nicknameSaving) setRoleSettingsOpen(false); }}>
          <section className="dialog-box neo-bedrock-role-dialog" role="dialog" aria-modal="true" aria-labelledby="bedrock-role-title">
            <header className="dialog-header"><h3 id="bedrock-role-title">{nickname ? "角色设置" : "添加角色"}</h3><button className="dialog-close" type="button" aria-label="关闭" disabled={nicknameSaving} onClick={() => setRoleSettingsOpen(false)}>&times;</button></header>
            <form onSubmit={(event) => { event.preventDefault(); void setServerNickname(); }}>
              <div className="dialog-body">
                <div className="form-group"><input autoFocus type="text" aria-label="角色名称" placeholder="输入角色名称" value={nicknameDraft} disabled={nicknameSaving} onChange={(event) => setNicknameDraft(event.target.value)} /></div>
                <div className="role-random-actions"><button className="btn-random-name" type="button" disabled={nicknameSaving} onClick={() => setNicknameDraft(generateRandomNickname())}>随机名字</button></div>
                {nicknameError ? <div className="dialog-error" role="alert">{nicknameError}</div> : null}
                <div className="neo-bedrock-role-preview"><SkinPreview skinBytes={skinBytes} /></div>
                <button className="btn-secondary" type="button" onClick={() => setSkinPickerOpen(true)}>修改皮肤</button>
                <div className="neo-bedrock-role-endpoint">{address.host ? `${address.host}:${address.port}` : "正在加载服务器地址…"}</div>
              </div>
              <footer className="dialog-footer"><button className="btn-secondary" type="button" disabled={nicknameSaving} onClick={() => setRoleSettingsOpen(false)}>取消</button><button className="btn-accent" type="submit" disabled={nicknameSaving}>{nicknameSaving ? "保存中…" : nickname ? "保存" : "添加"}</button></footer>
            </form>
          </section>
        </div>, document.body
      ) : null}
      {skinPickerOpen ? createPortal(<SkinPicker localSkins={localSkins} currentSkin={selectedSkin} onClose={() => setSkinPickerOpen(false)} onSelect={selectLocalSkin} />, document.body) : null}
      {passwordPromptOpen ? createPortal(
        <div className="dialog-overlay" role="presentation">
          <section className="dialog-box" role="dialog" aria-modal="true" aria-labelledby="rental-password-title" style={{ width: 360 }}>
            <header className="dialog-header"><h3 id="rental-password-title">服务器密码</h3><button className="dialog-close" type="button" aria-label="关闭" onClick={() => setPasswordPromptOpen(false)}>&times;</button></header>
            <form onSubmit={(event) => { event.preventDefault(); void submitRentalPassword(); }}>
              <div className="dialog-body">
                <div className="form-group"><input id="rental-server-password" aria-label="服务器密码" type="password" value={rentalPassword} onChange={(event) => setRentalPassword(event.target.value)} autoFocus autoComplete="off" placeholder="请输入服务器密码" /></div>
                {passwordError ? <div className="dialog-error" role="alert">{passwordError}</div> : null}
              </div>
              <footer className="dialog-footer"><button className="btn-secondary" type="button" onClick={() => setPasswordPromptOpen(false)}>取消</button><button className="btn-accent" type="submit">确认</button></footer>
            </form>
          </section>
        </div>, document.body
      ) : null}
      {modTask ? createPortal(
        <div className="dialog-overlay" role="presentation">
          <section className="dialog-box" role="dialog" aria-modal="true" aria-label="模组下载" style={{ width: 400 }}>
            <header className="dialog-header"><h3>模组下载</h3><button className="dialog-close" type="button" aria-label="关闭" onClick={() => { setModTask(null); modDismissedRef.current = true; }}>&times;</button></header>
            <div className="dialog-body realm-mod-body"><p className="realm-mod-line">{modTask.message}{modTask.modName ? `（${modTask.modName}）` : ""}<span>{modTask.done}/{modTask.total}</span></p><i className="realm-mod-bar"><b style={{ width: `${Math.min(Math.max((modTask.done / Math.max(modTask.total, 1)) * 100, 0), 100)}%` }} /></i><small className="realm-mod-hint">关闭弹窗不会取消下载，可在“游戏管理”页面取消</small></div>
          </section>
        </div>, document.body
      ) : null}
    </div>
  );
}
