import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

const linkClass = ({ isActive }: { isActive: boolean }) => (isActive ? 'on' : undefined);

export function Sidebar() {
  const { auth, logout } = useAuth();
  const initials = (auth?.email ?? '?').slice(0, 2).toUpperCase();

  return (
    <aside className="side">
      <div className="brand">
        <div className="logo">
          <svg width="22" height="22" viewBox="0 0 100 100" fill="none">
            <circle cx="50" cy="50" r="40" stroke="#fff" strokeWidth="15" />
          </svg>
        </div>
        <div className="name">
          Ofren<span> Collect</span>
        </div>
      </div>

      <nav className="nav">
        <NavLink to="/" end className={linkClass}>
          <svg viewBox="0 0 24 24">
            <rect x="3" y="3" width="7" height="7" rx="1.5" />
            <rect x="14" y="3" width="7" height="7" rx="1.5" />
            <rect x="3" y="14" width="7" height="7" rx="1.5" />
            <rect x="14" y="14" width="7" height="7" rx="1.5" />
          </svg>
          Dashboard
        </NavLink>
        <NavLink to="/customers" className={linkClass}>
          <svg viewBox="0 0 24 24">
            <path d="M17 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
            <circle cx="9.5" cy="7" r="4" />
            <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
          </svg>
          Customers
        </NavLink>
        <NavLink to="/plans" className={linkClass}>
          <svg viewBox="0 0 24 24">
            <path d="M4 4h16v6H4zM4 14h16v6H4z" />
          </svg>
          Plans
        </NavLink>
      </nav>

      <div className="side-foot">
        <div className="side-user">
          <div className="av">{initials}</div>
          <div className="who">
            <b>{auth?.email}</b>
            <span>{auth?.role}</span>
          </div>
        </div>
        <button type="button" className="side-logout" onClick={logout}>
          Sign out
        </button>
      </div>
    </aside>
  );
}
