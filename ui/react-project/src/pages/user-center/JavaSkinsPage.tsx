import { Check, LoaderCircle, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
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

function accountLabel(account: GatewayAccount): string {
  return account.alias || account.id;
}

function JavaSkinCard({ skin, onSelect }: { skin: JavaSkin; onSelect(): void }) {
  return (
    <button className="neo-skin-card" type="button" aria-label={`应用 ${skin.name} 皮肤`} onClick={onSelect}>
      <span className="neo-skin-placeholder" />
      {skin.title_image_url ? <img className="neo-skin-image" src={skin.title_image_url} alt={`${skin.name} 皮肤`} loading="lazy" onError={(event) => { event.currentTarget.hidden = true; }} /> : null}
      <span className="neo-skin-gradient" />
      <span className="neo-skin-body">
        <strong className="neo-skin-name" title={skin.name}>{skin.name}</strong>
        <span className="neo-skin-summary" title={skin.brief_summary}>{skin.brief_summary}</span>
        <small className="neo-skin-likes">{skin.like_num} 次点赞</small>
        <span className="neo-skin-apply">应用</span>
      </span>
    </button>
  );
}

function ApplySkinModal({ skinId, onClose }: { skinId: string; onClose(): void }) {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [accounts, setAccounts] = useState<GatewayAccount[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [state, setState] = useState<ApplyState>("idle");
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));
  const pollTimer = useRef<number | null>(null);
  const accountRef = useRef<GatewayAccount | null>(null);

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
        notify(error instanceof Error ? error.message : "无法查询皮肤购买状态。", "error");
      }
    };
    void run();
  }, [gateway.send, notify, stopPolling]);

  useEffect(() => {
    void gateway.send("get_accounts", "available").catch((error) => {
      notify(error instanceof Error ? error.message : "无法加载账号。", "error");
    });
    return stopPolling;
  }, [gateway.send, notify, stopPolling]);

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
        notify(error instanceof Error ? error.message : "皮肤服务返回的数据无效。", "error");
      }
    }
  }, [applySkin, close, gateway.messages, gateway.send, notify, pollPurchase, skinId, stopPolling]);

  const confirm = async () => {
    const account = accounts[selectedIndex];
    if (!account) return;
    accountRef.current = account;
    setState("purchasing");
    try {
      await gateway.send("java_edition/skin_details", { user_id: account.id, item_id: skinId });
    } catch (error) {
      setState("idle");
      notify(error instanceof Error ? error.message : "无法查询皮肤拥有状态。", "error");
    }
  };

  return (
    <div className="legacy-modal-backdrop skin-apply-backdrop" role="presentation" onMouseDown={close}>
      <section className="legacy-modal skin-apply-modal neo-skin-modal" role="dialog" aria-modal="true" aria-label="应用皮肤" onMouseDown={(event) => event.stopPropagation()}>
        {state === "idle" ? (
          <>
            <header className="legacy-modal-header"><h2>选择账号</h2><button type="button" aria-label="关闭" onClick={close}><X /></button></header>
            <div className="skin-apply-body">
              <SelectMenu
                title="请选择账号"
                items={accounts}
                selectedIndex={selectedIndex}
                itemLabel={(account) => accountLabel(account as GatewayAccount)}
                onChange={(index) => {
                  setSelectedIndex(index);
                  accountRef.current = accounts[index] ?? null;
                }}
              />
            </div>
            <footer className="skin-apply-actions"><button type="button" disabled={!accounts.length} onClick={() => void confirm()}>确认选择</button></footer>
          </>
        ) : (
          <div className="operation-state">
            {state === "purchasing" ? <LoaderCircle className="spin" /> : <span><Check /></span>}
            <div><h3>{state === "purchasing" ? "正在购买皮肤" : "皮肤已应用"}</h3><p>{state === "purchasing" ? "这可能需要几秒钟…" : "皮肤已成功应用"}</p></div>
          </div>
        )}
      </section>
    </div>
  );
}

export function JavaSkinsPage() {
  const gateway = useGateway();
  const { notify } = useToasts();
  const [skins, setSkins] = useState<JavaSkin[]>([]);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [selectedSkinId, setSelectedSkinId] = useState<string | null>(null);
  const pageRef = useRef<HTMLElement>(null);
  const offsetRef = useRef(0);
  const requestInFlight = useRef(false);
  const previousMessage = useRef<GatewayMessage | undefined>(gateway.messages.at(-1));

  const loadMore = useCallback(async () => {
    if (gateway.status !== "connected" || requestInFlight.current || !hasMore) return;
    requestInFlight.current = true;
    setLoading(true);
    try {
      await gateway.send("java_edition/skin_list", { offset: offsetRef.current, length: pageSize });
    } catch (error) {
      requestInFlight.current = false;
      setLoading(false);
      notify(error instanceof Error ? error.message : "无法加载皮肤。", "error");
    }
  }, [gateway.send, gateway.status, hasMore, notify]);

  useEffect(() => {
    if (gateway.status === "connected" && skins.length === 0) void loadMore();
  }, [gateway.status, loadMore, skins.length]);

  useEffect(() => {
    const pending = consumeGatewayMessages(gateway.messages, previousMessage);
    for (const message of pending) {
      if (message.type !== "java_edition/skin_list") continue;
      try {
        const response = parseGatewayPayload<SkinListResponse & { code?: number; message?: string }>(message.payload);
        if (response.code !== undefined && Number(response.code) !== 0) {
          notify(String(response.message ?? "无法加载皮肤。"), "error");
          setHasMore(false);
          continue;
        }
        if (!Array.isArray(response.entities)) {
          throw new Error("皮肤列表返回的数据无效。");
        }
        setSkins((current) => {
          const combined = [...current, ...response.entities];
          offsetRef.current = combined.length;
          setHasMore(combined.length < response.total);
          return combined;
        });
      } catch (error) {
        notify(error instanceof Error ? error.message : "皮肤列表返回的数据无效。", "error");
      } finally {
        requestInFlight.current = false;
        setLoading(false);
      }
    }
  }, [gateway.messages, notify]);

  useEffect(() => {
    const container = pageRef.current?.closest<HTMLElement>(".neo-content");
    const onScroll = () => {
      const nearBottom = container
        ? container.scrollTop + container.clientHeight >= container.scrollHeight - 200
        : window.innerHeight + window.scrollY >= document.body.offsetHeight - 200;
      if (nearBottom) void loadMore();
    };
    const scrollTarget = container ?? window;
    scrollTarget.addEventListener("scroll", onScroll);
    return () => scrollTarget.removeEventListener("scroll", onScroll);
  }, [loadMore]);

  const reload = () => {
    offsetRef.current = 0;
    requestInFlight.current = false;
    setSkins([]);
    setHasMore(true);
  };

  return (
    <main className="workspace-page skins-page neo-skins-page" ref={pageRef}>
      <header className="neo-management-header"><div><h1>皮肤</h1><p>浏览和应用 Minecraft 角色皮肤</p></div></header>
      {!skins.length && !loading && !hasMore ? <div className="neo-management-empty">暂无皮肤</div> : null}
      <section className="neo-skin-grid">
        {skins.map((skin, index) => <JavaSkinCard skin={skin} onSelect={() => setSelectedSkinId(skin.entity_id)} key={`skin-${index}-${skin.entity_id}`} />)}
      </section>
      <div className="neo-skin-list-footer">
        {loading ? <div>正在加载更多皮肤…</div> : null}
        {!hasMore && !loading ? <><div>已加载全部皮肤</div><button className="neo-management-button" type="button" onClick={reload}>重新加载</button></> : null}
      </div>
      {selectedSkinId ? <ApplySkinModal skinId={selectedSkinId} onClose={() => setSelectedSkinId(null)} /> : null}
    </main>
  );
}
