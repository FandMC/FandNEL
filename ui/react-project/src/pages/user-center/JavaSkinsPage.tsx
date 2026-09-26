import { Check, LoaderCircle, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { SelectMenu } from "../../components/JavaJoinGameModal";
import { useGateway, useToasts } from "../../context/AppContext";
import type { GatewayMessage } from "../../types";
import { consumeGatewayMessages, parseGatewayPayload } from "./gatewayData";

interface JavaSkin {
  entity_id: string;
  name: string;
  brief_summary: string;
  like_num: number;
  title_image_url: string;
}

interface GatewayAccount extends Record<string, unknown> {
  id: string;
  alias?: string;
}

interface SkinListResponse {
  entities: JavaSkin[];
  total: number;
}

interface PurchaseSkinResponse {
  entity_id: string;
  buy_type: string | number;
}

type ApplyState = "idle" | "purchasing" | "applied";

const pageSize = 20;
const debugSkin: JavaSkin = {
  entity_id: "debug-java-skin",
  name: "Classic Adventurer",
  brief_summary: "A custom Minecraft character skin available for your Java Edition role.",
  like_num: 128,
  title_image_url: "https://crafatar.com/renders/body/8667ba71-b85a-4004-af54-457a9734eed7?overlay",
};
const debugAccount: GatewayAccount = { id: "debug-java-account", alias: "Debug Java Account" };

function accountLabel(account: GatewayAccount): string {
  return account.alias || account.id;
}

function JavaSkinCard({ skin, onSelect }: { skin: JavaSkin; onSelect(): void }) {
  return (
    <button className="skin-market-card" type="button" onClick={onSelect}>
      <span className="skin-market-media">
        {skin.title_image_url ? <img src={skin.title_image_url} alt={`${skin.name} skin`} /> : <span />}
      </span>
      <span className="skin-market-body">
        <span className="skin-market-heading"><strong>{skin.name}</strong><small>{skin.like_num} likes</small></span>
        <span className="skin-market-summary">{skin.brief_summary}</span>
      </span>
    </button>
  );
}

function ApplySkinModal({ skinId, debugPreview, onClose }: { skinId: string; debugPreview: boolean; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<GatewayAccount[]>(debugPreview ? [debugAccount] : []);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [state, setState] = useState<ApplyState>("idle");
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const pollTimer = useRef<number | null>(null);
  const accountRef = useRef<GatewayAccount | null>(debugPreview ? debugAccount : null);

  const stopPolling = useCallback(() => {
    if (pollTimer.current !== null) window.clearTimeout(pollTimer.current);
    pollTimer.current = null;
  }, []);

  const close = useCallback(() => {
    stopPolling();
    onClose();
  }, [onClose, stopPolling]);

  const applySkin = useCallback(async () => {
    const account = accountRef.current;
    if (!account) return;
    await gateway.send("java_edition/apply_skin", { user_id: account.id, item_id: skinId });
  }, [gateway.send, skinId]);

  const pollPurchase = useCallback((orderId: string, buyType: string | number) => {
    stopPolling();
    const run = async () => {
      const account = accountRef.current;
      if (!account) return;
      try {
        await gateway.send("java_edition/buy_skin_result", {
          user_id: account.id,
          orderid: orderId,
          buy_type: buyType,
        });
        pollTimer.current = window.setTimeout(run, 500);
      } catch (error) {
        setState("idle");
        notify(error instanceof Error ? error.message : "Unable to check skin purchase.", "error");
      }
    };
    void run();
  }, [gateway.send, notify, stopPolling]);

  useEffect(() => {
    if (debugPreview) return;
    void gateway.send("get_accounts", "available").catch((error) => {
      notify(error instanceof Error ? error.message : "Unable to load accounts.", "error");
    });
    return stopPolling;
  }, [debugPreview, gateway.send, notify, stopPolling]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      try {
        if (message.type === "get_accounts") {
          const nextAccounts = parseGatewayPayload<GatewayAccount[]>(message.payload);
          setAccounts(nextAccounts);
          setSelectedIndex(0);
          accountRef.current = nextAccounts[0] ?? null;
        } else if (message.type === "java_edition/skin_error") {
          stopPolling();
          setState("idle");
        } else if (message.type === "java_edition/skin_details") {
          const details = parseGatewayPayload<{ is_has: boolean }>(message.payload);
          if (details.is_has) void applySkin();
          else {
            const account = accountRef.current;
            if (account) void gateway.send("java_edition/purchase_skin", { user_id: account.id, item_id: skinId });
          }
        } else if (message.type === "java_edition/purchase_skin") {
          const purchase = parseGatewayPayload<PurchaseSkinResponse>(message.payload);
          pollPurchase(purchase.entity_id, purchase.buy_type);
        } else if (message.type === "java_edition/buy_skin_result") {
          const result = parseGatewayPayload<{ code: number }>(message.payload);
          if (result.code === 0) {
            stopPolling();
            void applySkin();
          }
        } else if (message.type === "java_edition/apply_skin") {
          stopPolling();
          setState("applied");
          window.setTimeout(close, 500);
        }
      } catch (error) {
        stopPolling();
        setState("idle");
        notify(error instanceof Error ? error.message : "Invalid skin service response.", "error");
      }
    }
  }, [applySkin, close, gateway.messages, gateway.send, notify, pollPurchase, skinId, stopPolling]);

  const confirm = async () => {
    const account = accounts[selectedIndex];
    if (!account) return;
    accountRef.current = account;
    setState("purchasing");
    if (debugPreview) {
      window.setTimeout(() => {
        setState("applied");
        window.setTimeout(close, 500);
      }, 700);
      return;
    }
    try {
      await gateway.send("java_edition/skin_details", { user_id: account.id, item_id: skinId });
    } catch (error) {
      setState("idle");
      notify(error instanceof Error ? error.message : "Unable to inspect skin ownership.", "error");
    }
  };

  return (
    <div className="legacy-modal-backdrop skin-apply-backdrop" role="presentation" onMouseDown={close}>
      <section className="legacy-modal skin-apply-modal" role="dialog" aria-modal="true" aria-label="Apply Skin" onMouseDown={(event) => event.stopPropagation()}>
        {state === "idle" ? (
          <>
            <header className="legacy-modal-header"><h2>Select Account</h2><button type="button" aria-label="Close" onClick={close}><X /></button></header>
            <div className="skin-apply-body">
              <SelectMenu
                title="Choose an account"
                items={accounts}
                selectedIndex={selectedIndex}
                itemLabel={(account) => accountLabel(account as GatewayAccount)}
                onChange={(index) => {
                  setSelectedIndex(index);
                  accountRef.current = accounts[index] ?? null;
                }}
              />
            </div>
            <footer className="skin-apply-actions"><button type="button" disabled={!accounts.length} onClick={() => void confirm()}>Confirm Selection</button></footer>
          </>
        ) : (
          <div className="operation-state">
            {state === "purchasing" ? <LoaderCircle className="spin" /> : <span><Check /></span>}
            <div><h3>{state === "purchasing" ? "Purchasing Skin" : "Applied Skin"}</h3><p>{state === "purchasing" ? "This may take a few seconds..." : "Skin has been applied successfully"}</p></div>
          </div>
        )}
      </section>
    </div>
  );
}

export function JavaSkinsPage() {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [searchParams] = useSearchParams();
  const debugPreview = import.meta.env.DEV && searchParams.get("debug") === "1";
  const [skins, setSkins] = useState<JavaSkin[]>(debugPreview ? [debugSkin] : []);
  const [hasMore, setHasMore] = useState(!debugPreview);
  const [loading, setLoading] = useState(false);
  const [selectedSkinId, setSelectedSkinId] = useState<string | null>(null);
  const offsetRef = useRef(0);
  const requestInFlight = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  const loadMore = useCallback(async () => {
    if (debugPreview || gateway.status !== "connected" || requestInFlight.current || !hasMore) return;
    requestInFlight.current = true;
    setLoading(true);
    try {
      await gateway.send("java_edition/skin_list", { offset: offsetRef.current, length: pageSize });
    } catch (error) {
      requestInFlight.current = false;
      setLoading(false);
      notify(error instanceof Error ? error.message : "Unable to load skins.", "error");
    }
  }, [debugPreview, gateway.send, gateway.status, hasMore, notify]);

  useEffect(() => {
    if (!debugPreview && gateway.status === "connected" && skins.length === 0) void loadMore();
  }, [debugPreview, gateway.status, loadMore, skins.length]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      if (message.type !== "java_edition/skin_list") continue;
      try {
        const response = parseGatewayPayload<SkinListResponse & { code?: number; message?: string }>(message.payload);
        if (response.code !== undefined && Number(response.code) !== 0) {
          notify(String(response.message ?? "Unable to load skins."), "error");
          setHasMore(false);
          continue;
        }
        if (!Array.isArray(response.entities)) {
          throw new Error("Invalid skin list response.");
        }
        setSkins((current) => {
          const combined = [...current, ...response.entities];
          offsetRef.current = combined.length;
          setHasMore(combined.length < response.total);
          return combined;
        });
      } catch (error) {
        notify(error instanceof Error ? error.message : "Invalid skin list response.", "error");
      } finally {
        requestInFlight.current = false;
        setLoading(false);
      }
    }
  }, [gateway.messages, notify]);

  useEffect(() => {
    const onScroll = () => {
      if (window.innerHeight + window.scrollY >= document.body.offsetHeight - 200) void loadMore();
    };
    window.addEventListener("scroll", onScroll);
    return () => window.removeEventListener("scroll", onScroll);
  }, [loadMore]);

  const reload = () => {
    offsetRef.current = 0;
    requestInFlight.current = false;
    setSkins([]);
    setHasMore(true);
  };

  return (
    <main className="workspace-page skins-page">
      <section className="server-browser-header"><div><h1>Skins</h1><p>Purchase and use custom Minecraft character skins</p></div></section>
      <section className="skin-market-grid">
        {skins.map((skin, index) => <JavaSkinCard skin={skin} onSelect={() => setSelectedSkinId(skin.entity_id)} key={`skin-${index}-${skin.entity_id}`} />)}
      </section>
      <div className="server-list-footer">
        {loading ? <div>Loading more servers...</div> : null}
        {!hasMore && !loading ? <><div>You've reached the end of the server list</div><button type="button" onClick={reload}>Reload</button></> : null}
      </div>
      {selectedSkinId ? <ApplySkinModal skinId={selectedSkinId} debugPreview={debugPreview} onClose={() => setSelectedSkinId(null)} /> : null}
    </main>
  );
}
