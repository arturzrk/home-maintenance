import { test, expect } from '@playwright/test';

test.describe('WP02: Landing switch + dashboard page', () => {
  test('should complete Landing switch + dashboard page', async ({ page }) => {
    await page.goto('/');
    // Verify Stub sign-in without callbackUrl lands on `/` (manual check)
    // Navigate to /signin
    // Navigate to /properties/
    // Verify no JavaScript errors
  });
});
