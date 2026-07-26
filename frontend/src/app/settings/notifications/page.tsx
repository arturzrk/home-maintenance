import { notificationPreferences } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { NotificationSettingsToggle } from "@/components/notification-settings-toggle";

export const dynamic = "force-dynamic";

export default async function NotificationSettingsPage() {
  const session = await requireSession();
  const preferences = await notificationPreferences.get(session.idToken);

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <header>
        <h1 className="text-2xl font-bold tracking-tight">
          Notification settings
        </h1>
        <p className="mt-1 text-sm text-gray-500">
          Once a day, we email you a summary of jobs due within 3 days or
          overdue, grouped by property. Nothing is sent on days with nothing
          due.
        </p>
      </header>

      <section className="space-y-2">
        <p className="text-sm text-gray-700">
          Reminder emails are currently{" "}
          <strong>{preferences.remindersEnabled ? "on" : "off"}</strong>
          {preferences.email ? <> for {preferences.email}</> : null}.
        </p>
        <NotificationSettingsToggle
          remindersEnabled={preferences.remindersEnabled}
        />
      </section>
    </div>
  );
}
