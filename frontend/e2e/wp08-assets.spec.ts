import { test, expect } from "@playwright/test";
import {
  signInAs,
  createPropertyViaApi,
  createAssetViaApi,
  createJobViaApi,
  uniqueUser,
  todayIso,
} from "./helpers/setup";

test.describe("WP08: Assets", () => {
  test("WP08-1: create asset via UI shows card and clears input", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await expect(page.getByText("No assets yet.")).toBeVisible();

    await page.locator("#asset-name").fill("Boiler");
    await page.locator("#asset-category").fill("Heating");
    await page.getByRole("button", { name: "Add asset" }).click();

    const card = page.getByRole("link", { name: /Boiler/ });
    await expect(card).toBeVisible();
    await expect(card).toContainText("Heating");
    await expect(page.locator("#asset-name")).toHaveValue("");
  });

  test("WP08-2: asset detail shows fields and inline rename updates heading", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const assetId = await createAssetViaApi(token, propertyId, {
      name: "Boiler",
      category: "Heating",
    });

    await signInAs(page, sub);
    await page.goto(`/assets/${assetId}`);

    await expect(
      page.getByRole("button", { name: "Edit asset name" }),
    ).toContainText("Boiler");
    await expect(
      page.getByRole("button", { name: "Edit asset category" }),
    ).toContainText("Heating");

    await page.getByRole("button", { name: "Edit asset name" }).click();
    await page
      .getByRole("textbox", { name: "Edit asset name" })
      .fill("Combi boiler");
    await page.keyboard.press("Enter");

    await expect(
      page.getByRole("button", { name: "Edit asset name" }),
    ).toContainText("Combi boiler");
  });

  test("WP08-3: job created with asset selected links both ways", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const assetId = await createAssetViaApi(token, propertyId, {
      name: "Boiler",
    });

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.locator("#job-name").fill("Service boiler");
    await page.locator("#job-asset").selectOption(assetId);
    await page.getByRole("button", { name: "Create job" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    const assetLink = page.getByRole("link", { name: "Boiler" });
    await expect(assetLink).toBeVisible();
    await expect(assetLink).toHaveAttribute("href", `/assets/${assetId}`);

    await page.goto(`/assets/${assetId}`);
    await expect(
      page.locator('a[href^="/jobs/"]', { hasText: "Service boiler" }),
    ).toBeVisible();
  });

  test("WP08-4: definition scoped to asset generates scoped jobs", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const assetId = await createAssetViaApi(token, propertyId, {
      name: "Gutters",
    });

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    await page.getByRole("button", { name: "+ Add recurring job" }).click();
    await page.locator("#jd-name").fill("Clean gutters");
    await page.locator("#jd-start").fill(todayIso());
    await page.locator("#jd-asset").selectOption(assetId);
    await page
      .getByRole("button", { name: "Save recurring job" })
      .click();

    // Definition appears on the property page after refresh.
    await expect(
      page.locator('a[href^="/job-definitions/"]', { hasText: "Clean gutters" }),
    ).toBeVisible();

    // The asset detail page lists the definition and the jobs generated
    // inline over the horizon (several for a monthly schedule).
    await page.goto(`/assets/${assetId}`);
    await expect(
      page.locator('a[href^="/job-definitions/"]', { hasText: "Clean gutters" }),
    ).toBeVisible();
    await expect(
      page.locator('a[href^="/jobs/"]', { hasText: "Clean gutters" }).first(),
    ).toBeVisible();
  });

  test("WP08-5: obsolete lifecycle - badge, dropdown exclusion, reversible", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    const assetId = await createAssetViaApi(token, propertyId, {
      name: "Boiler",
    });
    const jobId = await createJobViaApi(
      token,
      propertyId,
      "Service boiler",
      [],
      assetId,
    );

    await signInAs(page, sub);
    await page.goto(`/assets/${assetId}`);

    await page.getByRole("button", { name: "Mark obsolete" }).click();
    await expect(page.getByText("Obsolete", { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Reactivate" })).toBeVisible();

    // Property page: card shows the badge; the job-asset dropdown is
    // gone entirely because no non-obsolete assets remain.
    await page.goto(`/properties/${propertyId}`);
    await expect(
      page.getByRole("link", { name: /Boiler/ }).getByText("Obsolete"),
    ).toBeVisible();
    await expect(page.locator("#job-asset")).toHaveCount(0);

    // Existing scoped work keeps its asset link (no cascade).
    await page.goto(`/jobs/${jobId}`);
    await expect(page.getByRole("link", { name: "Boiler" })).toBeVisible();

    // Reactivate: the dropdown offers the asset again.
    await page.goto(`/assets/${assetId}`);
    await page.getByRole("button", { name: "Reactivate" }).click();
    await expect(
      page.getByRole("button", { name: "Mark obsolete" }),
    ).toBeVisible();

    await page.goto(`/properties/${propertyId}`);
    await expect(
      page.locator(`#job-asset option[value="${assetId}"]`),
    ).toHaveCount(1);
  });

  test("WP08-6: job created without asset shows no asset link", async ({
    page,
  }) => {
    const { sub, token } = uniqueUser();
    const propertyId = await createPropertyViaApi(token, "My House");
    await createAssetViaApi(token, propertyId, { name: "Boiler" });

    await signInAs(page, sub);
    await page.goto(`/properties/${propertyId}`);

    // Dropdown is rendered but left at the default "No asset".
    await expect(page.locator("#job-asset")).toBeVisible();
    await page.locator("#job-name").fill("Paint fence");
    await page.getByRole("button", { name: "Create job" }).click();
    await page.waitForURL(/\/jobs\/.+/);

    await expect(
      page.getByRole("button", { name: "Edit job name" }),
    ).toContainText("Paint fence");
    await expect(page.getByText("Asset:")).toHaveCount(0);
  });
});
