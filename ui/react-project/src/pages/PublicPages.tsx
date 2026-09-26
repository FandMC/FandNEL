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
  const debugPreview = import.meta.env.DEV && searchParams.get("debug") === "1";

  useEffect(() => {
    if (gateway.status === "connected") navigate("/user-center", { replace: true });
  }, [gateway.status, navigate]);

  useEffect(() => {
    let cancelled = false;
    if (debugPreview) {
      setInitializing(false);
      setAutoAttempting(false);
      return () => {
        cancelled = true;
      };
    }
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
        if (!cancelled) setLocalError("Unable to auto-connect to the gateway. Please enter the address manually.");
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
  }, [debugPreview, defaultEndpoint]);

  const connect = async (event: FormEvent) => {
    event.preventDefault();
    setLocalError("");
    try {
      await gateway.connect(endpoint);
      navigate("/user-center");
    } catch (connectError) {
      setLocalError(connectError instanceof Error ? connectError.message : "Connection failed.");
    }
  };

  const discover = async () => {
    setLocalError("");
    try {
      setEndpoint(await gateway.autoDiscover());
    } catch (discoverError) {
      setLocalError(discoverError instanceof Error ? discoverError.message : "No gateway found.");
    }
  };

  if (initializing || autoAttempting) return <LoadingState />;
  return (
    <main className="gateway-screen">
      <form className="gateway-form" onSubmit={connect}>
        <h1>Connect to the Gateway</h1>
        <input value={endpoint} onChange={(event) => setEndpoint(event.target.value)} placeholder="ws://localhost:19541/gateway" />
        {localError || gateway.error ? <Notice tone="error">{localError || gateway.error}</Notice> : null}
        <div className="gateway-actions">
          <button type="button" disabled={gateway.status === "searching"} onClick={discover}>{gateway.status === "searching" ? "Searching..." : "Auto Search"}</button>
          <button type="submit" disabled={gateway.status === "connecting"}>{gateway.status === "connecting" ? "Connecting..." : "Connect"}</button>
        </div>
      </form>
      <button className="gateway-download" type="button" onClick={() => navigate("/download")}>No websocket link? Download gateway.</button>
    </main>
  );
}

export function NotFoundPage() {
  return <main className="center-screen compact-center"><h1>404</h1><p>This page could not be found.</p><Link className="button button-primary" to="/">Return home</Link></main>;
}
