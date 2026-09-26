import type {
  LegacyAnnouncement,
  LegacyComment,
  LegacyComponentDetails,
  LegacyComponentSummary,
} from "../types";
import { NEXUS_API } from "./legacyAuth";
import { md5 } from "./md5";

async function readJson<T>(response: Response, fallback: string): Promise<T> {
  const body = await response.json().catch(() => null) as ({ message?: string } & T) | null;
  if (response.status !== 200) throw new Error(body?.message || fallback);
  if (body === null) throw new Error(fallback);
  return body;
}

function authHeaders(token: string): HeadersInit {
  return token.trim() ? { Authorization: `Bearer ${token}` } : {};
}

export async function getAllComponents(offset = 0, limit = 20): Promise<{ total: number; items: LegacyComponentSummary[] }> {
  const url = new URL(`${NEXUS_API.domain}/components/get/all`);
  url.searchParams.set("offset", String(offset));
  url.searchParams.set("limit", String(limit));
  return readJson(await fetch(url), "Get all components failed.");
}

export async function getComponentById(id: string, token: string): Promise<LegacyComponentDetails> {
  const url = new URL(`${NEXUS_API.domain}/components/get/by-id`);
  url.searchParams.set("id", id);
  return readJson(await fetch(url, { headers: authHeaders(token) }), "Get components by id failed.");
}

export async function fetchAuthorizedText(url: string, token: string): Promise<string> {
  const response = await fetch(url, { headers: authHeaders(token) });
  if (!response.ok) {
    const messages: Record<number, string> = {
      400: "Invalid file name",
      401: "Authentication required",
      404: "File not found",
    };
    throw new Error(messages[response.status] ?? `Server error: ${response.status}`);
  }
  return response.text();
}

export async function downloadComponent(id: string, token: string): Promise<unknown> {
  return readJson(
    await fetch(`${NEXUS_API.domain}/components/download`, {
      method: "POST",
      headers: { ...authHeaders(token), "Content-Type": "application/json" },
      body: JSON.stringify({ componentId: id }),
    }),
    "Download components failed.",
  );
}

export async function getComments(
  componentId: string,
  token: string,
  offset = 0,
  limit = 20,
): Promise<{ total: number; items: LegacyComment[] }> {
  const url = new URL(`${NEXUS_API.domain}/Components/get/all/comments`);
  url.searchParams.set("componentId", componentId);
  url.searchParams.set("offset", String(offset));
  url.searchParams.set("limit", String(limit));
  const response = await readJson<{ total: number; comments: LegacyComment[] }>(
    await fetch(url, { headers: authHeaders(token) }),
    "Get comments failed.",
  );
  return { total: response.total, items: response.comments };
}

export async function sendComment(componentId: string, content: string, token: string): Promise<unknown> {
  return readJson(
    await fetch(`${NEXUS_API.domain}/components/comment`, {
      method: "POST",
      headers: { ...authHeaders(token), "Content-Type": "application/json" },
      body: JSON.stringify({ componentId, content }),
    }),
    "Send comment failed.",
  );
}

export async function getComponentsVersionByIds(
  ids: string[],
  token: string,
): Promise<{ total: number; items: Array<{ id: string; version: string }> }> {
  return readJson(
    await fetch(`${NEXUS_API.domain}/components/get/all/version`, {
      method: "POST",
      headers: { ...authHeaders(token), "Content-Type": "application/json" },
      body: JSON.stringify({ ids }),
    }),
    "Get components version by IDs failed.",
  );
}

export async function rechargeWithCode(code: string, token: string): Promise<string> {
  const response = await fetch(`${NEXUS_API.domain}/recharge`, {
    method: "POST",
    headers: { ...authHeaders(token), "Content-Type": "application/json" },
    body: JSON.stringify({ code }),
  });
  const body = await response.json().catch(() => null) as { message?: string } | null;
  return response.status === 200 ? "Recharge successful" : body?.message || "Recharge failed";
}

export async function transferFunds(
  toUserId: string,
  amount: number,
  token: string,
): Promise<string> {
  const response = await fetch(`${NEXUS_API.domain}/users/transfer`, {
    method: "POST",
    headers: { ...authHeaders(token), "Content-Type": "application/json" },
    body: JSON.stringify({ toUserId, amount }),
  });
  if (response.status === 200) return "Transfer successful";
  const body = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(body?.message || "Transfer failed");
}

export async function modifyUsername(newUsername: string, token: string): Promise<unknown> {
  const response = await fetch(`${NEXUS_API.domain}/users/personal/info/modify/username`, {
    method: "POST",
    headers: { ...authHeaders(token), "Content-Type": "application/json" },
    body: JSON.stringify({
      newUsername,
      md5Hash: md5(newUsername).toUpperCase(),
    }),
  });
  if (!response.ok) throw new Error("Failed to modify username");
  return response.json();
}

export async function getAnnouncements(): Promise<LegacyAnnouncement[]> {
  const response = await fetch(`${NEXUS_API.domain}/announcements`, {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });
  if (!response.ok) {
    throw new Error(`Failed to fetch announcement url: ${response.status} ${response.statusText}`);
  }
  const announcements = await response.json() as Omit<LegacyAnnouncement, "read">[];
  return announcements.map((announcement) => ({
    ...announcement,
    read: localStorage.getItem(`announcement_${md5(announcement.content)}`) === "true",
  }));
}

export function markAnnouncementRead(content: string): void {
  localStorage.setItem(`announcement_${md5(content)}`, "true");
}
