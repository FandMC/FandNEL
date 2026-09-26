import { AppProviders } from "./context/AppContext";
import { AppRouter } from "./router";

export function App() {
  return (
    <AppProviders>
      <AppRouter />
    </AppProviders>
  );
}
