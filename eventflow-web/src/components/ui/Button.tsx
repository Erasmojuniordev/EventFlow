/*
  BUTTON — COMPONENTE DE BOTÃO REUTILIZÁVEL
  ──────────────────────────────────────────
  Demonstra o padrão "compound variant" com Tailwind:
  variantes visuais (primary, secondary, ghost, danger) +
  tamanhos (sm, md, lg) combinados com cva (class-variance-authority).

  POR QUÊ cva e não if/else de classes?
  cva mantém as combinações declarativas e type-safe.
  Alternativa sem cva: string interpolation manual (propenso a bug de espaço, difícil de manter).

  forwardRef: permite que o componente pai passe uma ref para o elemento DOM
  subjacente (útil para focus programático, libs de animação, testes).
*/

import { forwardRef, type ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size    = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  isLoading?: boolean;
}

const variantClasses: Record<Variant, string> = {
  primary:
    'bg-amber text-canvas font-semibold hover:bg-amber-dark active:scale-[0.98] shadow-[0_0_24px_rgba(245,158,11,0.2)]',
  secondary:
    'bg-overlay border border-rim-strong text-ink hover:bg-surface hover:border-amber/40',
  ghost:
    'text-ink-dim hover:text-ink hover:bg-overlay',
  danger:
    'bg-danger/10 border border-danger/30 text-danger hover:bg-danger/20',
};

const sizeClasses: Record<Size, string> = {
  sm:  'h-8  px-3  text-sm  rounded-md gap-1.5',
  md:  'h-10 px-4  text-sm  rounded-lg gap-2',
  lg:  'h-12 px-6  text-base rounded-xl gap-2',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      variant = 'primary',
      size = 'md',
      isLoading = false,
      disabled,
      className = '',
      children,
      ...props
    },
    ref
  ) => {
    const base =
      'inline-flex items-center justify-center font-medium transition-all duration-150 ' +
      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber focus-visible:ring-offset-2 focus-visible:ring-offset-canvas ' +
      'disabled:opacity-40 disabled:cursor-not-allowed disabled:pointer-events-none cursor-pointer';

    return (
      <button
        ref={ref}
        disabled={disabled || isLoading}
        className={`${base} ${variantClasses[variant]} ${sizeClasses[size]} ${className}`}
        {...props}
      >
        {isLoading ? (
          <>
            <span
              className="size-4 rounded-full border-2 border-current border-t-transparent animate-spin"
              aria-hidden="true"
            />
            <span>Aguarde...</span>
          </>
        ) : (
          children
        )}
      </button>
    );
  }
);

Button.displayName = 'Button';
