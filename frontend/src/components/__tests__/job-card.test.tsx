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
});
