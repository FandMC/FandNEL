import type { UserProfile } from "../types";

/**
 * 公共资源接口仍使用这个地址；FandNEL 本身不再实现 Nexus 账号登录、
 * 令牌刷新或账号资料同步。
 */
export const NEXUS_API = {
  domain: import.meta.env.VITE_API_URL ?? "https://api.codexus.today/api",
  appId: "Codexus.NEL",
  appSecret: "",
  appVersion: "",
} as const;

export interface LegacySession {
  refreshToken: string;
  expiresAt: number;
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile | null;
}

export interface WebAuthnStatus {
  hasWebAuthn: boolean;
  isEnabled: boolean;
  isEnable?: boolean;
}

export function logoutLegacySession(): void {
  for (const key of ["isLoggedIn", "refreshToken", "expiresAt", "accessToken", "accessTokenExpiresAt", "userId", "UserEmail", "userName", "coins", "avatarUrl", "expires", "loginIp", "privateKey", "codexus.auth.session"]) {
    localStorage.removeItem(key);
  }
}

export async function getWebAuthnStatus(_email: string): Promise<WebAuthnStatus> { return { hasWebAuthn: false, isEnabled: false }; }
export async function registerWebAuthn(_email: string): Promise<unknown> { return null; }
export async function disableWebAuthn(_email: string): Promise<unknown> { return null; }
export async function getPrivateKey(_email: string, _password: string): Promise<string> { return ""; }
