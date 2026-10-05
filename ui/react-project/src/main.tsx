import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./styles.css";
import "./styles/neo.css";
import "./styles/neo-reference/index.css";
import "./styles/neo-reference/fandnel-overrides.css";
import { applyAppearance } from "./lib/appearance";

applyAppearance();

const root = document.getElementById("root");

if (!root) {
  throw new Error("React root element was not found.");
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
