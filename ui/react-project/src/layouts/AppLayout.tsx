import {
  Bell,
  Check,
  Info,
  Shield,
  TriangleAlert,
  UserRound,
  X,
  House,
  Server,
  Cloud,
  Shirt,
  Gamepad2,
  Settings,
} from "lucide-react";
import { ReactNode, useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { Navigate, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useGateway, useToasts } from "../context/AppContext";
import { consumeGatewayMessages } from "../pages/user-center/gatewayData";
import type { GatewayMessage, ToastMessage } from "../types";

type WindowAction = "window:drag" | "window:maximize" | "window:minimize" | "window:close";
type NativeWindowBridge = { sendMessage?(message: string): void };

export function AppLayout() {
  const { notify } = useToasts();
  const nativeBridge = window.external as unknown as NativeWindowBridge | undefined;
  const hasWindowControls = typeof nativeBridge?.sendMessage === "function";

  const sendWindowAction = (action: WindowAction) => {
    try {
      nativeBridge?.sendMessage?.(JSON.stringify({ action }));
    } catch {
      notify("窗口操作失败。", "error");
    }
  };

  return (
    <div className="app">
      <header className="titlebar" aria-label="FandNEL">
        <div
          className="titlebar-drag"
          onMouseDown={(event) => {
            if (hasWindowControls && event.button === 0 && event.detail === 1) sendWindowAction("window:drag");
          }}
          onDoubleClick={() => { if (hasWindowControls) sendWindowAction("window:maximize"); }}
        >
          <div className="titlebar-left" inert>
            <svg className="titlebar-logo" width="22" height="22" viewBox="0 0 200 200" fill="none" aria-hidden="true">
              <path
                d="M139.977 53.454C130.158 56.794 125.43 62.784 122.21 72.112c-.831 2.492-2.91 5.248-5.247 7.103l8.052 8.799-25.56-18.234-70.549-50.249s5.091 33.711 6.857 46.115c1.247 8.745 3.377 12.667 10.131 16.644l14.339 7.738-6.91-3.657 31.898 17.756-.208.478-34.339-16.22c1.818 6.361 5.35 18.605 6.857 24.012 1.61 5.83 3.429 7.95 8.988 10.018l10.234 3.816 6.338-2.544-8.052 5.46-40.262 52.103c26.754-25.336 49.405-34.347 65.977-41.715 21.144-9.329 33.871-15.318 42.184-36.839 5.922-15.106 10.545-34.452 16.417-41.927l12.52-16.324s-25.924 6.995-31.898 9.01Z"
                fill="#086DA4"
              />
            </svg>
            <span className="titlebar-title">FandNEL</span>
          </div>
        </div>
        {hasWindowControls ? (
          <div className="titlebar-controls">
            <button type="button" className="tb-btn tb-close" onClick={() => sendWindowAction("window:close")} title="关闭" aria-label="关闭窗口">
              <svg className="tb-icon" width="8" height="8" viewBox="0 0 8 8" aria-hidden="true"><line x1="1" y1="1" x2="7" y2="7" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" /><line x1="7" y1="1" x2="1" y2="7" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" /></svg>
            </button>
            <button type="button" className="tb-btn tb-minimize" onClick={() => sendWindowAction("window:minimize")} title="最小化" aria-label="最小化窗口">
              <svg className="tb-icon" width="8" height="2" viewBox="0 0 8 2" aria-hidden="true"><rect width="8" height="1.5" rx="0.75" fill="currentColor" /></svg>
            </button>
            <button type="button" className="tb-btn tb-maximize" onClick={() => sendWindowAction("window:maximize")} title="最大化或还原" aria-label="最大化或还原窗口">
              <svg className="tb-icon" width="8" height="8" viewBox="0 0 8 8" aria-hidden="true"><path d="M1 3L4 0.5 7 3M1 5L4 7.5 7 5" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round" /></svg>
            </button>
          </div>
        ) : null}
      </header>
      <Outlet />
      <ToastViewport />
    </div>
  );
}

const userCenterLinks: Array<
  | { to: string; label: string; Icon: typeof House }
  | { divider: true }
> = [
  { to: "/user-center", label: "概括", Icon: House },
  { divider: true },
  { to: "/user-center/servers", label: "网络服务器", Icon: Server },
  { to: "/user-center/rentals", label: "租赁服务器", Icon: Cloud },
  { divider: true },
  { to: "/user-center/bedrock", label: "网络服务器(PE)", Icon: Server },
  { to: "/user-center/rentals-for-bedrock", label: "租赁服务器(PE)", Icon: Cloud },
  { divider: true },
  { to: "/user-center/launchers", label: "游戏会话", Icon: Gamepad2 },
  { to: "/user-center/java-skins", label: "皮肤", Icon: Shirt },
  { divider: true },
  { to: "/user-center/settings", label: "设置", Icon: Settings },
];

export function UserCenterLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const gateway = useGateway();
  const { notify } = useToasts();
  const [captchaRequest, setCaptchaRequest] = useState<{ accountId: string; error: string } | null>(null);
  const lastCaptchaMessageRef = useRef(gateway.messages.at(-1));
  const lastNotificationMessageRef = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const notificationAudioRef = useRef<HTMLAudioElement>(null);
  const sidebarRef = useRef<HTMLElement>(null);
  const indicatorRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLElement>(null);

  useLayoutEffect(() => {
    contentRef.current?.scrollTo({ top: 0 });
  }, [location.pathname, location.search]);

  const updateIndicator = useCallback(() => {
    const sidebar = sidebarRef.current;
    const indicator = indicatorRef.current;
    const active = sidebar?.querySelector<HTMLElement>(".nav-item.active");
    if (!sidebar || !indicator) return;
    if (!active) {
      indicator.style.opacity = "0";
      return;
    }
    const sidebarRect = sidebar.getBoundingClientRect();
    const itemRect = active.getBoundingClientRect();
    indicator.style.top = `${itemRect.top - sidebarRect.top + sidebar.scrollTop + 8}px`;
    indicator.style.height = `${itemRect.height - 16}px`;
    indicator.style.opacity = "1";
  }, []);

  useEffect(() => {
    updateIndicator();
    window.addEventListener("resize", updateIndicator);
    const observer = new ResizeObserver(updateIndicator);
    if (sidebarRef.current) observer.observe(sidebarRef.current);
    return () => {
      window.removeEventListener("resize", updateIndicator);
      observer.disconnect();
    };
  }, [location.pathname, updateIndicator]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, lastNotificationMessageRef);
    const errorTypes = ["error_notification", "error_notification_back", "error_notification_rb", "error_notification_rt"];
    for (const message of pending) {
      if (typeof message.payload !== "string") continue;
      if (message.type !== "success_notification" && !errorTypes.includes(message.type)) continue;
      // Rental address errors are handled by the launch page so it can ask
      // for the password instead of navigating away from the page.
      if (
        location.pathname === "/user-center/rentals-for-bedrock/launch"
        && new URLSearchParams(location.search).has("password")
        && ["error_notification", "error_notification_back", "error_notification_rt"].includes(message.type)
      ) continue;
      if (
        location.pathname === "/user-center/rentals-for-bedrock"
        && ["error_notification", "error_notification_back", "error_notification_rt"].includes(message.type)
        && /密码|password|passwd|pwd/i.test(String(message.payload ?? ""))
      ) continue;
      const audio = notificationAudioRef.current;
      if (audio) {
        audio.currentTime = 0;
        void audio.play().catch(() => undefined);
      }
      const success = message.type === "success_notification";
      notify(message.payload, success ? "success" : "error", success ? 4_000 : 2_000);
      if (message.type === "error_notification_back") navigate(-1);
      if (message.type === "error_notification_rb") navigate("/user-center");
      if (message.type === "error_notification_rt") navigate("/user-center/rentals");
    }
  }, [gateway.messages, location.pathname, location.search, navigate, notify]);
  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, lastCaptchaMessageRef);
    for (const message of pending) {
      if (message.type !== "login" || typeof message.payload !== "string") continue;
      try {
        const response = JSON.parse(message.payload) as { code?: number; message?: string };
        if (response.code === 1001 && response.message?.startsWith("^Captcha required^")) {
          const accountId = response.message.split("|")[1] ?? "";
          setCaptchaRequest({ accountId, error: "" });
          continue;
        }
        if (!captchaRequest) continue;
        if (response.code === 0) setCaptchaRequest(null);
        else setCaptchaRequest((current) => current ? { ...current, error: response.message ?? "登录失败。" } : current);
      } catch {
        // Non-JSON login messages do not belong to the captcha flow.
      }
    }
  }, [captchaRequest, gateway.messages]);

  const submitCaptcha = async (identifier: string, captcha: string) => {
    if (!captchaRequest) return;
    await gateway.send("login", {
      channel: "active_with_captcha",
      type: "",
      details: JSON.stringify({ id: captchaRequest.accountId, identifier, captcha }),
    });
  };

  const lastProgressRef = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, lastProgressRef);
    for (const message of pending) {
      if (message.type !== "launch_progress") continue;
      try {
        JSON.parse(typeof message.payload === "string" ? message.payload : JSON.stringify(message.payload));
      } catch {
        // Ignore malformed progress payloads.
      }
    }
  }, [gateway.messages]);

  return (
    <div className="app-body">
      <audio ref={notificationAudioRef} src="/notify.mp3" preload="auto" />
      <nav ref={sidebarRef} className="sidebar" aria-label="用户中心导航">
        <div className="nav-indicator" ref={indicatorRef} aria-hidden="true" />
        {userCenterLinks.map((item, index) => "divider" in item ? (
          <div className="nav-divider nav-item-enter" role="separator" key={`divider-${index}`} style={{ animationDelay: `${index * 30}ms` }} />
        ) : (
          <NavLink
            to={item.to}
            end={item.to === "/user-center"}
            className={({ isActive }) => `nav-item nav-item-enter${isActive ? " active" : ""}`}
            style={{ animationDelay: `${index * 30}ms` }}
            key={item.to}
          >
            <span className="nav-icon"><item.Icon size={16} strokeWidth={1.3} aria-hidden="true" /></span>
            {item.label}
          </NavLink>
        ))}
      </nav>
      <main ref={contentRef} className="content content-main">
        <div className="page-content page-slide-up" key={location.pathname}><Outlet /></div>
      </main>
      {captchaRequest ? (
        <GlobalCaptchaModal
          key={captchaRequest.accountId}
          error={captchaRequest.error}
          onClose={() => setCaptchaRequest(null)}
          onSubmit={submitCaptcha}
        />
      ) : null}
    </div>
  );
}

function GlobalCaptchaModal({
  error,
  onClose,
  onSubmit,
}: {
  error: string;
  onClose(): void;
  onSubmit(identifier: string, captcha: string): Promise<void>;
}) {
  const createIdentifier = () => Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
  const [identifier, setIdentifier] = useState(createIdentifier);
  const [captcha, setCaptcha] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState("");

  const submit = async () => {
    if (!captcha.trim()) {
      setLocalError("请输入验证码");
      return;
    }
    setSubmitting(true);
    setLocalError("");
    try {
      await onSubmit(identifier, captcha.trim());
    } catch (submitError) {
      setLocalError(submitError instanceof Error ? submitError.message : "登录失败。");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="global-captcha-backdrop" role="presentation">
      <section className="global-captcha-modal" role="dialog" aria-modal="true" aria-labelledby="global-captcha-title">
        <header>
          <h2 id="global-captcha-title">身份验证</h2>
          <button type="button" aria-label="关闭弹窗" onClick={onClose}><X /></button>
        </header>
        <div className="global-captcha-body">
          <div>
            <input
              type="text"
              placeholder="验证码"
              value={captcha}
              autoComplete="off"
              onChange={(event) => { setCaptcha(event.target.value); setLocalError(""); }}
            />
            <button type="button" className="global-captcha-image" aria-label="刷新验证码" onClick={() => setIdentifier(createIdentifier())}>
              <img src={`https://ptlogin.4399.com/ptlogin/captcha.do?captchaId=${identifier}`} alt="验证码" />
            </button>
          </div>
          {localError || error ? <p>{localError || error}</p> : null}
          <button type="button" disabled={submitting} onClick={() => void submit()}>{submitting ? "正在登录…" : "登录"}</button>
        </div>
      </section>
    </div>
  );
}

const settingsLinks = [
  ["/user/settings", "Profile", UserRound, "Manage your basic information"],
  ["/user/settings/notifications", "Notification Settings", Bell, "Manage notification preferences"],
  ["/user/settings/security", "Security & Privacy", Shield, "Privacy settings and security options"],
] as const;

export function SettingsLayout() {
  return (
    <div className="settings-shell">
      <aside className="settings-nav">
        <h3>General Settings</h3>
        <NavLink to={settingsLinks[0][0]} end>
          <UserRound size={20} />
          <span><strong>{settingsLinks[0][1]}</strong><small>{settingsLinks[0][3]}</small></span>
        </NavLink>
        <div className="settings-divider" />
        <h3>Notifications &amp; Privacy</h3>
        {settingsLinks.slice(1).map(([to, label, Icon, description]) => (
          <NavLink key={to} to={to}>
            <Icon size={20} />
            <span><strong>{label}</strong><small>{description}</small></span>
          </NavLink>
        ))}
      </aside>
      <section className="settings-content"><Outlet /></section>
    </div>
  );
}

function ToastViewport() {
  const { toasts, dismiss } = useToasts();
  return (
    <div className="toast-viewport neo-island-viewport" aria-live="polite">
      {toasts.map((toast) => (
        <ToastCard key={toast.id} toast={toast} onDismiss={dismiss} />
      ))}
    </div>
  );
}

const toastIcons = {
  success: <Check aria-hidden="true" />,
  error: <X aria-hidden="true" />,
  warning: <TriangleAlert aria-hidden="true" />,
  info: <Info aria-hidden="true" />,
} as const;

const toastLabels = {
  success: "成功",
  error: "错误",
  warning: "警告",
  info: "提示",
} as const;

function ToastCard({ toast, onDismiss }: { toast: ToastMessage; onDismiss(id: string): void }) {
  const [paused, setPaused] = useState(false);
  const [closing, setClosing] = useState(false);
  const remaining = useRef(toast.duration);

  const close = useCallback(() => {
    if (closing) return;
    setClosing(true);
    window.setTimeout(() => onDismiss(toast.id), 200);
  }, [closing, onDismiss, toast.id]);

  useEffect(() => {
    if (paused || closing || remaining.current <= 0) return;
    const startedAt = Date.now();
    const timer = window.setTimeout(close, remaining.current);
    return () => {
      window.clearTimeout(timer);
      remaining.current = Math.max(0, remaining.current - (Date.now() - startedAt));
    };
  }, [close, closing, paused]);

  return (
    <article
      className={`toast neo-island toast-${toast.tone}${closing ? " toast-closing" : ""}`}
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      role={toast.tone === "error" ? "alert" : "status"}
    >
      <div className="toast-body">
        <span className="toast-icon">{toastIcons[toast.tone]}</span>
        <div className="toast-content">
          <div className="toast-heading">
            <span>{toastLabels[toast.tone]}</span>
            <button type="button" aria-label="关闭通知" onClick={close}><X aria-hidden="true" /></button>
          </div>
          <p>{toast.message}</p>
        </div>
      </div>
      {toast.duration > 0 ? (
        <span
          className={`toast-progress${paused ? " toast-progress-paused" : ""}`}
          style={{ animationDuration: `${toast.duration}ms` }}
        />
      ) : null}
    </article>
  );
}

export function RequireAuth({ children }: { children: ReactNode }) {
  return children;
}

export function RequireUserCenter({ children }: { children: ReactNode }) {
  const gateway = useGateway();
  const location = useLocation();
  const navigate = useNavigate();
  if (gateway.status === "connected") return children;
  if (!gateway.isCurrentSessionConnected || !gateway.lastConnectedUrl) {
    return <Navigate to="/gateway" replace />;
  }
  const connecting = gateway.status !== "error";
  return (
    <main className="gateway-reconnect">
      {connecting ? <span className="gateway-reconnect-spinner" /> : null}
      <h2>{connecting ? "正在连接网关" : "重新连接失败"}</h2>
      <p>{connecting ? "正在尝试连接本地网关…" : "无法连接网关"}</p>
      <button onClick={() => { gateway.setCurrentSessionConnected(false); gateway.setLastConnectedUrl(null); navigate("/gateway"); }}>配置网关</button>
      <button onClick={() => connecting ? gateway.disconnect() : void gateway.connect(gateway.lastConnectedUrl!).catch(() => undefined)}>{connecting ? "取消" : "重试"}</button>
    </main>
  );
}
