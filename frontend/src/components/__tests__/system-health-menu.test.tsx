import { render, screen, fireEvent } from "@testing-library/react";
import { SystemHealthMenu } from "@/components/system-health-menu";

function renderMenu(overrides: Partial<Parameters<typeof SystemHealthMenu>[0]> = {}) {
  const props = {
    healthy: true,
    apiInfo: { service: "HomeMaintenance API", version: "1.2.3", status: "Healthy" },
    ...overrides,
  };
  render(<SystemHealthMenu {...props} />);
}

function trigger() {
  return screen.getByRole("button", { name: /System Health/ });
}

function panel() {
  return document.getElementById("system-health-menu-panel");
}

function openMenu() {
  fireEvent.click(trigger());
}

describe("SystemHealthMenu", () => {
  it("panelClosedInitially", () => {
    renderMenu();
    expect(trigger()).toHaveAttribute("aria-expanded", "false");
    expect(panel()).toBeNull();
  });

  it("opensOnTriggerClick", () => {
    renderMenu();
    openMenu();
    expect(panel()).not.toBeNull();
    expect(trigger()).toHaveAttribute("aria-expanded", "true");
  });

  it("healthy_ShowsConnectedAndServiceInfo", () => {
    renderMenu({ healthy: true });
    openMenu();
    expect(screen.getByText("Connected")).toBeInTheDocument();
    expect(screen.getByText("HomeMaintenance API")).toBeInTheDocument();
    expect(screen.getByText("1.2.3")).toBeInTheDocument();
  });

  it("unhealthy_ShowsUnreachableAndTroubleshootingHint", () => {
    renderMenu({ healthy: false, apiInfo: null });
    openMenu();
    expect(screen.getByText("Unreachable")).toBeInTheDocument();
    expect(screen.getByText(/docker compose up/)).toBeInTheDocument();
  });

  it("healthy_OmitsTroubleshootingHint", () => {
    renderMenu({ healthy: true });
    openMenu();
    expect(screen.queryByText(/docker compose up/)).not.toBeInTheDocument();
  });

  it("noApiInfo_OmitsServiceDetails", () => {
    renderMenu({ apiInfo: null });
    openMenu();
    expect(screen.queryByText("HomeMaintenance API")).not.toBeInTheDocument();
  });

  it("closesOnEscape", () => {
    renderMenu();
    openMenu();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(panel()).toBeNull();
  });

  it("closesOnOutsideClick", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(document.body);
    expect(panel()).toBeNull();
  });

  it("stays open when clicking inside the panel", () => {
    renderMenu();
    openMenu();
    fireEvent.mouseDown(screen.getByText("Connected"));
    expect(panel()).not.toBeNull();
  });
});
