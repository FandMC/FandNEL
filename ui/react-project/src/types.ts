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
