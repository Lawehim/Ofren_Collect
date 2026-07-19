import type {
  AuthResult,
  CustomerResponse,
  DashboardResponse,
  PlanResponse,
  SubscriptionResponse,
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

  getDashboard: (token: string) => request<DashboardResponse>('/api/dashboard', { token }),

  listPlans: (token: string) => request<PlanResponse[]>('/api/plans', { token }),

  createPlan: (token: string, body: { name: string; amount: number; interval: string }) =>
    request<PlanResponse>('/api/plans', { method: 'POST', body, token }),

  registerCustomer: (token: string, body: { name: string; email: string }) =>
    request<CustomerResponse>('/api/customers', { method: 'POST', body, token }),

  enrolCustomer: (token: string, body: { customerId: string; planId: string }) =>
    request<SubscriptionResponse>('/api/subscriptions', { method: 'POST', body, token }),
};
