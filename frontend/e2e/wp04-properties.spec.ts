import { test, expect } from "@playwright/test";
import { signInAs, createPropertyViaApi, uniqueUser } from "./helpers/setup";

test.describe("WP04: Properties page", () => {
  test("WP04-1: shows empty state for fresh user", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);
    await page.goto("/properties");

    await expect(page.getByRole("heading", { name: "My properties" })).toBeVisible();
    await expect(page.getByText("No properties yet.")).toBeVisible();
    await expect(page.getByPlaceholder("Property name")).toBeVisible();
    await expect(page.getByRole("button", { name: "Create" })).toBeVisible();
  });

  test("WP04-2: create property → appears in list, input cleared", async ({ page }) => {
    const { sub } = uniqueUser();
    await signInAs(page, sub);
    await page.goto("/properties");

    await page.getByPlaceholder("Property name").fill("My House");
    await page.getByRole("button", { name: "Create" }).click();

    await expect(page.getByRole("link", { name: "My House" })).toBeVisible();
    await expect(page.getByPlaceholder("Property name")).toHaveValue("");
  });

  test("WP04-3: clicking card navigates to property detail", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    await signInAs(page, sub);
    await page.goto("/properties");

    await page.getByRole("link", { name: "My House" }).click();
    await page.waitForURL(/\/properties\/.+/);

    expect(page.url()).toContain(`/properties/${propertyId}`);
    await expect(page.getByRole("button", { name: "Edit property name" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Jobs", exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Recurring jobs" })).toBeVisible();
  });

  test("WP04-4: rename property via inline edit", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "Old Name");
    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.getByRole("button", { name: "Edit property name" }).click();
    const input = page.getByRole("textbox", { name: "Edit property name" });
    await expect(input).toBeVisible();
    await input.fill("New Name");
    await page.keyboard.press("Enter");

    await expect(
      page.getByRole("button", { name: "Edit property name" }),
    ).toContainText("New Name");
  });

  test("WP04-5: property detail shows jobs empty state", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await expect(page.getByText("No jobs yet. Create one above.")).toBeVisible();
  });

  test("WP04-6: unauthenticated visitor redirected to signin", async ({ page }) => {
    await page.goto("/properties");

    expect(page.url()).toContain("/signin");
  });
});
