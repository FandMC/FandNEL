export type ThemeMode = "system" | "light" | "dark";

export interface AppearanceSettings {
  themeMode: ThemeMode;
  themeColor: string;
}

const storageKey = "fandnel:appearance";
const defaults: AppearanceSettings = { themeMode: "system", themeColor: "#0078D4" };
export const appearanceChangedEvent = "fandnel:appearance-changed";

export function readAppearance(): AppearanceSettings {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) ?? "null") as Partial<AppearanceSettings> | null;
    return {
      themeMode: stored?.themeMode === "light" || stored?.themeMode === "dark" ? stored.themeMode : "system",
      themeColor: typeof stored?.themeColor === "string" && /^#[\da-f]{6}$/i.test(stored.themeColor)
        ? stored.themeColor : defaults.themeColor,
    };
  } catch {
    return { ...defaults };
  }
}

export function applyAppearance(): void {
  const settings = readAppearance();
  const dark = settings.themeMode === "dark" || (settings.themeMode === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
  const root = document.documentElement;
  root.dataset.theme = dark ? "dark" : "light";
  root.style.colorScheme = dark ? "dark" : "light";
  root.style.setProperty("--accent", settings.themeColor);
  const hover = settings.themeColor.slice(1).match(/../g)!
    .map((channel) => Math.round(parseInt(channel, 16) * 0.85).toString(16).padStart(2, "0")).join("");
  root.style.setProperty("--accent-hover", `#${hover}`);
}

export function writeAppearance(patch: Partial<AppearanceSettings>): void {
  const value = { ...readAppearance(), ...patch };
  if (!["system", "light", "dark"].includes(value.themeMode) || !/^#[\da-f]{6}$/i.test(value.themeColor))
    throw new Error("外观设置无效。");
  localStorage.setItem(storageKey, JSON.stringify(value));
  applyAppearance();
  window.dispatchEvent(new Event(appearanceChangedEvent));
}

export function observeAppearance(): () => void {
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  const update = () => applyAppearance();
  const syncStorage = (event: StorageEvent) => {
    if (event.key === storageKey) {
      applyAppearance();
      window.dispatchEvent(new Event(appearanceChangedEvent));
    }
  };
  media.addEventListener("change", update);
  window.addEventListener("storage", syncStorage);
  return () => {
    media.removeEventListener("change", update);
    window.removeEventListener("storage", syncStorage);
  };
}
