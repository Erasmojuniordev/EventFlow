/*
  SKELETON — PLACEHOLDER DE CARREGAMENTO
  ────────────────────────────────────────
  O Skeleton exibe a "silhueta" do conteúdo antes de ele chegar da API.
  Melhor UX que spinners genéricos porque reduz a percepção de tempo de espera —
  o usuário vê que algo está chegando no lugar certo.

  A classe .skeleton (animação shimmer) está definida em index.css.
*/

interface SkeletonProps {
  className?: string;
  /** Se true, usa rounded-full (para avatares, ícones circulares) */
  circle?: boolean;
}

export function Skeleton({ className = '', circle = false }: SkeletonProps) {
  return (
    <div
      className={`skeleton ${circle ? 'rounded-full' : 'rounded-lg'} ${className}`}
      aria-hidden="true"
    />
  );
}

/** Skeleton de card de evento — mesmo layout do EventCard real */
export function EventCardSkeleton() {
  return (
    <div className="rounded-2xl border border-rim bg-surface p-5 flex flex-col gap-3">
      <Skeleton className="h-5 w-3/4" />
      <Skeleton className="h-4 w-1/2" />
      <div className="flex gap-2 mt-1">
        <Skeleton className="h-5 w-20" />
        <Skeleton className="h-5 w-16" />
      </div>
      <div className="flex items-center justify-between mt-2">
        <Skeleton className="h-6 w-24" />
        <Skeleton className="h-9 w-28" />
      </div>
    </div>
  );
}
