import {
  ChevronLeft,
  Download,
  SlidersHorizontal,
  Smartphone,
} from "lucide-react";
import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { DynamicTabIndicator } from "../components/DynamicTabIndicator";
import { Button, PageHeader } from "../components/ui";
import { GameLaunchPage } from "../components/GameLaunchPage";
import { JavaGameDetailsPanel, JavaGameDetailsSkeleton } from "../components/JavaGameDetailsPanel";
import { JavaJoinGameModal, type JavaGameDetails, type JavaGameKind } from "../components/JavaJoinGameModal";
import { ServerBrowserPage } from "../components/ServerBrowserPage";
import { useAuth, useGateway, useToasts } from "../context/AppContext";
import { loadGatewaySettings, readGatewaySettings, writeGatewaySettings, type GatewaySettings } from "../lib/settingsStorage";

type Resource = Record<string, unknown>;

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

function asResources(payload: unknown): Resource[] {
  const value = parsePayload(payload);
  if (Array.isArray(value)) {
    return value.filter((item): item is Resource => Boolean(item && typeof item === "object"));
  }
  if (value && typeof value === "object") {
    for (const key of ["items", "entities", "data", "entity", "servers", "games", "plugins", "mods", "sessions", "accounts"]) {
      const nested = (value as Resource)[key];
      if (Array.isArray(nested)) {
        return nested.filter((item): item is Resource => Boolean(item && typeof item === "object"));
      }
      if (nested && typeof nested === "object") {
        return [nested as Resource];
      }
    }
  }
  return [];
}

function gatewayResponseError(payload: unknown): string {
  const value = parsePayload(payload);
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Resource;
    if (record.code !== undefined && Number(record.code) !== 0) {
      return String(record.message ?? "Gateway request failed.");
    }
  }
  return "";
}

function useGatewayResource(requestType: string, responseType = requestType, payload: unknown = "") {
  const gateway = useGateway();
  const [items, setItems] = useState<Resource[]>([]);
  const [response, setResponse] = useState<unknown>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const lastHandled = useRef(gateway.messages.at(-1));
  const activeRequestIdentify = useRef<string | undefined>(undefined);
  const requestGeneration = useRef(0);

  const refresh = async () => {
    const generation = ++requestGeneration.current;
    if (gateway.status !== "connected") {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const identify = await gateway.send(requestType, payload);
      if (generation === requestGeneration.current) activeRequestIdentify.current = identify;
    } catch (sendError) {
      if (generation === requestGeneration.current) {
        setError(sendError instanceof Error ? sendError.message : "Gateway request failed.");
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    requestGeneration.current += 1;
    activeRequestIdentify.current = undefined;
    lastHandled.current = gateway.messages.at(-1);
    setItems([]);
    setResponse(undefined);
    if (gateway.status === "connected") void refresh();
  }, [gateway.status, payload, requestType, responseType]);

  useEffect(() => {
    const previous = lastHandled.current;
    const previousIndex = previous ? gateway.messages.lastIndexOf(previous) : -1;
    const pending = previous && previousIndex === -1
      ? gateway.messages.slice(-1)
      : gateway.messages.slice(previousIndex + 1);
    lastHandled.current = gateway.messages.at(-1);

    for (const message of pending) {
      if (message.type !== responseType) continue;
      if (!activeRequestIdentify.current || message.identify !== activeRequestIdentify.current) continue;
      const parsed = parsePayload(message.payload);
      const responseError = gatewayResponseError(parsed);
      if (responseError) {
        setError(responseError);
        setItems([]);
        setResponse(undefined);
        setLoading(false);
        continue;
      }
      setResponse(parsed);
      setItems(asResources(parsed));
      setError("");
      setLoading(false);
    }
  }, [gateway.messages, responseType]);

  return { ...gateway, items, response, loading, error, refresh };
}

export function ServersPage() { return <ServerBrowserPage kind="java-server" />; }
export function RentalsPage() { return <ServerBrowserPage kind="java-rental" />; }
export function BedrockServersPage() { return <ServerBrowserPage kind="bedrock-server" />; }
export function BedrockRentalsPage() { return <ServerBrowserPage kind="bedrock-rental" />; }
export function BedrockRealmsPage() { return <ServerBrowserPage kind="bedrock-realms" />; }

function ServerDetails({ rental = false }: { rental?: boolean }) {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const id = searchParams.get("id") ?? "";
  const name = searchParams.get("name") ?? (rental ? "Rental Server" : "Server");
  const password = searchParams.get("password") ?? "";
  const responseType = rental ? "rental_games_detail" : "net_games_detail";
  const resource = useGatewayResource(responseType, responseType, rental && password.trim() ? `${id}:${password}` : id);
  const debugPreview = import.meta.env.DEV && (searchParams.get("debug") === "1" || id.startsWith("debug-"));
  const debugItem: JavaGameDetails = rental
    ? { image_url: "/avatar.jpg", owner_id: "Debug Owner", begin_time: Math.floor(Date.now() / 1000), mc_version: "1.21.1", server_ip: "127.0.0.1", server_port: 25565, brief_summary: "<p>Debug Java rental server</p>" }
    : { entity_id: id || "debug-server", video_info_list: [], brief_image_urls: ["/avatar.jpg", "/mask.png"], developer_name: "Debug Developer", publish_time: Math.floor(Date.now() / 1000), mc_version_list: [{ name: "1.21.1", mcversionid: 12101 }], server_address: "127.0.0.1", server_port: 25565, detail_description: "<p>Debug Java server</p>" };
  const item = resource.items[0]
    ?? (!resource.error && resource.response && typeof resource.response === "object" && !Array.isArray(resource.response) ? resource.response as Resource : undefined)
    ?? (debugPreview ? debugItem : undefined);
  const [joinOpen, setJoinOpen] = useState(false);
  const kind: JavaGameKind = rental ? "rental_game" : "net_game";

  return (
    <div className={`workspace-page detail-page ${rental ? "rental-detail-page" : "server-detail-page"}`}>
      {rental ? <PageHeader title={name} description={id ? `Server ID: ${id}` : "Server details"} onBack={() => navigate(-1)} actions={<Button className="java-join-trigger" disabled={!item} onClick={() => setJoinOpen(true)}>Join Game</Button>} /> : <header className="server-details-v253-header"><button type="button" aria-label="Back" onClick={() => navigate(-1)}><ChevronLeft /></button><h1>{name}</h1></header>}
      {!item && resource.loading ? <JavaGameDetailsSkeleton rental={rental} /> : item ? <JavaGameDetailsPanel details={item} rental={rental} onJoin={() => setJoinOpen(true)} /> : resource.error ? <div className="java-server-unavailable" role="alert"><p>{resource.error}</p><Button onClick={() => void resource.refresh()}>Retry</Button></div> : <div className="java-server-unavailable">Server information not available</div>}
      {joinOpen && item ? <JavaJoinGameModal kind={kind} gameId={id} gameName={name} details={item} debugPreview={debugPreview} onClose={() => setJoinOpen(false)} /> : null}
    </div>
  );
}

export function ServerDetailsPage() { return <ServerDetails />; }
export function RentalDetailsPage() { return <ServerDetails rental />; }

export function BedrockLaunchPage() { return <GameLaunchPage kind="netserver" />; }
export function BedrockRentalLaunchPage() { return <GameLaunchPage kind="realms" />; }
export function BedrockRealmLaunchPage() { return <GameLaunchPage kind="realm" />; }

function Toggle({ checked, onChange, label }: { checked: boolean; onChange(checked: boolean): void; label: string }) {
  return (
    <label className="uc-toggle">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} aria-label={label} />
      <span />
    </label>
  );
}

function SettingRow({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return <div className="setting-row"><div><h3>{title}</h3><p>{description}</p></div><div>{children}</div></div>;
}

function SettingsCard({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return <section className="settings-v253-card"><header><strong>{title}</strong>{description ? <p>{description}</p> : null}</header><div>{children}</div></section>;
}

export function GatewaySettingsPage() {
  const gateway = useGateway();
  const { notify } = useToasts();
  const { user } = useAuth();
  const [tab, setTab] = useState<"application" | "download" | "pe">("application");
  const [settings, setSettings] = useState<GatewaySettings>(readGatewaySettings());

  useEffect(() => {
    void loadGatewaySettings().then((loaded) => setSettings(loaded)).catch(() => {});
  }, []);

  const updateSettings = <Key extends keyof GatewaySettings>(key: Key, value: GatewaySettings[Key]) => {
    setSettings((current) => {
      const next = { ...current, [key]: value };
      void writeGatewaySettings(next).catch((error) => notify(error instanceof Error ? error.message : "Unable to save settings.", "error"));
      return next;
    });
  };
  const send = async (type: string, payload: unknown = "") => {
    if (gateway.status !== "connected") {
      notify("Connect to the gateway before applying this action.", "error");
      return;
    }
    try {
      await gateway.send(type, payload);
      notify("Setting applied.", "success");
    } catch (error) {
      notify(error instanceof Error ? error.message : "Unable to apply setting.", "error");
    }
  };

  const settingsTabs = [
    { id: "application" as const, label: "Application", icon: <SlidersHorizontal /> },
    { id: "download" as const, label: "Downloads", icon: <Download /> },
    { id: "pe" as const, label: "PE", icon: <Smartphone /> },
  ];

  return (
    <div className="workspace-page settings-management-page settings-v253-page">
      <header className="settings-v253-intro"><h1>Platform Settings</h1><p>Manage your application preferences and download configurations.</p></header>
      <nav className="platform-tabs settings-v253-tabs" aria-label="Settings sections">
        {settingsTabs.map((item) => <button aria-pressed={tab === item.id} className={tab === item.id ? "active" : ""} data-indicator-key={item.id} type="button" key={item.id} onClick={() => setTab(item.id)}>{item.icon}{item.label}</button>)}
        <DynamicTabIndicator activeKey={tab} />
      </nav>

      <div className="settings-v253-content">
        {tab === "application" ? <>
          <SettingsCard title="Core Configuration">
            <SettingRow title="JVM Maximum Memory" description="Allocated memory for the Java Virtual Machine (MB)."><input className="uc-setting-input" value={settings.jvmMaxMemory} placeholder="e.g. 4096" onChange={(event) => updateSettings("jvmMaxMemory", event.target.value)} /></SettingRow>
            <SettingRow title="Load Core Modules" description="Automatically load essential modules during startup."><Toggle label="Load Core Modules" checked={settings.loadCoreModules} onChange={(checked) => updateSettings("loadCoreModules", checked)} /></SettingRow>
            <SettingRow title="Netease Format" description="Use compatible format for Netease role names."><Toggle label="Netease Format" checked={settings.neteaseFormat} onChange={(checked) => updateSettings("neteaseFormat", checked)} /></SettingRow>
          </SettingsCard>
          <SettingsCard title="Interceptor Settings">
            <SettingRow title="Auto-navigate to Configuration" description="Automatically switch to configuration interface after successful interceptor startup."><Toggle label="Auto-navigate to Configuration" checked={settings.autoNavigateToConfig} onChange={(checked) => updateSettings("autoNavigateToConfig", checked)} /></SettingRow>
          </SettingsCard>
          <SettingsCard title="Troubleshooting">
            <SettingRow title="Repair Game Files" description="Trigger a comprehensive repair routine for game servers."><button className="settings-v253-outline-button" type="button" onClick={() => void send("clear_game")}>Start Repair</button></SettingRow>
          </SettingsCard>
          <SettingsCard title="Network Proxy">
            <SettingRow title="Enable Socks5 Proxy" description="Toggle the usage of a SOCKS5 proxy for all download operations."><Toggle label="Enable Socks5 Proxy" checked={settings.enableSocks5} onChange={(checked) => updateSettings("enableSocks5", checked)} /></SettingRow>
            {settings.enableSocks5 ? <div className="settings-v253-proxy">
              <div className="settings-v253-proxy-fields">
                <SettingRow title="Proxy Address" description="Format: IP:Port"><input className="uc-setting-input" type="text" value={settings.socks5Address} placeholder="127.0.0.1:1080" onChange={(event) => updateSettings("socks5Address", event.target.value)} /></SettingRow>
                <SettingRow title="Username" description="Optional authentication"><input className="uc-setting-input" type="text" value={settings.socks5Username} placeholder="Username" onChange={(event) => updateSettings("socks5Username", event.target.value)} /></SettingRow>
                <SettingRow title="Password" description="Optional authentication"><input className="uc-setting-input" type="password" value={settings.socks5Password} placeholder="Password" onChange={(event) => updateSettings("socks5Password", event.target.value)} /></SettingRow>
              </div>
            </div> : null}
          </SettingsCard>
        </> : tab === "download" ? <SettingsCard title="Performance">
          <SettingRow title="Download Threads" description={`Current: ${settings.downloadThreads} threads. Higher values utilize more bandwidth.`}><div className="thread-control settings-v253-thread-control"><span>1</span><input type="range" min="1" max="16" step="1" value={settings.downloadThreads} onChange={(event) => updateSettings("downloadThreads", Number(event.target.value))} /><span>16</span></div></SettingRow>
        </SettingsCard> : <SettingsCard title="PE Settings" description="Configure Bedrock Edition game launch path.">
          <SettingRow title="Game Path" description="Set a custom game directory.">
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <input className="uc-setting-input" type="text" value={settings.peLaunchPath} placeholder="e.g. D:\MCLDownload\x64_mc" onChange={(event) => updateSettings("peLaunchPath", event.target.value)} />
              <button className="settings-v253-outline-button" type="button" onClick={() => { void writeGatewaySettings(settings).then(() => notify("Game Path saved.", "success")).catch((error) => notify(error instanceof Error ? error.message : "Unable to save settings.", "error")); }}>Save</button>
            </div>
          </SettingRow>
        </SettingsCard>}
      </div>
    </div>
  );

}
