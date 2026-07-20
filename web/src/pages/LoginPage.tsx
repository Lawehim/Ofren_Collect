import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';

export function LoginPage() {
  const { login, register } = useAuth();
  const navigate = useNavigate();

  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [businessName, setBusinessName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === 'login') await login(email, password);
      else await register(businessName, email, password);
      navigate('/');
    } catch (err) {
      setError(err instanceof ApiError ? (err.errors[0] ?? err.message) : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  const isRegister = mode === 'register';

  return (
    <div className="auth-wrap">
      <div className="auth">
        <div className="auth-brand">
          <div className="brand2">
            <div className="logo">
              <svg width="22" height="22" viewBox="0 0 100 100" fill="none">
                <circle cx="50" cy="50" r="40" stroke="#3E5C82" strokeWidth="15" />
              </svg>
            </div>
            <b>
              Ofren<span> Collect</span>
            </b>
          </div>
          <div className="auth-hero">
            <h2>
              Stop matching payments <em>by hand.</em>
            </h2>
            <p>
              Give every customer their own account number. When money arrives, Ofren reconciles it
              to the right invoice — automatically, in real time.
            </p>
            <div className="auth-stats">
              <div>
                <div className="n serif">100%</div>
                <div className="l">auto-matched</div>
              </div>
              <div>
                <div className="n serif">0</div>
                <div className="l">spreadsheets</div>
              </div>
              <div>
                <div className="n serif">Live</div>
                <div className="l">dashboard</div>
              </div>
            </div>
          </div>
        </div>

        <form className="auth-form" onSubmit={submit}>
          <div className="k">{isRegister ? 'Get started' : 'Welcome back'}</div>
          <h3 className="serif">{isRegister ? 'Create your business' : 'Sign in to your business'}</h3>
          <div className="lead">
            {isRegister ? 'Set up your account in one step.' : 'Enter your details to reach your dashboard.'}
          </div>

          {error && <div className="form-error">{error}</div>}

          {isRegister && (
            <div className="field">
              <label>Business name</label>
              <input
                value={businessName}
                onChange={(e) => setBusinessName(e.target.value)}
                placeholder="BrightPath Tutors"
                required
              />
            </div>
          )}
          <div className="field">
            <label>Work email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@business.com"
              required
            />
          </div>
          <div className="field">
            <label>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••••"
              required
            />
          </div>

          <button type="submit" className="btn primary" disabled={busy}>
            {busy ? 'Please wait…' : isRegister ? 'Create account' : 'Sign in'}
            <svg viewBox="0 0 24 24">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </button>

          <div className="auth-alt">
            {isRegister ? (
              <>
                Already have an account?{' '}
                <a onClick={() => { setMode('login'); setError(null); }}>Sign in</a>
              </>
            ) : (
              <>
                New to Ofren? <a onClick={() => { setMode('register'); setError(null); }}>Create your business account</a>
              </>
            )}
          </div>
        </form>
      </div>
    </div>
  );
}
