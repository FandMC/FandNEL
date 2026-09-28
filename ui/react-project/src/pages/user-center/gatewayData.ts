import { MutableRefObject, useCallback, useEffect, useRef, useState } from "react";
import { useGateway } from "../../context/AppContext";
import type { GatewayMessage } from "../../types";

export function parseGatewayPayload<T>(payload: unknown): T {
  let value = payload;
  for (let index = 0; index < 2 && typeof value === "string"; index += 1) {
    value = JSON.parse(value) as unknown;
  }
  return value as T;
}

export function consumeGatewayMessages(
  messages: GatewayMessage[],
  previous: MutableRefObject<GatewayMessage | undefined>,
): GatewayMessage[] {
  const last = messages.at(-1);
  if (!last || last === previous.current) return [];
  const previousIndex = previous.current ? messages.lastIndexOf(previous.current) : -1;
  const pending = previous.current && previousIndex === -1
    ? [last]
    : messages.slice(previousIndex + 1);
  previous.current = last;
  return pending;
}

export function useGatewayList<T>(type: string) {
  const gateway = useGateway();
  const [items, setItems] = useState<T[]>([]);
  const [loading, setLoading] = useState(gateway.status === "connected");
  const [error, setError] = useState("");
  const previousResponse = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  const refresh = useCallback(async () => {
    if (gateway.status !== "connected") {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      await gateway.send(type);
    } catch (sendError) {
      setError(sendError instanceof Error ? sendError.message : "网关请求失败。");
      setLoading(false);
    }
  }, [gateway.status, gateway.send, type]);

  useEffect(() => {
    previousResponse.current = gateway.messages.at(-1);
    void refresh();
  }, [refresh]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousResponse);
    for (const message of pending) {
      if (message.type !== type) continue;
      try {
        const parsed = parseGatewayPayload<unknown>(message.payload);
        if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
          const envelope = parsed as Record<string, unknown>;
          const code = envelope.code;
          if (code !== undefined && Number(code) !== 0) {
            setError(String(envelope.message ?? "网关请求失败。"));
            continue;
          }
          const list = ["items", "entities", "data", "sessions", "games", "mods"]
            .map((key) => envelope[key])
            .find((value) => Array.isArray(value));
          setItems(Array.isArray(list) ? list.filter(Boolean) as T[] : []);
        } else if (Array.isArray(parsed)) {
          setItems(parsed.filter(Boolean) as T[]);
        } else {
          setItems([]);
        }
        setError("");
      } finally {
        setLoading(false);
      }
    }
  }, [gateway.messages, type]);

  return { gateway, items, setItems, loading, error, refresh };
}
