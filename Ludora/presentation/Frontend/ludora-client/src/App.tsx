import { Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./components/auth/ProtectedRoute";
import { HomePage } from "./pages/HomePage";
import { LoginPage } from "./pages/LoginPage";
import { SettingsPage } from "./pages/SettingsPage";
import { RegisterPage } from "./pages/RegisterPage";
import { GamesPage } from "./pages/GamesPage";
import { GameDetailPage } from "./pages/GameDetailPage";
import { CatalogEntityPage } from "./pages/CatalogEntityPage";

function App() {
  return (
    <Routes>
      <Route
        path="/"
        element={<HomePage />}
      />

      <Route
        path="/login"
        element={<LoginPage />}
      />

      <Route
        path="/register"
        element={<RegisterPage />}
      />
      <Route
        path="/games"
        element={<GamesPage />}
      />

      <Route
        path="/games/:source/:id"
        element={<GameDetailPage />}
      />

      <Route
        path="/genres/:source/:id"
        element={<CatalogEntityPage kind="genre" />}
      />

      <Route
        path="/platforms/:source/:id"
        element={<CatalogEntityPage kind="platform" />}
      />

      <Route
        path="/companies/:source/:id"
        element={<CatalogEntityPage kind="company" />}
      />
      
      <Route
        path="/settings"
        element={
          <ProtectedRoute>
            <SettingsPage />
          </ProtectedRoute>
        }
      />
    </Routes>
  );
}

export default App;