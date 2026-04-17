/*
  APP.TSX — ROTEADOR RAIZ DA APLICAÇÃO
  ─────────────────────────────────────
  Este arquivo define a estrutura de rotas usando React Router v6.

  CONCEITOS IMPORTANTES:

  BrowserRouter vs HashRouter:
  - BrowserRouter usa a History API do browser (/eventos, /login)
  - HashRouter usa o hash da URL (/#/eventos) — não requer configuração de servidor
  - Usamos BrowserRouter: URLs mais limpas, Vite já serve corretamente em dev
  - Em produção: o servidor precisa retornar index.html para qualquer rota

  Routes + Route (v6) vs Switch + Route (v5):
  - v6 elimina o <Switch>, usa <Routes> com matching automático
  - <Route element={<Componente />}> — JSX direto, não component prop
  - Rotas aninhadas: <Route path="/organizer/*"> agrupa subrotas do organizador

  Lazy loading (React.lazy + Suspense):
  - Divide o bundle em chunks: cada página é carregada só quando acessada
  - POR QUÊ? O bundle inicial fica menor, o app carrega mais rápido
  - <Suspense fallback={<LoadingSpinner />}> exibe loading enquanto o chunk carrega
  - Sem Suspense, React lança um erro quando encontra um lazy component
*/

import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from './components/layout/Layout';
import { ProtectedRoute } from './components/layout/ProtectedRoute';
import { LoadingSpinner } from './components/ui/LoadingSpinner';

// ── Lazy imports — cada página vira um chunk separado no build ──────────────
const HomePage        = lazy(() => import('./pages/HomePage'));
const LoginPage       = lazy(() => import('./pages/LoginPage'));
const RegisterPage    = lazy(() => import('./pages/RegisterPage'));
const EventDetailPage = lazy(() => import('./pages/EventDetailPage'));
const MyTicketsPage   = lazy(() => import('./pages/MyTicketsPage'));
const OrganizerPage   = lazy(() => import('./pages/OrganizerPage'));

// ── Fallback de carregamento entre navegações ────────────────────────────────
function PageLoader() {
  return (
    <div className="flex items-center justify-center min-h-screen bg-canvas">
      <LoadingSpinner size="lg" />
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Rotas públicas com layout (Navbar + container) */}
          <Route element={<Layout />}>
            <Route index element={<HomePage />} />
            <Route path="/eventos/:id" element={<EventDetailPage />} />
          </Route>

          {/* Rotas de autenticação — sem Navbar */}
          <Route path="/login" element={<LoginPage />} />
          <Route path="/registro" element={<RegisterPage />} />

          {/* Rotas protegidas — requer login */}
          <Route element={<ProtectedRoute />}>
            <Route element={<Layout />}>
              <Route path="/meus-ingressos" element={<MyTicketsPage />} />
            </Route>
          </Route>

          {/* Rotas protegidas — requer role Organizer */}
          <Route element={<ProtectedRoute requiredRole="Organizer" />}>
            <Route element={<Layout />}>
              <Route path="/organizer" element={<OrganizerPage />} />
            </Route>
          </Route>

          {/* Rota 404 — redireciona para home */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
