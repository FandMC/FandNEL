import {
  Bell,
  Check,
  Info,
  Shield,
  TriangleAlert,
  UserRound,
  Menu,
  X,
} from "lucide-react";
import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { Navigate, NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { DebugNavigator } from "../components/DebugNavigator";
import { Brand } from "../components/ui";
import { useGateway, useToasts } from "../context/AppContext";
import { consumeGatewayMessages } from "../pages/user-center/gatewayData";
import type { GatewayMessage, ToastMessage } from "../types";

export function AppLayout() {
  return (
    <div className="app-root">
      {import.meta.env.DEV ? <DebugNavigator /> : null}
      <header className="topbar">
        <Brand />
      </header>
      <Outlet />
      <ToastViewport />
    </div>
  );
}

const userCenterGroups = [
  { title: "General", links: [["/user-center", "Dashboard"]] },
  {
    title: "Java Edition",
    links: [
      ["/user-center/servers", "Servers"],
      ["/user-center/rentals", "Rental Servers"],
      ["/user-center/java-skins", "Skins"],
    ],
  },
  {
    title: "Bedrock Edition",
    links: [
      ["/user-center/bedrock", "Servers"],
      ["/user-center/rentals-for-bedrock", "Rental Servers"],
      ["/user-center/bedrock-realms", "Realms"],
    ],
  },
  {
    title: "Advanced",
    links: [
      ["/user-center/launchers", "Games"],
      ["/user-center/settings", "Settings"],
      ["/user-center/mods", "Mods"],
    ],
  },
] as const;

export function UserCenterLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const gateway = useGateway();
  const { notify } = useToasts();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [captchaRequest, setCaptchaRequest] = useState<{ accountId: string; error: string } | null>(null);
  const lastCaptchaMessageRef = useRef(gateway.messages.at(-1));
  const lastNotificationMessageRef = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const notificationAudioRef = useRef<HTMLAudioElement>(null);

  useEffect(() => setSidebarOpen(false), [location.pathname]);
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
        else setCaptchaRequest((current) => current ? { ...current, error: response.message ?? "Login failed." } : current);
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
    <div className="workspace">
      <audio ref={notificationAudioRef} src="/notify.mp3" preload="auto" />
      <button
        className="sidebar-toggle"
        type="button"
        aria-label="Toggle menu"
        aria-expanded={sidebarOpen}
        onClick={() => setSidebarOpen((open) => !open)}
      >
        {sidebarOpen ? <X /> : <Menu />}
      </button>
      <button
        className={`sidebar-backdrop ${sidebarOpen ? "sidebar-backdrop-open" : ""}`}
        type="button"
        aria-label="Close menu"
        onClick={() => setSidebarOpen(false)}
      />
      <aside className={`sidebar ${sidebarOpen ? "sidebar-open" : ""}`}>
        <nav aria-label="User center navigation">
          {userCenterGroups.map((group) => (
            <div className="sidebar-group" key={group.title}>
              <h3>{group.title}</h3>
              <ul>
                {group.links.map(([to, label]) => (
                  <li key={to}>
                    <NavLink
                      to={to}
                      end={to === "/user-center"}
                      className={({ isActive }) => (isActive ? "active" : undefined)}
                    >
                      <span>{label}</span>
                    </NavLink>
                  </li>
                ))}
              </ul>
              <div className="sidebar-separator" role="separator" />
            </div>
          ))}
        </nav>
      </aside>
      <main className="workspace-content"><Outlet /></main>
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
      setLocalError("Please enter the captcha");
      return;
    }
    setSubmitting(true);
    setLocalError("");
    try {
      await onSubmit(identifier, captcha.trim());
    } catch (submitError) {
      setLocalError(submitError instanceof Error ? submitError.message : "Login failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="global-captcha-backdrop" role="presentation">
      <section className="global-captcha-modal" role="dialog" aria-modal="true" aria-labelledby="global-captcha-title">
        <header>
          <h2 id="global-captcha-title">Verification</h2>
          <button type="button" aria-label="Close modal" onClick={onClose}><X /></button>
        </header>
        <div className="global-captcha-body">
          <div>
            <input
              type="text"
              placeholder="Captcha"
              value={captcha}
              autoComplete="off"
              onChange={(event) => { setCaptcha(event.target.value); setLocalError(""); }}
            />
            <button type="button" className="global-captcha-image" aria-label="Refresh captcha" onClick={() => setIdentifier(createIdentifier())}>
              <img src={`https://ptlogin.4399.com/ptlogin/captcha.do?captchaId=${identifier}`} alt="Captcha" />
            </button>
          </div>
          {localError || error ? <p>{localError || error}</p> : null}
          <button type="button" disabled={submitting} onClick={() => void submit()}>{submitting ? "Login..." : "Login"}</button>
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
    <div className="toast-viewport" aria-live="polite">
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
      className={`toast toast-${toast.tone}${closing ? " toast-closing" : ""}`}
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      role={toast.tone === "error" ? "alert" : "status"}
    >
      <div className="toast-body">
        <span className="toast-icon">{toastIcons[toast.tone]}</span>
        <div className="toast-content">
          <div className="toast-heading">
            <span>{toast.tone}</span>
            <button type="button" aria-label="Dismiss notification" onClick={close}><X aria-hidden="true" /></button>
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
  const debugAccess = isDebugAccess(location.search);

  if (debugAccess) return children;
  if (gateway.status === "connected") return children;
  if (!gateway.isCurrentSessionConnected || !gateway.lastConnectedUrl) {
    return <Navigate to="/gateway" replace />;
  }
  const connecting = gateway.status !== "error";
  return (
    <main className="gateway-reconnect">
      {connecting ? <span className="gateway-reconnect-spinner" /> : null}
      <h2>{connecting ? "Connecting to Gateway" : "Reconnect failed"}</h2>
      <p>{connecting ? "Attempting to connect to local gateway..." : "Unable to connect to the gateway"}</p>
      <button onClick={() => { gateway.setCurrentSessionConnected(false); gateway.setLastConnectedUrl(null); navigate("/gateway"); }}>Configure Gateway</button>
      <button onClick={() => connecting ? gateway.disconnect() : void gateway.connect(gateway.lastConnectedUrl!).catch(() => undefined)}>{connecting ? "Cancel" : "Retry"}</button>
    </main>
  );
}

function isDebugAccess(search: string): boolean {
  if (!import.meta.env.DEV) return false;
  const params = new URLSearchParams(search);
  const explicitAccess = params.get("debug") === "1" || params.get("id")?.startsWith("debug-") === true;
  if (explicitAccess) sessionStorage.setItem("codexus.debug-access", "1");
  return explicitAccess || sessionStorage.getItem("codexus.debug-access") === "1";
}
