import { Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./components/auth/ProtectedRoute";
import { HomePage } from "./pages/HomePage";
import { LoginPage } from "./pages/LoginPage";
import { SettingsPage } from "./pages/SettingsPage";
import { RegisterPage } from "./pages/RegisterPage";
import { ResendEmailConfirmationPage } from "./pages/ResendEmailConfirmationPage";
import { ProfilePage } from "./pages/ProfilePage";
import { GamesPage } from "./pages/GamesPage";
import { GameDetailPage } from "./pages/GameDetailPage";

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
        path="/resend-email-confirmation"
        element={<ResendEmailConfirmationPage />}
      />

      <Route
        path="/users/:userName"
        element={<ProfilePage />}
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