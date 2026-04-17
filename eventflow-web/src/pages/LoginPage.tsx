/*
  LOGIN PAGE
  ──────────────────────────────────────────────────────────────
  Demonstra a integração React Hook Form + Zod + Zustand + Framer Motion.

  FLUXO COMPLETO:
  1. useForm({ resolver: zodResolver(loginSchema) }) — conecta Zod ao formulário
  2. Usuário digita → Zod valida em tempo real (mode: 'onChange')
  3. Submit → handleSubmit() valida tudo → chama authStore.login()
  4. authStore.login() chama a API, persiste token, atualiza estado
  5. Sucesso → navega para "from" (página anterior) ou "/"
  6. Erro da API → exibe mensagem inline
*/

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuthStore } from '../stores/authStore';
import { loginSchema, type LoginFormData } from '../lib/schemas';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';

export default function LoginPage() {
  const navigate  = useNavigate();
  const location  = useLocation();
  const { login } = useAuthStore();

  // Redireciona para a página que o usuário tentou acessar antes do login
  const from = (location.state as { from?: { pathname: string } })?.from?.pathname ?? '/';

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    mode: 'onBlur',   // Valida ao sair do campo — menos intrusivo que onChange
  });

  async function onSubmit(data: LoginFormData) {
    try {
      await login(data);
      navigate(from, { replace: true });
    } catch {
      // Erro já foi capturado pelo authStore; setError exibe no campo raiz
      setError('root', { message: 'Email ou senha inválidos.' });
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-4">
      <motion.div
        initial={{ opacity: 0, y: 24 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
        className="w-full max-w-sm"
      >
        {/* Cabeçalho */}
        <div className="mb-8 text-center">
          <Link to="/" className="text-2xl font-bold text-ink">
            Event<span className="text-amber">Flow</span>
          </Link>
          <p className="mt-2 text-sm text-ink-dim">Entre na sua conta para continuar</p>
        </div>

        {/* Card do formulário */}
        <div className="rounded-2xl border border-rim bg-surface p-6 shadow-[0_0_40px_rgba(0,0,0,0.4)]">
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <Input
              label="Email"
              type="email"
              autoComplete="email"
              placeholder="seu@email.com"
              error={errors.email?.message}
              {...register('email')}
            />

            <Input
              label="Senha"
              type="password"
              autoComplete="current-password"
              placeholder="••••••••"
              error={errors.password?.message}
              {...register('password')}
            />

            {/* Erro global (credenciais inválidas) */}
            {errors.root && (
              <motion.p
                initial={{ opacity: 0, y: -4 }}
                animate={{ opacity: 1, y: 0 }}
                role="alert"
                className="rounded-lg bg-danger/10 border border-danger/20 px-3 py-2.5 text-sm text-danger"
              >
                {errors.root.message}
              </motion.p>
            )}

            <Button
              type="submit"
              variant="primary"
              size="lg"
              isLoading={isSubmitting}
              className="mt-1 w-full"
            >
              Entrar
            </Button>
          </form>
        </div>

        <p className="mt-4 text-center text-sm text-ink-dim">
          Ainda não tem conta?{' '}
          <Link to="/registro" className="text-amber hover:text-amber-dark transition-colors font-medium">
            Criar conta
          </Link>
        </p>
      </motion.div>
    </div>
  );
}
