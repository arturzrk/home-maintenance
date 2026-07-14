import { test, expect } from "@playwright/test";
import { signInAs, createPropertyViaApi, uniqueUser } from "./helpers/setup";

function trigger(page: import("@playwright/test").Page) {
  return page.locator("#system-menu-trigger");
}

test.describe("WP09: System menu", () => {
  test("WP09-1: sign-in lands on the dashboard", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    expect(new URL(page.url()).pathname).toBe("/");
    await expect(
      page.getByRole("heading", { name: "Welcome back" }),
    ).toBeVisible();
    await expect(page.locator("#dashboard-properties-link")).toBeVisible();
  });

  test("WP09-2: menu shows identity and navigates to My properties", async ({
    page,
  }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    // Dev-stub sessions carry the sub as the display name.
    await expect(trigger(page)).toContainText(sub);

    await trigger(page).click();
    await page
      .locator("#system-menu-panel")
      .getByRole("link", { name: "My properties" })
      .click();
    await page.waitForURL(/\/properties$/);

    await expect(
      page.getByRole("heading", { name: "My properties" }),
    ).toBeVisible();
    await expect(page.locator("#system-menu-panel")).toHaveCount(0);
  });

  test("WP09-3: system info shows version and Connected", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await trigger(page).click();
    const panel = page.locator("#system-menu-panel");
    await expect(panel.getByText(/^Version .+/)).toBeVisible();
    await expect(panel.getByText("Version unknown")).toHaveCount(0);
    await expect(panel.getByText(/API: Connected/)).toBeVisible();
  });

  test("WP09-4: sign out returns to signin and locks the app", async ({
    page,
  }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await trigger(page).click();
    await page.getByRole("button", { name: "Sign out" }).click();
    await page.waitForURL(/\/signin/);

    // Protected pages redirect back to signin without a session.
    await page.goto("/properties");
    await page.waitForURL(/\/signin/);
    await expect(
      page.getByRole("button", { name: /Sign in as dev user/i }),
    ).toBeVisible();
  });

  test("WP09-5: deep link survives the sign-in round trip", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");

    // Visit the protected page signed out: middleware sends us to signin
    // with a callbackUrl. Complete the stub form by hand (signInAs waits
    // for the dashboard, which is not where this flow ends).
    await page.goto(`/properties/${propertyId}`);
    await page.waitForURL(/\/signin\?callbackUrl=/);

    await page.getByLabel(/OwnerId/i).fill(sub);
    await page.getByRole("button", { name: /Sign in as dev user/i }).click();
    await page.waitForURL(new RegExp(`/properties/${propertyId}`));

    await expect(
      page.getByRole("button", { name: "Edit property name" }),
    ).toContainText("My House");
  });

  test("WP09-6: dashboard CTA navigates to My properties", async ({
    page,
  }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await page.locator("#dashboard-properties-link").click();
    await page.waitForURL(/\/properties$/);
    await expect(
      page.getByRole("heading", { name: "My properties" }),
    ).toBeVisible();
  });
});
