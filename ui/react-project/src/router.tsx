import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import {
  AppLayout,
  RequireUserCenter,
  UserCenterLayout,
} from "./layouts/AppLayout";
import {
  GatewayPage,
  HomeRedirect,
  NotFoundPage,
} from "./pages/PublicPages";
import {
  BedrockLaunchPage,
  BedrockRealmLaunchPage,
  BedrockRealmsPage,
  BedrockRentalLaunchPage,
  BedrockRentalsPage,
  BedrockServersPage,
  GatewaySettingsPage,
  RentalDetailsPage,
  RentalsPage,
  ServerDetailsPage,
  ServersPage,
} from "./pages/UserCenterPages";
import { DashboardPage } from "./pages/user-center/DashboardPage";
import { JavaSkinsPage } from "./pages/user-center/JavaSkinsPage";
import {
  InterceptorConfigPage,
  InterceptorConfigurationLayout,
  InterceptorPlaceholderPage,
} from "./pages/user-center/InterceptorConfiguration";
import { ConsolePage, LaunchersPage, ModsPage } from "./pages/user-center/ManagementPages";

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<HomeRedirect />} />
          <Route path="gateway" element={<GatewayPage />} />
          <Route path="user-center" element={<RequireUserCenter><UserCenterLayout /></RequireUserCenter>}>
            <Route index element={<DashboardPage />} />
            <Route path="servers" element={<ServersPage />} />
            <Route path="servers/details" element={<ServerDetailsPage />} />
            <Route path="rentals" element={<RentalsPage />} />
            <Route path="rentals/details" element={<RentalDetailsPage />} />
            <Route path="rentals-for-bedrock" element={<BedrockRentalsPage />} />
            <Route path="rentals-for-bedrock/launch" element={<BedrockRentalLaunchPage />} />
            <Route path="bedrock-realms" element={<BedrockRealmsPage />} />
            <Route path="bedrock-realms/launch" element={<BedrockRealmLaunchPage />} />
            <Route path="bedrock" element={<BedrockServersPage />} />
            <Route path="bedrock/launch" element={<BedrockLaunchPage />} />
            <Route path="launchers" element={<LaunchersPage />} />
            <Route path="launchers/configuration" element={<InterceptorConfigurationLayout />}>
              <Route index element={<InterceptorConfigPage />} />
              <Route path="actions" element={<InterceptorPlaceholderPage section="Actions" />} />
              <Route path="active-channels" element={<InterceptorPlaceholderPage section="Active Channels" />} />
              <Route path="packet-monitor" element={<InterceptorPlaceholderPage section="Packet Monitor" />} />
            </Route>
            <Route path="java-skins" element={<JavaSkinsPage />} />
            <Route path="mods" element={<ModsPage />} />
            <Route path="console" element={<ConsolePage />} />
            <Route path="settings" element={<GatewaySettingsPage />} />
          </Route>

          <Route path="user/settings" element={<Navigate to="/user-center/settings" replace />} />
          <Route path="user/settings/*" element={<Navigate to="/user-center/settings" replace />} />

          <Route path="404" element={<NotFoundPage />} />
          <Route path="_not-found" element={<NotFoundPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
