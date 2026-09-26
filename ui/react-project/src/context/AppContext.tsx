import {
  createContext,
  ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  logoutLegacySession,
  type LegacySession,
  type WebAuthnStatus,
} from "../lib/legacyAuth";
import type {
  GatewayMessage,
  GatewayStatus,
  LogEntry,
  ToastMessage,
  UserProfile,
} from "../types";

interface AuthContextValue {
  session: LegacySession | null;
  user: UserProfile | null;
  loading: boolean;
  checkEmail(email: string): Promise<boolean>;
  login(email: string, password: string): Promise<void>;
  register(email: string, password: string, firstName: string, lastName: string): Promise<void>;
  loginPrivateKey(privateKey: string): Promise<void>;
  webAuthnStatus(email: string): Promise<WebAuthnStatus>;
  loginPasskey(email: string): Promise<void>;
  refreshSession(): Promise<void>;
  logout(): Promise<void>;
  getAccessToken(): Promise<string>;
  updateUser(user: UserProfile): void;
}

interface GatewayContextValue {
  url: string;
  status: GatewayStatus;
  error: string | null;
  messages: GatewayMessage[];
  logs: LogEntry[];
  lastConnectedUrl: string | null;
  isCurrentSessionConnected: boolean;
  setUrl(url: string): void;
  setLastConnectedUrl(url: string | null): void;
  setCurrentSessionConnected(connected: boolean): void;
  connect(url?: string): Promise<void>;
  disconnect(): void;
  autoDiscover(): Promise<string>;
  send(type: string, payload?: unknown, identify?: string): Promise<string>;
  clearLogs(): void;
}

interface ToastContextValue {
  toasts: ToastMessage[];
  notify(message: string, tone?: ToastMessage["tone"], duration?: number): void;
  dismiss(id: string): void;
}

const AuthContext = createContext<AuthContextValue | null>(null);
const GatewayContext = createContext<GatewayContextValue | null>(null);
const ToastContext = createContext<ToastContextValue | null>(null);

function toBase64(buffer: ArrayBuffer): string {
  return btoa(String.fromCharCode(...new Uint8Array(buffer)));
}

function fromBase64(value: string): ArrayBuffer {
  const bytes = Uint8Array.from(atob(value), (character) => character.charCodeAt(0));
  return bytes.buffer;
}

async function sha256(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

function createGatewayIdentify(length = 20): string {
  const alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-";
  return [...crypto.getRandomValues(new Uint8Array(length))]
    .map((byte) => alphabet[byte & 63])
    .join("");
}

async function createGatewayMessage(
  type: string,
  payload: unknown = "",
  identify = createGatewayIdentify(),
): Promise<GatewayMessage<string>> {
  const serializedPayload = typeof payload === "string" ? payload : JSON.stringify(payload);
  return {
    type,
    payload: serializedPayload,
    sign: await sha256(serializedPayload),
    identify,
  };
}

function AuthProvider({ children }: { children: ReactNode }) {
  // FandNEL does not use the legacy Codexus/Nexus account session.  Keep the
  // context shape for pages shared with the old UI, but make it anonymous and
  // side-effect free so a missing account can never block the gateway flow.
  const [session] = useState<LegacySession | null>(null);
  const loading = false;
  const checkEmail = useCallback(async (_email: string) => false, []);
  const login = useCallback(async (_email: string, _password: string) => undefined, []);
  const register = useCallback(async (_email: string, _password: string, _firstName: string, _lastName: string) => undefined, []);
  const loginPrivateKey = useCallback(async (_privateKey: string) => undefined, []);
  const webAuthnStatus = useCallback(async (_email: string): Promise<WebAuthnStatus> => ({ hasWebAuthn: false, isEnabled: false }), []);
  const loginPasskey = useCallback(async (_email: string) => undefined, []);
  const refreshSession = useCallback(async () => undefined, []);
  const getAccessToken = useCallback(async () => "", []);

  const logout = useCallback(async () => {
    logoutLegacySession();
  }, []);

  const updateUser = useCallback((_user: UserProfile) => undefined, []);

  const value = useMemo(
    () => ({
      session,
      user: session?.user ?? null,
      loading,
      checkEmail,
      login,
      register,
      loginPrivateKey,
      webAuthnStatus,
      loginPasskey,
      refreshSession,
      logout,
      getAccessToken,
      updateUser,
    }),
    [session, loading, checkEmail, login, register, loginPrivateKey, webAuthnStatus, loginPasskey, refreshSession, logout, getAccessToken, updateUser],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function GatewayProvider({ children }: { children: ReactNode }) {
  const persistedGateway = useMemo(() => {
    try {
      const value = JSON.parse(sessionStorage.getItem("websocket-storage") ?? "null") as {
        state?: { lastConnectedUrl?: string | null; isCurrentSessionConnected?: boolean };
      } | null;
      return value?.state ?? {};
    } catch {
      return {};
    }
  }, []);
  const [url, setUrlState] = useState("");
  const [status, setStatus] = useState<GatewayStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [messages, setMessages] = useState<GatewayMessage[]>([]);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [lastConnectedUrl, setLastConnectedUrlState] = useState<string | null>(persistedGateway.lastConnectedUrl ?? null);
  const [isCurrentSessionConnected, setCurrentSessionConnectedState] = useState(Boolean(persistedGateway.isCurrentSessionConnected));
  const socketRef = useRef<WebSocket | null>(null);
  const keyPairRef = useRef<CryptoKeyPair | null>(null);
  const sessionKeyRef = useRef<CryptoKey | null>(null);
  // A socket may finish closing after a new connection has already started. Keep
  // a monotonically increasing generation so stale callbacks cannot overwrite
  // the state of the current connection.
  const connectionGenerationRef = useRef(0);
  const pendingConnectionRejectRef = useRef<((reason?: unknown) => void) | null>(null);
  const discoveryGenerationRef = useRef(0);
  const discoverySocketsRef = useRef<Set<WebSocket>>(new Set());

  const addLog = useCallback((content: string, type: LogEntry["type"] = "info") => {
    setLogs((current) => [
      ...current.slice(-299),
      { id: crypto.randomUUID(), timestamp: Date.now(), type, content },
    ]);
  }, []);

  const setUrl = useCallback((nextUrl: string) => {
    setUrlState(nextUrl);
  }, []);

  const persistGatewayState = useCallback((lastUrl: string | null, connected: boolean) => {
    sessionStorage.setItem("websocket-storage", JSON.stringify({
      state: { sessionKey: null, lastConnectedUrl: lastUrl, isCurrentSessionConnected: connected },
      version: 0,
    }));
  }, []);

  const setLastConnectedUrl = useCallback((nextUrl: string | null) => {
    setLastConnectedUrlState(nextUrl);
    persistGatewayState(nextUrl, isCurrentSessionConnected);
  }, [isCurrentSessionConnected, persistGatewayState]);

  const setCurrentSessionConnected = useCallback((connected: boolean) => {
    setCurrentSessionConnectedState(connected);
    persistGatewayState(lastConnectedUrl, connected);
  }, [lastConnectedUrl, persistGatewayState]);

  const decodeMessage = useCallback(async (data: string | ArrayBuffer | Blob) => {
    let bytes: ArrayBuffer;
    if (typeof data === "string") bytes = new TextEncoder().encode(data).buffer;
    else if (data instanceof Blob) bytes = await data.arrayBuffer();
    else bytes = data;

    // Capture the key for this message. A newer connection can replace the
    // ref while an async Blob/decrypt operation is still in flight.
    const sessionKey = sessionKeyRef.current;
    if (!sessionKey) {
      return new TextDecoder().decode(bytes);
    }
    const value = new Uint8Array(bytes);
    const decrypted = await crypto.subtle.decrypt(
      { name: "AES-CBC", iv: value.slice(0, 16) },
      sessionKey,
      value.slice(16),
    );
    return new TextDecoder().decode(decrypted);
  }, []);

  const send = useCallback(
    async (type: string, payload: unknown = "", identify = createGatewayIdentify()) => {
      const socket = socketRef.current;
      if (!socket || socket.readyState !== WebSocket.OPEN) {
        throw new Error("Gateway is not connected.");
      }
      const gatewayMessage = await createGatewayMessage(type, payload, identify);
      const message = JSON.stringify(gatewayMessage);
      const sessionKey = sessionKeyRef.current;
      if (socketRef.current !== socket || socket.readyState !== WebSocket.OPEN) {
        throw new Error("Gateway connection was replaced.");
      }
      if (!sessionKey) {
        socket.send(message);
      } else {
        const iv = crypto.getRandomValues(new Uint8Array(16));
        const encrypted = await crypto.subtle.encrypt(
          { name: "AES-CBC", iv },
          sessionKey,
          new TextEncoder().encode(message),
        );
        if (socketRef.current !== socket || socket.readyState !== WebSocket.OPEN) {
          throw new Error("Gateway connection was replaced.");
        }
        const combined = new Uint8Array(16 + encrypted.byteLength);
        combined.set(iv);
        combined.set(new Uint8Array(encrypted), 16);
        socket.send(combined);
      }
      addLog(`${type}${gatewayMessage.payload ? ` ${gatewayMessage.payload}` : ""}`, "command");
      return identify;
    },
    [addLog],
  );

  const stopDiscovery = useCallback(() => {
    discoveryGenerationRef.current += 1;
    for (const socket of discoverySocketsRef.current) {
      if (socket.readyState < WebSocket.CLOSING) socket.close();
    }
    discoverySocketsRef.current.clear();
  }, []);

  const invalidateConnection = useCallback((nextStatus: GatewayStatus = "idle") => {
    connectionGenerationRef.current += 1;
    const pendingReject = pendingConnectionRejectRef.current;
    pendingConnectionRejectRef.current = null;
    const socket = socketRef.current;
    socketRef.current = null;
    keyPairRef.current = null;
    sessionKeyRef.current = null;
    stopDiscovery();
    if (socket && socket.readyState < WebSocket.CLOSING) socket.close();
    pendingReject?.(new Error("Gateway connection attempt superseded."));
    setStatus(nextStatus);
  }, [stopDiscovery]);

  const disconnect = useCallback(() => {
    invalidateConnection("idle");
  }, [invalidateConnection]);

  const connect = useCallback(
    async (candidate = url) => {
      if (!candidate) throw new Error("Enter a gateway WebSocket URL.");
      invalidateConnection("idle");
      const generation = connectionGenerationRef.current;
      setStatus("connecting");
      setError(null);
      setUrl(candidate);
      addLog(`Connecting to ${candidate}`);

      const keyPair = (await crypto.subtle.generateKey(
        { name: "ECDH", namedCurve: "P-256" },
        true,
        ["deriveBits"],
      )) as CryptoKeyPair;
      if (generation !== connectionGenerationRef.current) {
        throw new Error("Gateway connection attempt superseded.");
      }
      keyPairRef.current = keyPair;

      await new Promise<void>((resolve, reject) => {
        let settled = false;
        const socket = new WebSocket(candidate);
        socket.binaryType = "arraybuffer";
        if (generation !== connectionGenerationRef.current) {
          socket.close();
          reject(new Error("Gateway connection attempt superseded."));
          return;
        }
        socketRef.current = socket;
        let timeout = 0;

        const isCurrent = () => socketRef.current === socket && generation === connectionGenerationRef.current;
        const settle = (action: () => void) => {
          if (settled) return;
          settled = true;
          window.clearTimeout(timeout);
          if (pendingConnectionRejectRef.current === rejectConnection) {
            pendingConnectionRejectRef.current = null;
          }
          action();
        };
        const resolveConnection = () => settle(() => resolve());
        const rejectConnection = (reason: unknown) => settle(() => reject(reason));
        pendingConnectionRejectRef.current = rejectConnection;
        timeout = window.setTimeout(() => {
          if (socketRef.current !== socket || generation !== connectionGenerationRef.current) return;
          rejectConnection(new Error("Gateway handshake timed out."));
        }, 8_000);

        socket.onopen = async () => {
          if (!isCurrent()) return;
          try {
            const publicKey = await crypto.subtle.exportKey("spki", keyPair.publicKey);
            if (!isCurrent()) return;
            const payload = toBase64(publicKey);
            socket.send(JSON.stringify(await createGatewayMessage("handshake", payload)));
          } catch (openError) {
            if (isCurrent()) rejectConnection(openError);
          }
        };
        socket.onmessage = async ({ data }) => {
          if (!isCurrent()) return;
          try {
            const decoded = await decodeMessage(data);
            if (!isCurrent()) return;
            const message = JSON.parse(decoded) as GatewayMessage<string>;
            if (message.type === "handshake" && !sessionKeyRef.current) {
              const serverKey = await crypto.subtle.importKey(
                "spki",
                fromBase64(message.payload),
                { name: "ECDH", namedCurve: "P-256" },
                false,
                [],
              );
              const secret = await crypto.subtle.deriveBits(
                { name: "ECDH", public: serverKey },
                keyPair.privateKey,
                256,
              );
              const hkdfKey = await crypto.subtle.importKey("raw", secret, "HKDF", false, ["deriveKey"]);
              const sessionKey = await crypto.subtle.deriveKey(
                {
                  name: "HKDF",
                  hash: "SHA-256",
                  salt: new TextEncoder().encode("codexus.today.websocket.establishing"),
                  info: new TextEncoder().encode("codexus.today.aes.key"),
                },
                hkdfKey,
                { name: "AES-CBC", length: 256 },
                false,
                ["encrypt", "decrypt"],
              );
              if (!isCurrent()) return;
              sessionKeyRef.current = sessionKey;
              window.clearTimeout(timeout);
              setStatus("connected");
              setLastConnectedUrlState(candidate);
              setCurrentSessionConnectedState(true);
              persistGatewayState(candidate, true);
              addLog("Gateway handshake completed", "success");
              resolveConnection();
            }
            setMessages((current) => [...current.slice(-199), message]);
          } catch (messageError) {
            if (isCurrent()) {
              addLog(messageError instanceof Error ? messageError.message : "Invalid gateway message", "error");
              rejectConnection(messageError);
            }
          }
        };
        socket.onerror = () => {
          if (isCurrent()) rejectConnection(new Error("Unable to connect to the gateway."));
        };
        socket.onclose = () => {
          if (!isCurrent()) return;
          sessionKeyRef.current = null;
          if (!settled) {
            rejectConnection(new Error("Gateway connection closed before handshake."));
            return;
          }
          setStatus("idle");
          addLog("Gateway disconnected", "warning");
        };
      }).catch((connectError: unknown) => {
        const message = connectError instanceof Error ? connectError.message : "Gateway connection failed.";
        if (generation === connectionGenerationRef.current) {
          setError(message);
          invalidateConnection("error");
          addLog(message, "error");
        }
        throw connectError;
      });
    },
    [addLog, decodeMessage, invalidateConnection, persistGatewayState, setUrl, url],
  );

  const autoDiscover = useCallback(async () => {
    const generation = discoveryGenerationRef.current + 1;
    discoveryGenerationRef.current = generation;
    setStatus("searching");
    const localProbeSockets = new Set<WebSocket>();
    const probes = Array.from({ length: 20 }, (_, index) => 19541 + index).map((port) => {
      const candidate = `ws://localhost:${port}/gateway/`;
      return new Promise<string>((resolve, reject) => {
        let socket: WebSocket | null = null;
        let settled = false;
        let timer = 0;
        const cleanup = () => {
          window.clearTimeout(timer);
          if (socket) {
            localProbeSockets.delete(socket);
            discoverySocketsRef.current.delete(socket);
            if (socket.readyState < WebSocket.CLOSING) socket.close();
          }
        };
        const succeed = () => {
          if (settled) return;
          settled = true;
          cleanup();
          resolve(candidate);
        };
        const fail = () => {
          if (settled) return;
          settled = true;
          cleanup();
          reject(new Error("unavailable"));
        };
        try {
          socket = new WebSocket(candidate);
          localProbeSockets.add(socket);
          discoverySocketsRef.current.add(socket);
          timer = window.setTimeout(fail, 1_500);
          socket.onopen = succeed;
          socket.onerror = fail;
          socket.onclose = fail;
        } catch {
          fail();
        }
      });
    });
    try {
      const discovered = await Promise.any(probes);
      if (generation !== discoveryGenerationRef.current) throw new Error("Gateway discovery superseded.");
      setUrl(discovered);
      setStatus("idle");
      return discovered;
    } catch {
      if (generation === discoveryGenerationRef.current) setStatus("idle");
      throw new Error("No local gateway was discovered on ports 19541-19560.");
    } finally {
      for (const socket of localProbeSockets) {
        discoverySocketsRef.current.delete(socket);
        if (socket.readyState < WebSocket.CLOSING) socket.close();
      }
    }
  }, [setUrl]);

  useEffect(() => () => {
    connectionGenerationRef.current += 1;
    pendingConnectionRejectRef.current?.(new Error("Gateway provider unmounted."));
    pendingConnectionRejectRef.current = null;
    socketRef.current?.close();
    socketRef.current = null;
    for (const socket of discoverySocketsRef.current) socket.close();
    discoverySocketsRef.current.clear();
  }, []);

  const value = useMemo(
    () => ({
      url,
      status,
      error,
      messages,
      logs,
      lastConnectedUrl,
      isCurrentSessionConnected,
      setUrl,
      setLastConnectedUrl,
      setCurrentSessionConnected,
      connect,
      disconnect,
      autoDiscover,
      send,
      clearLogs: () => setLogs([]),
    }),
    [url, status, error, messages, logs, lastConnectedUrl, isCurrentSessionConnected, setUrl, setLastConnectedUrl, setCurrentSessionConnected, connect, disconnect, autoDiscover, send],
  );
  return <GatewayContext.Provider value={value}>{children}</GatewayContext.Provider>;
}

function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const dismiss = useCallback((id: string) => setToasts((current) => current.filter((item) => item.id !== id)), []);
  const notify = useCallback(
    (message: string, tone: ToastMessage["tone"] = "info", duration = 5_000) => {
      const id = crypto.randomUUID();
      setToasts((current) => [...current, { id, tone, message, duration }]);
    },
    [],
  );
  return <ToastContext.Provider value={{ toasts, notify, dismiss }}>{children}</ToastContext.Provider>;
}

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ToastProvider>
      <AuthProvider>
        <GatewayProvider>{children}</GatewayProvider>
      </AuthProvider>
    </ToastProvider>
  );
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used inside AppProviders");
  return value;
}

export function useGateway() {
  const value = useContext(GatewayContext);
  if (!value) throw new Error("useGateway must be used inside AppProviders");
  return value;
}

export function useToasts() {
  const value = useContext(ToastContext);
  if (!value) throw new Error("useToasts must be used inside AppProviders");
  return value;
}
