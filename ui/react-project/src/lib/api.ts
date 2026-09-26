import type { Order, Plugin, PluginList } from "../types";
import { NEXUS_API } from "./legacyAuth";

export const API_BASE = NEXUS_API.domain;

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly details?: unknown,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function readResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const body = contentType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message =
      typeof body === "object" && body && "message" in body
        ? String(body.message)
        : typeof body === "string" && body
          ? body
          : `Request failed with status ${response.status}`;
    throw new ApiError(message, response.status, body);
  }

  return body as T;
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  accessToken?: string | null,
): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers });
  return readResponse<T>(response);
}

export const onlineApi = {
  health: () => apiRequest<{ status?: string; service?: string }>("/health"),
  plugins: (query = "", page = 1, pageSize = 30) => {
    const search = new URLSearchParams({ query, page: String(page), pageSize: String(pageSize) });
    return apiRequest<PluginList>(`/plugins?${search}`);
  },
  plugin: (slug: string) => apiRequest<Plugin>(`/plugins/${encodeURIComponent(slug)}`),
  createOrder: (pluginSlug: string, accessToken: string) =>
    apiRequest<Order>(
      "/orders",
      { method: "POST", body: JSON.stringify({ pluginSlug }) },
      accessToken,
    ),
  orders: (accessToken: string) => apiRequest<Order[]>("/orders", {}, accessToken),
};
