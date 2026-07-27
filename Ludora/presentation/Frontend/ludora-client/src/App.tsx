import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AdminLayout } from "./components/admin/AdminLayout";
import { AdminRoute } from "./components/admin/AdminRoute";
import { ProtectedRoute } from "./components/auth/ProtectedRoute";

const HomePage = lazy(() =>
  import("./pages/HomePage").then((module) => ({
    default: module.HomePage,
  })),
);
const LoginPage = lazy(() =>
  import("./pages/LoginPage").then((module) => ({
    default: module.LoginPage,
  })),
);
const SettingsPage = lazy(() =>
  import("./pages/SettingsPage").then((module) => ({
    default: module.SettingsPage,
  })),
);
const EditProfilePage = lazy(() =>
  import("./pages/EditProfilePage").then((module) => ({
    default: module.EditProfilePage,
  })),
);
const RegisterPage = lazy(() =>
  import("./pages/RegisterPage").then((module) => ({
    default: module.RegisterPage,
  })),
);
const ConfirmEmailPage = lazy(() =>
  import("./pages/ConfirmEmailPage").then((module) => ({
    default: module.ConfirmEmailPage,
  })),
);
const ResendEmailConfirmationPage = lazy(() =>
  import("./pages/ResendEmailConfirmationPage").then((module) => ({
    default: module.ResendEmailConfirmationPage,
  })),
);
const ProfilePage = lazy(() =>
  import("./pages/ProfilePage").then((module) => ({
    default: module.ProfilePage,
  })),
);
const GamesPage = lazy(() =>
  import("./pages/GamesPage").then((module) => ({
    default: module.GamesPage,
  })),
);
const GameDetailPage = lazy(() =>
  import("./pages/GameDetailPage").then((module) => ({
    default: module.GameDetailPage,
  })),
);
const GameListDetailPage = lazy(() =>
  import("./pages/GameListDetailPage").then((module) => ({
    default: module.GameListDetailPage,
  })),
);
const SearchPage = lazy(() =>
  import("./pages/SearchPage").then((module) => ({
    default: module.SearchPage,
  })),
);
const MessagesPage = lazy(() =>
  import("./pages/MessagesPage").then((module) => ({
    default: module.MessagesPage,
  })),
);
const ForumPage = lazy(() =>
  import("./pages/ForumPage").then((module) => ({
    default: module.ForumPage,
  })),
);
const ForumDiscussionPage = lazy(() =>
  import("./pages/ForumDiscussionPage").then((module) => ({
    default: module.ForumDiscussionPage,
  })),
);
const ForumCommunityPage = lazy(() =>
  import("./pages/ForumCommunityPage").then((module) => ({
    default: module.ForumCommunityPage,
  })),
);
const AdminDashboardPage = lazy(() =>
  import("./pages/admin/AdminDashboardPage").then((module) => ({
    default: module.AdminDashboardPage,
  })),
);
const AdminUsersPage = lazy(() =>
  import("./pages/admin/AdminUsersPage").then((module) => ({
    default: module.AdminUsersPage,
  })),
);
const AdminUserDetailPage = lazy(() =>
  import("./pages/admin/AdminUserDetailPage").then((module) => ({
    default: module.AdminUserDetailPage,
  })),
);
const AdminReportsPage = lazy(() =>
  import("./pages/admin/AdminReportsPage").then((module) => ({
    default: module.AdminReportsPage,
  })),
);
const AdminReportedUserPage = lazy(() =>
  import("./pages/admin/AdminReportedUserPage").then((module) => ({
    default: module.AdminReportedUserPage,
  })),
);
const AdminPostsPage = lazy(() =>
  import("./pages/admin/AdminPostsPage").then((module) => ({
    default: module.AdminPostsPage,
  })),
);

function RouteFallback() {
  return (
    <main
      aria-busy="true"
      aria-live="polite"
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
      }}
    >
      Səhifə yüklənir...
    </main>
  );
}

function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path="/confirm-email"
          element={<ConfirmEmailPage />}
        />
        <Route
          path="/resend-email-confirmation"
          element={<ResendEmailConfirmationPage />}
        />
        <Route path="/users/:userName" element={<ProfilePage />} />
        <Route path="/games" element={<GamesPage />} />
        <Route
          path="/games/:source/:id"
          element={<GameDetailPage />}
        />
        <Route
          path="/lists/:id"
          element={<GameListDetailPage />}
        />
        <Route path="/search" element={<SearchPage />} />
        <Route path="/forum" element={<ForumPage />} />
        <Route
          path="/forum/discussions/:slug"
          element={<ForumDiscussionPage />}
        />
        <Route
          path="/forum/communities/:slug"
          element={<ForumCommunityPage />}
        />
        <Route
          path="/messages"
          element={
            <ProtectedRoute>
              <MessagesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/profile/edit"
          element={
            <ProtectedRoute>
              <EditProfilePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings"
          element={
            <ProtectedRoute>
              <SettingsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin"
          element={
            <AdminRoute>
              <AdminLayout />
            </AdminRoute>
          }
        >
          <Route index element={<AdminDashboardPage />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route
            path="users/:userId"
            element={<AdminUserDetailPage />}
          />
          <Route path="reports" element={<AdminReportsPage />} />
          <Route
            path="reports/users/:userId"
            element={<AdminReportedUserPage />}
          />
          <Route path="posts" element={<AdminPostsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
}

export default App;
