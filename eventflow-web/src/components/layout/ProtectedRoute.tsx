/*
  PROTECTED ROUTE — GUARDA DE ROTAS
  ────────────────────────────────────
  Implementa dois tipos de proteção:
  1. Autenticação: redireciona para /login se não logado
  2. Autorização por role: redireciona para / se logado mas sem a role necessária

  O state={{ from: location }} preserva a URL original.
  Após o login, podemos redirecionar o usuário de volta:
    const location = useLocation();
    const from = location.state?.from?.pathname ?? '/';
    navigate(from, { replace: true });

  <Outlet /> renderiza as rotas filhas quando todas as verificações passam.
  POR QUÊ não retornar o componente diretamente?
  React Router v6 usa composição — ProtectedRoute é um layout route
  que envolve múltiplas rotas filhas, não um wrapper de componente único.
*/

import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';

interface ProtectedRouteProps {
  /** Role obrigatória para acessar as rotas filhas */
  requiredRole?: string;
}

export function ProtectedRoute({ requiredRole }: ProtectedRouteProps) {
  const location = useLocation();
  const { isAuthenticated, hasRole } = useAuthStore();

  if (!isAuthenticated()) {
    // Preserva a URL original para redirecionar após login
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (requiredRole && !hasRole(requiredRole)) {
    // Autenticado mas sem permissão — vai para a home silenciosamente
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
