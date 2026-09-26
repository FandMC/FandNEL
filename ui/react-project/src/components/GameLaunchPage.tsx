import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { SkinViewer, WalkingAnimation } from "skinview3d";
import { useAuth, useGateway, useToasts } from "../context/AppContext";
import { loadGatewaySettings } from "../lib/settingsStorage";
import { generateRandomNickname } from "../lib/randomNickname";
import type { GatewayMessage } from "../types";

type Account = string | Record<string, unknown>;
type LaunchKind = "netserver" | "realms" | "realm";

const debugAccount = { id: "debug-account", username: "Debug Account" };

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
      return String(record.message ?? "Gateway request failed.");
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
  return "Unknown account";
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
  return <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M15 18L9 12L15 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function EditIcon() {
  return <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 17.25V21h3.75L17.81 10.94l-3.75-3.75L4 17.25zM20.71 7.04a1.003 1.003 0 000-1.42l-2.34-2.34a1.003 1.003 0 00-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" fill="currentColor" /></svg>;
}

function ShuffleIcon() {
  return <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>;
}

function ChevronIcon() {
  return <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M6 9L12 15L18 9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function Spinner() {
  return (
    <svg className="game-launch-spinner" width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle opacity=".25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path opacity=".75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
    </svg>
  );
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
        {imageUrl ? <img src={imageUrl} alt={skin[0]} /> : <span className="skin-card-spinner" aria-label={`Loading ${skin[0]} skin`} />}
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
    <div className="game-account-select" ref={rootRef}>
      <button type="button" className={`game-account-trigger ${open ? "open" : ""}`} aria-haspopup="listbox" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
        <span><small>Choose Account</small><strong>{selected ? accountText(selected) : "Select an option"}</strong></span>
        <span className="game-account-chevron"><ChevronIcon /></span>
      </button>
      {open ? (
        <ul className="game-account-menu" role="listbox" aria-label="Choose Account">
          {accounts.map((account, index) => (
            <li key={`${accountId(account)}-${index}`}>
              <button type="button" role="option" aria-selected={account === selected} className={account === selected ? "selected" : ""} onClick={() => { onChange(account); setOpen(false); }}>{accountText(account)}</button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

function SkinPicker({ onClose, localSkins, onSelect, currentSkin }: { onClose(): void; localSkins: string[]; onSelect(skin: string): void; currentSkin: string }) {
  const [localSelected, setLocalSelected] = useState<string | null>(currentSkin || null);

  return (
    <div className="skin-picker-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="skin-picker" role="dialog" aria-modal="true" aria-label="Skin Viewer" onMouseDown={(event) => event.stopPropagation()}>
        <header><h2>Skin Viewer</h2><button type="button" aria-label="Close" onClick={onClose}><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" /></svg></button></header>
        {localSkins.length > 0 ? <div className="skin-picker-grid">{localSkins.map((skin) => <SkinThumbnail key={skin} skin={[skin, skin]} selected={localSelected === skin} onSelect={() => setLocalSelected(skin)} skinUrl={skin.endsWith(".zip") ? `/api/skins/thumbnail?file=${encodeURIComponent(skin)}` : `/skins/${skin}`} />)}</div> : <p className="skin-picker-empty">No skins found in resources/skins</p>}
        <footer><button type="button" onClick={onClose}>Cancel</button>{localSelected ? <button className="primary" type="button" onClick={() => { onSelect(localSelected); onClose(); }}>Select</button> : null}</footer>
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
  const navigate = useNavigate();
  const { getAccessToken } = useAuth();
  const { status, send, messages } = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selectedAccount, setSelectedAccount] = useState<Account | null>(null);
  const [nickname, setNickname] = useState("");
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
  const name = searchParams.get("name") ?? "Game Server";
  const realms = kind === "realms";
  const domainRealm = kind === "realm";
  const debugPreview = import.meta.env.DEV && (searchParams.get("debug") === "1" || id.startsWith("debug-"));
  const userId = selectedAccount ? accountId(selectedAccount) : "";

  const showError = useCallback((error: unknown, fallback: string) => {
    notify(error instanceof Error ? error.message : fallback, "error");
  }, [notify]);

  useEffect(() => {
    handledMessageRef.current = messages.at(-1) ?? null;
    accountsRef.current = debugPreview ? [debugAccount] : [];
    setAccounts(accountsRef.current);
    setSelectedAccount(debugPreview ? debugAccount : null);
    setNickname(debugPreview ? "Steve" : "");
    setAddress(debugPreview ? { host: "127.0.0.1", port: 19132 } : { host: "", port: 0 });
    setSkinBytes([]);
    setLaunching(false);
    setJoining(false);
    setRentalPassword("");
    setPasswordPromptOpen(false);
    setPasswordError("");
    addressUserIdRef.current = "";
  }, [debugPreview, id, realms]);

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
    } else {
      const addressType = realms ? "g79_rental_game_address" : "g79_net_game_address";
      const addressPayload = realms ? { game: id, password: searchParams.get("password") ?? "" } : { game: id };
      void Promise.all([
        send("get_accounts", "available-for-mobile"),
        send(addressType, addressPayload),
      ]).catch((error) => showError(error, "Unable to load launch settings."));
    }
  }, [domainRealm, id, realms, searchParams, send, showError, status]);

  const requestRentalAddress = useCallback(async (password: string) => {
    await send("g79_rental_game_address", { game: id, password });
  }, [id, send]);

  useEffect(() => {
    if (!userId || status !== "connected") return;
    void send("g79_get_nickname", userId).catch((error) => showError(error, "Unable to load nickname."));
  }, [send, showError, status, userId]);

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
        const error = gatewayErrorMessage(message.payload);
        if (error) showError(new Error(error), "Unable to load nickname.");
        else if (typeof message.payload === "string") setNickname(message.payload);
        else {
          const payload = recordPayload(message.payload);
          const nicknameValue = payload.nickname ?? payload.name ?? payload.role_name;
          if (nicknameValue !== undefined) setNickname(String(nicknameValue));
        }
      } else if (realms && (
        message.type === "g79_rental_game_password"
        || (searchParams.has("password") && ["error_notification", "error_notification_back", "error_notification_rt"].includes(message.type))
      )) {
        const error = gatewayErrorMessage(message.payload)
          || (typeof message.payload === "string" ? message.payload : "请输入租赁服务器密码。");
        setPasswordError(error);
        setPasswordPromptOpen(true);
      } else if (message.type === (realms ? "g79_rental_game_address" : "g79_net_game_address")) {
        const responseError = gatewayErrorMessage(message.payload);
        if (responseError) {
          showError(new Error(responseError), "Unable to load server address.");
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
        setAddress({
          host: String(realms ? payload.mcserver_host ?? "" : payload.host ?? ""),
          port: Number(realms ? payload.mcserver_port ?? 0 : payload.port ?? 0),
        });
      } else if (message.type === "g79_query_skin" || message.type === "g79_modify_skin") {
        setSkinBytes(decodeSkin(message.payload));
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
      } else if (!realms && message.type === "g79_launch_game_done" && (String(message.payload) === id || String(message.payload) === `${id}:DomainGame`)) {
        setLaunching(false);
      } else if (!realms && message.type === "g79_join_game_done" && String(message.payload) === id) {
        setJoining(false);
      }
    }
    handledMessageRef.current = messages.at(-1) ?? null;
  }, [id, messages, realms, searchParams, showError]);

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
      showError(error, "Unable to verify rental server password.");
    }
  };

  const setServerNickname = async () => {
    try {
      await send("g79_set_nickname", { id: userId, new: nickname });
    } catch (error) {
      showError(error, "Unable to update nickname.");
    }
  };

	const sendLaunch = async (interceptor: boolean) => {
		if (!realms) interceptor ? setJoining(true) : setLaunching(true);
		try {
			if (!address.host.trim() || !Number.isInteger(address.port) || address.port <= 0 || address.port > 65535) {
				throw new Error("The server has not provided a valid connection address. Please try again later.");
			}
			const accessToken = await getAccessToken();
      const gatewaySettings = await loadGatewaySettings();
      const pePath = gatewaySettings.peLaunchPath;
      const hasCustomPath = pePath.trim().length > 0;
      const itemIds = domainRealm ? readRealmModItemIds(id) : [];
      const payload: Record<string, unknown> = {
        game_name: name,
        game_id: id,
        role_name: nickname,
        user_id: userId,
        ...(interceptor ? {} : { client_type: 2, launch_type: hasCustomPath ? 0 : 1, ...(hasCustomPath ? { launch_path: pePath } : {}) }),
        game_type: realms || domainRealm ? 8 : 2,
        ...(domainRealm ? { is_domain_game: true } : {}),
        access_token: accessToken,
        server_ip: address.host,
        server_port: address.port,
        skin_path: "",
        ...(itemIds.length > 0 ? { item_ids: itemIds } : {}),
      };
      const doLaunch = () => {
        void send(interceptor ? "g79_join_game" : "g79_launch_game", payload).catch((error) => {
          if (!realms) interceptor ? setJoining(false) : setLaunching(false);
          showError(error, interceptor ? "Unable to launch interceptor." : "Unable to launch game.");
        });
      };
      if (domainRealm && itemIds.length > 0) {
        pendingLaunchRef.current = doLaunch;
        await send("realm_mod_download", { id: userId, sid: id, item_ids: itemIds });
        return;
      }
      doLaunch();
    } catch (error) {
      if (!realms) interceptor ? setJoining(false) : setLaunching(false);
      pendingLaunchRef.current = null;
      showError(error, interceptor ? "Unable to launch interceptor." : "Unable to launch game.");
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

  return (
    <div className="game-launch-page">
      <header className="game-launch-heading">
        <button type="button" aria-label="Back" onClick={() => navigate(-1)}><BackIcon /></button>
        <div><h1>{name}</h1><p>Server ID: {id}</p></div>
      </header>
      <div className="game-launch-grid">
        <section className="game-skin-preview" aria-label="Character skin preview">
          <SkinPreview skinBytes={skinBytes} fallbackSkinUrl={undefined} />
          <button className="game-skin-edit" type="button" aria-label="Edit skin" onClick={() => setSkinPickerOpen(true)}><EditIcon /></button>
        </section>
        <div className="game-launch-panel">
          <section className="game-launch-card game-launch-settings">
            <header><h2>Settings</h2><p>Choose your account and character to play.</p></header>
            <AccountSelector accounts={accounts} selected={selectedAccount} onChange={setSelectedAccount} />
            <div className="game-nickname-field">
              <label htmlFor="game-nickname"><small>Nick Name</small><input id="game-nickname" type="text" value={nickname} onChange={(event) => setNickname(event.target.value)} /></label>
              <div><button type="button" aria-label="Generate random nickname" onClick={() => setNickname(generateRandomNickname())}><ShuffleIcon /></button><button type="button" onClick={() => void setServerNickname()}>Enter</button></div>
            </div>
          </section>
          <section className="game-launch-card game-launch-actions">
            <button className="primary" type="button" disabled={launching} onClick={() => void sendLaunch(false)}>{launching ? <span><Spinner />Launching...</span> : "Launch Game"}</button>
          </section>
          <section className="game-launch-info"><dl><div><dt>Address:</dt><dd>{address.host}:{address.port}</dd></div><div><dt>Server Type:</dt><dd>{domainRealm ? "DomainGame" : (realms ? "RentalGame" : "NetGames")}</dd></div></dl></section>
        </div>
      </div>
      {skinPickerOpen ? <SkinPicker localSkins={localSkins} currentSkin={selectedSkin} onClose={() => setSkinPickerOpen(false)} onSelect={selectLocalSkin} /> : null}
      {passwordPromptOpen ? (
        <div className="java-join-backdrop" role="presentation">
          <section className="java-join-modal" role="dialog" aria-modal="true" aria-labelledby="rental-password-title">
            <header>
              <h2 id="rental-password-title">服务器密码</h2>
              <button type="button" aria-label="Close" onClick={() => setPasswordPromptOpen(false)}>
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 6.41L17.59 5L12 10.59L6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 19 19 20.41 17.59 13.41 12z" fill="currentColor" /></svg>
              </button>
            </header>
            <form className="java-password-body" onSubmit={(event) => { event.preventDefault(); void submitRentalPassword(); }}>
              <label htmlFor="rental-server-password">该租赁服务器需要密码</label>
              <input id="rental-server-password" type="password" value={rentalPassword} onChange={(event) => setRentalPassword(event.target.value)} autoFocus autoComplete="off" placeholder="请输入服务器密码" />
              {passwordError ? <p>{passwordError}</p> : null}
              <button type="submit">确认</button>
            </form>
          </section>
        </div>
      ) : null}
      {modTask ? (
        <div className="java-join-backdrop" role="presentation">
          <section className="java-join-modal" role="dialog" aria-modal="true" aria-label="Mod Download">
            <header>
              <h2>模组下载</h2>
              <button type="button" aria-label="Close" onClick={() => { setModTask(null); modDismissedRef.current = true; }}>
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor" /></svg>
              </button>
            </header>
            <div className="java-password-body realm-mod-body">
              <p className="realm-mod-line">{modTask.message}{modTask.modName ? `（${modTask.modName}）` : ""}<span>{modTask.done}/{modTask.total}</span></p>
              <i className="realm-mod-bar"><b style={{ width: `${Math.min(Math.max((modTask.done / Math.max(modTask.total, 1)) * 100, 0), 100)}%` }} /></i>
              <small className="realm-mod-hint">关闭弹窗不会取消下载，可在 Games 中取消</small>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  );
}
