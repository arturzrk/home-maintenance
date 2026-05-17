import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CreateJobForm } from "@/components/create-job-form";

jest.mock("@/app/jobs/actions", () => ({
  createJob: jest.fn(),
}));

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
}));

import { createJob } from "@/app/jobs/actions";

describe("CreateJobForm", () => {
  beforeEach(() => (createJob as jest.Mock).mockReset());

  it("submits propertyId, name, dueDate, and steps split by newline", async () => {
    (createJob as jest.Mock).mockResolvedValueOnce({ ok: true, value: { id: "job-1" } });

    render(<CreateJobForm propertyId="prop-1" />);

    fireEvent.change(screen.getByLabelText(/^Name$/i), {
      target: { value: "Service boiler" },
    });
    fireEvent.change(screen.getByLabelText(/Due date/i), {
      target: { value: "2026-06-01" },
    });
    fireEvent.change(screen.getByLabelText(/Steps/i), {
      target: { value: "Shut off gas\nDrain system\nReplace filter" },
    });
    fireEvent.click(screen.getByRole("button", { name: /create job/i }));

    await waitFor(() => expect(createJob).toHaveBeenCalledTimes(1));
    const fd = (createJob as jest.Mock).mock.calls[0][0] as FormData;
    expect(fd.get("propertyId")).toBe("prop-1");
    expect(fd.get("name")).toBe("Service boiler");
    expect(fd.get("dueDate")).toBe("2026-06-01");
    expect(fd.get("steps")).toBe("Shut off gas\nDrain system\nReplace filter");
  });

  it("renders the action's error message", async () => {
    (createJob as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Name must be 200 characters or fewer",
    });

    render(<CreateJobForm propertyId="prop-1" />);
    fireEvent.change(screen.getByLabelText(/^Name$/i), { target: { value: "x" } });
    fireEvent.click(screen.getByRole("button", { name: /create job/i }));

    await waitFor(() =>
      expect(
        screen.getByText("Name must be 200 characters or fewer"),
      ).toBeInTheDocument(),
    );
  });
});
