import { type HTMLAttributes } from 'react';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Adiciona um sutil efeito hover de borda âmbar — útil para cards clicáveis */
  hoverable?: boolean;
}

export function Card({ hoverable = false, className = '', children, ...props }: CardProps) {
  return (
    <div
      className={
        'rounded-2xl border border-rim bg-surface ' +
        (hoverable
          ? 'transition-all duration-200 hover:border-amber/25 hover:shadow-[0_0_32px_rgba(245,158,11,0.06)] cursor-pointer '
          : '') +
        className
      }
      {...props}
    >
      {children}
    </div>
  );
}
