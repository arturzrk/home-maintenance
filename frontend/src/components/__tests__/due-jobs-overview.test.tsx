import { render, screen } from "@testing-library/react";
import { DueJobsOverview } from "@/components/due-jobs-overview";
import type { DueJobBuckets, DueJobItem } from "@/lib/due-jobs";

function item(id: string, bucket: DueJobItem["bucket"]): DueJobItem {
  return {
    job: {
      id,
      propertyId: "prop-1",
      name: `Job ${id}`,
      dueDate: "2026-11-01",
      status: "Active",
      completedAt: null,
      stepCount: 1,
      completedStepCount: 0,
      jobDefinitionId: null,
      assetId: null,
    },
    propertyName: "Lakeview Cottage",
    daysUntilDue: 0,
    dueLabel: "Due today",
    bucket,
  };
}

const empty: DueJobBuckets = { overdue: [], dueThisWeek: [], comingUp: [] };

describe("DueJobsOverview", () => {
  it("allBucketsEmpty_RendersCaughtUpMessage_NoSectionTitles", () => {
    render(<DueJobsOverview buckets={empty} />);
    expect(screen.getByText(/you're all caught up/i)).toBeInTheDocument();
    expect(screen.queryByText("Overdue")).not.toBeInTheDocument();
    expect(screen.queryByText("Due this week")).not.toBeInTheDocument();
    expect(screen.queryByText("Coming up this month")).not.toBeInTheDocument();
  });

  it("onlyOneBucketPopulated_RendersOnlyThatSection_NoMessage", () => {
    render(
      <DueJobsOverview
        buckets={{ ...empty, dueThisWeek: [item("j1", "dueThisWeek")] }}
      />,
    );
    expect(screen.getByText("Due this week")).toBeInTheDocument();
    expect(screen.queryByText("Overdue")).not.toBeInTheDocument();
    expect(screen.queryByText("Coming up this month")).not.toBeInTheDocument();
    expect(screen.queryByText(/you're all caught up/i)).not.toBeInTheDocument();
  });

  it("allBucketsPopulated_RendersAllThreeInFixedOrder", () => {
    render(
      <DueJobsOverview
        buckets={{
          overdue: [item("j1", "overdue")],
          dueThisWeek: [item("j2", "dueThisWeek")],
          comingUp: [item("j3", "comingUp")],
        }}
      />,
    );
    const headings = screen.getAllByRole("heading", { level: 2 }).map((h) => h.textContent);
    expect(headings).toEqual(["Overdue", "Due this week", "Coming up this month"]);
  });
});
