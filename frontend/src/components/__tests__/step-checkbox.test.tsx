import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { StepCheckbox } from "@/components/step-checkbox";

jest.mock("@/app/jobs/actions", () => ({
  tickStep: jest.fn(),
  untickStep: jest.fn(),
}));

import { tickStep, untickStep } from "@/app/jobs/actions";

function step(overrides: Partial<{ id: string; isCompleted: boolean; description: string }> = {}) {
  return {
    id: overrides.id ?? "step-1",
    order: 0,
    description: overrides.description ?? "Shut off gas",
    isCompleted: overrides.isCompleted ?? false,
    completedAt: null,
  };
}

describe("StepCheckbox", () => {
  beforeEach(() => {
    (tickStep as jest.Mock).mockReset();
    (untickStep as jest.Mock).mockReset();
  });

  it("ticks optimistically and calls the action", async () => {
    (tickStep as jest.Mock).mockResolvedValueOnce({ ok: true, value: {} });

    render(<StepCheckbox jobId="job-1" step={step()} jobLocked={false} />);
    const box = screen.getByRole("checkbox") as HTMLInputElement;

    fireEvent.click(box);

    // Optimistic: checkbox is checked immediately, before action resolves.
    expect(box.checked).toBe(true);
    await waitFor(() =>
      expect(tickStep).toHaveBeenCalledWith("job-1", "step-1"),
    );
  });

  it("rolls back when the action fails", async () => {
    (tickStep as jest.Mock).mockResolvedValueOnce({
      ok: false,
      error: "boom",
    });

    render(<StepCheckbox jobId="job-1" step={step()} jobLocked={false} />);
    const box = screen.getByRole("checkbox") as HTMLInputElement;

    fireEvent.click(box);
    await waitFor(() => expect(box.checked).toBe(false));
    expect(screen.getByText("boom")).toBeInTheDocument();
  });

  it("calls untick when the step starts as completed", async () => {
    (untickStep as jest.Mock).mockResolvedValueOnce({ ok: true, value: {} });

    render(
      <StepCheckbox
        jobId="job-1"
        step={step({ isCompleted: true })}
        jobLocked={false}
      />,
    );
    fireEvent.click(screen.getByRole("checkbox"));
    await waitFor(() =>
      expect(untickStep).toHaveBeenCalledWith("job-1", "step-1"),
    );
  });

  it("disables interaction when the job is locked", () => {
    render(
      <StepCheckbox jobId="job-1" step={step()} jobLocked={true} />,
    );
    const box = screen.getByRole("checkbox") as HTMLInputElement;
    expect(box.disabled).toBe(true);
  });
});
