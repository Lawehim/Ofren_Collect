import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../api/client';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.forgotPassword(email);
      setSent(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="auth-wrap">
      <div className="auth solo">
        <form className="auth-form" onSubmit={submit}>
          <div className="k">Reset access</div>
          <h3 className="serif">Forgot your password?</h3>
          <div className="lead">Enter your email and we'll send you a reset link.</div>

          {sent ? (
            <div className="form-success">
              If that email has an account, a reset link is on its way. Check your inbox.
            </div>
          ) : (
            <>
              {error && <div className="form-error">{error}</div>}
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
              <button type="submit" className="btn primary" disabled={busy}>
                {busy ? 'Sending…' : 'Send reset link'}
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
