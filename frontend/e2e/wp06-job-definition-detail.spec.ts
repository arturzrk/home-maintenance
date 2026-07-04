import { test, expect } from "@playwright/test";
import {
  signInAs,
  createPropertyViaApi,
  createJobDefinitionViaApi,
  uniqueUser,
  todayIso,
} from "./helpers/setup";

test.describe("WP06: JobDefinition detail page", () => {
  test("WP06-1: shows definition name, schedule label, step templates", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Service boiler",
      schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
      stepTemplates: [{ description: "Check pressure" }],
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await expect(
      page.getByRole("button", { name: "Edit definition name" }),
    ).toContainText("Service boiler");
    await expect(page.getByText(/Every month from/)).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toContainText("Check pressure");
  });

  test("WP06-2: rename definition via inline edit", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Old Definition",
      schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await page.getByRole("button", { name: "Edit definition name" }).click();
    await page
      .getByRole("textbox", { name: "Edit definition name" })
      .fill("New Definition");
    await page.keyboard.press("Enter");

    await expect(
      page.getByRole("button", { name: "Edit definition name" }),
    ).toContainText("New Definition");
  });

  test("WP06-3: add step template", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Service boiler",
      schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await expect(page.getByText("No step templates yet.")).toBeVisible();

    const input = page.getByPlaceholder("Add a step template");
    await input.fill("Bleed radiators");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toContainText("Bleed radiators");
    await expect(input).toHaveValue("");
  });

  test("WP06-4: remove step template", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Service boiler",
      schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
      stepTemplates: [{ description: "Old step" }],
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toContainText("Old step");

    await page
      .getByRole("button", { name: 'Remove step template "Old step"' })
      .click();

    await expect(page.getByText("No step templates yet.")).toBeVisible();
    await expect(
      page.getByRole("button", { name: /Remove step template/ }),
    ).toBeHidden();
  });

  test("WP06-5: generate next navigates to new job", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Service boiler",
      schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await page.getByRole("button", { name: "Generate next" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    expect(page.url()).toMatch(/\/jobs\/.+/);
  });

  // The duplicate-occurrence guard (next_occurrence_already_exists) only
  // fires on concurrent requests -- sequential clicks always advance to a
  // later occurrence, so it cannot be reached deterministically end-to-end.
  // The reachable inline-error path is a schedule exhausted by its endDate.
  test("WP06-6: exhausted schedule shows inline error on generate next", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    // Start beyond the 3-month inline-generation horizon so the first
    // occurrence is only created by the Generate next click.
    const start = new Date(Date.now() + 200 * 24 * 60 * 60 * 1000);
    const end = new Date(start.getTime() + 5 * 24 * 60 * 60 * 1000);
    const defId = await createJobDefinitionViaApi(token, propertyId, {
      name: "Service boiler",
      schedule: {
        unit: "Month",
        multiplier: 1,
        startDate: start.toISOString().split("T")[0],
        endDate: end.toISOString().split("T")[0],
      },
    });

    await signInAs(page, sub);
    await page.goto(`/job-definitions/${defId}`);

    await page.getByRole("button", { name: "Generate next" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    await page.goto(`/job-definitions/${defId}`);
    await expect(
      page.getByRole("button", { name: "Generate next" }),
    ).toBeVisible();

    await page.getByRole("button", { name: "Generate next" }).click();

    await expect(
      page.getByText("The schedule has no future occurrences."),
    ).toBeVisible();
    expect(page.url()).toMatch(/\/job-definitions\/.+/);
  });
});
