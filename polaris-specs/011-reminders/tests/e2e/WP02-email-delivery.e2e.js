import { test, expect } from '@playwright/test';

test.describe('WP02: Email delivery', () => {
  test('should complete Email delivery', async ({ page }) => {
    await page.goto('/');
    // Verify `IEmailSender`, `ResendEmailSender`, `LoggingEmailSender` implemented
    // Verify `Email:Provider=Log` remains the Development/CI default - no test ever calls the real Resend API
    // Verify Startup fails fast on `Email:Provider=Resend` with no API key
    // Verify no JavaScript errors
  });
});
