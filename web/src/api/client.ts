import type {
  AssistantAnswer,
  AuthResult,
  CustomerResponse,
  DashboardResponse,
  PlanResponse,
  RefundResult,
  SubscriptionResponse,
  TransactionRow,
} from '../types/models';

export const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

export class ApiError extends Error {
  readonly status: number;
  readonly errors: string[];

  constructor(status: number, message: string, errors: string[] = []) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  token?: string | null;
}

// Called when an authenticated request is rejected with 401 (expired/invalid token) so the app can
// sign the user out and send them back to login. Registered by the auth provider.
let onUnauthorized: (() => void) | null = null;
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, token } = options;

  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    // A 401 on a request that carried a token means the session expired — sign out and redirect.
    // (A 401 without a token is a failed login, handled by the caller.)
    if (response.status === 401 && token) {
      onUnauthorized?.();
    }

    let message = `Request failed (${response.status}).`;
    let errors: string[] = [];
    try {
      const problem = await response.json();
      if (typeof problem.title === 'string') message = problem.title;
      if (Array.isArray(problem.errors)) errors = problem.errors as string[];
    } catch {
      // non-JSON error body; keep the default message
    }
    throw new ApiError(response.status, message, errors);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  register: (body: { businessName: string; email: string; password: string }) =>
    request<AuthResult>('/api/auth/register', { method: 'POST', body }),

  login: (body: { email: string; password: string }) =>
    request<AuthResult>('/api/auth/login', { method: 'POST', body }),

  forgotPassword: (email: string) =>
    request<{ message: string }>('/api/auth/forgot-password', { method: 'POST', body: { email } }),

  resetPassword: (token: string, newPassword: string) =>
    request<{ message: string }>('/api/auth/reset-password', { method: 'POST', body: { token, newPassword } }),

  getDashboard: (token: string) => request<DashboardResponse>('/api/dashboard', { token }),

  listPlans: (token: string) => request<PlanResponse[]>('/api/plans', { token }),

  createPlan: (token: string, body: { name: string; amount: number; interval: string }) =>
    request<PlanResponse>('/api/plans', { method: 'POST', body, token }),

  registerCustomer: (token: string, body: { name: string; email: string }) =>
    request<CustomerResponse>('/api/customers', { method: 'POST', body, token }),

  enrolCustomer: (token: string, body: { customerId: string; planId: string }) =>
    request<SubscriptionResponse>('/api/subscriptions', { method: 'POST', body, token }),

  askAssistant: (token: string, question: string) =>
    request<AssistantAnswer>('/api/assistant/ask', { method: 'POST', body: { question }, token }),

  listTransactions: (token: string) => request<TransactionRow[]>('/api/transactions', { token }),

  initiateRefund: (
    token: string,
    body: { originalTransactionReference: string; amount: number; reason: string; refundReference: string },
  ) => request<RefundResult>('/api/refunds', { method: 'POST', body, token }),
};
