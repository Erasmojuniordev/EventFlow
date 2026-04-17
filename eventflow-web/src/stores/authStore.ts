/*
  ZUSTAND — GERENCIAMENTO DE ESTADO DE AUTENTICAÇÃO
  ──────────────────────────────────────────────────
  O que é Zustand?
  Uma biblioteca de gerenciamento de estado minimalista para React.
  Alternativas: Redux Toolkit (mais verbose, mais poder), Jotai (atomic),
  React Context (nativo, mas re-renderiza toda a árvore).

  POR QUÊ Zustand e não React Context para auth?
  - Context re-renderiza TODOS os consumidores quando qualquer parte do estado muda
  - Zustand re-renderiza apenas os componentes que usam o slice de estado que mudou
  - Sintaxe mais simples que Redux, sem boilerplate de actions/reducers
  - Funciona fora de componentes React (ex: interceptors do Axios, testes)

  POR QUÊ persist middleware?
  O estado do Zustand vive em memória — ao recarregar a página, o usuário
  seria deslogado mesmo com um token válido. O middleware persist serializa
  o estado no localStorage e o reidrata na inicialização.

  ATENÇÃO: apenas user e token vão para o localStorage — informações
  sensíveis como senha NUNCA devem ser persistidas.
*/

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { jwtDecode } from 'jwt-decode';
import { authApi } from '../services/api';
import type { User, LoginRequest, RegisterRequest } from '../types';

// ─────────────────────────────────────────────
// TIPOS DA STORE
// ─────────────────────────────────────────────

interface AuthState {
  /** Usuário autenticado; null se não logado */
  user: User | null;

  /** Access token JWT; null se não logado */
  accessToken: string | null;

  /** True durante chamadas de login/register/refresh */
  isLoading: boolean;

  /** Mensagem de erro da última operação de auth */
  error: string | null;
}

interface AuthActions {
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;

  /** Limpa o estado sem chamar o backend (ex: refresh falhou) */
  clearAuth: () => void;

  /** Verifica se o token atual ainda é válido (não expirado) */
  isAuthenticated: () => boolean;

  /** Verifica se o usuário tem determinada role */
  hasRole: (role: string) => boolean;
}

type AuthStore = AuthState & AuthActions;

// ─────────────────────────────────────────────
// HELPERS
// ─────────────────────────────────────────────

/** Claims que extraímos do JWT — deve corresponder ao que o backend coloca no token */
interface JwtClaims {
  sub: string;        // userId
  email: string;
  name: string;
  role: string | string[];  // .NET Identity pode enviar string única ou array
  exp: number;        // Unix timestamp de expiração
}

function parseToken(token: string): User {
  const claims = jwtDecode<JwtClaims>(token);
  return {
    id: claims.sub,
    email: claims.email,
    name: claims.name,
    // .NET Identity envia role como string se há 1 role, array se há múltiplas
    roles: Array.isArray(claims.role) ? claims.role : [claims.role],
  };
}

function isTokenExpired(token: string): boolean {
  try {
    const { exp } = jwtDecode<JwtClaims>(token);
    // Margem de 10s para evitar race condition entre verificação e uso do token
    return Date.now() >= (exp * 1000) - 10_000;
  } catch {
    return true;
  }
}

// ─────────────────────────────────────────────
// STORE
// ─────────────────────────────────────────────

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({
      // Estado inicial
      user: null,
      accessToken: null,
      isLoading: false,
      error: null,

      // ── ACTIONS ──────────────────────────────

      login: async (data: LoginRequest) => {
        set({ isLoading: true, error: null });
        try {
          const response = await authApi.login(data);
          const { accessToken, user } = response.data;

          localStorage.setItem('accessToken', accessToken);
          set({ user, accessToken, isLoading: false });
        } catch (err: unknown) {
          const message = extractErrorMessage(err) ?? 'Credenciais inválidas.';
          set({ isLoading: false, error: message });
          throw err;  // Propaga para o componente exibir feedback
        }
      },

      register: async (data: RegisterRequest) => {
        set({ isLoading: true, error: null });
        try {
          const response = await authApi.register(data);
          const { accessToken, user } = response.data;

          localStorage.setItem('accessToken', accessToken);
          set({ user, accessToken, isLoading: false });
        } catch (err: unknown) {
          const message = extractErrorMessage(err) ?? 'Erro ao criar conta.';
          set({ isLoading: false, error: message });
          throw err;
        }
      },

      logout: async () => {
        try {
          // Tenta revogar o refresh token no backend
          await authApi.logout();
        } catch {
          // Falha silenciosa — o importante é limpar o estado local
        } finally {
          get().clearAuth();
        }
      },

      clearAuth: () => {
        localStorage.removeItem('accessToken');
        set({ user: null, accessToken: null, error: null });
      },

      isAuthenticated: () => {
        const { accessToken } = get();
        if (!accessToken) return false;
        return !isTokenExpired(accessToken);
      },

      hasRole: (role: string) => {
        const { user } = get();
        return user?.roles.includes(role) ?? false;
      },
    }),

    {
      name: 'eventflow-auth',  // chave no localStorage

      /*
        Persiste apenas user e accessToken — isLoading e error são estado
        transitório que não faz sentido restaurar entre sessões.
      */
      partialize: (state) => ({
        user: state.user,
        accessToken: state.accessToken,
      }),

      /*
        onRehydrateStorage: ao reidratar do localStorage, verifica se o token
        ainda é válido. Se expirou, limpa o estado para forçar novo login.
      */
      onRehydrateStorage: () => (state) => {
        if (state?.accessToken && isTokenExpired(state.accessToken)) {
          state.clearAuth();
        } else if (state?.accessToken && !state.user) {
          // Token existe mas user não foi persistido — reconstrói do token
          try {
            state.user = parseToken(state.accessToken);
          } catch {
            state.clearAuth();
          }
        }
      },
    }
  )
);

// ─────────────────────────────────────────────
// LISTENER DE LOGOUT FORÇADO
// ─────────────────────────────────────────────

/*
  O interceptor de Axios (api.ts) dispara 'auth:logout' quando o refresh
  token falha. Aqui escutamos esse evento para limpar o estado da store
  sem criar dependência circular (store → api → store).
*/
window.addEventListener('auth:logout', () => {
  useAuthStore.getState().clearAuth();
});

// ─────────────────────────────────────────────
// HELPER LOCAL
// ─────────────────────────────────────────────

function extractErrorMessage(err: unknown): string | null {
  if (
    typeof err === 'object' &&
    err !== null &&
    'response' in err
  ) {
    const response = (err as { response?: { data?: { detail?: string; title?: string } } }).response;
    return response?.data?.detail ?? response?.data?.title ?? null;
  }
  return null;
}
