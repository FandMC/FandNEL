export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  role: string;
  username?: string;
  coins?: number;
  avatarUrl?: string;
  subscriptionExpires?: number;
  createdAt?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: UserProfile;
}

export interface PluginVersion {
  version: string;
  downloadUrl: string;
  releaseNotes: string;
}

export interface Plugin {
  id: string;
  slug: string;
  name: string;
  summary: string;
  description: string;
  iconUrl: string;
  price: number;
  currency: string;
  tags: string[];
  versions: PluginVersion[];
}

export interface PluginList {
  items: Plugin[];
  page: number;
  pageSize: number;
  total: number;
}

export interface LegacyComponentSummary {
  id: string;
  name: string;
  shortDescription: string;
  publisher: string;
  downloadCount: number;
  score: number;
  type: string;
}

export interface LegacyComponentDetails extends LegacyComponentSummary {
  coinValue: number;
  detailDescription: string;
  logoUrl: string;
  publishDate: string;
  version: string;
  dependencies?: Array<{ id: string; name: string }>;
}

export interface LegacyComment {
  id: string;
  content: string;
  createdAt: string;
}

export interface LegacyAnnouncement {
  id: string | number;
  title: string;
  content: string;
  date: string;
  read: boolean;
}

export interface Order {
  id: string;
  pluginSlug: string;
  amount: number;
  currency: string;
  status: string;
  createdAt: string;
  checkoutUrl?: string;
}

export interface CartItem {
  plugin: Plugin;
  quantity: number;
}

export interface GatewayMessage<T = unknown> {
  type: string;
  payload: T;
  sign?: string;
  identify?: string;
}

export interface LogEntry {
  id: string;
  timestamp: number;
  type: "info" | "success" | "warning" | "error" | "command";
  content: string;
}

export type GatewayStatus = "idle" | "searching" | "connecting" | "connected" | "error";

export interface ToastMessage {
  id: string;
  tone: "info" | "success" | "warning" | "error";
  message: string;
  duration: number;
}
