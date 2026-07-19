import { render, screen } from "@testing-library/react";
import { LandingPage } from "@/components/landing-page";
import PrivacyPage from "@/app/privacy/page";
import TermsPage from "@/app/terms/page";

describe("LandingPage", () => {
  it("renders the brand and value proposition", () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("heading", { level: 1, name: "Maintained House" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Track the maintenance of your home/),
    ).toBeInTheDocument();
  });

  it("has a sign-in CTA pointing at /signin", () => {
    render(<LandingPage />);
    const cta = screen.getByRole("link", { name: /Sign in to get started/ });
    expect(cta).toHaveAttribute("href", "/signin");
    expect(cta).toHaveAttribute("id", "landing-signin-cta");
  });

  it("renders the four feature highlights", () => {
    render(<LandingPage />);
    for (const title of [
      "Properties",
      "Recurring schedules",
      "Step checklists",
      "Assets",
    ]) {
      expect(screen.getByRole("heading", { name: title })).toBeInTheDocument();
    }
  });

  it("links the user guide, privacy policy, and terms", () => {
    render(<LandingPage />);
    const guide = screen.getByRole("link", { name: /User guide/ });
    expect(guide).toHaveAttribute("href", "/user-manual/index.html");
    expect(guide).toHaveAttribute("target", "_blank");
    expect(
      screen.getByRole("link", { name: "Privacy policy" }),
    ).toHaveAttribute("href", "/privacy");
    expect(
      screen.getByRole("link", { name: "Terms of service" }),
    ).toHaveAttribute("href", "/terms");
  });
});

describe("Legal pages", () => {
  it("privacy page renders key sections and the contact address", () => {
    render(<PrivacyPage />);
    expect(
      screen.getByRole("heading", { level: 1, name: "Privacy policy" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "What we collect" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Retention and removal" }),
    ).toBeInTheDocument();
    expect(
      screen.getAllByRole("link", { name: "contact@maintained.house" }).length,
    ).toBeGreaterThan(0);
  });

  it("terms page renders key sections including governing law", () => {
    render(<TermsPage />);
    expect(
      screen.getByRole("heading", { level: 1, name: "Terms of service" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Governing law" }),
    ).toBeInTheDocument();
    expect(screen.getByText(/law of Poland/)).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Limitation of liability" }),
    ).toBeInTheDocument();
  });
});
