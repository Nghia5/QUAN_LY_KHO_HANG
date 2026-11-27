import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import type { AuthUserSummary } from '../types';
import { authApi } from '../api/services';

interface AuthState {
  user: (AuthUserSummary & { permissions?: string[] }) | null;
  isAuthenticated: boolean;
  loading: boolean;
  login: (accessToken: string, refreshToken: string, user: AuthUserSummary) => void;
  logout: () => void;
  hasPermission: (perm: string) => boolean;
}

const AuthContext = createContext<AuthState>(null!);

export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<(AuthUserSummary & { permissions?: string[] }) | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      authApi.me().then(({ data }) => {
        setUser(data as unknown as AuthUserSummary & { permissions?: string[] });
      }).catch(() => {
        localStorage.clear();
      }).finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  const login = (accessToken: string, refreshToken: string, u: AuthUserSummary) => {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    setUser(u);
  };

  const logout = () => {
    const rt = localStorage.getItem('refreshToken');
    if (rt) authApi.revoke(rt).catch(() => {});
    localStorage.clear();
    setUser(null);
  };

  const hasPermission = (perm: string) => {
    if (!user) return false;
    if (user.roles?.includes('ADMIN')) return true;
    return user.permissions?.includes(perm) ?? false;
  };

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, loading, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}
