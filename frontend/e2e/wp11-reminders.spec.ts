import { test, expect } from "@playwright/test";
import { signInAs, uniqueUser } from "./helpers/setup";

function trigger(page: import("@playwright/test").Page) {
  return page.locator("#system-menu-trigger");
}

function toggleButton(page: import("@playwright/test").Page) {
  return page.getByRole("button", { name: /^Turn reminder emails (on|off)$/ });
}

test.describe("WP11: Notification settings", () => {
  test("WP11-1: menu link navigates to settings", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await trigger(page).click();
    await page
      .locator("#system-menu-panel")
      .getByRole("link", { name: "Notification settings" })
      .click();
    await page.waitForURL(/\/settings\/notifications$/);

    await expect(
      page.getByRole("heading", { name: "Notification settings" }),
    ).toBeVisible();
    await expect(page.locator("#system-menu-panel")).toHaveCount(0);
  });

  test("WP11-2: toggle off persists", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await page.goto("/settings/notifications");
    await expect(toggleButton(page)).toHaveText("Turn reminder emails off");

    await toggleButton(page).click();
    await expect(toggleButton(page)).toHaveText("Turn reminder emails on");

    await page.reload();
    await expect(toggleButton(page)).toHaveText("Turn reminder emails on");
  });

  test("WP11-3: toggle back on persists", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);

    await page.goto("/settings/notifications");
    await toggleButton(page).click();
    await expect(toggleButton(page)).toHaveText("Turn reminder emails on");

    await toggleButton(page).click();
    await expect(toggleButton(page)).toHaveText("Turn reminder emails off");

    await page.reload();
    await expect(toggleButton(page)).toHaveText("Turn reminder emails off");
  });
});
