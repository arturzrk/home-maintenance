// Pure calendar-day math for due-date calculations. No knowledge of jobs
// or properties - kept separate so the timezone-safety invariant below
// gets its own exhaustive boundary tests instead of being buried inside
// business-logic tests.

const MS_PER_DAY = 86_400_000;

/**
 * Invariant: this never returns a Date object to the caller, only an
 * integer derived from Date.UTC(...). Nothing downstream ever reads a
 * UTC-constructed Date back with local getters (or vice versa) - that
 * mismatch is the classic source of an off-by-one-day bug across
 * timezones, and avoiding it entirely is why parseIsoDateOnly and
 * todayEpochDay both reduce to a plain integer before any arithmetic.
 */
function toEpochDay(year: number, month: number, day: number): number {
  return Math.floor(Date.UTC(year, month, day) / MS_PER_DAY);
}

/** "yyyy-MM-dd" -> days since the Unix epoch, calendar-only. */
export function parseIsoDateOnly(iso: string): number {
  const [year, month, day] = iso.split("-").map(Number);
  return toEpochDay(year, month - 1, day);
}

/**
 * today's local wall-clock calendar date (ignoring time-of-day),
 * converted to the same epoch-day space as parseIsoDateOnly.
 *
 * Known limitation: `today` is constructed server-side (new Date() in
 * a Server Component), so "local" here means the server process's
 * timezone, not the viewing user's. A user far from the server's
 * timezone can see a job's bucket flip a day earlier/later than their
 * own calendar near midnight. Fixing this properly means threading the
 * viewer's timezone from the client into the server render, which is
 * out of scope for this pass - tracked as a known gap, not silently
 * ignored.
 */
export function todayEpochDay(today: Date): number {
  return toEpochDay(today.getFullYear(), today.getMonth(), today.getDate());
}

/** Positive = due in the future, 0 = due today, negative = overdue. */
export function daysUntilDue(today: Date, dueDateIso: string): number {
  return parseIsoDateOnly(dueDateIso) - todayEpochDay(today);
}

function pluralDays(n: number): string {
  return `${n} day${n === 1 ? "" : "s"}`;
}

export function formatDueLabel(days: number): string {
  if (days < 0) return `Overdue by ${pluralDays(-days)}`;
  if (days === 0) return "Due today";
  return `Due in ${pluralDays(days)}`;
}
