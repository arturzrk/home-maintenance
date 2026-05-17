import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { InlineEditableText } from "@/components/inline-editable-text";

describe("InlineEditableText", () => {
  it("renders the value as a button when not editing", () => {
    render(<InlineEditableText value="Original" save={jest.fn()} />);
    expect(screen.getByRole("button", { name: /edit/i })).toHaveTextContent("Original");
  });

  it("calls save on Enter and exits edit mode", async () => {
    const save = jest.fn().mockResolvedValue({ ok: true });
    render(<InlineEditableText value="Original" save={save} />);

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    const input = screen.getByRole("textbox") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Updated" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() => expect(save).toHaveBeenCalledWith("Updated"));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument(),
    );
  });

  it("does not save when value is unchanged", async () => {
    const save = jest.fn();
    render(<InlineEditableText value="Original" save={save} />);

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    fireEvent.keyDown(screen.getByRole("textbox"), { key: "Enter" });

    await waitFor(() =>
      expect(screen.getByRole("button", { name: /edit/i })).toBeInTheDocument(),
    );
    expect(save).not.toHaveBeenCalled();
  });

  it("rolls back and surfaces error on save failure", async () => {
    const save = jest.fn().mockResolvedValue({ ok: false, error: "boom" });
    render(<InlineEditableText value="Original" save={save} />);

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    const input = screen.getByRole("textbox") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Bad" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() => expect(screen.getByText("boom")).toBeInTheDocument());
  });

  it("Escape cancels and reverts to display mode", () => {
    const save = jest.fn();
    render(<InlineEditableText value="Original" save={save} />);

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    const input = screen.getByRole("textbox") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Changed" } });
    fireEvent.keyDown(input, { key: "Escape" });

    expect(save).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: /edit/i })).toHaveTextContent("Original");
  });

  it("renders disabled as static span (no edit affordance)", () => {
    render(<InlineEditableText value="Locked" disabled save={jest.fn()} />);
    expect(screen.queryByRole("button")).toBeNull();
    expect(screen.getByText("Locked")).toBeInTheDocument();
  });
});
