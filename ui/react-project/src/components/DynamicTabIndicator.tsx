import { CSSProperties, useLayoutEffect, useRef, useState } from "react";

type IndicatorGeometry = {
  left: number;
  top: number;
  visible: boolean;
  width: number;
};

type DynamicTabIndicatorProps = {
  activeKey: string;
};

export function DynamicTabIndicator({ activeKey }: DynamicTabIndicatorProps) {
  const indicatorRef = useRef<HTMLSpanElement>(null);
  const [transitionsEnabled, setTransitionsEnabled] = useState(false);
  const [geometry, setGeometry] = useState<IndicatorGeometry>({ left: 0, top: 0, visible: false, width: 0 });

  useLayoutEffect(() => {
    const container = indicatorRef.current?.parentElement;
    if (!container) return;

    const update = () => {
      const activeTab = Array.from(container.querySelectorAll<HTMLElement>("[data-indicator-key]"))
        .find((element) => element.dataset.indicatorKey === activeKey);

      if (!activeTab) {
        setGeometry((current) => current.visible ? { ...current, visible: false } : current);
        return;
      }

      const next = {
        left: activeTab.offsetLeft,
        top: activeTab.offsetTop + activeTab.offsetHeight - 2,
        visible: true,
        width: activeTab.offsetWidth,
      };
      setGeometry((current) => (
        current.left === next.left
        && current.top === next.top
        && current.visible === next.visible
        && current.width === next.width
          ? current
          : next
      ));
    };

    update();
    const frame = window.requestAnimationFrame(() => {
      update();
      setTransitionsEnabled(true);
    });
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(update);
    observer?.observe(container);
    container.querySelectorAll<HTMLElement>("[data-indicator-key]").forEach((element) => observer?.observe(element));
    window.addEventListener("resize", update);

    return () => {
      window.cancelAnimationFrame(frame);
      observer?.disconnect();
      window.removeEventListener("resize", update);
    };
  }, [activeKey]);

  const style: CSSProperties = {
    opacity: geometry.visible ? 1 : 0,
    transform: `translate3d(${geometry.left}px, ${geometry.top}px, 0)`,
    width: geometry.width,
  };

  return <span ref={indicatorRef} className={`dynamic-tab-indicator${transitionsEnabled ? " is-ready" : ""}`} style={style} aria-hidden="true" />;
}
