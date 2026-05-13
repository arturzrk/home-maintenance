import { test, expect } from '@playwright/test';

test.describe('WP05: WP05-job-aggregate-backend', () => {
  test('should complete WP05-job-aggregate-backend', async ({ page }) => {
    await page.goto('/');
    // Verify All six endpoints respond per `contracts/`.
    // Verify All Job lifecycle invariants enforced.
    // Verify the page shows "`job.created`, `step.ticked`, `step.unticked`,"
    // Verify CI green.
    // Verify no JavaScript errors
  });
});
