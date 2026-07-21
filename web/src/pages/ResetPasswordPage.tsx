import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const navigate = useNavigate();

  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.resetPassword(token, password);
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? (err.errors[0] ?? err.message) : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="auth-wrap">
      <div className="auth solo">
        <form className="auth-form" onSubmit={submit}>
          <div className="k">Reset access</div>
          <h3 className="serif">Set a new password</h3>

          {!token ? (
            <div className="form-error">This reset link is missing its token — request a new one.</div>
          ) : done ? (
            <>
              <div className="form-success">Your password has been reset.</div>
              <button type="button" className="btn primary" onClick={() => navigate('/login')}>
                Sign in
              </button>
            </>
          ) : (
            <>
              {error && <div className="form-error">{error}</div>}
              <div className="field">
                <label>New password</label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="At least 8 characters"
                  minLength={8}
                  required
                />
              </div>
              <button type="submit" className="btn primary" disabled={busy}>
                {busy ? 'Saving…' : 'Reset password'}
              </button>
            </>
          )}

          <div className="auth-alt">
            <Link to="/login">Back to sign in</Link>
          </div>
        </form>
      </div>
    </div>
  );
}
