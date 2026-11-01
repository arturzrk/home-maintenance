// Pure calendar-day math for due-date calculations. No knowledge of jobs
// or properties - kept separate so the timezone-safety invariant below
// gets its own exhaustive boundary tests instead of being buried inside
// business-logic tests.

const MS_PER_DAY = 86_400_000;

/**
 * "yyyy-MM-dd" -> days since the Unix epoch, calendar-only.
 *
 * Invariant: this never returns a Date object to the caller, only an
 * integer derived from Date.UTC(...). Nothing downstream ever reads a
 * UTC-constructed Date back with local getters (or vice versa) - that
 * mismatch is the classic source of an off-by-one-day bug across
 * timezones, and avoiding it entirely is why this function and
 * todayEpochDay both reduce to a plain integer before any arithmetic.
 */
export function parseIsoDateOnly(iso: string): number {
  const [year, month, day] = iso.split("-").map(Number);
  return Math.floor(Date.UTC(year, month - 1, day) / MS_PER_DAY);
}

/**
 * today's local wall-clock calendar date (ignoring time-of-day),
 * converted to the same epoch-day space as parseIsoDateOnly.
 */
export function todayEpochDay(today: Date): number {
  return Math.floor(
    Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()) /
      MS_PER_DAY,
  );
}

/** Positive = due in the future, 0 = due today, negative = overdue. */
export function daysUntilDue(today: Date, dueDateIso: string): number {
  return parseIsoDateOnly(dueDateIso) - todayEpochDay(today);
}

export function formatDueLabel(days: number): string {
  if (days < 0) return `Overdue by ${-days} day${-days === 1 ? "" : "s"}`;
  if (days === 0) return "Due today";
  return `Due in ${days} day${days === 1 ? "" : "s"}`;
}
