import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { formatAccount } from '../lib/format';
import type { CustomerResponse, SubscriptionResponse } from '../types/models';

function errorText(err: unknown, fallback: string): string {
  return err instanceof ApiError ? (err.errors[0] ?? err.message) : fallback;
}

export function CustomersPage() {
  const token = useAuth().auth!.token;
  const queryClient = useQueryClient();

  const { data: plans = [] } = useQuery({ queryKey: ['plans'], queryFn: () => api.listPlans(token) });

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [customer, setCustomer] = useState<CustomerResponse | null>(null);
  const [planId, setPlanId] = useState('');
  const [enrolment, setEnrolment] = useState<SubscriptionResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const register = useMutation({
    mutationFn: () => api.registerCustomer(token, { name, email }),
    onSuccess: (created) => {
      setCustomer(created);
      setError(null);
    },
    onError: (err) => setError(errorText(err, 'Failed to register customer.')),
  });

  const enrol = useMutation({
    mutationFn: () => api.enrolCustomer(token, { customerId: customer!.id, planId }),
    onSuccess: (subscription) => {
      setEnrolment(subscription);
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (err) => setError(errorText(err, 'Enrolment failed.')),
  });

  const reset = () => {
    setName('');
    setEmail('');
    setCustomer(null);
    setPlanId('');
    setEnrolment(null);
    setError(null);
  };

  return (
    <>
      <div className="topbar">
        <div className="greet">
          <div className="k">Customers</div>
          <h1 className="serif">Enrol a customer</h1>
        </div>
      </div>

      <div className="panel" style={{ maxWidth: 560 }}>
        <div className="panel-head">
          <h3>
            {enrolment ? 'Reserved account ready' : customer ? `Enrol ${customer.name}` : 'New customer'}
          </h3>
        </div>
        <div style={{ padding: 22 }}>
          {error && <div className="form-error">{error}</div>}

          {!customer && (
            <>
              <div className="field">
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Chidi Eze" />
              </div>
              <div className="field">
                <label>Email</label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="chidi@mail.com"
                />
              </div>
              <button
                type="button"
                className="btn primary"
                style={{ width: '100%', justifyContent: 'center', padding: 12 }}
                disabled={register.isPending}
                onClick={() => register.mutate()}
              >
                {register.isPending ? 'Saving…' : 'Save customer'}
              </button>
            </>
          )}

          {customer && !enrolment && (
            <>
              <p style={{ color: 'var(--muted)', fontSize: 14, marginBottom: 18 }}>
                Enrolling <b style={{ color: 'var(--ink)' }}>{customer.name}</b> provisions a dedicated
                Monnify account for them to pay into.
              </p>
              <div className="field">
                <label>Plan</label>
                <select value={planId} onChange={(e) => setPlanId(e.target.value)}>
                  <option value="">Select a plan…</option>
                  {plans.map((plan) => (
                    <option key={plan.id} value={plan.id}>
                      {plan.name} · ₦{plan.amount.toLocaleString()} / {plan.interval.toLowerCase()}
                    </option>
                  ))}
                </select>
              </div>
              <button
                type="button"
                className="btn primary"
                style={{ width: '100%', justifyContent: 'center', padding: 12 }}
                disabled={!planId || enrol.isPending}
                onClick={() => enrol.mutate()}
              >
                {enrol.isPending ? 'Provisioning account…' : 'Enrol & provision account'}
              </button>
            </>
          )}

          {enrolment && (
            <>
              <p style={{ color: 'var(--muted)', fontSize: 14, marginBottom: 14 }}>
                Share this dedicated account with <b style={{ color: 'var(--ink)' }}>{customer?.name}</b>.
                Any payment into it reconciles automatically.
              </p>
              <div
                style={{
                  border: '1px solid var(--line)',
                  borderRadius: 12,
                  padding: '18px 20px',
                  marginBottom: 18,
                }}
              >
                <div className="mono" style={{ fontSize: 22, fontWeight: 500 }}>
                  {formatAccount(enrolment.reservedAccountNumber)}
                </div>
                <div style={{ fontSize: 13, color: 'var(--muted)', marginTop: 4 }}>
                  {enrolment.reservedBankName} · {customer?.name}
                </div>
                <button
                  type="button"
                  className="btn"
                  style={{ width: '100%', justifyContent: 'center', marginTop: 14 }}
                  onClick={() =>
                    void navigator.clipboard.writeText(enrolment.reservedAccountNumber ?? '')
                  }
                >
                  Copy account number
                </button>
              </div>
              <button
                type="button"
                className="btn primary"
                style={{ width: '100%', justifyContent: 'center', padding: 12 }}
                onClick={reset}
              >
                Enrol another customer
              </button>
            </>
          )}
        </div>
      </div>
    </>
  );
}
