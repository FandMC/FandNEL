export interface GatewaySettings {
  jvmMaxMemory: string;
  loadCoreModules: boolean;
  downloadThreads: number;
  enableSocks5: boolean;
  socks5Address: string;
  socks5Username: string;
  socks5Password: string;
  neteaseFormat: boolean;
  autoNavigateToConfig: boolean;
  peLaunchPath: string;
}

export const defaultGatewaySettings: GatewaySettings = {
  jvmMaxMemory: "2048",
  loadCoreModules: true,
  downloadThreads: 4,
  enableSocks5: false,
  socks5Address: "",
  socks5Username: "",
  socks5Password: "",
  neteaseFormat: true,
  autoNavigateToConfig: false,
  peLaunchPath: "",
};

let currentSettings: GatewaySettings | null = null;
let settingsLoaded = false;

async function loadFromFile(): Promise<GatewaySettings> {
  try {
    const response = await fetch("/api/settings");
    if (!response.ok) return { ...defaultGatewaySettings };
    const raw = await response.json() as Record<string, unknown>;
    const value: Partial<GatewaySettings> = {
      peLaunchPath: typeof raw.pe_launch_path === "string" ? raw.pe_launch_path : (typeof raw.peLaunchPath === "string" ? raw.peLaunchPath : undefined),
      jvmMaxMemory: typeof raw.jvmMaxMemory === "string" ? raw.jvmMaxMemory : undefined,
      loadCoreModules: typeof raw.loadCoreModules === "boolean" ? raw.loadCoreModules : undefined,
      downloadThreads: typeof raw.downloadThreads === "number" ? raw.downloadThreads : undefined,
      enableSocks5: typeof raw.enableSocks5 === "boolean" ? raw.enableSocks5 : undefined,
      socks5Address: typeof raw.socks5Address === "string" ? raw.socks5Address : undefined,
      socks5Username: typeof raw.socks5Username === "string" ? raw.socks5Username : undefined,
      socks5Password: typeof raw.socks5Password === "string" ? raw.socks5Password : undefined,
      neteaseFormat: typeof raw.neteaseFormat === "boolean" ? raw.neteaseFormat : undefined,
      autoNavigateToConfig: typeof raw.autoNavigateToConfig === "boolean" ? raw.autoNavigateToConfig : undefined,
    };
    return {
      jvmMaxMemory: typeof value.jvmMaxMemory === "string" ? value.jvmMaxMemory : defaultGatewaySettings.jvmMaxMemory,
      loadCoreModules: typeof value.loadCoreModules === "boolean" ? value.loadCoreModules : defaultGatewaySettings.loadCoreModules,
      downloadThreads: typeof value.downloadThreads === "number" ? value.downloadThreads : defaultGatewaySettings.downloadThreads,
      enableSocks5: typeof value.enableSocks5 === "boolean" ? value.enableSocks5 : defaultGatewaySettings.enableSocks5,
      socks5Address: typeof value.socks5Address === "string" ? value.socks5Address : defaultGatewaySettings.socks5Address,
      socks5Username: typeof value.socks5Username === "string" ? value.socks5Username : defaultGatewaySettings.socks5Username,
      socks5Password: typeof value.socks5Password === "string" ? value.socks5Password : defaultGatewaySettings.socks5Password,
      neteaseFormat: typeof value.neteaseFormat === "boolean" ? value.neteaseFormat : defaultGatewaySettings.neteaseFormat,
      autoNavigateToConfig: typeof value.autoNavigateToConfig === "boolean" ? value.autoNavigateToConfig : defaultGatewaySettings.autoNavigateToConfig,
      peLaunchPath: typeof value.peLaunchPath === "string" ? value.peLaunchPath : defaultGatewaySettings.peLaunchPath,
    };
  } catch {
    return { ...defaultGatewaySettings };
  }
}

export async function loadGatewaySettings(): Promise<GatewaySettings> {
  if (currentSettings && settingsLoaded) return { ...currentSettings };
  const value = await loadFromFile();
  currentSettings = value;
  settingsLoaded = true;
  return { ...currentSettings };
}

export function readGatewaySettings(): GatewaySettings {
  if (currentSettings) return { ...currentSettings };
  currentSettings = { ...defaultGatewaySettings };
  return { ...currentSettings };
}

export async function writeGatewaySettings(settings: GatewaySettings): Promise<void> {
  currentSettings = { ...settings };
  const payload = {
    pe_launch_path: settings.peLaunchPath,
    jvmMaxMemory: settings.jvmMaxMemory,
    loadCoreModules: settings.loadCoreModules,
    downloadThreads: settings.downloadThreads,
    enableSocks5: settings.enableSocks5,
    socks5Address: settings.socks5Address,
    socks5Username: settings.socks5Username,
    socks5Password: settings.socks5Password,
    neteaseFormat: settings.neteaseFormat,
    autoNavigateToConfig: settings.autoNavigateToConfig,
  };
  try {
    const response = await fetch("/api/settings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => null) as { error?: string } | null;
      throw new Error(error?.error ?? "Failed to save settings");
    }
  } catch (error) {
    throw error;
  }
}
