import type { Metadata } from "next";
import { LegalPage } from "@/components/legal-page";

export const metadata: Metadata = {
  title: "Privacy policy - Maintained House",
};

export default function PrivacyPage() {
  return (
    <LegalPage title="Privacy policy" updated="2026-07-19">
      <p>
        Maintained House is a personal home-maintenance tracker. This policy
        describes, in plain language, what data the service holds about you
        and why. We collect the minimum needed to run the service and
        nothing else.
      </p>

      <h2>What we collect</h2>
      <ul>
        <li>
          <strong>Your Google account identity</strong> - when you sign in
          with Google we receive your name, email address, and Google
          subject identifier. We use the identifier to keep your account
          separate from everyone else&apos;s.
        </li>
        <li>
          <strong>The maintenance data you enter</strong> - properties,
          assets, jobs, schedules, checklist steps, and completion history.
        </li>
      </ul>

      <h2>What we use it for</h2>
      <p>
        Solely to provide the service: showing you your own data and
        keeping it safe. We do not sell data, share it with third parties,
        profile you, or send marketing.
      </p>

      <h2>Where it is stored</h2>
      <p>
        Data is hosted on Microsoft Azure (application) and MongoDB
        (database), in European Union regions. Access is restricted to your
        signed-in account; the operator can access stored data only for
        maintenance and support of the service.
      </p>

      <h2>Cookies</h2>
      <p>
        We use session cookies for sign-in only. There are no advertising
        or analytics trackers of any kind.
      </p>

      <h2>Audit records</h2>
      <p>
        The service keeps internal audit records of account actions (for
        example when a job is created or completed) to support
        troubleshooting and integrity of your history.
      </p>

      <h2>Retention and removal</h2>
      <p>
        Your data is kept until you ask us to remove it. To request
        deletion of your account and all associated data, or a copy of
        your data, email{" "}
        <a
          href="mailto:contact@maintained.house"
          className="text-gray-900 underline"
        >
          contact@maintained.house
        </a>
        . We will action requests within 30 days.
      </p>

      <h2>Changes to this policy</h2>
      <p>
        If this policy changes, the &quot;Last updated&quot; date above
        changes with it. Material changes will be announced on the sign-in
        page.
      </p>

      <h2>Contact</h2>
      <p>
        Questions about this policy:{" "}
        <a
          href="mailto:contact@maintained.house"
          className="text-gray-900 underline"
        >
          contact@maintained.house
        </a>
        .
      </p>
    </LegalPage>
  );
}
