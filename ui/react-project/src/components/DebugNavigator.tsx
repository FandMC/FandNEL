import { Bug, Search, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";

interface DebugRoute {
  label: string;
  path: string;
}

interface DebugRouteGroup {
  label: string;
  routes: readonly DebugRoute[];
}

const debugRouteGroups: readonly DebugRouteGroup[] = [
  {
    label: "Core",
    routes: [
      { label: "Home redirect", path: "/" },
      { label: "Gateway", path: "/gateway" },
      { label: "Not Found", path: "/404" },
      { label: "Next Not Found", path: "/_not-found" },
    ],
  },
  {
    label: "User Center",
    routes: [
      { label: "Dashboard", path: "/user-center" },
      { label: "Servers", path: "/user-center/servers" },
      { label: "Server Details", path: "/user-center/servers/details?id=debug-server&name=Debug%20Server" },
      { label: "Rentals", path: "/user-center/rentals" },
      { label: "Rental Details", path: "/user-center/rentals/details?id=debug-rental&name=Debug%20Rental" },
      { label: "Bedrock Rentals", path: "/user-center/rentals-for-bedrock" },
      { label: "Realms Launch", path: "/user-center/rentals-for-bedrock/launch?id=debug-realms&name=Debug%20Realms" },
      { label: "Bedrock", path: "/user-center/bedrock" },
      { label: "NetServer Launch", path: "/user-center/bedrock/launch?id=debug-netserver&name=Debug%20NetServer" },
      { label: "Java Skins", path: "/user-center/java-skins" },
      { label: "Games", path: "/user-center/launchers" },
      { label: "Interceptor Config", path: "/user-center/launchers/configuration?id=debug-interceptor" },
      { label: "Active Channels", path: "/user-center/launchers/configuration/active-channels?id=debug-interceptor" },
      { label: "Packet Monitor", path: "/user-center/launchers/configuration/packet-monitor?id=debug-interceptor" },
      { label: "Interceptor Actions", path: "/user-center/launchers/configuration/actions?id=debug-interceptor" },
      { label: "Mods", path: "/user-center/mods" },
      { label: "Console", path: "/user-center/console" },
      { label: "Settings", path: "/user-center/settings" },
    ],
  },
];

const routeCount = debugRouteGroups.reduce((count, group) => count + group.routes.length, 0);

function debugTarget(path: string): string {
  const [pathname, query = ""] = path.split("?");
  const supportsPreview = pathname.startsWith("/user") || pathname === "/gateway";
  if (!supportsPreview) return path;
  const params = new URLSearchParams(query);
  params.set("debug", "1");
  return `${pathname}?${params.toString()}`;
}

export function DebugNavigator() {
  const location = useLocation();
  const containerRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const hasTopbar = true;

  const filteredGroups = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) return debugRouteGroups;
    return debugRouteGroups
      .map((group) => ({
        ...group,
        routes: group.routes.filter((route) => `${route.label} ${route.path}`.toLowerCase().includes(normalizedQuery)),
      }))
      .filter((group) => group.routes.length > 0);
  }, [query]);

  useEffect(() => {
    setOpen(false);
    setQuery("");
  }, [location.pathname, location.search]);

  useEffect(() => {
    if (!open) return;
    searchRef.current?.focus();

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    const closeOnOutsideClick = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };

    window.addEventListener("keydown", closeOnEscape);
    window.addEventListener("pointerdown", closeOnOutsideClick);
    return () => {
      window.removeEventListener("keydown", closeOnEscape);
      window.removeEventListener("pointerdown", closeOnOutsideClick);
    };
  }, [open]);

  return (
    <div ref={containerRef} className={`debug-navigator ${hasTopbar ? "debug-navigator-below-topbar" : ""}`}>
      <button
        className="debug-trigger"
        type="button"
        aria-label="Open debug page navigator"
        aria-expanded={open}
        aria-controls="debug-page-navigator"
        title="Debug page navigator"
        onClick={() => setOpen((current) => !current)}
      >
        <Bug />
        <span>Debug</span>
      </button>

      {open ? (
        <section id="debug-page-navigator" className="debug-panel" aria-label="Debug page navigator">
          <header className="debug-panel-header">
            <div>
              <strong>Debug pages</strong>
              <span>{routeCount}</span>
            </div>
            <button type="button" aria-label="Close debug page navigator" onClick={() => setOpen(false)}><X /></button>
          </header>
          <label className="debug-route-search">
            <Search />
            <input ref={searchRef} type="search" placeholder="Filter pages..." value={query} onChange={(event) => setQuery(event.target.value)} />
          </label>
          <nav className="debug-route-list" aria-label="All application pages">
            {filteredGroups.length ? filteredGroups.map((group) => (
              <div className="debug-route-group" key={group.label}>
                <h2>{group.label}</h2>
                {group.routes.map((route) => {
                  const pathname = route.path.split("?")[0];
                  const active = location.pathname === pathname;
                  const target = debugTarget(route.path);
                  return (
                    <Link className={active ? "active" : undefined} to={target} key={route.path} aria-current={active ? "page" : undefined}>
                      <span>{route.label}</span>
                      <code>{route.path}</code>
                    </Link>
                  );
                })}
              </div>
            )) : <p className="debug-route-empty">No matching pages</p>}
          </nav>
        </section>
      ) : null}
    </div>
  );
}
