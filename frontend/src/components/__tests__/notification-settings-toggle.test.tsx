import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { NotificationSettingsToggle } from "@/components/notification-settings-toggle";

jest.mock("@/app/settings/notifications/actions", () => ({
  updateNotificationPreferences: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn() }),
}));

import { updateNotificationPreferences } from "@/app/settings/notifications/actions";

describe("NotificationSettingsToggle", () => {
  beforeEach(() => (updateNotificationPreferences as jest.Mock).mockReset());

  it("shows the turn-off action when reminders are enabled", () => {
    render(<NotificationSettingsToggle remindersEnabled={true} />);
    expect(
      screen.getByRole("button", { name: "Turn reminder emails off" }),
    ).toBeInTheDocument();
  });

  it("shows the turn-on action when reminders are disabled", () => {
    render(<NotificationSettingsToggle remindersEnabled={false} />);
    expect(
      screen.getByRole("button", { name: "Turn reminder emails on" }),
    ).toBeInTheDocument();
  });

  it("toggles reminders off via updateNotificationPreferences", async () => {
    (updateNotificationPreferences as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { email: "alice@example.com", remindersEnabled: false },
    });

    render(<NotificationSettingsToggle remindersEnabled={true} />);
    fireEvent.click(screen.getByRole("button", { name: "Turn reminder emails off" }));

    await waitFor(() =>
      expect(updateNotificationPreferences).toHaveBeenCalledWith(false),
    );
  });

  it("toggles reminders on via updateNotificationPreferences", async () => {
    (updateNotificationPreferences as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { email: "alice@example.com", remindersEnabled: true },
    });

    render(<NotificationSettingsToggle remindersEnabled={false} />);
    fireEvent.click(screen.getByRole("button", { name: "Turn reminder emails on" }));

    await waitFor(() =>
      expect(updateNotificationPreferences).toHaveBeenCalledWith(true),
    );
  });

  it("surfaces the toggle error inline", async () => {
    (updateNotificationPreferences as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Something went wrong",
    });

    render(<NotificationSettingsToggle remindersEnabled={true} />);
    fireEvent.click(screen.getByRole("button", { name: "Turn reminder emails off" }));

    await waitFor(() =>
      expect(screen.getByText("Something went wrong")).toBeInTheDocument(),
    );
  });
});
