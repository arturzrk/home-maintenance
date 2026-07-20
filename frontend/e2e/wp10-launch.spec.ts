import { test, expect } from "@playwright/test";
import { signInAs, uniqueUser } from "./helpers/setup";

test.describe("WP10: Launch pages", () => {
  test("WP10-1: anonymous / renders the landing page", async ({ page }) => {
    await page.goto("/");

    // No redirect: the public root stays put.
    expect(new URL(page.url()).pathname).toBe("/");

    await expect(
      page.getByRole("heading", { level: 1, name: "Maintained House" }),
    ).toBeVisible();
    await expect(page.locator("#landing-signin-cta")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Privacy policy" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Terms of service" }),
    ).toBeVisible();
    // The anonymous header carries its own User guide link; assert the
    // landing footer's copy specifically.
    await expect(
      page.getByRole("main").getByRole("link", { name: /User guide/ }),
    ).toBeVisible();
  });

  test("WP10-2: landing CTA navigates to the sign-in page", async ({
    page,
  }) => {
    await page.goto("/");
    await page.locator("#landing-signin-cta").click();
    await page.waitForURL(/\/signin/);

    await expect(
      page.getByRole("heading", { name: "Sign in" }),
    ).toBeVisible();
  });

  test("WP10-3: privacy and terms are public", async ({ page }) => {
    await page.goto("/privacy");
    expect(new URL(page.url()).pathname).toBe("/privacy");
    await expect(
      page.getByRole("heading", { level: 1, name: "Privacy policy" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "contact@maintained.house" }).first(),
    ).toBeVisible();

    await page.goto("/terms");
    expect(new URL(page.url()).pathname).toBe("/terms");
    await expect(
      page.getByRole("heading", { level: 1, name: "Terms of service" }),
    ).toBeVisible();
    await expect(page.getByText(/law of Poland/)).toBeVisible();
  });

  test("WP10-4: signed-in / still renders the dashboard", async ({
    page,
  }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await expect(
      page.getByRole("heading", { name: "Welcome back" }),
    ).toBeVisible();
    await expect(page.locator("#dashboard-properties-link")).toBeVisible();
    await expect(page.locator("#landing-signin-cta")).toHaveCount(0);
  });
});
