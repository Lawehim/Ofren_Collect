import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { Modal } from './Modal';
import type { CreateMandateResult, MandateDebitResult } from '../types/models';

interface Props {
  subscriptionId: string;
  customerName: string;
  onClose: () => void;
}

export function DirectDebitModal({ subscriptionId, customerName, onClose }: Props) {
  const { auth } = useAuth();
  const token = auth!.token;

  const [accountNumber, setAccountNumber] = useState('');
  const [bankCode, setBankCode] = useState('');
  const [address, setAddress] = useState('');
  const [phone, setPhone] = useState('');
  const [mandate, setMandate] = useState<CreateMandateResult | null>(null);
  const [status, setStatus] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.createMandate(token, {
        subscriptionId,
        customerAccountNumber: accountNumber.trim(),
        customerAccountBankCode: bankCode.trim(),
        customerAddress: address.trim(),
        customerPhoneNumber: phone.trim(),
      }),
    onSuccess: (result) => {
      setMandate(result);
      setStatus(result.status);
    },
  });

  const refresh = useMutation({
    mutationFn: () => api.refreshMandate(token, mandate!.mandateReference),
    onSuccess: (result) => setStatus(result.status),
  });

  const [debit, setDebit] = useState<MandateDebitResult | null>(null);
  const charge = useMutation({
    mutationFn: () => api.debitMandate(token, subscriptionId),
    onSuccess: (result) => setDebit(result),
  });
  const checkPayment = useMutation({
    mutationFn: () => api.reconcileDebit(token, debit!.paymentReference),
    onSuccess: (result) => setDebit(result),
  });
  const cancel = useMutation({
    mutationFn: () => api.cancelMandate(token, mandate!.mandateReference),
    onSuccess: onClose,
  });

  const errorText = (error: unknown) =>
    error instanceof ApiError
      ? error.status === 404
        ? 'Direct debit is not enabled on this environment.'
        : (error.errors[0] ?? error.message)
      : error
        ? 'Something went wrong.'
        : null;

  const formValid = accountNumber.trim() && bankCode.trim() && address.trim() && phone.trim();
  const isActive = status?.toLowerCase() === 'active';

  return (
    <Modal
      title="Set up direct debit"
      onClose={onClose}
      footer={
        mandate ? (
          <>
            <button type="button" className="btn" onClick={onClose}>
              Close
            </button>
            <button
              type="button"
              className="btn primary"
              disabled={isActive || refresh.isPending}
              onClick={() => refresh.mutate()}
            >
              {isActive ? 'Authorized ✓' : refresh.isPending ? 'Checking…' : 'Check status'}
            </button>
          </>
        ) : (
          <>
            <button type="button" className="btn" onClick={onClose}>
              Cancel
            </button>
            <button
              type="button"
              className="btn primary"
              disabled={!formValid || create.isPending}
              onClick={() => create.mutate()}
            >
              {create.isPending ? 'Creating…' : 'Create mandate'}
            </button>
          </>
        )
      }
    >
      {!mandate ? (
        <>
          <p className="assistant-note" style={{ marginTop: 0 }}>
            Authorise recurring debits for <b>{customerName}</b>. Enter their bank details (with their
            consent); Monnify will send them a link to authorise.
          </p>
          {create.error && <div className="form-error">{errorText(create.error)}</div>}
          <div className="field">
            <label>Account number</label>
            <input value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} placeholder="0051762787" />
          </div>
          <div className="field">
            <label>Bank code</label>
            <input value={bankCode} onChange={(e) => setBankCode(e.target.value)} placeholder="044" />
          </div>
          <div className="field">
            <label>Address</label>
            <input value={address} onChange={(e) => setAddress(e.target.value)} placeholder="12 Lagos Street" />
          </div>
          <div className="field">
            <label>Phone number</label>
            <input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="08012345678" />
          </div>
        </>
      ) : (
        <>
          <div className={`assistant-answer ${isActive ? 'grounded' : 'declined'}`}>
            <span className="assistant-badge">{isActive ? 'Authorized' : `Status: ${status}`}</span>
            <p>
              {isActive
                ? 'The customer has authorized the mandate. It can now be debited on schedule.'
                : 'Send this link to the customer to authorize recurring debits, then check the status.'}
            </p>
          </div>
          {!isActive && (
            <div className="field">
              <label>Authorization link</label>
              <input value={mandate.authorizationLink} readOnly onFocus={(e) => e.target.select()} />
            </div>
          )}
          {refresh.error && <div className="form-error">{errorText(refresh.error)}</div>}

          {isActive && (
            <div className="field" style={{ marginTop: 14 }}>
              {!debit ? (
                <button
                  type="button"
                  className="btn primary"
                  disabled={charge.isPending}
                  onClick={() => charge.mutate()}
                >
                  {charge.isPending ? 'Charging…' : 'Charge current invoice now'}
                </button>
              ) : (
                <div className={`assistant-answer ${debit.status === 'Paid' ? 'grounded' : 'declined'}`}>
                  <span className="assistant-badge">Debit: {debit.status}</span>
                  <p>
                    {debit.status === 'Paid'
                      ? 'The debit succeeded and the invoice is now paid.'
                      : 'Debit initiated. Check its status once Monnify has processed it.'}
                  </p>
                  {debit.status !== 'Paid' && (
                    <button
                      type="button"
                      className="btn"
                      disabled={checkPayment.isPending}
                      onClick={() => checkPayment.mutate()}
                    >
                      {checkPayment.isPending ? 'Checking…' : 'Check payment'}
                    </button>
                  )}
                </div>
              )}
              {(charge.error || checkPayment.error) && (
                <div className="form-error">{errorText(charge.error ?? checkPayment.error)}</div>
              )}
            </div>
          )}

          <div style={{ marginTop: 16, textAlign: 'center' }}>
            <button
              type="button"
              className="side-logout"
              disabled={cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending ? 'Cancelling…' : 'Cancel this mandate'}
            </button>
          </div>
        </>
      )}
    </Modal>
  );
}
