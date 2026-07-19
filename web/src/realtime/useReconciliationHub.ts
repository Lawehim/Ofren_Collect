import { useEffect, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { API_BASE } from '../api/client';
import type { PaymentReconciledEvent } from '../types/models';

/**
 * Holds a live SignalR connection to the backend hub, scoped to the caller's tenant by the
 * token. On a reconciliation event it refreshes the dashboard query and returns the event so
 * the UI can surface the live toast. The REST data stays correct even if the socket never
 * connects — this is an enhancement over it, not a dependency.
 */
export function useReconciliationHub(token: string): PaymentReconciledEvent | null {
  const queryClient = useQueryClient();
  const [lastEvent, setLastEvent] = useState<PaymentReconciledEvent | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notifications`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    const refreshDashboard = () => {
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    };

    connection.on('PaymentReconciled', (payload: PaymentReconciledEvent) => {
      setLastEvent(payload);
      refreshDashboard();
    });
    connection.on('SubscriptionOverdue', refreshDashboard);

    // On (re)connect, resync once so a missed message never leaves the UI stale.
    connection.onreconnected(refreshDashboard);
    connection.start().then(refreshDashboard).catch(() => {
      /* socket optional — REST baseline remains correct */
    });

    return () => {
      void connection.stop();
    };
  }, [token, queryClient]);

  return lastEvent;
}
