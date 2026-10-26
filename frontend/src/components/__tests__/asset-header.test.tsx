import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AssetHeader } from "@/components/asset-header";
import type { AssetDto } from "@/lib/api-client";

jest.mock("@/app/assets/actions", () => ({
  updateAsset: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn() }),
}));

import { updateAsset } from "@/app/assets/actions";

function asset(overrides: Partial<AssetDto> = {}): AssetDto {
  return {
    id: "asset-1",
    propertyId: "prop-1",
    name: "Boiler",
    category: "Heating",
    notes: null,
    isObsolete: false,
    ...overrides,
  };
}

describe("AssetHeader", () => {
  beforeEach(() => (updateAsset as jest.Mock).mockReset());

  it("renders name, category and no badge for an active asset", () => {
    render(<AssetHeader asset={asset()} />);
    expect(screen.getByRole("button", { name: "Edit asset name" })).toHaveTextContent("Boiler");
    expect(screen.getByRole("button", { name: "Edit asset category" })).toHaveTextContent("Heating");
    expect(screen.queryByText("Obsolete")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Mark obsolete" })).toBeInTheDocument();
  });

  it("marks an active asset obsolete via updateAsset", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: asset({ isObsolete: true }),
    });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Mark obsolete" }));

    await waitFor(() => expect(updateAsset).toHaveBeenCalledTimes(1));
    expect(updateAsset).toHaveBeenCalledWith("asset-1", { isObsolete: true });
  });

  it("shows the badge and Reactivate for an obsolete asset, and reactivates", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: asset(),
    });

    render(<AssetHeader asset={asset({ isObsolete: true })} />);
    expect(screen.getByText("Obsolete")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    await waitFor(() =>
      expect(updateAsset).toHaveBeenCalledWith("asset-1", { isObsolete: false }),
    );
  });

  it("surfaces the toggle error inline", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Something went wrong",
    });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Mark obsolete" }));

    await waitFor(() =>
      expect(screen.getByText("Something went wrong")).toBeInTheDocument(),
    );
  });

  it("renames via the inline editor", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: asset({ name: "New boiler" }),
    });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset name" }));
    const input = screen.getByRole("textbox", { name: "Edit asset name" });
    fireEvent.change(input, { target: { value: "New boiler" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() =>
      expect(updateAsset).toHaveBeenCalledWith("asset-1", { name: "New boiler" }),
    );
  });

  it("rename failure surfaces the error without refreshing", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Name too long" });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset name" }));
    const input = screen.getByRole("textbox", { name: "Edit asset name" });
    fireEvent.change(input, { target: { value: "x" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(await screen.findByText("Name too long")).toBeInTheDocument();
  });

  it("edits the category via the inline editor", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: asset({ category: "Plumbing" }),
    });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset category" }));
    const input = screen.getByRole("textbox", { name: "Edit asset category" });
    fireEvent.change(input, { target: { value: "Plumbing" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() =>
      expect(updateAsset).toHaveBeenCalledWith("asset-1", { category: "Plumbing" }),
    );
  });

  it("category edit failure surfaces the error", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Category rejected" });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset category" }));
    const input = screen.getByRole("textbox", { name: "Edit asset category" });
    fireEvent.change(input, { target: { value: "Bad" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(await screen.findByText("Category rejected")).toBeInTheDocument();
  });

  it("edits notes via the inline editor", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: asset({ notes: "Serviced yearly" }),
    });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset notes" }));
    const input = screen.getByRole("textbox", { name: "Edit asset notes" });
    fireEvent.change(input, { target: { value: "Serviced yearly" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() =>
      expect(updateAsset).toHaveBeenCalledWith("asset-1", { notes: "Serviced yearly" }),
    );
  });

  it("notes edit failure surfaces the error", async () => {
    (updateAsset as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Notes rejected" });

    render(<AssetHeader asset={asset()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit asset notes" }));
    const input = screen.getByRole("textbox", { name: "Edit asset notes" });
    fireEvent.change(input, { target: { value: "Bad" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(await screen.findByText("Notes rejected")).toBeInTheDocument();
  });
});
