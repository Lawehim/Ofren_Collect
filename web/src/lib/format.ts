const AVATAR_COLORS = ['#101826', '#2A3B52', '#3E5C82', '#2A3547', '#33507A', '#B4791A'];

export function naira(amount: number): string {
  return new Intl.NumberFormat('en-NG', { maximumFractionDigits: 0 }).format(amount);
}

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  return parts
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');
}

export function avatarColor(seed: string): string {
  const code = seed.charCodeAt(0) || 0;
  return AVATAR_COLORS[code % AVATAR_COLORS.length]!;
}

export function formatAccount(accountNumber: string | null): string {
  if (!accountNumber) return '—';
  return accountNumber.replace(/(\d{4})(?=\d)/g, '$1 ').trim();
}
