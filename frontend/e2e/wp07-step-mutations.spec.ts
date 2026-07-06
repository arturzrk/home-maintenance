import { test, expect } from "@playwright/test";
import {
  signInAs,
  createPropertyViaApi,
  createJobViaApi,
  createAndCompleteJobViaApi,
  uniqueUser,
} from "./helpers/setup";

test.describe("WP07: Step mutations & job rename", () => {
  test("WP07-1: add a step on an active job", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Paint fence");

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await expect(page.getByText("No steps on this job.")).toBeVisible();

    const input = page.getByPlaceholder("Add a step");
    await input.fill("Buy paint");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toContainText("Buy paint");
    await expect(input).toHaveValue("");
  });

  test("WP07-2: remove a step", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Service boiler", [
      "First step",
      "Second step",
    ]);

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page
      .getByRole("button", { name: 'Remove step "First step"' })
      .click();

    await expect(page.getByText("First step")).toBeHidden();
    await expect(page.getByText("Second step")).toBeVisible();
  });

  test("WP07-3: edit a step description inline", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Service boiler", [
      "Old description",
    ]);

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page
      .getByRole("button", { name: "Edit description for step 1" })
      .click();
    await page
      .getByRole("textbox", { name: "Edit description for step 1" })
      .fill("New description");
    await page.keyboard.press("Enter");

    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toContainText("New description");
  });

  test("WP07-4: reorder steps with the down button", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Service boiler", [
      "First",
      "Second",
    ]);

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await expect(page.getByRole("listitem").first()).toContainText("First");

    await page.getByRole("button", { name: 'Move "First" down' }).click();

    await expect(page.getByRole("listitem").first()).toContainText("Second");
    await expect(page.getByRole("listitem").nth(1)).toContainText("First");
  });

  test("WP07-5: step mutations disabled on a completed job", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createAndCompleteJobViaApi(
      token,
      propertyId,
      "Done job",
      ["Only step"],
    );

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await expect(page.getByText(/Completed on/)).toBeVisible();
    // Add-step form unmounts entirely when the job is locked.
    await expect(page.getByPlaceholder("Add a step")).toBeHidden();
    // Disabled InlineEditableText renders a plain span, so the edit
    // affordances lose their button role while the text stays visible.
    await expect(
      page.getByRole("button", { name: "Edit job name" }),
    ).toHaveCount(0);
    await expect(page.getByText("Done job")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Edit description for step 1" }),
    ).toHaveCount(0);
    await expect(page.getByText("Only step")).toBeVisible();
    // Remove/checkbox stay rendered but disabled (steps remain visible as
    // a record of what was done).
    await expect(
      page.getByRole("button", { name: 'Remove step "Only step"' }),
    ).toBeDisabled();
    const checkbox = page.getByRole("checkbox", {
      name: 'Toggle "Only step"',
    });
    await expect(checkbox).toBeDisabled();
    await expect(checkbox).toBeChecked();
  });

  test("WP07-6: rename job via inline edit", async ({ page }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const jobId = await createJobViaApi(token, propertyId, "Old Job Name");

    await signInAs(page, sub);
    await page.goto(`/jobs/${jobId}`);

    await page.getByRole("button", { name: "Edit job name" }).click();
    await page
      .getByRole("textbox", { name: "Edit job name" })
      .fill("New Job Name");
    await page.keyboard.press("Enter");

    await expect(
      page.getByRole("button", { name: "Edit job name" }),
    ).toContainText("New Job Name");
  });
});
