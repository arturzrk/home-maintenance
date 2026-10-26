import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StepTemplateList } from "@/app/job-definitions/[id]/components/StepTemplateList";
import type { StepTemplateDto } from "@/lib/api-client";

jest.mock("@/app/job-definitions/actions", () => ({
  updateJobDefinition: jest.fn(),
}));

const mockRefresh = jest.fn();
jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: mockRefresh, push: jest.fn() }),
}));

import { updateJobDefinition } from "@/app/job-definitions/actions";

function template(id: string, description: string, order: number): StepTemplateDto {
  return { id, order, description };
}

const templates = [
  template("t1", "Shut off water", 0),
  template("t2", "Drain system", 1),
  template("t3", "Replace filter", 2),
];

describe("StepTemplateList", () => {
  beforeEach(() => {
    (updateJobDefinition as jest.Mock).mockReset();
    mockRefresh.mockReset();
  });

  it("emptyList_RendersPlaceholderText", () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={[]} />);
    expect(screen.getByText("No step templates yet.")).toBeInTheDocument();
  });

  it("addStep_EmptyDescription_ShowsValidationError_AndDoesNotCallApi", () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    const submit = screen.getByRole("button", { name: /^add$/i });
    expect(submit).toBeDisabled();
    expect(updateJobDefinition).not.toHaveBeenCalled();
  });

  it("addStep_TooLongDescription_ShowsValidationError", async () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    const input = screen.getByPlaceholderText(/add a step template/i);
    fireEvent.change(input, { target: { value: "x".repeat(501) } });

    const form = input.closest("form")!;
    fireEvent.submit(form);

    expect(await screen.findByText("Description must be 500 characters or fewer")).toBeInTheDocument();
    expect(updateJobDefinition).not.toHaveBeenCalled();
  });

  it("addStep_ApiFailure_ShowsServerError", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Server rejected step" });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    const input = screen.getByPlaceholderText(/add a step template/i);
    await userEvent.type(input, "Check pressure");
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    expect(await screen.findByText("Server rejected step")).toBeInTheDocument();
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("removeStep_ApiFailure_DoesNotRefresh", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Cannot remove" });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(
      screen.getByRole("button", { name: /Remove step template "Shut off water"/i }),
    );

    await waitFor(() => expect(updateJobDefinition).toHaveBeenCalled());
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("editStep_ApiFailure_ShowsError", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Edit failed" });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit description for step 1/i }));
    const input = screen.getByRole("textbox", { name: /Edit description for step 1/i });
    await userEvent.clear(input);
    await userEvent.type(input, "Updated");
    fireEvent.blur(input);

    expect(await screen.findByText("Edit failed")).toBeInTheDocument();
  });

  it("moveUp_FirstTemplate_ButtonDisabled", () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    expect(screen.getByRole("button", { name: /Move "Shut off water" up/i })).toBeDisabled();
  });

  it("moveDown_LastTemplate_ButtonDisabled", () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    expect(screen.getByRole("button", { name: /Move "Replace filter" down/i })).toBeDisabled();
  });

  it("moveDown_MiddleTemplate_CallsApiWithSwappedOrder", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", stepTemplates: templates },
    });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(screen.getByRole("button", { name: /Move "Drain system" down/i }));

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", {
        reorderStepTemplateIds: ["t1", "t3", "t2"],
      }),
    );
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("moveUp_ApiFailure_ShowsReorderError", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Reorder rejected" });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(screen.getByRole("button", { name: /Move "Drain system" up/i }));

    expect(await screen.findByText("Reorder rejected")).toBeInTheDocument();
  });

  it("renders_StepTemplateDescriptions", () => {
    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);
    expect(screen.getByText("Shut off water")).toBeInTheDocument();
    expect(screen.getByText("Drain system")).toBeInTheDocument();
    expect(screen.getByText("Replace filter")).toBeInTheDocument();
  });

  it("addStep_CallsApiClient_WithDescription", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", stepTemplates: templates },
    });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    const input = screen.getByPlaceholderText(/add a step template/i);
    await userEvent.type(input, "Check pressure");
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", {
        addStepTemplates: [{ description: "Check pressure" }],
      }),
    );
  });

  it("removeStep_CallsApiClient_WithId", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", stepTemplates: templates.slice(1) },
    });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(
      screen.getByRole("button", { name: /Remove step template "Shut off water"/i }),
    );

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", {
        removeStepTemplateIds: ["t1"],
      }),
    );
  });

  it("editStep_CallsApiClient_OnBlur", async () => {
    (updateJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", stepTemplates: templates },
    });

    render(<StepTemplateList definitionId="def-1" stepTemplates={templates} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit description for step 1/i }));

    const input = screen.getByRole("textbox", { name: /Edit description for step 1/i });
    await userEvent.clear(input);
    await userEvent.type(input, "Updated description");
    fireEvent.blur(input);

    await waitFor(() =>
      expect(updateJobDefinition).toHaveBeenCalledWith("def-1", {
        editStepTemplates: [{ id: "t1", description: "Updated description" }],
      }),
    );
  });
});
