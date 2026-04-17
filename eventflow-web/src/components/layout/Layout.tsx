/*
  LAYOUT — ESTRUTURA COMPARTILHADA DE PÁGINAS
  ─────────────────────────────────────────────
  <Outlet /> é o ponto de injeção do React Router v6.
  Cada <Route element={<Layout />}> envolve as rotas filhas neste shell.

  AnimatePresence + motion.main:
  Cada troca de rota executa uma animação de fade + slide para cima,
  dando sensação de navegação fluida sem recarregar a página.

  useLocation().key como key do motion.main:
  Quando a chave muda (nova rota), Framer Motion desmonta o componente
  atual com a animação de saída e monta o novo com a de entrada.
*/

import { Outlet, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Navbar } from './Navbar';

const pageVariants = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
  exit:    { opacity: 0, y: -8 },
};

const pageTransition = {
  duration: 0.22,
  ease: 'easeOut',
};

export function Layout() {
  const location = useLocation();

  return (
    <div className="flex min-h-screen flex-col bg-canvas">
      <Navbar />

      <AnimatePresence mode="wait">
        <motion.main
          key={location.key}
          variants={pageVariants}
          initial="initial"
          animate="animate"
          exit="exit"
          transition={pageTransition}
          className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 sm:px-6"
        >
          <Outlet />
        </motion.main>
      </AnimatePresence>

      <footer className="border-t border-rim py-6 text-center text-xs text-ink-dim">
        EventFlow &copy; {new Date().getFullYear()} — Projeto de estudo
      </footer>
    </div>
  );
}
