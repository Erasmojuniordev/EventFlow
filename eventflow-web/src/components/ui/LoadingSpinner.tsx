interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeClasses = {
  sm: 'size-4 border-2',
  md: 'size-6 border-2',
  lg: 'size-10 border-[3px]',
};

export function LoadingSpinner({ size = 'md', className = '' }: SpinnerProps) {
  return (
    <span
      role="status"
      aria-label="Carregando"
      className={
        `rounded-full border-surface border-t-amber animate-spin ${sizeClasses[size]} ${className}`
      }
    />
  );
}
