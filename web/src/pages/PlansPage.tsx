import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { Modal } from '../components/Modal';
import { naira } from '../lib/format';

export function PlansPage() {
  const token = useAuth().auth!.token;
  const queryClient = useQueryClient();

  const { data: plans = [], isLoading } = useQuery({
    queryKey: ['plans'],
    queryFn: () => api.listPlans(token),
  });

  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [amount, setAmount] = useState('');
  const [intervalValue, setIntervalValue] = useState('Monthly');
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.createPlan(token, { name, amount: Number(amount), interval: intervalValue }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['plans'] });
      setOpen(false);
      setName('');
      setAmount('');
      setError(null);
    },
    onError: (err) =>
      setError(err instanceof ApiError ? (err.errors[0] ?? err.message) : 'Failed to create plan.'),
  });

  return (
    <>
      <div className="topbar">
        <div className="greet">
          <div className="k">Billing</div>
          <h1 className="serif">Plans</h1>
        </div>
        <div className="actions">
          <button type="button" className="btn primary" onClick={() => setOpen(true)}>
            <svg viewBox="0 0 24 24">
              <path d="M12 5v14M5 12h14" />
            </svg>
            New plan
          </button>
        </div>
      </div>

      <div className="panel">
        <div className="panel-head">
          <h3>Your plans</h3>
        </div>
        {isLoading ? (
          <div className="empty">Loading…</div>
        ) : plans.length === 0 ? (
          <div className="empty">No plans yet — create your first recurring charge.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Amount</th>
                <th>Interval</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {plans.map((plan) => (
                <tr key={plan.id}>
                  <td>
                    <b style={{ fontWeight: 500 }}>{plan.name}</b>
                  </td>
                  <td className="amt">₦{naira(plan.amount)}</td>
                  <td>{plan.interval}</td>
                  <td>
                    <span className={`badge ${plan.isActive ? 'active' : 'pending'}`}>
                      <span className="d" />
                      {plan.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {open && (
        <Modal
          title="New plan"
          onClose={() => setOpen(false)}
          footer={
            <>
              <button type="button" className="btn" onClick={() => setOpen(false)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn primary"
                disabled={create.isPending}
                onClick={() => create.mutate()}
              >
                {create.isPending ? 'Creating…' : 'Create plan'}
              </button>
            </>
          }
        >
          {error && <div className="form-error">{error}</div>}
          <div className="field">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Basic" />
          </div>
          <div className="field">
            <label>Amount (₦)</label>
            <input
              type="number"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="5000"
            />
          </div>
          <div className="field">
            <label>Interval</label>
            <select value={intervalValue} onChange={(e) => setIntervalValue(e.target.value)}>
              <option>Weekly</option>
              <option>Monthly</option>
              <option>Yearly</option>
            </select>
          </div>
        </Modal>
      )}
    </>
  );
}
