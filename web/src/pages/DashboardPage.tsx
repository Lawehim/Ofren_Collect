import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { AssistantPanel } from '../components/AssistantPanel';
import { StatusBadge } from '../components/StatusBadge';
import { avatarColor, formatAccount, initials, naira } from '../lib/format';
import { useReconciliationHub } from '../realtime/useReconciliationHub';
import type { DashboardSubscriptionRow } from '../types/models';

function rowStatus(row: DashboardSubscriptionRow): string {
  if (row.status === 'Overdue' || row.status === 'Cancelled') return row.status;
  return row.currentInvoiceStatus ?? row.status;
}

export function DashboardPage() {
  const { auth } = useAuth();
  const token = auth!.token;
  const navigate = useNavigate();

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api.getDashboard(token),
  });

  const lastEvent = useReconciliationHub(token);
  const [toastVisible, setToastVisible] = useState(false);
  useEffect(() => {
    if (!lastEvent) return;
    setToastVisible(true);
    const timer = window.setTimeout(() => setToastVisible(false), 6000);
    return () => window.clearTimeout(timer);
  }, [lastEvent]);

  const subscriptions = data?.subscriptions ?? [];
  const summary = data?.summary;
  const activeCount = subscriptions.filter((s) => s.status === 'Active').length;
  const underpaidCount = subscriptions.filter((s) => s.currentInvoiceStatus === 'Underpaid').length;

  return (
    <>
      <div className="topbar">
        <div className="greet">
          <div className="k">Your collections</div>
          <h1 className="serif">
            Good morning, <em>{auth?.email.split('@')[0]}</em>
          </h1>
        </div>
        <div className="actions">
          <button type="button" className="btn primary" onClick={() => navigate('/customers')}>
            <svg viewBox="0 0 24 24">
              <path d="M12 5v14M5 12h14" />
            </svg>
            Enrol customer
          </button>
        </div>
      </div>

      <div className="metrics">
        <div className="metric">
          <div className="lab">
            <span className="dot" style={{ background: '#101826' }} />
            Collected this period
          </div>
          <div className="val">
            <span className="cur">₦</span>
            {naira(summary?.collectedThisPeriod ?? 0)}
          </div>
        </div>
        <div className="metric">
          <div className="lab">
            <span className="dot" style={{ background: '#0e1b2c' }} />
            Active subscriptions
          </div>
          <div className="val">{activeCount}</div>
        </div>
        <div className="metric">
          <div className="lab">
            <span className="dot" style={{ background: '#b4791a' }} />
            Underpaid
          </div>
          <div className="val">{underpaidCount}</div>
          <div className="sub warn">Needs follow-up</div>
        </div>
        <div className="metric">
          <div className="lab">
            <span className="dot" style={{ background: '#b23a3a' }} />
            Overdue
          </div>
          <div className="val">{summary?.overdueCount ?? 0}</div>
          <div className="sub down">Past due date</div>
        </div>
      </div>

      <AssistantPanel />

      <div className="panel">
        <div className="panel-head">
          <h3>Subscriptions</h3>
        </div>
        {isLoading ? (
          <div className="empty">Loading…</div>
        ) : subscriptions.length === 0 ? (
          <div className="empty">No subscriptions yet — enrol a customer to get started.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Customer</th>
                <th>Plan</th>
                <th>Reserved account</th>
                <th>Amount</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {subscriptions.map((row) => (
                <tr key={row.subscriptionId}>
                  <td>
                    <div className="cust">
                      <div className="av" style={{ background: avatarColor(row.customerName) }}>
                        {initials(row.customerName)}
                      </div>
                      <div className="nm">
                        <b>{row.customerName}</b>
                      </div>
                    </div>
                  </td>
                  <td>
                    {row.planName} · ₦{naira(row.planAmount)}
                  </td>
                  <td className="acct">
                    {formatAccount(row.reservedAccountNumber)}
                    <span>{row.reservedBankName ?? ''}</span>
                  </td>
                  <td className="amt">₦{naira(row.planAmount)}</td>
                  <td>
                    <StatusBadge status={rowStatus(row)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {toastVisible && lastEvent && (
        <div className="live">
          <span className="pulse" />
          <div className="txt">
            <b>Payment reconciled</b>
            <br />
            <span>
              ₦{naira(lastEvent.amountPaid)} matched — invoice {lastEvent.status.toLowerCase()}
            </span>
          </div>
        </div>
      )}
    </>
  );
}
