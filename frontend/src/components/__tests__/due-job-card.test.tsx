import { render, screen } from "@testing-library/react";
import { DueJobCard } from "@/components/due-job-card";
import type { DueJobItem } from "@/lib/due-jobs";

function item(overrides: Partial<DueJobItem>): DueJobItem {
  return {
    job: {
      id: "job-1",
      propertyId: "prop-1",
      name: "Service boiler",
      dueDate: "2026-11-01",
      status: "Active",
      completedAt: null,
      stepCount: 2,
      completedStepCount: 0,
      jobDefinitionId: null,
      assetId: null,
    },
    propertyName: "Lakeview Cottage",
    daysUntilDue: 0,
    dueLabel: "Due today",
    bucket: "dueThisWeek",
    ...overrides,
  };
}

describe("DueJobCard", () => {
  it("rendersJobNamePropertyNameAndDueLabel", () => {
    render(<DueJobCard item={item({})} textClass="text-amber-600" />);
    expect(screen.getByText("Service boiler")).toBeInTheDocument();
    expect(screen.getByText("Lakeview Cottage")).toBeInTheDocument();
    expect(screen.getByText("Due today")).toBeInTheDocument();
  });

  it("linksToTheJobDetailPage", () => {
    render(
      <DueJobCard
        item={item({ job: { ...item({}).job, id: "job-42" } })}
        textClass="text-amber-600"
      />,
    );
    expect(screen.getByRole("link")).toHaveAttribute("href", "/jobs/job-42");
  });

  it("dueLabel_UsesTheGivenTextClass", () => {
    render(<DueJobCard item={item({ dueLabel: "Overdue by 1 day" })} textClass="text-rose-500" />);
    expect(screen.getByText("Overdue by 1 day")).toHaveClass("text-rose-500");
  });
});
