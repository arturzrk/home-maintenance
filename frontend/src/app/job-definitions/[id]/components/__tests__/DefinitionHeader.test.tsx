import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DefinitionHeader } from "@/app/job-definitions/[id]/components/DefinitionHeader";
import type { JobDefinitionDto } from "@/lib/api-client";

jest.mock("@/app/job-definitions/actions", () => ({
  updateJobDefinition: jest.fn(),
}));

const mockRefresh = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: mockRefresh, push: jest.fn() }),
}));

import { updateJobDefinition } from "@/app/job-definitions/actions";

function definition(overrides: Partial<JobDefinitionDto> = {}): JobDefinitionDto {
  return {
    id: "def-1",
    propertyId: "prop-1",
    name: "Service boiler",
    schedule: { unit: "Month", multiplier: 3, startDate: "2026-01-01", endDate: null },
    stepTemplates: [],
    ...overrides,
  };
}

describe("DefinitionHeader", () => {
  beforeEach(() => {
    (updateJobDefinition as jest.Mock).mockReset();
    mockRefresh.mockReset();
  });

  it("nameEdit_CallsApiClientOnEnter", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: true, value: definition() });

    render(<DefinitionHeader definition={definition()} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit definition name/i }));
    const input = screen.getByRole("textbox", { name: /Edit definition name/i });
    await userEvent.clear(input);
    await userEvent.type(input, "Updated name");
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", { name: "Updated name" }),
    );
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("nameEdit_ApiFailure_DoesNotRefresh", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Name too long" });

    render(<DefinitionHeader definition={definition()} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit definition name/i }));
    const input = screen.getByRole("textbox", { name: /Edit definition name/i });
    await userEvent.clear(input);
    await userEvent.type(input, "Bad");
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() => expect(updateJobDefinition).toHaveBeenCalled());
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("schedule_OpensEditorOnButtonClick", () => {
    render(<DefinitionHeader definition={definition()} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    expect(screen.getByRole("heading", { name: /Edit schedule/i })).toBeInTheDocument();
  });

  it("schedule_MultiplierLessThanOne_ShowsValidationError", async () => {
    render(<DefinitionHeader definition={definition()} />);
    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    fireEvent.change(screen.getByLabelText("Every"), { target: { value: "0" } });
    fireEvent.click(screen.getByRole("button", { name: /Save/i }));

    expect(await screen.findByText("Multiplier must be at least 1")).toBeInTheDocument();
    expect(updateJobDefinition).not.toHaveBeenCalled();
  });

  it("schedule_MissingStartDate_ShowsValidationError", async () => {
    render(<DefinitionHeader definition={definition()} />);
    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    fireEvent.change(screen.getByLabelText("Start date"), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: /Save/i }));

    expect(await screen.findByText("Start date is required")).toBeInTheDocument();
    expect(updateJobDefinition).not.toHaveBeenCalled();
  });

  it("schedule_ValidSave_CallsApiAndCloses", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: true, value: definition() });
    render(<DefinitionHeader definition={definition()} />);
    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    fireEvent.change(screen.getByLabelText("Every"), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: /Save/i }));

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", {
        schedule: { unit: "Month", multiplier: 2, startDate: "2026-01-01", endDate: null },
      }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: /Edit schedule/i })).not.toBeInTheDocument(),
    );
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("schedule_SaveFailure_ShowsServerError", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Server rejected schedule" });
    render(<DefinitionHeader definition={definition()} />);
    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    fireEvent.click(screen.getByRole("button", { name: /Save/i }));

    expect(await screen.findByText("Server rejected schedule")).toBeInTheDocument();
  });

  it("schedule_Cancel_ResetsFieldsAndCloses", () => {
    render(<DefinitionHeader definition={definition()} />);
    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));

    fireEvent.change(screen.getByLabelText("Every"), { target: { value: "9" } });
    fireEvent.click(screen.getByRole("button", { name: /Cancel/i }));

    expect(screen.queryByRole("heading", { name: /Edit schedule/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Edit schedule/i }));
    expect(screen.getByLabelText("Every")).toHaveValue(3);
  });

  it("scheduleLabel_PluralMultiplier_RendersPluralUnit", () => {
    render(<DefinitionHeader definition={definition({
      schedule: { unit: "Week", multiplier: 2, startDate: "2026-03-01", endDate: null },
    })} />);

    expect(screen.getByText(/Every 2 weeks from/i)).toBeInTheDocument();
  });

  it("scheduleLabel_SingularMultiplier_RendersSingularUnit", () => {
    render(<DefinitionHeader definition={definition({
      schedule: { unit: "Month", multiplier: 1, startDate: "2026-03-01", endDate: null },
    })} />);

    expect(screen.getByText(/Every month from/i)).toBeInTheDocument();
  });
});
