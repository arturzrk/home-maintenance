import type { Metadata } from "next";
import { LegalPage } from "@/components/legal-page";

export const metadata: Metadata = {
  title: "Terms of service - Maintained House",
};

export default function TermsPage() {
  return (
    <LegalPage title="Terms of service" updated="2026-07-19">
      <h2>What the service is</h2>
      <p>
        Maintained House is a web application for tracking the maintenance
        of homes you look after: properties, assets, one-off jobs, and
        recurring schedules with checklists. It is provided free of charge
        for personal use.
      </p>

      <h2>Your account</h2>
      <p>
        You sign in with your Google account; your Google identity is your
        account. You are responsible for activity under your account. See
        the privacy policy for what identity data we hold.
      </p>

      <h2>Acceptable use</h2>
      <p>
        The service is intended for personal and household maintenance
        tracking. You agree not to abuse it - no attempts to access other
        users&apos; data, disrupt the service, or use it for unlawful
        purposes.
      </p>

      <h2>Your data</h2>
      <p>
        The maintenance data you enter is yours. We store it to provide
        the service and delete it on request, as described in the privacy
        policy.
      </p>

      <h2>No warranty</h2>
      <p>
        The service is provided &quot;as is&quot; and &quot;as
        available&quot;, without warranties of any kind. We aim for
        reliability but do not guarantee uninterrupted availability or
        that the service is error-free. Keep independent records of
        anything critical (for example gas-safety certificates).
      </p>

      <h2>Limitation of liability</h2>
      <p>
        To the maximum extent permitted by law, the operator is not liable
        for indirect or consequential damages arising from use of the
        service, including missed maintenance, data loss, or property
        damage. Nothing in these terms limits liability that cannot be
        limited under applicable law.
      </p>

      <h2>Governing law</h2>
      <p>
        These terms are governed by the law of Poland, and disputes are
        subject to the jurisdiction of Polish courts.
      </p>

      <h2>Changes to these terms</h2>
      <p>
        If these terms change, the &quot;Last updated&quot; date above
        changes with it. Continued use after a change constitutes
        acceptance.
      </p>

      <h2>Contact</h2>
      <p>
        Questions about these terms:{" "}
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
