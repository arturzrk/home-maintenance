import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { JobChecklist } from "@/components/job-checklist";
import type { StepDto } from "@/lib/api-client";

jest.mock("@/app/jobs/actions", () => ({
  addStep: jest.fn(),
  removeStep: jest.fn(),
  reorderSteps: jest.fn(),
  editStepDescription: jest.fn(),
  tickStep: jest.fn(),
  untickStep: jest.fn(),
}));

import {
  addStep,
  removeStep,
  reorderSteps,
} from "@/app/jobs/actions";

function step(id: string, description: string, order: number): StepDto {
  return { id, order, description, isCompleted: false, completedAt: null };
}

const initialSteps = [
  step("s1", "Shut off gas", 0),
  step("s2", "Drain system", 1),
  step("s3", "Replace filter", 2),
];

describe("JobChecklist", () => {
  beforeEach(() => {
    (addStep as jest.Mock).mockReset();
    (removeStep as jest.Mock).mockReset();
    (reorderSteps as jest.Mock).mockReset();
  });

  it("renders each step in order", () => {
    render(<JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={false} />);
    expect(screen.getByText("Shut off gas")).toBeInTheDocument();
    expect(screen.getByText("Drain system")).toBeInTheDocument();
    expect(screen.getByText("Replace filter")).toBeInTheDocument();
  });

  it("calls addStep with the trimmed description", async () => {
    (addStep as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "job-1", steps: [...initialSteps, step("s4", "New", 3)] },
    });

    render(<JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={false} />);

    const input = screen.getByPlaceholderText(/add a step/i) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "  New  " } });
    fireEvent.click(screen.getByRole("button", { name: /^add$/i }));

    await waitFor(() => expect(addStep).toHaveBeenCalledWith("job-1", "New"));
  });

  it("moving up calls reorderSteps with the swapped order", async () => {
    (reorderSteps as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: {
        id: "job-1",
        steps: [
          step("s2", "Drain system", 0),
          step("s1", "Shut off gas", 1),
          step("s3", "Replace filter", 2),
        ],
      },
    });

    render(<JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={false} />);

    // Find the up-arrow for step 2 ("Drain system") and click.
    fireEvent.click(
      screen.getByRole("button", { name: /move "Drain system" up/i }),
    );

    await waitFor(() =>
      expect(reorderSteps).toHaveBeenCalledWith("job-1", ["s2", "s1", "s3"]),
    );
  });

  it("calls removeStep when Remove is clicked", async () => {
    (removeStep as jest.Mock).mockResolvedValueOnce({
      ok: true,
      value: { id: "job-1", steps: initialSteps.slice(1) },
    });

    render(<JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={false} />);

    fireEvent.click(
      screen.getByRole("button", { name: /Remove step "Shut off gas"/i }),
    );
    await waitFor(() => expect(removeStep).toHaveBeenCalledWith("job-1", "s1"));
  });

  it("resyncs the rendered list when initialSteps changes", () => {
    const { rerender } = render(
      <JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={false} />,
    );
    expect(screen.getByText("Shut off gas")).toBeInTheDocument();

    // Simulate a server revalidation after a StepRow action (remove s1,
    // edit s2's description).
    rerender(
      <JobChecklist
        jobId="job-1"
        initialSteps={[
          step("s2", "Drain system fully", 0),
          step("s3", "Replace filter", 1),
        ]}
        jobLocked={false}
      />,
    );

    expect(screen.queryByText("Shut off gas")).toBeNull();
    expect(screen.getByText("Drain system fully")).toBeInTheDocument();
    expect(screen.getByText("Replace filter")).toBeInTheDocument();
  });

  it("hides the add form when job is locked", () => {
    render(
      <JobChecklist jobId="job-1" initialSteps={initialSteps} jobLocked={true} />,
    );
    expect(screen.queryByPlaceholderText(/add a step/i)).toBeNull();
  });
});
