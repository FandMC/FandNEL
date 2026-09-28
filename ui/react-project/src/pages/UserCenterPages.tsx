import {
  ChevronLeft,
  Download,
  SlidersHorizontal,
  Smartphone,
} from "lucide-react";
import { ReactNode, useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { DynamicTabIndicator } from "../components/DynamicTabIndicator";
import { Button } from "../components/ui";
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
    for (const key of ["items", "entities", "data", "entity", "servers", "games", "mods", "sessions", "accounts"]) {
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
      return String(record.message ?? "网关请求失败。");
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
        setError(sendError instanceof Error ? sendError.message : "网关请求失败。");
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
  const name = searchParams.get("name") ?? (rental ? "租赁服" : "服务器");
  const password = searchParams.get("password") ?? "";
  const responseType = rental ? "rental_games_detail" : "net_games_detail";
  const resource = useGatewayResource(responseType, responseType, rental && password.trim() ? `${id}:${password}` : id);
  const item = resource.items[0]
    ?? (!resource.error && resource.response && typeof resource.response === "object" && !Array.isArray(resource.response) ? resource.response as Resource : undefined)
    ;
  const [joinOpen, setJoinOpen] = useState(false);
  const kind: JavaGameKind = rental ? "rental_game" : "net_game";

  return (
    <div className="workspace-page detail-page server-detail-page">
      <header className="server-details-v253-header"><button type="button" aria-label="返回" onClick={() => navigate(-1)}><ChevronLeft /></button><h1>{name}</h1></header>
      {!item && resource.loading ? <JavaGameDetailsSkeleton /> : item ? <JavaGameDetailsPanel details={item} rental={rental} onJoin={() => setJoinOpen(true)} /> : resource.error ? <div className="java-server-unavailable" role="alert"><p>{resource.error}</p><Button onClick={() => void resource.refresh()}>重试</Button></div> : <div className="java-server-unavailable">暂无服务器信息</div>}
      {joinOpen && item ? <JavaJoinGameModal kind={kind} gameId={id} gameName={name} details={item} onClose={() => setJoinOpen(false)} /> : null}
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
      void writeGatewaySettings(next).catch((error) => notify(error instanceof Error ? error.message : "无法保存设置。", "error"));
      return next;
    });
  };
  const send = async (type: string, payload: unknown = "") => {
    if (gateway.status !== "connected") {
      notify("请先连接网关再执行此操作。", "error");
      return;
    }
    try {
      await gateway.send(type, payload);
      notify("设置已应用。", "success");
    } catch (error) {
      notify(error instanceof Error ? error.message : "无法应用设置。", "error");
    }
  };

  const settingsTabs = [
    { id: "application" as const, label: "应用", icon: <SlidersHorizontal /> },
    { id: "download" as const, label: "下载", icon: <Download /> },
    { id: "pe" as const, label: "基岩版", icon: <Smartphone /> },
  ];

  return (
    <div className="workspace-page settings-management-page settings-v253-page">
      <header className="settings-v253-intro"><h1>平台设置</h1><p>管理应用偏好与下载配置。</p></header>
      <nav className="platform-tabs settings-v253-tabs" aria-label="设置分类">
        {settingsTabs.map((item) => <button aria-pressed={tab === item.id} className={tab === item.id ? "active" : ""} data-indicator-key={item.id} type="button" key={item.id} onClick={() => setTab(item.id)}>{item.icon}{item.label}</button>)}
        <DynamicTabIndicator activeKey={tab} />
      </nav>

      <div className="settings-v253-content">
        {tab === "application" ? <>
          <SettingsCard title="核心配置">
            <SettingRow title="JVM 最大内存" description="分配给 Java 虚拟机的内存（MB）。"><input className="uc-setting-input" value={settings.jvmMaxMemory} placeholder="例如：4096" onChange={(event) => updateSettings("jvmMaxMemory", event.target.value)} /></SettingRow>
            <SettingRow title="加载核心模块" description="启动时自动加载必要模块。"><Toggle label="加载核心模块" checked={settings.loadCoreModules} onChange={(checked) => updateSettings("loadCoreModules", checked)} /></SettingRow>
            <SettingRow title="使用网易风格名称" description="随机生成角色名时使用网易风格。"><Toggle label="使用网易风格名称" checked={settings.neteaseFormat} onChange={(checked) => updateSettings("neteaseFormat", checked)} /></SettingRow>
          </SettingsCard>
          <SettingsCard title="代理通道设置">
            <SettingRow title="自动打开配置页" description="代理通道启动成功后自动跳转到配置页。"><Toggle label="自动打开配置页" checked={settings.autoNavigateToConfig} onChange={(checked) => updateSettings("autoNavigateToConfig", checked)} /></SettingRow>
          </SettingsCard>
          <SettingsCard title="故障排查">
            <SettingRow title="修复游戏文件" description="对游戏服务器相关文件执行完整修复。"><button className="settings-v253-outline-button" type="button" onClick={() => void send("clear_game")}>开始修复</button></SettingRow>
          </SettingsCard>
          <SettingsCard title="网络代理">
            <SettingRow title="启用 SOCKS5 代理" description="为所有下载操作启用 SOCKS5 代理。"><Toggle label="启用 SOCKS5 代理" checked={settings.enableSocks5} onChange={(checked) => updateSettings("enableSocks5", checked)} /></SettingRow>
            {settings.enableSocks5 ? <div className="settings-v253-proxy">
              <div className="settings-v253-proxy-fields">
                <SettingRow title="代理地址" description="格式：IP:端口"><input className="uc-setting-input" type="text" value={settings.socks5Address} placeholder="127.0.0.1:1080" onChange={(event) => updateSettings("socks5Address", event.target.value)} /></SettingRow>
                <SettingRow title="用户名" description="可选的身份验证信息"><input className="uc-setting-input" type="text" value={settings.socks5Username} placeholder="用户名" onChange={(event) => updateSettings("socks5Username", event.target.value)} /></SettingRow>
                <SettingRow title="密码" description="可选的身份验证信息"><input className="uc-setting-input" type="password" value={settings.socks5Password} placeholder="密码" onChange={(event) => updateSettings("socks5Password", event.target.value)} /></SettingRow>
              </div>
            </div> : null}
          </SettingsCard>
        </> : tab === "download" ? <SettingsCard title="性能">
          <SettingRow title="下载线程数" description={`当前使用 ${settings.downloadThreads} 个线程，线程越多，占用的带宽越大。`}><div className="thread-control settings-v253-thread-control"><span>1</span><input type="range" min="1" max="16" step="1" value={settings.downloadThreads} onChange={(event) => updateSettings("downloadThreads", Number(event.target.value))} /><span>16</span></div></SettingRow>
        </SettingsCard> : <SettingsCard title="基岩版设置" description="配置基岩版游戏启动路径。">
          <SettingRow title="游戏路径" description="设置自定义游戏目录。">
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <input className="uc-setting-input" type="text" value={settings.peLaunchPath} placeholder="例如：D:\MCLDownload\x64_mc" onChange={(event) => updateSettings("peLaunchPath", event.target.value)} />
              <button className="settings-v253-outline-button" type="button" onClick={() => { void writeGatewaySettings(settings).then(() => notify("游戏路径已保存。", "success")).catch((error) => notify(error instanceof Error ? error.message : "无法保存设置。", "error")); }}>保存</button>
            </div>
          </SettingRow>
        </SettingsCard>}
      </div>
    </div>
  );

}
