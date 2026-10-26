import { render, screen } from "@testing-library/react";
import { JobCard } from "@/components/job-card";
import type { JobSummary } from "@/lib/api-client";

const base: JobSummary = {
  id: "job-1",
  propertyId: "prop-1",
  name: "Service boiler",
  dueDate: null,
  status: "Active",
  completedAt: null,
  stepCount: 2,
  completedStepCount: 0,
  jobDefinitionId: null,
};

describe("JobCard", () => {
  it("badge_Present_WhenJobDefinitionIdSet", () => {
    render(<JobCard job={{ ...base, jobDefinitionId: "def-abc" }} />);
    expect(screen.getByText("Recurring")).toBeInTheDocument();
  });

  it("badge_Absent_WhenJobDefinitionIdNull", () => {
    render(<JobCard job={base} />);
    expect(screen.queryByText("Recurring")).not.toBeInTheDocument();
  });

  it("statusBadge_Completed_RendersGreenStyling", () => {
    render(<JobCard job={{ ...base, status: "Completed" }} />);
    expect(screen.getByText("Completed")).toHaveClass("bg-green-100");
  });

  it("statusBadge_Active_RendersBlueStyling", () => {
    render(<JobCard job={{ ...base, status: "Active" }} />);
    expect(screen.getByText("Active")).toHaveClass("bg-blue-100");
  });

  it("dueDate_Present_RendersDueText", () => {
    render(<JobCard job={{ ...base, dueDate: "2026-07-01" }} />);
    expect(screen.getByText(/due 2026-07-01/i)).toBeInTheDocument();
  });

  it("dueDate_Null_OmitsDueText", () => {
    render(<JobCard job={base} />);
    expect(screen.queryByText(/due/i)).not.toBeInTheDocument();
  });
});
