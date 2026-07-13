import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AssetList } from "@/components/asset-list";
import type { AssetDto } from "@/lib/api-client";

jest.mock("@/app/assets/actions", () => ({
  createAsset: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn() }),
}));

import { createAsset } from "@/app/assets/actions";

function asset(overrides: Partial<AssetDto> = {}): AssetDto {
  return {
    id: "asset-1",
    propertyId: "prop-1",
    name: "Boiler",
    category: null,
    notes: null,
    isObsolete: false,
    ...overrides,
  };
}

describe("AssetList", () => {
  beforeEach(() => (createAsset as jest.Mock).mockReset());

  it("shows the empty state when there are no assets", () => {
    render(<AssetList propertyId="prop-1" assets={[]} />);
    expect(screen.getByText("No assets yet.")).toBeInTheDocument();
  });

  it("renders asset cards linking to the detail page, with category", () => {
    render(
      <AssetList
        propertyId="prop-1"
        assets={[asset({ id: "a1", name: "Boiler", category: "Heating" })]}
      />,
    );
    const link = screen.getByRole("link", { name: /Boiler/ });
    expect(link).toHaveAttribute("href", "/assets/a1");
    expect(screen.getByText("Heating")).toBeInTheDocument();
    expect(screen.queryByText("Obsolete")).not.toBeInTheDocument();
  });

  it("shows the Obsolete badge on obsolete assets", () => {
    render(
      <AssetList
        propertyId="prop-1"
        assets={[asset({ name: "Old fridge", isObsolete: true })]}
      />,
    );
    expect(screen.getByText("Obsolete")).toBeInTheDocument();
  });

  it("submits propertyId, name and category via the create form", async () => {
    (createAsset as jest.Mock).mockResolvedValueOnce({ ok: true, value: { id: "a1" } });

    render(<AssetList propertyId="prop-1" assets={[]} />);

    fireEvent.change(screen.getByLabelText(/^Name$/i), {
      target: { value: "Boiler" },
    });
    fireEvent.change(screen.getByLabelText(/Category/i), {
      target: { value: "Heating" },
    });
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));

    await waitFor(() => expect(createAsset).toHaveBeenCalledTimes(1));
    const fd = (createAsset as jest.Mock).mock.calls[0][0] as FormData;
    expect(fd.get("propertyId")).toBe("prop-1");
    expect(fd.get("name")).toBe("Boiler");
    expect(fd.get("category")).toBe("Heating");
  });

  it("renders the action's error message", async () => {
    (createAsset as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Name must be 200 characters or fewer",
    });

    render(<AssetList propertyId="prop-1" assets={[]} />);
    fireEvent.change(screen.getByLabelText(/^Name$/i), { target: { value: "x" } });
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));

    await waitFor(() =>
      expect(
        screen.getByText("Name must be 200 characters or fewer"),
      ).toBeInTheDocument(),
    );
  });
});
