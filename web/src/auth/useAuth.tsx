import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setUnauthorizedHandler } from '../api/client';
import type { AuthResult } from '../types/models';

interface AuthState {
  token: string;
  email: string;
  role: string;
}

interface AuthContextValue {
  auth: AuthState | null;
  login: (email: string, password: string) => Promise<void>;
  register: (businessName: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const STORAGE_KEY = 'ofren.auth';
const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function loadAuth(): AuthState | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthState) : null;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthState | null>(loadAuth);

  const persist = useCallback((result: AuthResult) => {
    const state: AuthState = { token: result.token, email: result.email, role: result.role };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    setAuth(state);
  }, []);

  const login = useCallback(
    async (email: string, password: string) => persist(await api.login({ email, password })),
    [persist],
  );

  const register = useCallback(
    async (businessName: string, email: string, password: string) =>
      persist(await api.register({ businessName, email, password })),
    [persist],
  );

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setAuth(null);
  }, []);

  // Sign out automatically when the API reports the session has expired (401 on an authed call).
  // Clearing auth makes the router redirect to /login.
  useEffect(() => {
    setUnauthorizedHandler(() => logout());
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const value = useMemo(
    () => ({ auth, login, register, logout }),
    [auth, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
