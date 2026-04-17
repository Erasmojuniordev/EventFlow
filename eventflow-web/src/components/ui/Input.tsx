/*
  INPUT — CAMPO DE TEXTO COM LABEL E MENSAGEM DE ERRO
  ──────────────────────────────────────────────────────
  Encapsula label + input + mensagem de erro em um único componente
  para não repetir essa estrutura em cada formulário.

  Integração com React Hook Form:
  O componente aceita todas as props nativas de <input> (InputHTMLAttributes),
  incluindo as que register() retorna: name, ref, onChange, onBlur.

  Uso:
    const { register, formState: { errors } } = useForm<LoginFormData>();
    <Input
      label="Email"
      type="email"
      error={errors.email?.message}
      {...register('email')}
    />

  O spread de register() injeta name, ref, onChange e onBlur automaticamente.
*/

import { forwardRef, type InputHTMLAttributes } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, hint, id, className = '', ...props }, ref) => {
    // id automático baseado no name para acessibilidade (htmlFor + aria-describedby)
    const inputId = id ?? (props.name ? `field-${props.name}` : undefined);
    const errorId = inputId ? `${inputId}-error` : undefined;

    return (
      <div className="flex flex-col gap-1.5">
        {label && (
          <label
            htmlFor={inputId}
            className="text-sm font-medium text-ink-dim"
          >
            {label}
          </label>
        )}

        <input
          ref={ref}
          id={inputId}
          aria-describedby={error ? errorId : undefined}
          aria-invalid={!!error}
          className={
            'w-full rounded-lg border bg-surface px-3.5 py-2.5 text-sm text-ink ' +
            'placeholder:text-ink-dim/60 ' +
            'transition-colors duration-150 ' +
            'focus:outline-none focus:ring-2 focus:ring-amber focus:border-transparent ' +
            (error
              ? 'border-danger/60 focus:ring-danger '
              : 'border-rim-strong hover:border-amber/30 ') +
            className
          }
          {...props}
        />

        {/* Mensagem de erro — aria-live para leitores de tela */}
        {error && (
          <p
            id={errorId}
            role="alert"
            className="text-xs text-danger"
          >
            {error}
          </p>
        )}

        {hint && !error && (
          <p className="text-xs text-ink-dim">{hint}</p>
        )}
      </div>
    );
  }
);

Input.displayName = 'Input';
