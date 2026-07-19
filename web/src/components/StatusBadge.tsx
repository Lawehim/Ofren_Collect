const CLASS_BY_STATUS: Record<string, string> = {
  paid: 'paid',
  underpaid: 'under',
  overpaid: 'over',
  overdue: 'overdue',
  active: 'active',
  pending: 'pending',
  cancelled: 'pending',
};

export function StatusBadge({ status }: { status: string }) {
  const cls = CLASS_BY_STATUS[status.toLowerCase()] ?? 'pending';
  return (
    <span className={`badge ${cls}`}>
      <span className="d" />
      {status}
    </span>
  );
}
