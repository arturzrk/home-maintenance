import { render, screen } from "@testing-library/react";
import { DueJobSection } from "@/components/due-job-section";
import type { DueJobItem } from "@/lib/due-jobs";

function item(id: string, name: string): DueJobItem {
  return {
    job: {
      id,
      propertyId: "prop-1",
      name,
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
    bucket: "dueThisWeek",
  };
}

describe("DueJobSection", () => {
  it("nonEmptyItems_RendersTitleAndOneRowPerItem", () => {
    render(
      <DueJobSection
        title="Due this week"
        items={[item("job-1", "Service boiler"), item("job-2", "Clean gutters")]}
      />,
    );
    expect(screen.getByText("Due this week")).toBeInTheDocument();
    expect(screen.getByText("Service boiler")).toBeInTheDocument();
    expect(screen.getByText("Clean gutters")).toBeInTheDocument();
    expect(screen.getAllByRole("link")).toHaveLength(2);
  });

  it("emptyItems_RendersNothing", () => {
    const { container } = render(<DueJobSection title="Due this week" items={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
