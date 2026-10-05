import { FormEvent, useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { LoadingState, Notice } from "../components/ui";
import { useGateway } from "../context/AppContext";

export function HomeRedirect() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const defaultEndpoint = searchParams.get("default");

  useEffect(() => {
    const destination = defaultEndpoint
      ? `/gateway?default=${encodeURIComponent(defaultEndpoint)}`
      : "/gateway";
    void navigate(destination, { replace: true });
  }, [defaultEndpoint, navigate]);
  return <LoadingState />;
}

export function GatewayPage() {
  const gateway = useGateway();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const defaultEndpoint = searchParams.get("default");
  const [endpoint, setEndpoint] = useState(defaultEndpoint ?? "");
  const [localError, setLocalError] = useState("");
  const [initializing, setInitializing] = useState(true);
  const [autoAttempting, setAutoAttempting] = useState(true);
  const autoAttemptStarted = useRef(false);

  useEffect(() => {
    if (gateway.status === "connected") navigate("/user-center", { replace: true });
  }, [gateway.status, navigate]);

  useEffect(() => {
    let cancelled = false;
    if (autoAttemptStarted.current) return () => {
      cancelled = true;
    };
    autoAttemptStarted.current = true;

    const tryConnect = async () => {
      const candidates: string[] = [];
      if (defaultEndpoint) candidates.push(defaultEndpoint);
      if (gateway.lastConnectedUrl && gateway.lastConnectedUrl !== defaultEndpoint) candidates.push(gateway.lastConnectedUrl);

      for (const candidate of candidates) {
        if (cancelled) return;
        try {
          await gateway.connect(candidate);
          if (cancelled) return;
          navigate("/user-center", { replace: true });
          return;
        } catch {
          continue;
        }
      }

      if (cancelled) return;
      try {
        const discovered = await gateway.autoDiscover();
        if (cancelled) return;
        setEndpoint(discovered);
        await gateway.connect(discovered);
        if (cancelled) return;
        navigate("/user-center", { replace: true });
        return;
      } catch {
        if (!cancelled) setLocalError("无法自动连接网关，请手动输入地址。");
      }

      if (cancelled) return;
      setInitializing(false);
      setAutoAttempting(false);
    };

    void tryConnect();
    return () => {
      cancelled = true;
    };
  // Auto-connect is intentionally a one-shot operation for this page. Gateway
  // callbacks are recreated when their internal URL state changes; depending on
  // them here would cancel an in-flight handshake and leave the page loading.
  }, [defaultEndpoint]);

  const connect = async (event: FormEvent) => {
    event.preventDefault();
    setLocalError("");
    try {
      await gateway.connect(endpoint);
      navigate("/user-center");
    } catch (connectError) {
      setLocalError(connectError instanceof Error ? connectError.message : "连接失败。");
    }
  };

  const discover = async () => {
    setLocalError("");
    try {
      setEndpoint(await gateway.autoDiscover());
    } catch (discoverError) {
      setLocalError(discoverError instanceof Error ? discoverError.message : "未找到网关。");
    }
  };

  if (initializing || autoAttempting) return <LoadingState />;
  return (
    <main className="content gateway-content">
      <div className="page-content login-page gateway-page">
        <form className="card gateway-form" onSubmit={connect}>
        <h2>连接网关</h2>
        <p className="subtitle">连接本地网关后进入用户中心</p>
        <div className="form-group">
          <input value={endpoint} onChange={(event) => setEndpoint(event.target.value)} placeholder="ws://localhost:19541/gateway" />
        </div>
        {localError || gateway.error ? <Notice tone="error">{localError || gateway.error}</Notice> : null}
        <div className="gateway-actions">
          <button className="btn-secondary" type="button" disabled={gateway.status === "searching"} onClick={discover}>{gateway.status === "searching" ? "正在搜索…" : "自动搜索"}</button>
          <button className="btn-accent" type="submit" disabled={gateway.status === "connecting"}>{gateway.status === "connecting" ? "正在连接…" : "连接"}</button>
        </div>
        </form>
        <button className="gateway-download form-link" type="button" onClick={() => navigate("/download")}>没有 WebSocket 地址？下载网关。</button>
      </div>
    </main>
  );
}

export function NotFoundPage() {
  return <main className="center-screen compact-center"><h1>404</h1><p>找不到此页面。</p><Link className="button button-primary" to="/">返回首页</Link></main>;
}
