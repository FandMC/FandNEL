import { useState } from "react";
import type { JavaGameDetails } from "./JavaJoinGameModal";

type Resource = Record<string, unknown>;

function text(item: Resource, key: string): string {
  const value = item[key];
  return value === undefined || value === null ? "" : String(value);
}

function strings(value: unknown): string[] {
  return Array.isArray(value) ? value.map(String) : [];
}

export function JavaGameDetailsSkeleton() {
  return <div className="detail-no-roles" aria-label="正在加载服务器详情" aria-busy="true">加载中...</div>;
}

export function JavaGameDetailsPanel({ details, rental }: { details: JavaGameDetails; rental: boolean }) {
  const [failedSource, setFailedSource] = useState("");
  const images = rental ? [text(details, "image_url")] : strings(details.brief_image_urls);
  const source = images.find((image) => image.trim()) ?? "";
  const description = text(details, rental ? "brief_summary" : "detail_description").replace(/<img[^>]*>/gi, "");

  return (
    <>
      {source ? (
        <div className="server-detail-images">
          {failedSource === source ? <div className="detail-img-placeholder" /> : <img src={source} alt="服务器预览图片" onError={() => setFailedSource(source)} />}
        </div>
      ) : null}
      {description ? <div className="server-detail-desc" dangerouslySetInnerHTML={{ __html: description }} /> : null}
    </>
  );
}
