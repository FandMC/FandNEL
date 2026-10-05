import { useEffect } from "react";
import { AppProviders } from "./context/AppContext";
import { AppRouter } from "./router";
import { observeAppearance } from "./lib/appearance";

export function App() {
  useEffect(observeAppearance, []);
  return (
    <AppProviders>
      <AppRouter />
    </AppProviders>
  );
}
