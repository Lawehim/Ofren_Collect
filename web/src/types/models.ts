export interface AuthResult {
  token: string;
  email: string;
  role: string;
}

export type BillingInterval = 'Weekly' | 'Monthly' | 'Yearly';

export interface PlanResponse {
  id: string;
  name: string;
  amount: number;
  interval: string;
  isActive: boolean;
}

export interface CustomerResponse {
  id: string;
  name: string;
  email: string;
}

export interface SubscriptionResponse {
  id: string;
  customerId: string;
  planId: string;
  reservedAccountNumber: string | null;
  reservedBankName: string | null;
  nextDueDate: string;
  status: string;
}

export interface DashboardSubscriptionRow {
  subscriptionId: string;
  customerName: string;
  planName: string;
  planAmount: number;
  reservedAccountNumber: string | null;
  reservedBankName: string | null;
  nextDueDate: string;
  status: string;
  currentInvoiceStatus: string | null;
}

export interface DashboardSummary {
  collectedThisPeriod: number;
  overdueCount: number;
}

export interface DashboardResponse {
  subscriptions: DashboardSubscriptionRow[];
  summary: DashboardSummary;
}

export interface PaymentReconciledEvent {
  subscriptionId: string;
  invoiceId: string;
  status: string;
  amountPaid: number;
}

export interface AssistantAnswer {
  answer: string;
  grounded: boolean;
  intent: string;
}

export interface TransactionRow {
  transactionReference: string;
  customerName: string;
  amount: number;
  refundedAmount: number;
  refundableAmount: number;
  reservedAccountNumber: string;
  paidAt: string;
}

export interface RefundResult {
  id: string;
  refundReference: string;
  originalTransactionReference: string;
  amount: number;
  status: string;
}
