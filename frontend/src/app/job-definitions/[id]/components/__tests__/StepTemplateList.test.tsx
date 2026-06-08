import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StepTemplateList } from "@/app/job-definitions/[id]/components/StepTemplateList";
import type { StepTemplateDto } from "@/lib/api-client";

jest.mock("@/app/job-definitions/actions", () => ({
  updateJobDefinition: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn(), push: jest.fn() }),
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
  beforeEach(() => (updateJobDefinition as jest.Mock).mockReset());

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
