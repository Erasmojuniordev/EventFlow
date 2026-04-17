/*
  TIPOS CENTRAIS DO EVENTFLOW — FRONTEND
  ──────────────────────────────────────
  Estes tipos espelham os DTOs do backend (src/EventFlow.Application/.../DTOs/).

  POR QUÊ tipos explícitos e não "any"?
  - `any` desabilita toda a verificação de tipo do TypeScript
  - Tipos explícitos garantem que o frontend quebre em compile-time se o contrato
    da API mudar — muito melhor que um bug silencioso em runtime
  - IDEs usam esses tipos para autocomplete e navegação

  CONVENÇÃO: tipos de entidade (Event, Ticket) sem prefixo, tipos de request/response
  com sufixo Request/Response.
*/

// ─────────────────────────────────────────────
// AUTENTICAÇÃO
// ─────────────────────────────────────────────

/** Representa o usuário autenticado atual (claims do JWT) */
export interface User {
  id: string;
  email: string;
  name: string;
  roles: string[];
}

/** Resposta de login/register — JWT access token + dados básicos do usuário.
 *  O refresh token NÃO aparece aqui: o backend o envia como httpOnly cookie.
 */
export interface AuthResponse {
  accessToken: string;
  expiresIn: number;    // segundos até expirar
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  name: string;
  email: string;
  password: string;
  role: 'Attendee' | 'Organizer';
}

// ─────────────────────────────────────────────
// EVENTOS
// ─────────────────────────────────────────────

/**
 * Status do evento — string enum idêntico ao backend.
 * POR QUÊ string e não number? O backend salva como string no PostgreSQL
 * para legibilidade e resiliência a reordenação de enum.
 */
export type EventStatus = 'Draft' | 'Published' | 'Cancelled' | 'Completed';

export interface Event {
  id: string;
  title: string;
  description: string;
  location: string;
  startDate: string;   // ISO 8601 — converter com new Date() no componente
  endDate: string;
  capacity: number;
  ticketsSold: number;
  price: number;
  status: EventStatus;
  organizerId: string;
  organizerName: string;
  createdAt: string;
}

export interface CreateEventRequest {
  title: string;
  description: string;
  location: string;
  startDate: string;
  endDate: string;
  capacity: number;
  price: number;
}

export interface UpdateEventRequest extends Partial<CreateEventRequest> {
  status?: EventStatus;
}

// ─────────────────────────────────────────────
// INGRESSOS
// ─────────────────────────────────────────────

/** Status do ingresso — máquina de estados: Reserved → Confirmed, ou → Cancelled */
export type TicketStatus = 'Reserved' | 'Confirmed' | 'Cancelled';

export interface Ticket {
  id: string;
  eventId: string;
  eventTitle: string;
  attendeeId: string;
  status: TicketStatus;
  ticketCode: string | null;   // preenchido após Confirmed
  createdAt: string;
}

/** Resposta de BookTicket — pode ser ingresso ou posição na fila de espera */
export interface BookTicketResponse {
  isTicket: boolean;
  ticket: Ticket | null;
  waitlistPosition: WaitlistPositionDto | null;
}

export interface WaitlistPositionDto {
  eventId: string;
  position: number;
  totalInQueue: number;
}

// ─────────────────────────────────────────────
// RESPOSTA GENÉRICA DA API (Problem Details)
// ─────────────────────────────────────────────

/**
 * Formato de erro padrão do backend (RFC 7807 Problem Details).
 * O ExceptionMiddleware e ValidationBehavior retornam este formato.
 *
 * Exemplo de resposta de erro:
 * {
 *   "title": "Validation failed",
 *   "detail": "...",
 *   "status": 400,
 *   "errors": { "email": ["Email inválido"] }
 * }
 */
export interface ProblemDetails {
  title: string;
  detail?: string;
  status: number;
  errors?: Record<string, string[]>;
}

// ─────────────────────────────────────────────
// PAGINAÇÃO
// ─────────────────────────────────────────────

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface EventFilters {
  search?: string;
  status?: EventStatus;
  page?: number;
  pageSize?: number;
}
