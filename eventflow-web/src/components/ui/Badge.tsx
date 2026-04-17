import type { EventStatus, TicketStatus } from '../../types';

type BadgeVariant = 'amber' | 'success' | 'danger' | 'info' | 'neutral';

interface BadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  className?: string;
}

const variantClasses: Record<BadgeVariant, string> = {
  amber:   'bg-amber/10   text-amber   border-amber/20',
  success: 'bg-success/10 text-success border-success/20',
  danger:  'bg-danger/10  text-danger  border-danger/20',
  info:    'bg-info/10    text-info    border-info/20',
  neutral: 'bg-overlay    text-ink-dim border-rim',
};

export function Badge({ children, variant = 'neutral', className = '' }: BadgeProps) {
  return (
    <span
      className={
        'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ' +
        variantClasses[variant] + ' ' + className
      }
    >
      {children}
    </span>
  );
}

// ── Helpers de mapeamento ─────────────────────────────────────────────────────

const eventStatusConfig: Record<EventStatus, { label: string; variant: BadgeVariant }> = {
  Draft:     { label: 'Rascunho',   variant: 'neutral' },
  Published: { label: 'Publicado',  variant: 'success' },
  Cancelled: { label: 'Cancelado',  variant: 'danger' },
  Completed: { label: 'Concluído',  variant: 'info' },
};

const ticketStatusConfig: Record<TicketStatus, { label: string; variant: BadgeVariant }> = {
  Reserved:  { label: 'Reservado',  variant: 'amber' },
  Confirmed: { label: 'Confirmado', variant: 'success' },
  Cancelled: { label: 'Cancelado',  variant: 'danger' },
};

export function EventStatusBadge({ status }: { status: EventStatus }) {
  const { label, variant } = eventStatusConfig[status];
  return <Badge variant={variant}>{label}</Badge>;
}

export function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const { label, variant } = ticketStatusConfig[status];
  return <Badge variant={variant}>{label}</Badge>;
}
