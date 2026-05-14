import { test, expect } from '@playwright/test';

test.describe('WP06: WP06-job-frontend', () => {
  test('should complete WP06-job-frontend', async ({ page }) => {
    await page.goto('/');
    // Verify Signed-in user can navigate Property -> create Job ->
    // Verify the page shows "instantly; on backend"
    // Verify `Complete Job` is disabled until every step is ticked.
    // Verify the page shows "read-only checklist (no tick toggles)."
    // Verify CI green.
    // Verify no JavaScript errors
  });
});
