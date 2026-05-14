import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CreatePropertyForm } from "@/components/create-property-form";

jest.mock("@/app/properties/actions", () => ({
  createProperty: jest.fn(),
}));

import { createProperty } from "@/app/properties/actions";

describe("CreatePropertyForm", () => {
  beforeEach(() => {
    (createProperty as jest.Mock).mockReset();
  });

  it("submits the typed name to the Server Action", async () => {
    (createProperty as jest.Mock).mockResolvedValueOnce({ ok: true });

    render(<CreatePropertyForm />);

    const input = screen.getByPlaceholderText("Property name") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Main House" } });
    fireEvent.click(screen.getByRole("button", { name: /create/i }));

    await waitFor(() => {
      expect(createProperty).toHaveBeenCalledTimes(1);
    });
    const formData = (createProperty as jest.Mock).mock.calls[0][0] as FormData;
    expect(formData.get("name")).toBe("Main House");
  });

  it("renders the error returned by the Server Action", async () => {
    (createProperty as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Name must be 100 characters or fewer",
    });

    render(<CreatePropertyForm />);

    const input = screen.getByPlaceholderText("Property name") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "x" } });
    fireEvent.click(screen.getByRole("button", { name: /create/i }));

    await waitFor(() => {
      expect(
        screen.getByText("Name must be 100 characters or fewer"),
      ).toBeInTheDocument();
    });
  });

  it("clears the input after a successful create", async () => {
    (createProperty as jest.Mock).mockResolvedValueOnce({ ok: true });

    render(<CreatePropertyForm />);

    const input = screen.getByPlaceholderText("Property name") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Main House" } });
    fireEvent.click(screen.getByRole("button", { name: /create/i }));

    await waitFor(() => {
      expect(input.value).toBe("");
    });
  });
});
