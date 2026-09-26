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
          videoUrl ? <video className="java-details-video" controls autoPlay playsInline><source src={videoUrl} type="video/mp4" />Your browser does not support the video tag.</video> : <div className="java-details-media-error">Error loading media</div>
        ) : <img src={source} alt="Carousel item" />}
        {!playing && videoSelected ? <button className="java-details-play" type="button" aria-label="Play video" onClick={() => setPlaying(true)}><Play fill="currentColor" /></button> : null}
      </div>
      {media.length > 1 ? (
        <div className="java-details-thumbnails" ref={thumbnailsRef}>
          {media.map((image, index) => (
            <button className={selectedIndex === index ? "selected" : ""} type="button" aria-label={`Go to item ${index + 1}`} onClick={() => choose(index)} key={`${image}-${index}`}>
              <img src={image} alt="" />
              {index < videos.length ? <span><Play fill="currentColor" /></span> : null}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

export function JavaGameDetailsSkeleton({ rental = false }: { rental?: boolean }) {
  if (!rental) {
    return (
      <section className="java-server-details-layout java-details-skeleton" aria-label="Loading server details" aria-busy="true">
        <div className="java-server-details-main">
          <div className="java-details-skeleton-media" />
          <div className="java-details-skeleton-lines"><i /><i /><i /></div>
        </div>
        <aside className="java-server-details-aside"><i /><div className="java-server-facts">{Array.from({ length: 5 }, (_, index) => <div key={index} />)}</div></aside>
      </section>
    );
  }

  return (
    <section className="java-server-details-card java-details-skeleton" aria-label="Loading server details" aria-busy="true">
      <div className="java-details-skeleton-media" />
      <div className="java-server-facts">{Array.from({ length: 4 }, (_, index) => <div key={index} />)}</div>
      <div className="java-details-skeleton-lines"><i /><i /><i /></div>
    </section>
  );
}

function JavaServerFacts({ details }: { details: JavaGameDetails }) {
  const versions = resources(details.mc_version_list).map((item) => text(item, "name")).join(", ");
  const publishTime = number(details, "publish_time");
  return (
    <dl className="java-server-facts">
      <div><dt>Developer</dt><dd>{text(details, "developer_name")}</dd></div>
      <div><dt>Version</dt><dd>{versions}</dd></div>
      <div><dt>Published</dt><dd>{publishTime ? new Date(publishTime * 1000).toLocaleDateString() : ""}</dd></div>
      <div><dt>Server IP</dt><dd>{text(details, "server_address")}:{text(details, "server_port")}</dd></div>
      <div><dt>Server Id</dt><dd>{text(details, "entity_id")}</dd></div>
    </dl>
  );
}

export function JavaGameDetailsPanel({ details, rental, onJoin }: { details: JavaGameDetails; rental: boolean; onJoin?(): void }) {
  const videos = rental ? [] : resources(details.video_info_list);
  const images = rental ? [text(details, "image_url")] : strings(details.brief_image_urls);
  const versions = resources(details.mc_version_list);
  const description = text(details, rental ? "brief_summary" : "detail_description");
  const developer = text(details, rental ? "owner_id" : "developer_name");
  const publishTime = number(details, rental ? "begin_time" : "publish_time");
  const version = rental ? text(details, "mc_version") : versions.map((item) => text(item, "name")).join(", ");
  const address = text(details, rental ? "server_ip" : "server_address");

  if (rental) return (
    <section className="java-server-details-card">
      <div className="java-details-media">{images[0] ? <img src={images[0]} alt="Carousel item" /> : <div className="java-details-media-error">Error loading media</div>}</div>
      <dl className="java-server-facts">
        <div><dt>Developer</dt><dd>{developer}</dd></div>
        <div><dt>Publish Time</dt><dd>{new Date(publishTime * 1000).toLocaleString()}</dd></div>
        <div><dt>MC Version</dt><dd>{version}</dd></div>
        <div><dt>IP Address</dt><dd>{address}:{text(details, "server_port")}</dd></div>
      </dl>
      <div className="java-server-description" dangerouslySetInnerHTML={{ __html: description }} />
    </section>
  );

  return (
    <section className="java-server-details-layout">
      <div className="java-server-details-main">
        <div className="java-details-media-frame"><MediaCarousel videos={videos} images={images} /></div>
        <div className="java-server-details-mobile-actions">
          <button className="java-details-join" type="button" onClick={onJoin}>Join Game</button>
          <JavaServerFacts details={details} />
        </div>
        <div className="java-server-description" dangerouslySetInnerHTML={{ __html: description }} />
      </div>
      <aside className="java-server-details-aside">
        <button className="java-details-join" type="button" onClick={onJoin}>Join Game</button>
        <JavaServerFacts details={details} />
        <p>Clicking join will configure the launcher automatically.</p>
      </aside>
    </section>
  );
}
