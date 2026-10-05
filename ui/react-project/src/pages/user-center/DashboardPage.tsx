import { useEffect, useRef, useState } from "react";
import { useGateway, useToasts } from "../../context/AppContext";
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

type AccountTab = "cookie" | "email" | "4399pc";

const accountTabs: Array<{ label: string; value: AccountTab }> = [
  { label: "4399", value: "4399pc" },
  { label: "网易邮箱", value: "email" },
  { label: "Cookie", value: "cookie" },
];

export function AccountsPage() {
  return <DashboardPage accountsOnly />;
}

export function DashboardPage({ accountsOnly = false }: { accountsOnly?: boolean }) {
  const { gateway, items: accountItems, loading } = useGatewayList<GatewayAccount>("get_accounts");
  const { notify } = useToasts();
  const [loginPlatform, setLoginPlatform] = useState<"pc" | "pe" | null>(null);
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
    });
  };

  const randomLogin = async () => {
    const account = accounts[0];
    if (!account) {
      notify("暂无可登录账号", "info");
      return;
    }
    await toggleAccount(account);
  };

  return (
    <div className={`page-account${accountsOnly ? " page-account-only" : ""}`}>
      <header className="account-header">
        <div>
          <h2>账号管理</h2>
          <p className="page-desc">管理您的游戏账号</p>
        </div>
        <div className="account-actions">
          <button className="btn-secondary" type="button" onClick={() => void randomLogin()}>随机登录</button>
          <button className="btn-accent" type="button" onClick={() => setLoginPlatform("pc")}>添加账号(PC)</button>
          <button className="btn-accent" type="button" onClick={() => setLoginPlatform("pe")}>添加账号(PE)</button>
        </div>
      </header>

      {loading ? <div className="account-loading">加载中...</div> : accounts.length ? (
        <div className="account-table">
          <div className="account-row account-header-row" aria-hidden="true">
            <div className="account-cell cell-id">账号ID</div><div className="account-cell cell-status">状态</div><div className="account-cell cell-type">登录方式</div><div className="account-cell cell-platform">平台</div><div className="account-cell cell-alias">备注</div><div className="account-cell cell-actions">操作</div>
          </div>
          {accounts.map((account, index) => (
            <AccountRow account={account} key={`${account.id}-${account.platform}-${index}`} onToggle={() => toggleAccount(account)} />
          ))}
        </div>
      ) : <div className="account-empty"><div className="account-empty-title">暂无账号</div><div className="account-empty-desc">点击上方按钮添加您的第一个账号</div></div>}

      {loginPlatform ? <AccountLoginModal platform={loginPlatform} onClose={() => setLoginPlatform(null)} /> : null}
    </div>
  );
}

function AccountRow({ account, onToggle }: { account: GatewayAccount; onToggle(): Promise<void> }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [alias, setAlias] = useState(account.alias || "");
  const [connecting, setConnecting] = useState(false);
  const connectingRef = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  useEffect(() => setAlias(account.alias || ""), [account.alias]);
  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    if (pending.some((message) => message.type === "login/success" && String(message.payload) === account.id)) {
      setConnecting(false);
      connectingRef.current = false;
      notify("登录成功", "success", 4_000);
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
      notify(error instanceof Error ? error.message : "登录失败", "error");
    }
  };

  const saveAlias = () => {
    void gateway.send("update_user_alias", { id: account.id, platform: account.platform, alias });
  };

  return (
    <div className="account-row">
      <div className="account-cell cell-id">
        <span title={account.id}>{account.id}</span>
      </div>
      <div className="account-cell cell-status"><span className={`status-badge ${account.authorized ? "status-online" : "status-offline"}`}>{account.authorized ? "Online" : "Offline"}</span></div>
      <div className="account-cell cell-type">{account.type || account.channel}</div>
      <div className="account-cell cell-platform"><span className={`platform-badge ${account.platform === 1 ? "platform-pe" : "platform-pc"}`}>{account.platform === 1 ? "PE" : "PC"}</span></div>
      <div className="account-cell cell-alias"><input className="alias-input" type="text" value={alias} onChange={(event) => setAlias(event.target.value)} onBlur={saveAlias} placeholder="添加备注..." aria-label={`账号 ${account.id} 的备注`} /></div>
      <div className="account-cell cell-actions">
        <button className={account.authorized ? "btn-secondary btn-sm" : "btn-accent btn-sm"} type="button" disabled={connecting} onClick={() => void toggle()}>{account.authorized ? "注销" : "登录"}</button>
        <button className="btn-danger btn-sm" type="button" onClick={() => { if (window.confirm("确定要删除此账号吗？")) void gateway.send("delete_user", { id: account.id, platform: account.platform }); }}>删除</button>
      </div>
    </div>
  );
}

export function AccountLoginModal({ platform = "pc", onClose }: { platform?: "pc" | "pe"; onClose(): void }) {
  const gateway = useGateway();
  const [tab, setTab] = useState<AccountTab>(() => accountTabs[Number(sessionStorage.getItem("X-ACTIVE-TAB") || 0)]?.value || "4399pc");
  const [cookie, setCookie] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pcUser, setPcUser] = useState("");
  const [pcPassword, setPcPassword] = useState("");
  const [captchaId, setCaptchaId] = useState<string | null>(null);
  const [captcha, setCaptcha] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

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

  const sendNeteaseLogin = async (type: "cookie" | "password", details: string, platform: number) => {
    setBusy(true);
    setError("");
    try {
      await gateway.send("login", { channel: "netease", type, details, platform });
    } catch (sendError) {
      setBusy(false);
      setError(sendError instanceof Error ? sendError.message : "登录失败。");
    }
  };

  const login = async (platform: number) => {
    if (tab === "cookie") {
      if (!cookie.trim()) return setError("请输入 Cookie");
      return sendNeteaseLogin("cookie", cookie, platform);
    }
    if (tab === "email") {
      if (!email.trim() || !password.trim()) return setError("请输入邮箱和密码");
      return sendNeteaseLogin("password", JSON.stringify({ account: email, password }), platform);
    }
    if (!pcUser.trim() || !pcPassword.trim()) return setError("请输入用户名和密码");
    setBusy(true);
    setError("");
    try {
      await gateway.send("login", {
        channel: "4399pc",
        type: "password",
        details: JSON.stringify({ account: pcUser, password: pcPassword, captcha_identifier: captchaId, captcha: captcha || null }),
        platform,
      });
    } catch (sendError) {
      setBusy(false);
      setError(sendError instanceof Error ? sendError.message : "登录失败，请检查用户名和密码。");
    }
  };

  const primaryPlatform = platform === "pe" ? 1 : 0;

  return (
    <div className="dialog-overlay" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="dialog-box" role="dialog" aria-modal="true" aria-labelledby="account-dialog-title">
        <div className="dialog-header">
          <h3 id="account-dialog-title">添加账号{platform === "pe" ? "(PE)" : ""}</h3>
          <button className="dialog-close" type="button" onClick={onClose} aria-label="关闭弹窗">×</button>
        </div>
        <div className="dialog-body">
          <div className="dialog-tabs" role="tablist" aria-label="登录方式">
            {accountTabs.map((item, index) => <button className={`dialog-tab${tab === item.value ? " active" : ""}`} type="button" role="tab" aria-selected={tab === item.value} key={item.value} onClick={() => { setTab(item.value); setError(""); sessionStorage.setItem("X-ACTIVE-TAB", String(index)); }}>{item.label}</button>)}
          </div>
          {tab === "cookie" ? <textarea value={cookie} onChange={(event) => { setCookie(event.target.value); setError(""); }} placeholder="粘贴 Cookie 内容..." disabled={busy} /> : null}
          {tab === "email" ? <><div className="form-group"><input type="email" value={email} onChange={(event) => { setEmail(event.target.value); setError(""); }} placeholder="邮箱地址" autoComplete="email" disabled={busy} /></div><div className="form-group"><input type="password" value={password} onChange={(event) => { setPassword(event.target.value); setError(""); }} placeholder="密码" autoComplete="current-password" disabled={busy} /></div></> : null}
          {tab === "4399pc" ? <><div className="form-group"><input type="text" value={pcUser} onChange={(event) => { setPcUser(event.target.value); setError(""); }} placeholder="用户名" autoComplete="username" disabled={busy} /></div><div className="form-group"><input type="password" value={pcPassword} onChange={(event) => { setPcPassword(event.target.value); setError(""); }} placeholder="密码" autoComplete="current-password" disabled={busy} /></div>{captchaId ? <div className="captcha-field"><input type="text" value={captcha} onChange={(event) => setCaptcha(event.target.value)} placeholder="图形验证码" autoComplete="off" /><img src={`https://ptlogin.4399.com/ptlogin/captcha.do?captchaId=${captchaId}`} onClick={() => setCaptchaId(randomCaptchaId())} alt="图形验证码" title="点击刷新" /></div> : null}</> : null}
          {error ? <p className="dialog-error">{error}</p> : null}
        </div>
        <div className="dialog-footer">
          <button className="btn-secondary" type="button" onClick={onClose}>取消</button>
          <button className="btn-accent" type="button" disabled={busy} onClick={() => void login(primaryPlatform)}>{busy ? "登录中…" : "登录"}</button>
        </div>
      </section>
    </div>
  );
}

function randomCaptchaId(): string {
  return Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
}

