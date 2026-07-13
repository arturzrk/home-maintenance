import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CreateJobDefinitionForm } from "@/components/create-job-definition-form";

jest.mock("@/app/properties/actions", () => ({
  createJobDefinition: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn() }),
}));

import { createJobDefinition } from "@/app/properties/actions";

function openForm() {
  fireEvent.click(screen.getByRole("button", { name: /add recurring job/i }));
}

describe("CreateJobDefinitionForm", () => {
  beforeEach(() => (createJobDefinition as jest.Mock).mockReset());

  it("renders_AllFormFields", () => {
    render(<CreateJobDefinitionForm propertyId="prop-1" />);
    openForm();
    expect(screen.getByLabelText(/^Name$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Unit$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Every$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Start date/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/End date/i)).toBeInTheDocument();
  });

  it("submit_CallsApiClientWithCorrectPayload", async () => {
    (createJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", propertyId: "prop-1", name: "Service boiler", schedule: {}, stepTemplates: [] },
    });

    render(<CreateJobDefinitionForm propertyId="prop-1" />);
    openForm();

    await userEvent.clear(screen.getByLabelText(/^Name$/i));
    await userEvent.type(screen.getByLabelText(/^Name$/i), "Service boiler");
    await userEvent.clear(screen.getByLabelText(/^Every$/i));
    await userEvent.type(screen.getByLabelText(/^Every$/i), "3");
    await userEvent.selectOptions(screen.getByLabelText(/^Unit$/i), "Month");
    fireEvent.change(screen.getByLabelText(/Start date/i), { target: { value: "2026-06-01" } });

    fireEvent.click(screen.getByRole("button", { name: /save recurring job/i }));

    await waitFor(() => expect(createJobDefinition).toHaveBeenCalledTimes(1));
    const [propId, body] = (createJobDefinition as jest.Mock).mock.calls[0];
    expect(propId).toBe("prop-1");
    expect(body.name).toBe("Service boiler");
    expect(body.schedule.unit).toBe("Month");
    expect(body.schedule.startDate).toBe("2026-06-01");
  });

  it("submit_EmptyName_ShowsValidationError", async () => {
    render(<CreateJobDefinitionForm propertyId="prop-1" />);
    openForm();

    fireEvent.change(screen.getByLabelText(/Start date/i), { target: { value: "2026-06-01" } });
    fireEvent.click(screen.getByRole("button", { name: /save recurring job/i }));

    expect(screen.getByText(/Name is required/i)).toBeInTheDocument();
    expect(createJobDefinition).not.toHaveBeenCalled();
  });

  it("assetDropdown_OmittedWhenNoAssets", () => {
    render(<CreateJobDefinitionForm propertyId="prop-1" />);
    openForm();
    expect(screen.queryByLabelText(/Asset \(optional\)/i)).not.toBeInTheDocument();
  });

  it("assetDropdown_SelectedAssetIdIncludedInPayload", async () => {
    (createJobDefinition as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "def-1", propertyId: "prop-1", name: "Service boiler", schedule: {}, stepTemplates: [] },
    });

    render(
      <CreateJobDefinitionForm
        propertyId="prop-1"
        assets={[
          { id: "a1", propertyId: "prop-1", name: "Boiler", category: null, notes: null, isObsolete: false },
        ]}
      />,
    );
    openForm();

    await userEvent.type(screen.getByLabelText(/^Name$/i), "Service boiler");
    fireEvent.change(screen.getByLabelText(/Start date/i), { target: { value: "2026-06-01" } });
    await userEvent.selectOptions(screen.getByLabelText(/Asset \(optional\)/i), "a1");

    fireEvent.click(screen.getByRole("button", { name: /save recurring job/i }));

    await waitFor(() => expect(createJobDefinition).toHaveBeenCalledTimes(1));
    const [, body] = (createJobDefinition as jest.Mock).mock.calls[0];
    expect(body.assetId).toBe("a1");
  });

  it("addStep_AppendsStepRow", async () => {
    render(<CreateJobDefinitionForm propertyId="prop-1" />);
    openForm();

    expect(screen.queryByLabelText(/Step 1/i)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /\+ add step/i }));
    expect(screen.getByLabelText(/Step 1/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /\+ add step/i }));
    expect(screen.getByLabelText(/Step 2/i)).toBeInTheDocument();
  });
});
