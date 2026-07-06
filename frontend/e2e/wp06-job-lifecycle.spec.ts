import { test, expect } from "@playwright/test";
import {
  signInAs,
  createPropertyViaApi,
  createJobViaApi,
  uniqueUser,
} from "./helpers/setup";

test.describe("WP06: Job lifecycle", () => {
  test("WP06-1: create job without steps navigates to detail", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.locator("#job-name").fill("Paint fence");
    await page.getByRole("button", { name: "Create job" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    await expect(
      page.getByRole("button", { name: "Edit job name" }),
    ).toContainText("Paint fence");
    await expect(page.getByText("No steps on this job.")).toBeVisible();
  });

  test("WP06-2: create job with steps shows unchecked checklist", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.locator("#job-name").fill("Service boiler");
    await page
      .locator("#job-steps")
      .fill("Shut off gas\nDrain system\nReplace filter");
    await page.getByRole("button", { name: "Create job" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    const checkboxes = page.getByRole("checkbox");
    await expect(checkboxes).toHaveCount(3);
    for (const box of await checkboxes.all()) {
      await expect(box).not.toBeChecked();
    }
    await expect(
      page.getByRole("button", { name: "Complete job" }),
    ).toBeDisabled();
  });

  test("WP06-3: ticking a step strikes it through, button stays disabled", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Service boiler", [
      "Shut off gas",
      "Drain system",
    ]);

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page
      .getByRole("checkbox", { name: 'Toggle "Shut off gas"' })
      .check();

    await expect(
      page
        .locator("li", { hasText: "Shut off gas" })
        .locator("span.line-through"),
    ).toBeVisible();
    await expect(
      page.getByRole("checkbox", { name: 'Toggle "Drain system"' }),
    ).not.toBeChecked();
    await expect(
      page.getByRole("button", { name: "Complete job" }),
    ).toBeDisabled();
  });

  test("WP06-4: ticking all steps and completing locks the job", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Paint fence", [
      "Buy paint",
    ]);

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page.getByRole("checkbox", { name: 'Toggle "Buy paint"' }).check();
    const completeButton = page.getByRole("button", { name: "Complete job" });
    await expect(completeButton).toBeEnabled();
    await completeButton.click();

    await expect(page.getByText(/Completed on/)).toBeVisible();
    // On a completed job the add-step form unmounts, while StepRow keeps
    // its Remove button rendered but disabled (steps stay visible as a
    // record of what was done).
    await expect(page.getByPlaceholder("Add a step")).toBeHidden();
    await expect(
      page.getByRole("button", { name: 'Remove step "Buy paint"' }),
    ).toBeDisabled();
  });

  test("WP06-5: job card shows step count and Active badge", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    await createJobViaApi(token, propertyId, "Service boiler", [
      "Shut off gas",
      "Drain system",
    ]);

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    const card = page.getByRole("link", { name: /Service boiler/ });
    await expect(card).toContainText("0 of 2 steps");
    await expect(card).toContainText("Active");
  });

  test("WP06-6: back to property link navigates to the property", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Paint fence");

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page.getByRole("link", { name: "Back to property" }).click();
    await page.waitForURL(new RegExp(`/properties/${propertyId}`));

    expect(page.url()).toContain(`/properties/${propertyId}`);
  });
});
