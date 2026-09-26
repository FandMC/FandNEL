import {
  Laptop,
  LoaderCircle,
  Monitor,
  Pencil,
  Plus,
  Power,
  Settings,
  Smartphone,
  Trash2,
  X,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth, useGateway, useToasts } from "../../context/AppContext";
import { consumeGatewayMessages, parseGatewayPayload, useGatewayList } from "./gatewayData";
import type { GatewayMessage } from "../../types";

interface GatewayAccount {
  alias: string;
  authorized: boolean;
  channel: string;
  id: string;
  platform: 0 | 1 | 2;
  type: string;
}

interface LoginResponse {
  code: number;
  message: string;
}

type AccountTab = "cookie" | "email" | "sms" | "4399pc";
type AccountFilter = "all" | "online" | "offline";

const accountTabs: Array<{ label: string; value: AccountTab }> = [
  { label: "Cookie", value: "cookie" },
  { label: "Email", value: "email" },
  { label: "SMS", value: "sms" },
  { label: "Pc4399", value: "4399pc" },
];

export function DashboardPage() {
  const { gateway, items: accountItems, loading } = useGatewayList<GatewayAccount>("get_accounts");
  const { user, getAccessToken } = useAuth();
  const { notify } = useToasts();
  const navigate = useNavigate();
  const [loginOpen, setLoginOpen] = useState(false);
  const [accountFilter, setAccountFilter] = useState<AccountFilter>("all");
  const accounts = [...new Map(accountItems.map((account) => [account.id, account])).values()];

  const toggleAccount = async (account: GatewayAccount) => {
    if (account.authorized) {
      await gateway.send("user_inactive", account.id);
      return;
    }
    await gateway.send("login", {
      channel: "active",
      type: "",
      details: account.id,
      platform: account.platform,
      token: await getAccessToken(),
    });
  };

  const filteredAccounts = accounts.filter((account) => {
    if (accountFilter === "online") return account.authorized;
    if (accountFilter === "offline") return !account.authorized;
    return true;
  });

  return (
    <div className="workspace-page dashboard-page dashboard-v253">
      <section className="dashboard-v253-header">
        <div>
          <h1>Dashboard</h1>
          <p>Welcome back, <strong>{user?.username || user?.displayName || ""}</strong></p>
        </div>
        <div className="dashboard-v253-header-actions">
          <button type="button" onClick={() => navigate("/user-center/settings")}><Settings />Settings</button>
        </div>
      </section>

      <section className="dashboard-main-grid">
        <div className="dashboard-accounts-column">
          <header className="dashboard-accounts-heading">
            <div>
              <h2>Game Accounts</h2>
              <p>Manage connected accounts</p>
            </div>
            <button type="button" onClick={() => setLoginOpen(true)}><Plus />Add Account</button>
          </header>

          <div className="dashboard-account-card">
            <header>
              <strong>Accounts ({accounts.length})</strong>
              <div className="dashboard-account-filters">
                {(["all", "online", "offline"] as AccountFilter[]).map((filter) => (
                  <button className={accountFilter === filter ? "active" : ""} type="button" key={filter} onClick={() => setAccountFilter(filter)}>{filter}</button>
                ))}
              </div>
            </header>
            {loading ? <div className="dashboard-account-loading" /> : filteredAccounts.length ? (
              <div className="dashboard-account-list">
                {filteredAccounts.map((account, index) => (
                  <AccountRow
                    account={account}
                    key={`${account.id}-${account.platform}-${index}`}
                    onToggle={() => toggleAccount(account)}
                  />
                ))}
              </div>
            ) : <div className="dashboard-account-empty">No accounts found in this category.</div>}
          </div>
        </div>

      </section>

      {loginOpen ? <AccountLoginModal onClose={() => setLoginOpen(false)} /> : null}
    </div>
  );
}

function PlatformIcon({ platform }: { platform: number }) {
  if (platform === 1) return <Smartphone />;
  if (platform === 2) return <Laptop />;
  return <Monitor />;
}

function AccountRow({ account, onToggle }: { account: GatewayAccount; onToggle(): Promise<void> }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [alias, setAlias] = useState(account.alias || "");
  const [editing, setEditing] = useState(false);
  const [connecting, setConnecting] = useState(false);
  const connectingRef = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => setAlias(account.alias || ""), [account.alias]);
  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    if (pending.some((message) => message.type === "login/success" && String(message.payload) === account.id)) {
      setConnecting(false);
      connectingRef.current = false;
      notify("Login successful", "success", 4_000);
      const audio = document.querySelector<HTMLAudioElement>('audio[src="/notify.mp3"]');
      if (audio) {
        audio.currentTime = 0;
        void audio.play().catch(() => undefined);
      }
    }
  }, [account.id, gateway.messages, notify]);
  useEffect(() => {
    if (!connecting) return;
    const timer = window.setTimeout(() => {
      connectingRef.current = false;
      setConnecting(false);
    }, 15_000);
    return () => window.clearTimeout(timer);
  }, [connecting]);

  const toggle = async () => {
    if (connectingRef.current) return;
    if (!account.authorized) {
      connectingRef.current = true;
      setConnecting(true);
    }
    try {
      await onToggle();
    } catch (error) {
      setConnecting(false);
      connectingRef.current = false;
      notify(error instanceof Error ? error.message : "Login failed", "error");
    }
  };

  const saveAlias = () => {
    void gateway.send("update_user_alias", { id: account.id, platform: account.platform, alias });
    setEditing(false);
  };

  return (
    <div className="dashboard-account-row" role="button" tabIndex={0} onClick={() => void toggle()} onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") void toggle(); }}>
      <div className="dashboard-account-identity">
        <span className="dashboard-platform-icon"><PlatformIcon platform={account.platform} /></span>
        <div>
          <div className="dashboard-account-name">
            {editing ? <input autoFocus type="text" value={alias} onClick={(event) => event.stopPropagation()} onChange={(event) => setAlias(event.target.value)} onBlur={saveAlias} onKeyDown={(event) => { if (event.key === "Enter") saveAlias(); }} /> : <strong title={alias || "No Alias"}>{alias || account.id}</strong>}
            {!editing ? <button type="button" onClick={(event) => { event.stopPropagation(); setEditing(true); }} aria-label="Edit alias" title="Edit alias"><Pencil /></button> : null}
          </div>
          <small>{account.channel} &bull; {account.id} &bull; {account.type}</small>
        </div>
      </div>
      <div className="dashboard-account-actions">
        <span className={account.authorized ? "online" : "offline"}>{account.authorized ? "Online" : "Offline"}</span>
        <div>
          <button type="button" disabled={connecting} onClick={(event) => { event.stopPropagation(); void toggle(); }} aria-label={account.authorized ? "Disconnect" : "Connect"} title={account.authorized ? "Disconnect" : "Connect"}>{connecting ? <LoaderCircle className="spin" /> : <Power />}</button>
          <button type="button" onClick={(event) => { event.stopPropagation(); if (window.confirm("Are you sure you want to delete this account?")) void gateway.send("delete_user", { id: account.id, platform: account.platform }); }} aria-label="Delete account" title="Delete Account"><Trash2 /></button>
        </div>
      </div>
    </div>
  );
}

export function AccountLoginModal({ onClose }: { onClose(): void }) {
  const gateway = useGateway();
  const { getAccessToken } = useAuth();
  const [tab, setTab] = useState<AccountTab>(() => accountTabs[Number(sessionStorage.getItem("X-ACTIVE-TAB") || 0)]?.value || "cookie");
  const [cookie, setCookie] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [pcUser, setPcUser] = useState("");
  const [pcPassword, setPcPassword] = useState("");
  const [captchaId, setCaptchaId] = useState<string | null>(null);
  const [captcha, setCaptcha] = useState("");
  const [sendingCode, setSendingCode] = useState(false);
  const [countdown, setCountdown] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => {
    if (countdown <= 0) return;
    const timer = window.setTimeout(() => setCountdown((value) => value - 1), 1_000);
    return () => window.clearTimeout(timer);
  }, [countdown]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      if (message.type === "register_4399") {
        const credentials = parseGatewayPayload<{ account: string; password: string }>(message.payload);
        setPcUser(credentials.account);
        setPcPassword(credentials.password);
        setBusy(false);
      }
      if (message.type !== "login") continue;
      const response = parseGatewayPayload<LoginResponse>(message.payload);
      setBusy(false);
      setSendingCode(false);
      if (response.code === 36) {
        setCountdown(60);
        setError("");
        continue;
      }
      if (response.code === 1002) {
        const verification = parseGatewayPayload<{ reason: string; verify_url: string }>(response.message);
        setError(verification.reason);
        window.open(verification.verify_url, "_blank");
        continue;
      }
      if (response.code !== 0) {
        if (tab === "4399pc" && response.message.startsWith("^Captcha required^")) {
          setCaptchaId(randomCaptchaId());
          setError("");
        } else {
          setError(response.message);
        }
        continue;
      }
      setError("");
      onClose();
    }
  }, [gateway.messages, onClose, tab]);

  const sendNeteaseLogin = async (type: "cookie" | "password" | "sms", details: string, platform: number) => {
    setBusy(true);
    setError("");
    try {
      await gateway.send("login", { channel: "netease", type, details, platform, token: await getAccessToken() });
    } catch (sendError) {
      setBusy(false);
      setError(sendError instanceof Error ? sendError.message : "Login failed.");
    }
  };

  const login = async (platform: number) => {
    if (tab === "cookie") {
      if (!cookie.trim()) return setError("Please enter a cookie");
      return sendNeteaseLogin("cookie", cookie, platform);
    }
    if (tab === "email") {
      if (!email.trim() || !password.trim()) return setError("Please enter both email and password");
      return sendNeteaseLogin("password", JSON.stringify({ account: email, password }), platform);
    }
    if (tab === "sms") {
      if (!phone.trim()) return setError("Please enter a phone number");
      if (!code.trim()) return setError("Please enter the verification code");
      return sendNeteaseLogin("sms", JSON.stringify({ phone, code }), platform);
    }
    if (!pcUser.trim() || !pcPassword.trim()) return setError("Please enter both username and password");
    setBusy(true);
    setError("");
    try {
      await gateway.send("login", {
        channel: "4399pc",
        type: "password",
        details: JSON.stringify({ account: pcUser, password: pcPassword, captcha_identifier: captchaId, captcha: captcha || null }),
        platform,
        token: await getAccessToken(),
      });
    } catch (sendError) {
      setBusy(false);
      setError(sendError instanceof Error ? sendError.message : "Login failed. Please check your username and password.");
    }
  };

  const sendCode = async () => {
    if (!phone.trim()) return setError("Please enter a phone number");
    setSendingCode(true);
    setError("");
    try {
      await gateway.send("login", { channel: "send_code", type: "", details: phone });
    } catch (sendError) {
      setSendingCode(false);
      setError(sendError instanceof Error ? sendError.message : "Failed to send verification code. Please try again.");
    }
  };

  return (
    <ModalFrame className="account-login-modal" onClose={onClose}>
      <div className="account-login-tabs">
        <div>{accountTabs.map((item, index) => <button className={tab === item.value ? "active" : ""} type="button" key={item.value} onClick={() => { setTab(item.value); setError(""); sessionStorage.setItem("X-ACTIVE-TAB", String(index)); }}>{item.label}</button>)}</div>
        <button className="modal-close" type="button" onClick={onClose} aria-label="Close modal"><X /></button>
      </div>
      <div className="account-login-body">
        {tab === "cookie" ? <><textarea value={cookie} onChange={(event) => { setCookie(event.target.value); setError(""); }} placeholder="Paste your cookie here..." disabled={busy} /><p className="account-login-note"><b>Note:</b> The 4399 cookie from the Desktop version cannot be used to log in on Mobile.</p></> : null}
        {tab === "email" ? <><input type="email" value={email} onChange={(event) => { setEmail(event.target.value); setError(""); }} placeholder="Email address" autoComplete="email" disabled={busy} /><input type="password" value={password} onChange={(event) => { setPassword(event.target.value); setError(""); }} placeholder="Password" autoComplete="current-password" disabled={busy} /></> : null}
        {tab === "sms" ? <><div className="sms-field"><input type="tel" value={phone} onChange={(event) => { setPhone(event.target.value); setError(""); }} placeholder="Phone number" autoComplete="tel" disabled={busy} /><button type="button" disabled={sendingCode || countdown > 0 || busy} onClick={() => void sendCode()}>{sendingCode ? "Sending..." : countdown > 0 ? `${countdown}s` : "Send Code"}</button></div><input type="text" value={code} onChange={(event) => { setCode(event.target.value); setError(""); }} placeholder="Verification code" autoComplete="one-time-code" disabled={busy} /></> : null}
        {tab === "4399pc" ? <><input type="text" value={pcUser} onChange={(event) => { setPcUser(event.target.value); setError(""); }} placeholder="Username" autoComplete="username" disabled={busy} /><input type="password" value={pcPassword} onChange={(event) => { setPcPassword(event.target.value); setError(""); }} placeholder="Password" autoComplete="current-password" disabled={busy} />{captchaId ? <div className="captcha-field"><input type="text" value={captcha} onChange={(event) => setCaptcha(event.target.value)} placeholder="Captcha" autoComplete="off" /><img src={`https://ptlogin.4399.com/ptlogin/captcha.do?captchaId=${captchaId}`} onClick={() => setCaptchaId(randomCaptchaId())} alt="Captcha" title="Click to refresh" /></div> : null}</> : null}
        {error ? <p className="account-login-error">{error}</p> : null}
        {tab === "4399pc" ? <div className="pc-login-actions"><button type="button" disabled={busy} onClick={() => void login(1)}>Mobile Login</button><button type="button" disabled={busy} onClick={() => void login(0)}>Desktop Login</button><button className="primary" type="button" disabled={busy} onClick={() => void login(2)}>{busy ? "Login..." : "Mixed Login"}</button><button className="register" type="button" disabled={busy} onClick={() => { setBusy(true); void gateway.send("register_4399").catch(() => setBusy(false)); }}>One-Click Auto Register</button></div> : <div className="netease-login-actions"><button type="button" disabled={busy} onClick={() => void login(1)}>{busy ? "Logging in..." : "Login Mobile"}</button><button className="primary" type="button" disabled={busy} onClick={() => void login(0)}>{busy ? "Logging in..." : "Login Desktop"}</button></div>}
      </div>
    </ModalFrame>
  );
}

function randomCaptchaId(): string {
  return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
}

function ModalFrame({ children, className = "", onClose }: { children: React.ReactNode; className?: string; onClose(): void }) {
  void onClose;
  return <div className="legacy-modal-backdrop" role="presentation"><section className={`legacy-modal ${className}`} role="dialog" aria-modal="true">{children}</section></div>;
}

