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
  });
});
