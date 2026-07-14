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

function openMenu() {
  fireEvent.click(screen.getByRole("button", { name: "System menu" }));
}

describe("SystemMenu", () => {
  it("shows the identity on the trigger, menu closed initially", () => {
    renderMenu();
    const trigger = screen.getByRole("button", { name: "System menu" });
    expect(trigger).toHaveTextContent("alice@example.com");
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("opens on trigger click with all items", () => {
    renderMenu();
    openMenu();

    expect(screen.getByRole("menu")).toBeInTheDocument();
    const properties = screen.getByRole("menuitem", { name: "My properties" });
    expect(properties).toHaveAttribute("href", "/properties");

    const guide = screen.getByRole("menuitem", { name: /User guide/ });
    expect(guide).toHaveAttribute("href", "/user-manual/index.html");
    expect(guide).toHaveAttribute("target", "_blank");
    expect(guide).toHaveAttribute("rel", "noopener noreferrer");

    expect(screen.getByText("Version 1.2.3")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
  });

  it("closes on Escape", () => {
    renderMenu();
    openMenu();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("closes on outside click", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("stays open when clicking inside the panel", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(screen.getByText("Version 1.2.3"));
    expect(screen.getByRole("menu")).toBeInTheDocument();
  });

  it("closes after choosing a navigation item", () => {
    renderMenu();
    openMenu();
    fireEvent.click(screen.getByRole("menuitem", { name: "My properties" }));
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("shows Connected when healthy and Unreachable otherwise", () => {
    renderMenu({ healthy: true });
    openMenu();
    expect(screen.getByText(/API: Connected/)).toBeInTheDocument();

    fireEvent.keyDown(document, { key: "Escape" });
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
    const button = screen.getByRole("menuitem", { name: "Sign out" });
    expect(button.closest("form")).not.toBeNull();
    fireEvent.submit(button.closest("form")!);
    expect(signOutAction).toHaveBeenCalledTimes(1);
  });
});
