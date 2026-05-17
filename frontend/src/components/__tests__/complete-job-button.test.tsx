import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CompleteJobButton } from "@/components/complete-job-button";
import type { JobDetail } from "@/lib/api-client";

jest.mock("@/app/jobs/actions", () => ({
  completeJob: jest.fn(),
}));

import { completeJob } from "@/app/jobs/actions";

function job(overrides: Partial<JobDetail> = {}): JobDetail {
  return {
    id: "job-1",
    propertyId: "prop-1",
    name: "Service boiler",
    dueDate: null,
    status: "Active",
    completedAt: null,
    steps: [
      { id: "s1", order: 0, description: "a", isCompleted: false, completedAt: null },
      { id: "s2", order: 1, description: "b", isCompleted: false, completedAt: null },
    ],
    ...overrides,
  };
}

describe("CompleteJobButton", () => {
  beforeEach(() => (completeJob as jest.Mock).mockReset());

  it("is disabled when any step is incomplete", () => {
    render(<CompleteJobButton job={job()} />);
    const btn = screen.getByRole("button", { name: /complete job/i }) as HTMLButtonElement;
    expect(btn.disabled).toBe(true);
  });

  it("is enabled when every step is complete", () => {
    render(
      <CompleteJobButton
        job={job({
          steps: [
            { id: "s1", order: 0, description: "a", isCompleted: true, completedAt: "2026-05-17T10:00:00Z" },
            { id: "s2", order: 1, description: "b", isCompleted: true, completedAt: "2026-05-17T10:01:00Z" },
          ],
        })}
      />,
    );
    const btn = screen.getByRole("button", { name: /complete job/i }) as HTMLButtonElement;
    expect(btn.disabled).toBe(false);
  });

  it("calls completeJob action when clicked", async () => {
    (completeJob as jest.Mock).mockResolvedValueOnce({ ok: true, value: {} });

    render(
      <CompleteJobButton
        job={job({
          steps: [
            { id: "s1", order: 0, description: "a", isCompleted: true, completedAt: "2026-05-17T10:00:00Z" },
          ],
        })}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: /complete job/i }));
    await waitFor(() => expect(completeJob).toHaveBeenCalledWith("job-1"));
  });

  it("renders the action's error", async () => {
    (completeJob as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "Not all steps are completed.",
    });

    render(
      <CompleteJobButton
        job={job({
          steps: [
            { id: "s1", order: 0, description: "a", isCompleted: true, completedAt: "2026-05-17T10:00:00Z" },
          ],
        })}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: /complete job/i }));
    await waitFor(() =>
      expect(screen.getByText("Not all steps are completed.")).toBeInTheDocument(),
    );
  });

  it("shows the completion timestamp when the job is Completed", () => {
    render(
      <CompleteJobButton
        job={job({
          status: "Completed",
          completedAt: "2026-05-17T12:34:56Z",
          steps: [
            { id: "s1", order: 0, description: "a", isCompleted: true, completedAt: "2026-05-17T10:00:00Z" },
          ],
        })}
      />,
    );
    expect(screen.queryByRole("button")).toBeNull();
    expect(screen.getByText(/Completed on/i)).toBeInTheDocument();
  });
});
