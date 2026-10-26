import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { StepRow } from "@/components/step-row";
import type { StepDto } from "@/lib/api-client";

jest.mock("@/app/jobs/actions", () => ({
  editStepDescription: jest.fn(),
  removeStep: jest.fn(),
  tickStep: jest.fn(),
  untickStep: jest.fn(),
}));

import { removeStep, tickStep, untickStep } from "@/app/jobs/actions";

function step(overrides: Partial<StepDto> = {}): StepDto {
  return {
    id: "s1",
    order: 0,
    description: "Shut off gas",
    isCompleted: false,
    completedAt: null,
    ...overrides,
  };
}

describe("StepRow", () => {
  const onMove = jest.fn();

  beforeEach(() => {
    (removeStep as jest.Mock).mockReset();
    (tickStep as jest.Mock).mockReset();
    (untickStep as jest.Mock).mockReset();
    onMove.mockReset();
  });

  it("toggle_UncompletedStep_CallsTickStep", async () => {
    (tickStep as jest.Mock).mockResolvedValueOnce({ ok: true });
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("checkbox"));

    await waitFor(() => expect(tickStep).toHaveBeenCalledWith("job-1", "s1"));
  });

  it("toggle_CompletedStep_CallsUntickStep", async () => {
    (untickStep as jest.Mock).mockResolvedValueOnce({ ok: true });
    render(<StepRow jobId="job-1" step={step({ isCompleted: true })} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("checkbox"));

    await waitFor(() => expect(untickStep).toHaveBeenCalledWith("job-1", "s1"));
  });

  it("toggle_ApiFailure_RevertsOptimisticStateAndShowsError", async () => {
    (tickStep as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Job is completed" });
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    const checkbox = screen.getByRole("checkbox") as HTMLInputElement;
    fireEvent.click(checkbox);

    await waitFor(() => expect(screen.getByText("Job is completed")).toBeInTheDocument());
    expect(checkbox.checked).toBe(false);
  });

  it("toggle_WhenJobLocked_DoesNothing", () => {
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={true} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("checkbox"));

    expect(tickStep).not.toHaveBeenCalled();
  });

  it("remove_Click_CallsRemoveStep", async () => {
    (removeStep as jest.Mock).mockResolvedValueOnce({ ok: true });
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("button", { name: /Remove step/i }));

    await waitFor(() => expect(removeStep).toHaveBeenCalledWith("job-1", "s1"));
  });

  it("remove_ApiFailure_ShowsError", async () => {
    (removeStep as jest.Mock).mockResolvedValueOnce({ ok: false, error: "Cannot remove last step" });
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("button", { name: /Remove step/i }));

    expect(await screen.findByText("Cannot remove last step")).toBeInTheDocument();
  });

  it("remove_WhenJobLocked_DoesNothing", () => {
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={true} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("button", { name: /Remove step/i }));

    expect(removeStep).not.toHaveBeenCalled();
  });

  it("moveButtons_FirstStep_UpDisabled_DownEnabled", () => {
    render(<StepRow jobId="job-1" step={step()} index={0} total={3} jobLocked={false} onMove={onMove} moving={false} />);

    expect(screen.getByRole("button", { name: /Move .* up/i })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Move .* down/i })).toBeEnabled();
  });

  it("moveButtons_LastStep_DownDisabled", () => {
    render(<StepRow jobId="job-1" step={step()} index={2} total={3} jobLocked={false} onMove={onMove} moving={false} />);

    expect(screen.getByRole("button", { name: /Move .* down/i })).toBeDisabled();
  });

  it("moveButtons_Click_CallsOnMoveWithDirection", () => {
    render(<StepRow jobId="job-1" step={step()} index={1} total={3} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("button", { name: /Move .* up/i }));
    expect(onMove).toHaveBeenCalledWith("up");

    fireEvent.click(screen.getByRole("button", { name: /Move .* down/i }));
    expect(onMove).toHaveBeenCalledWith("down");
  });

  it("editDescription_Save_CallsEditStepDescription", async () => {
    const { editStepDescription } = jest.requireMock("@/app/jobs/actions");
    editStepDescription.mockResolvedValueOnce({ ok: true });
    render(<StepRow jobId="job-1" step={step()} index={0} total={1} jobLocked={false} onMove={onMove} moving={false} />);

    fireEvent.click(screen.getByRole("button", { name: /Edit description/i }));
    const input = screen.getByRole("textbox", { name: /Edit description/i });
    fireEvent.change(input, { target: { value: "Updated description" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() =>
      expect(editStepDescription).toHaveBeenCalledWith("job-1", "s1", "Updated description"),
    );
  });
});
