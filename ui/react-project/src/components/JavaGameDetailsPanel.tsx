import { Play } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { JavaGameDetails } from "./JavaJoinGameModal";

type Resource = Record<string, unknown>;

function text(item: Resource, key: string): string {
  const value = item[key];
  return value === undefined || value === null ? "" : String(value);
}

function number(item: Resource, key: string): number {
  const value = Number(item[key]);
  return Number.isFinite(value) ? value : 0;
}

function resources(value: unknown): Resource[] {
  return Array.isArray(value)
    ? value.filter((item): item is Resource => Boolean(item && typeof item === "object"))
    : [];
}

function strings(value: unknown): string[] {
  return Array.isArray(value) ? value.map(String) : [];
}

function MediaCarousel({ videos, images }: { videos: Resource[]; images: string[] }) {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [playing, setPlaying] = useState(false);
  const thumbnailsRef = useRef<HTMLDivElement>(null);
  const media = [...videos.map((video) => text(video, "cover")), ...images];
  const videoSelected = selectedIndex < videos.length;
  const source = media[selectedIndex] ?? "";
  const videoUrl = videoSelected ? text(videos[selectedIndex], "url") : "";

  const choose = (index: number) => {
    setSelectedIndex(index);
    setPlaying(false);
  };

  useEffect(() => {
    const selected = thumbnailsRef.current?.children[selectedIndex];
    selected?.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
  }, [selectedIndex]);

  if (!media.length) return <div className="java-details-media placeholder" />;

  return (
    <div className="java-details-carousel">
      <div className="java-details-media">
        {playing ? (
          videoUrl ? <video className="java-details-video" controls autoPlay playsInline><source src={videoUrl} type="video/mp4" />当前浏览器不支持视频播放。</video> : <div className="java-details-media-error">媒体加载失败</div>
        ) : <img src={source} alt="服务器预览图片" />}
        {!playing && videoSelected ? <button className="java-details-play" type="button" aria-label="播放视频" onClick={() => setPlaying(true)}><Play fill="currentColor" /></button> : null}
      </div>
      {media.length > 1 ? (
        <div className="java-details-thumbnails" ref={thumbnailsRef}>
          {media.map((image, index) => (
            <button className={selectedIndex === index ? "selected" : ""} type="button" aria-label={`查看第 ${index + 1} 项`} onClick={() => choose(index)} key={`${image}-${index}`}>
              <img src={image} alt="" />
              {index < videos.length ? <span><Play fill="currentColor" /></span> : null}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

export function JavaGameDetailsSkeleton() {
  return (
    <section className="java-server-details-layout java-details-skeleton" aria-label="正在加载服务器详情" aria-busy="true">
      <div className="java-server-details-main">
        <div className="java-details-skeleton-media" />
        <div className="java-details-skeleton-lines"><i /><i /><i /></div>
      </div>
      <aside className="java-server-details-aside"><i /><div className="java-server-facts">{Array.from({ length: 5 }, (_, index) => <div key={index} />)}</div></aside>
    </section>
  );
}

function JavaServerFacts({ details, rental }: { details: JavaGameDetails; rental: boolean }) {
  const versions = rental
    ? text(details, "mc_version")
    : resources(details.mc_version_list).map((item) => text(item, "name")).join(", ");
  const publishTime = number(details, rental ? "begin_time" : "publish_time");
  const developer = text(details, rental ? "owner_id" : "developer_name");
  const address = text(details, rental ? "server_ip" : "server_address");
  return (
    <dl className="java-server-facts">
      <div><dt>开发者</dt><dd>{developer}</dd></div>
      <div><dt>版本</dt><dd>{versions}</dd></div>
      <div><dt>发布时间</dt><dd>{publishTime ? new Date(publishTime * 1000).toLocaleDateString("zh-CN") : ""}</dd></div>
      <div><dt>服务器地址</dt><dd>{address}:{text(details, "server_port")}</dd></div>
      <div><dt>服务器编号</dt><dd>{text(details, "entity_id")}</dd></div>
    </dl>
  );
}

export function JavaGameDetailsPanel({ details, rental, onJoin }: { details: JavaGameDetails; rental: boolean; onJoin?(): void }) {
  const videos = rental ? [] : resources(details.video_info_list);
  const images = rental ? [text(details, "image_url")] : strings(details.brief_image_urls);
  const description = text(details, rental ? "brief_summary" : "detail_description");

  return (
    <section className="java-server-details-layout">
      <div className="java-server-details-main">
        <div className="java-details-media-frame"><MediaCarousel videos={videos} images={images} /></div>
        <div className="java-server-details-mobile-actions">
          <button className="java-details-join" type="button" onClick={onJoin}>加入游戏</button>
          <JavaServerFacts details={details} rental={rental} />
        </div>
        <div className="java-server-description" dangerouslySetInnerHTML={{ __html: description }} />
      </div>
      <aside className="java-server-details-aside">
        <button className="java-details-join" type="button" onClick={onJoin}>加入游戏</button>
        <JavaServerFacts details={details} rental={rental} />
        <p>点击“加入游戏”后将自动配置启动器。</p>
      </aside>
    </section>
  );
}
