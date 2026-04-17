/*
  NAVBAR — BARRA DE NAVEGAÇÃO PRINCIPAL
  ──────────────────────────────────────
  Usa motion.nav do Framer Motion para deslizar ao entrar.
  O estado de scroll adiciona blur + borda para separar do conteúdo atrás.

  useScrollEffect: hook local que escuta o scroll para aplicar o "sticky glass" effect.
  POR QUÊ não CSS puro?
  CSS position:sticky + backdrop-filter funciona, mas precisamos trocar classes
  dinamicamente com base no scroll — mais fácil com estado React.
*/

import { useState, useEffect } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useAuthStore } from '../../stores/authStore';

export function Navbar() {
  const { user, isAuthenticated, logout, hasRole } = useAuthStore();
  const navigate = useNavigate();
  const [scrolled, setScrolled]   = useState(false);
  const [menuOpen, setMenuOpen]   = useState(false);

  // Detecta scroll para aplicar efeito de vidro na navbar
  useEffect(() => {
    const handler = () => setScrolled(window.scrollY > 12);
    window.addEventListener('scroll', handler, { passive: true });
    return () => window.removeEventListener('scroll', handler);
  }, []);

  // Fecha o menu mobile ao navegar
  useEffect(() => {
    setMenuOpen(false);
  }, [navigate]);

  const authenticated = isAuthenticated();

  async function handleLogout() {
    await logout();
    navigate('/login');
  }

  return (
    <motion.header
      initial={{ y: -60, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.4, ease: 'easeOut' }}
      className={
        'sticky top-0 z-50 transition-all duration-300 ' +
        (scrolled
          ? 'border-b border-rim bg-canvas/80 backdrop-blur-lg'
          : 'bg-transparent')
      }
    >
      <nav className="mx-auto flex max-w-6xl items-center justify-between px-4 py-4 sm:px-6">
        {/* Logo */}
        <Link
          to="/"
          className="text-xl font-bold tracking-tight text-ink hover:text-amber transition-colors"
        >
          Event<span className="text-amber">Flow</span>
        </Link>

        {/* Links desktop */}
        <div className="hidden items-center gap-6 sm:flex">
          <NavLinks authenticated={authenticated} hasOrganizerRole={hasRole('Organizer')} />
        </div>

        {/* Auth desktop */}
        <div className="hidden items-center gap-3 sm:flex">
          {authenticated ? (
            <AuthMenu name={user?.name ?? ''} onLogout={handleLogout} />
          ) : (
            <GuestButtons />
          )}
        </div>

        {/* Hamburger mobile */}
        <button
          aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'}
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((v) => !v)}
          className="flex size-9 items-center justify-center rounded-lg text-ink-dim hover:text-ink hover:bg-overlay transition-colors sm:hidden"
        >
          {menuOpen ? <XIcon /> : <MenuIcon />}
        </button>
      </nav>

      {/* Menu mobile */}
      <AnimatePresence>
        {menuOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.2 }}
            className="border-t border-rim bg-canvas/95 backdrop-blur-lg sm:hidden overflow-hidden"
          >
            <div className="flex flex-col gap-1 p-4">
              <NavLinks
                mobile
                authenticated={authenticated}
                hasOrganizerRole={hasRole('Organizer')}
              />
              <div className="mt-3 border-t border-rim pt-3">
                {authenticated ? (
                  <button
                    onClick={handleLogout}
                    className="w-full rounded-lg py-2.5 text-sm text-ink-dim hover:text-danger transition-colors text-left px-2"
                  >
                    Sair
                  </button>
                ) : (
                  <div className="flex flex-col gap-2">
                    <Link
                      to="/login"
                      className="rounded-lg border border-rim-strong py-2.5 text-center text-sm text-ink hover:bg-overlay transition-colors"
                    >
                      Entrar
                    </Link>
                    <Link
                      to="/registro"
                      className="rounded-lg bg-amber py-2.5 text-center text-sm font-semibold text-canvas hover:bg-amber-dark transition-colors"
                    >
                      Criar conta
                    </Link>
                  </div>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.header>
  );
}

// ── Sub-componentes ───────────────────────────────────────────────────────────

function NavLinks({
  mobile = false,
  authenticated,
  hasOrganizerRole,
}: {
  mobile?: boolean;
  authenticated: boolean;
  hasOrganizerRole: boolean;
}) {
  const base = mobile
    ? 'block rounded-lg px-3 py-2.5 text-sm transition-colors'
    : 'text-sm transition-colors';

  const activeClass   = 'text-amber font-medium';
  const inactiveClass = 'text-ink-dim hover:text-ink';

  return (
    <>
      <NavLink
        to="/"
        end
        className={({ isActive }) =>
          `${base} ${isActive ? activeClass : inactiveClass}`
        }
      >
        Eventos
      </NavLink>

      {authenticated && (
        <NavLink
          to="/meus-ingressos"
          className={({ isActive }) =>
            `${base} ${isActive ? activeClass : inactiveClass}`
          }
        >
          Meus ingressos
        </NavLink>
      )}

      {hasOrganizerRole && (
        <NavLink
          to="/organizer"
          className={({ isActive }) =>
            `${base} ${isActive ? activeClass : inactiveClass}`
          }
        >
          Painel
        </NavLink>
      )}
    </>
  );
}

function GuestButtons() {
  return (
    <>
      <Link
        to="/login"
        className="text-sm text-ink-dim hover:text-ink transition-colors"
      >
        Entrar
      </Link>
      <Link
        to="/registro"
        className="rounded-lg bg-amber px-4 py-2 text-sm font-semibold text-canvas hover:bg-amber-dark transition-colors"
      >
        Criar conta
      </Link>
    </>
  );
}

function AuthMenu({ name, onLogout }: { name: string; onLogout: () => void }) {
  const initials = name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0]?.toUpperCase() ?? '')
    .join('');

  return (
    <div className="flex items-center gap-3">
      {/* Avatar com iniciais */}
      <div
        aria-hidden="true"
        className="flex size-8 items-center justify-center rounded-full bg-amber/15 text-xs font-bold text-amber border border-amber/20"
      >
        {initials}
      </div>
      <span className="text-sm text-ink-dim">{name.split(' ')[0]}</span>
      <button
        onClick={onLogout}
        className="text-sm text-ink-dim hover:text-danger transition-colors"
      >
        Sair
      </button>
    </div>
  );
}

// ── Ícones SVG inline (sem dependência externa) ───────────────────────────────

function MenuIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true">
      <path d="M3 5h14M3 10h14M3 15h14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

function XIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true">
      <path d="M5 5l10 10M15 5L5 15" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}
