import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { Modal } from '../components/Modal';
import { avatarColor, formatAccount, initials, naira } from '../lib/format';
import type { TransactionRow } from '../types/models';

const MIN_REFUND = 100;

export function TransactionsPage() {
  const { auth } = useAuth();
  const token = auth!.token;
  const isOwner = auth?.role === 'Owner';
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['transactions'],
    queryFn: () => api.listTransactions(token),
  });

  const [target, setTarget] = useState<TransactionRow | null>(null);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');

  const refund = useMutation({
    mutationFn: () =>
      api.initiateRefund(token, {
        originalTransactionReference: target!.transactionReference,
        amount: Number(amount),
        reason: reason.trim(),
        refundReference: crypto.randomUUID(),
      }),
    onSuccess: async () => {
      setTarget(null);
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
    },
  });

  const openRefund = (row: TransactionRow) => {
    setTarget(row);
    setAmount(String(row.refundableAmount));
    setReason('');
    refund.reset();
  };

  const refundError =
    refund.error instanceof ApiError
      ? refund.error.status === 404
        ? 'Refunds are not enabled on this environment.'
        : (refund.error.errors[0] ?? refund.error.message)
      : refund.error
        ? 'Something went wrong.'
        : null;

  const amountValue = Number(amount);
  const amountValid =
    Number.isFinite(amountValue) &&
    amountValue >= MIN_REFUND &&
    target != null &&
    amountValue <= target.refundableAmount;

  const rows = data ?? [];

  return (
    <>
      <div className="topbar">
        <div className="greet">
          <div className="k">Money in</div>
          <h1 className="serif">Transactions</h1>
        </div>
      </div>

      <div className="panel">
        <div className="panel-head">
          <h3>Reconciled payments</h3>
        </div>
        {isLoading ? (
          <div className="empty">Loading…</div>
        ) : rows.length === 0 ? (
          <div className="empty">No transactions yet — reconciled payments appear here.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Customer</th>
                <th>Reference</th>
                <th>Paid</th>
                <th>Amount</th>
                <th>Refunded</th>
                {isOwner && <th />}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.transactionReference}>
                  <td>
                    <div className="cust">
                      <div className="av" style={{ background: avatarColor(row.customerName || '?') }}>
                        {initials(row.customerName || '?')}
                      </div>
                      <div className="nm">
                        <b>{row.customerName || 'Unknown'}</b>
                        <span>{formatAccount(row.reservedAccountNumber)}</span>
                      </div>
                    </div>
                  </td>
                  <td className="acct">{row.transactionReference}</td>
                  <td>{new Date(row.paidAt).toLocaleDateString('en-NG')}</td>
                  <td className="amt">₦{naira(row.amount)}</td>
                  <td className="amt">{row.refundedAmount > 0 ? `₦${naira(row.refundedAmount)}` : '—'}</td>
                  {isOwner && (
                    <td>
                      <button
                        type="button"
                        className="btn"
                        disabled={row.refundableAmount < MIN_REFUND}
                        onClick={() => openRefund(row)}
                      >
                        Refund
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {target && (
        <Modal
          title="Refund a payment"
          onClose={() => setTarget(null)}
          footer={
            <>
              <button type="button" className="btn" onClick={() => setTarget(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn primary"
                disabled={!amountValid || !reason.trim() || refund.isPending}
                onClick={() => refund.mutate()}
              >
                {refund.isPending ? 'Processing…' : 'Refund'}
              </button>
            </>
          }
        >
          {refundError && <div className="form-error">{refundError}</div>}
          <div className="field">
            <label>Customer</label>
            <input value={target.customerName || 'Unknown'} disabled />
          </div>
          <div className="field">
            <label>Amount (max ₦{naira(target.refundableAmount)})</label>
            <input
              type="number"
              min={MIN_REFUND}
              max={target.refundableAmount}
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
          </div>
          <div className="field">
            <label>Reason</label>
            <input
              value={reason}
              maxLength={64}
              placeholder="e.g. Customer cancelled"
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
        </Modal>
      )}
    </>
  );
}
