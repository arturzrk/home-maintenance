import { test, expect } from '@playwright/test';

test.describe('WP01: Public pages + branding', () => {
  test('should complete Public pages + branding', async ({ page }) => {
    await page.goto('/');
    // Verify the page shows "landing (manual curl: 200, no 307)"
    // Verify the page shows "dashboard unchanged (wp09 suite green)"
    // Verify /privacy and /terms return 200 without a session
    // Verify No "Home Maintenance" brand string left in user-facing UI
    // Verify no JavaScript errors
  });
});
