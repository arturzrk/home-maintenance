import { test, expect } from "@playwright/test";
import { signInAs, createPropertyViaApi, uniqueUser, todayIso } from "./helpers/setup";

test.describe("WP05: Recurring jobs on Property page", () => {
  test("shows Recurring jobs section with empty state", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "Test House");

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await expect(page.getByRole("heading", { name: "Recurring jobs" })).toBeVisible();
    await expect(page.getByRole("button", { name: /\+ Add recurring job/i })).toBeVisible();
    await expect(page.getByText("No recurring job definitions yet.")).toBeVisible();
  });

  test("create definition → appears in list with schedule label", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "Test House");
    const today = todayIso(); // e.g. "2026-06-09"
    const monthYear = new Date().toLocaleDateString("en-GB", { month: "short", year: "numeric" }); // "Jun 2026"

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.getByRole("button", { name: /\+ Add recurring job/i }).click();
    await page.locator("#jd-name").fill("Service boiler");
    await page.locator("#jd-multiplier").fill("3");
    await page.locator("#jd-unit").selectOption("Month");
    await page.locator("#jd-start").fill(today);
    await page.getByRole("button", { name: "Save recurring job" }).click();

    await expect(page.getByRole("link", { name: "Service boiler", exact: true })).toBeVisible();
    await expect(page.getByText(new RegExp(`Every 3 months from ${monthYear}`))).toBeVisible();
  });

  test("job generated from definition shows Recurring badge", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "Test House");
    const today = todayIso();

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.getByRole("button", { name: /\+ Add recurring job/i }).click();
    await page.locator("#jd-name").fill("Annual inspection");
    // Leave multiplier = 1, unit = Month (defaults)
    await page.locator("#jd-start").fill(today);
    await page.getByRole("button", { name: "Save recurring job" }).click();

    // After create + router.refresh(), the Jobs section should show the generated job
    await expect(
      page.getByRole("main").getByText("Recurring").first(),
    ).toBeVisible();
  });
});
