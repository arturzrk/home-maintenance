import { render, screen, fireEvent } from "@testing-library/react";
import { SystemMenu } from "@/components/system-menu";

function renderMenu(overrides: Partial<Parameters<typeof SystemMenu>[0]> = {}) {
  const signOutAction = jest.fn().mockResolvedValue(undefined);
  const props = {
    identity: "alice@example.com",
    version: "1.2.3",
    healthy: true,
    signOutAction,
    ...overrides,
  };
  render(<SystemMenu {...props} />);
  return { signOutAction };
}

// The trigger's accessible name IS the visible identity (no aria-label
// override), so assistive tech announces who is signed in.
function trigger() {
  return screen.getByRole("button", { name: /alice@example.com/ });
}

function panel() {
  return document.getElementById("system-menu-panel");
}

function openMenu() {
  fireEvent.click(trigger());
}

describe("SystemMenu", () => {
  it("shows the identity on the trigger, panel closed initially", () => {
    renderMenu();
    expect(trigger()).toHaveAttribute("aria-expanded", "false");
    expect(panel()).toBeNull();
  });

  it("opens on trigger click with all items", () => {
    renderMenu();
    openMenu();

    expect(panel()).not.toBeNull();
    expect(trigger()).toHaveAttribute("aria-expanded", "true");

    const properties = screen.getByRole("link", { name: "My properties" });
    expect(properties).toHaveAttribute("href", "/properties");

    const guide = screen.getByRole("link", { name: /User guide/ });
    expect(guide).toHaveAttribute("href", "/user-manual/index.html");
    expect(guide).toHaveAttribute("target", "_blank");
    expect(guide).toHaveAttribute("rel", "noopener noreferrer");

    expect(screen.getByText("Version 1.2.3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign out" })).toBeInTheDocument();
  });

  it("closes on Escape", () => {
    renderMenu();
    openMenu();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(panel()).toBeNull();
  });

  it("closes on outside click", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(document.body);
    expect(panel()).toBeNull();
  });

  it("stays open when clicking inside the panel", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(screen.getByText("Version 1.2.3"));
    expect(panel()).not.toBeNull();
  });

  it("closes after choosing a navigation item", () => {
    renderMenu();
    openMenu();
    fireEvent.click(screen.getByRole("link", { name: "My properties" }));
    expect(panel()).toBeNull();
  });

  it("shows Connected when healthy", () => {
    renderMenu({ healthy: true });
    openMenu();
    expect(screen.getByText(/API: Connected/)).toBeInTheDocument();
  });

  it("shows Unreachable and unknown version on degraded backend", () => {
    renderMenu({ healthy: false, version: null });
    openMenu();
    expect(screen.getByText(/API: Unreachable/)).toBeInTheDocument();
    expect(screen.getByText("Version unknown")).toBeInTheDocument();
  });

  it("wires the sign-out form to the provided action", () => {
    const { signOutAction } = renderMenu();
    openMenu();
    const button = screen.getByRole("button", { name: "Sign out" });
    expect(button.closest("form")).not.toBeNull();
    fireEvent.submit(button.closest("form")!);
    expect(signOutAction).toHaveBeenCalledTimes(1);
  });
});
