/*
  CLIENTE HTTP — AXIOS COM REFRESH TOKEN AUTOMÁTICO
  ──────────────────────────────────────────────────
  Este arquivo configura o Axios como cliente HTTP central da aplicação.

  FLUXO DE AUTENTICAÇÃO COM JWT:
  1. Login → backend retorna access token (15min) + seta cookie httpOnly refresh token (7 dias)
  2. Cada request → interceptor de request injeta "Authorization: Bearer <token>" no header
  3. Token expirado (401) → interceptor de response chama /api/auth/refresh automaticamente
  4. /api/auth/refresh usa o cookie httpOnly (enviado automaticamente pelo browser) para emitir
     um novo access token
  5. Retenta a request original com o novo token

  POR QUÊ withCredentials: true?
  Cookies httpOnly não são enviados automaticamente por Axios — é preciso setar
  withCredentials: true para que o browser inclua cookies cross-origin.
  No backend, isso requer AllowCredentials() no CORS.

  POR QUÊ a fila de requests pendentes (requestQueue)?
  Se múltiplas requests chegam com 401 ao mesmo tempo (ex: 3 chamadas paralelas
  com token expirado), sem a fila cada uma tentaria fazer o refresh simultaneamente,
  causando race condition e invalidação mútua de tokens.
  A fila garante que apenas UM refresh aconteça; as demais aguardam o resultado.
*/

import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { AuthResponse } from '../types';

// ─────────────────────────────────────────────
// INSTÂNCIA PRINCIPAL
// ─────────────────────────────────────────────

const api = axios.create({
  baseURL: '/api',           // Vite proxy redireciona /api → http://localhost:5000
  withCredentials: true,     // Envia cookies httpOnly (refresh token) em cross-origin
  headers: {
    'Content-Type': 'application/json',
  },
});

// ─────────────────────────────────────────────
// CONTROLE DE REFRESH TOKEN
// ─────────────────────────────────────────────

/** Flag que evita loops infinitos de refresh */
let isRefreshing = false;

/**
 * Fila de callbacks aguardando o refresh completar.
 * Cada item é uma função que resolve ou rejeita a request original.
 *
 * POR QUÊ callbacks e não Promises diretamente?
 * Permite que múltiplas requests aguardem o mesmo refresh sem criar
 * referências circulares ou memory leaks.
 */
let requestQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: unknown) => void;
}> = [];

/** Notifica todos na fila com o novo token (ou erro) */
function flushQueue(error: unknown, token: string | null) {
  requestQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token!);
    }
  });
  requestQueue = [];
}

// ─────────────────────────────────────────────
// INTERCEPTOR DE REQUEST — injeta access token
// ─────────────────────────────────────────────

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  /*
    Lê o token do localStorage.
    POR QUÊ localStorage e não memória?
    Memória seria mais segura (não exposta a XSS), mas o token é perdido
    em refresh de página. Para este projeto didático, localStorage é suficiente.
    Em produção com requisitos de segurança elevados: considere memory-only com
    silent refresh via iframe ou service worker.
  */
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ─────────────────────────────────────────────
// INTERCEPTOR DE RESPONSE — trata 401
// ─────────────────────────────────────────────

api.interceptors.response.use(
  // Resposta bem-sucedida: passa direto
  (response) => response,

  // Erro: verificar se é 401 e tentar refresh
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Só tenta refresh em 401 que ainda não foram retentados
    // _retry evita loop infinito caso o próprio refresh retorne 401
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      if (isRefreshing) {
        // Outro refresh já está em andamento — entra na fila
        return new Promise((resolve, reject) => {
          requestQueue.push({
            resolve: (token: string) => {
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(api(originalRequest));
            },
            reject,
          });
        });
      }

      isRefreshing = true;

      try {
        // Chama endpoint de refresh — backend usa o cookie httpOnly automaticamente
        const response = await axios.post<AuthResponse>(
          '/api/auth/refresh',
          {},
          { withCredentials: true }
        );

        const { accessToken } = response.data;
        localStorage.setItem('accessToken', accessToken);

        // Atualiza o header da request original e libera a fila
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        flushQueue(null, accessToken);

        return api(originalRequest);
      } catch (refreshError) {
        // Refresh falhou (token expirado ou inválido) — logout
        flushQueue(refreshError, null);
        localStorage.removeItem('accessToken');

        /*
          Dispara evento customizado para que o authStore possa limpar o estado
          sem que este módulo precise importar a store (evita dependência circular).
          O authStore escuta 'auth:logout' e faz o redirect para /login.
        */
        window.dispatchEvent(new Event('auth:logout'));

        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

// ─────────────────────────────────────────────
// ENDPOINTS — AUTENTICAÇÃO
// ─────────────────────────────────────────────

export const authApi = {
  login: (data: { email: string; password: string }) =>
    api.post<AuthResponse>('/auth/login', data),

  register: (data: { name: string; email: string; password: string; role: string }) =>
    api.post<AuthResponse>('/auth/register', data),

  /** Refresh silencioso — usa cookie httpOnly, não precisa de body */
  refresh: () => api.post<AuthResponse>('/auth/refresh'),

  /** Revoga refresh token no backend e limpa o cookie */
  logout: () => api.post('/auth/logout'),
};

// ─────────────────────────────────────────────
// ENDPOINTS — EVENTOS
// ─────────────────────────────────────────────

import type { Event, CreateEventRequest, UpdateEventRequest, EventFilters, PaginatedResult } from '../types';

export const eventsApi = {
  list: (filters?: EventFilters) =>
    api.get<PaginatedResult<Event>>('/events', { params: filters }),

  getById: (id: string) =>
    api.get<Event>(`/events/${id}`),

  create: (data: CreateEventRequest) =>
    api.post<Event>('/events', data),

  update: (id: string, data: UpdateEventRequest) =>
    api.put<Event>(`/events/${id}`, data),

  delete: (id: string) =>
    api.delete(`/events/${id}`),

  publish: (id: string) =>
    api.post<Event>(`/events/${id}/publish`),

  cancel: (id: string) =>
    api.post<Event>(`/events/${id}/cancel`),
};

// ─────────────────────────────────────────────
// ENDPOINTS — INGRESSOS
// ─────────────────────────────────────────────

import type { Ticket, BookTicketResponse, WaitlistPositionDto } from '../types';

export const ticketsApi = {
  book: (eventId: string) =>
    api.post<BookTicketResponse>(`/events/${eventId}/tickets`),

  confirm: (ticketId: string) =>
    api.post<Ticket>(`/tickets/${ticketId}/confirm`),

  cancel: (ticketId: string) =>
    api.delete(`/tickets/${ticketId}`),

  mine: () =>
    api.get<Ticket[]>('/tickets/mine'),

  waitlistPosition: (eventId: string) =>
    api.get<WaitlistPositionDto>(`/events/${eventId}/waitlist/position`),
};

export default api;
