import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { GenerateNextButton } from "./GenerateNextButton";

jest.mock("@/app/job-definitions/actions", () => ({
  generateNextOccurrence: jest.fn(),
}));

const mockPush = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, refresh: jest.fn() }),
}));

import { generateNextOccurrence } from "@/app/job-definitions/actions";

describe("GenerateNextButton", () => {
  beforeEach(() => {
    (generateNextOccurrence as jest.Mock).mockReset();
    mockPush.mockReset();
  });

  it("success_NavigatesToNewJob", async () => {
    (generateNextOccurrence as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "job-1", propertyId: "prop-1", name: "Service boiler", status: "Active", steps: [], jobDefinitionId: "def-1" },
    });

    render(<GenerateNextButton definitionId="def-1" />);
    fireEvent.click(screen.getByRole("button", { name: /generate next/i }));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/jobs/job-1"));
    expect(screen.queryByText(/already scheduled/i)).toBeNull();
  });

  it("duplicateError_ShowsInlineError", async () => {
    (generateNextOccurrence as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Next occurrence already exists",
      code: "next_occurrence_already_exists",
    });

    render(<GenerateNextButton definitionId="def-1" />);
    fireEvent.click(screen.getByRole("button", { name: /generate next/i }));

    await waitFor(() =>
      expect(screen.getByText("The next occurrence is already scheduled.")).toBeInTheDocument(),
    );
    expect(mockPush).not.toHaveBeenCalled();
  });

  it("loadingState_DisablesButton", async () => {
    let resolve: (v: unknown) => void;
    (generateNextOccurrence as jest.Mock).mockImplementationOnce(
      () => new Promise((r) => { resolve = r; }),
    );

    render(<GenerateNextButton definitionId="def-1" />);
    const btn = screen.getByRole("button", { name: /generate next/i }) as HTMLButtonElement;
    fireEvent.click(btn);

    await waitFor(() => expect(btn.disabled).toBe(true));
    resolve!({ ok: true, value: { id: "job-x" } });
  });
});
