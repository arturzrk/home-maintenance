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

const baseProps = {
  id: "dueThisWeek",
  title: "Due this week",
  icon: <svg data-testid="section-icon" />,
  iconBg: "bg-amber-50",
  textClass: "text-amber-600",
};

describe("DueJobSection", () => {
  it("nonEmptyItems_RendersTitleAndOneRowPerItem", () => {
    render(
      <DueJobSection
        {...baseProps}
        items={[item("job-1", "Service boiler"), item("job-2", "Clean gutters")]}
      />,
    );
    expect(screen.getByText("Due this week")).toBeInTheDocument();
    expect(screen.getByText("Service boiler")).toBeInTheDocument();
    expect(screen.getByText("Clean gutters")).toBeInTheDocument();
    expect(screen.getAllByRole("link")).toHaveLength(2);
  });

  it("emptyItems_RendersNothing", () => {
    const { container } = render(<DueJobSection {...baseProps} items={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it("nonEmptyItems_SectionIsLabelledByItsHeading", () => {
    render(<DueJobSection {...baseProps} items={[item("job-1", "Service boiler")]} />);
    const heading = screen.getByRole("heading", { level: 2, name: "Due this week" });
    const region = screen.getByRole("region", { name: "Due this week" });
    expect(region).toContainElement(heading);
  });

  it("differentIds_ProduceDifferentHeadingIds", () => {
    const { rerender } = render(
      <DueJobSection {...baseProps} id="overdue" items={[item("job-1", "A")]} />,
    );
    const firstId = screen.getByRole("heading", { level: 2 }).id;

    rerender(<DueJobSection {...baseProps} id="comingUp" items={[item("job-1", "A")]} />);
    const secondId = screen.getByRole("heading", { level: 2 }).id;

    expect(firstId).not.toBe(secondId);
  });

  it("rendersTheIcon_InsideTheHeading", () => {
    render(<DueJobSection {...baseProps} items={[item("job-1", "A")]} />);
    const heading = screen.getByRole("heading", { level: 2, name: "Due this week" });
    expect(heading).toContainElement(screen.getByTestId("section-icon"));
  });

  it("rendersTheItemCount_WithAJobsLabel", () => {
    render(
      <DueJobSection
        {...baseProps}
        items={[item("job-1", "A"), item("job-2", "B"), item("job-3", "C")]}
      />,
    );
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("jobs")).toBeInTheDocument();
  });

  it("countNumber_UsesTheGivenTextClass", () => {
    render(<DueJobSection {...baseProps} items={[item("job-1", "A")]} />);
    expect(screen.getByText("1")).toHaveClass("text-amber-600");
  });

  it("countDoesNotLeakIntoTheHeadingsAccessibleName", () => {
    render(
      <DueJobSection
        {...baseProps}
        items={[item("job-1", "A"), item("job-2", "B")]}
      />,
    );
    // Accessible name must stay exactly the title - the count/"jobs"
    // label sits beside the heading, not inside its accessible name.
    expect(
      screen.getByRole("heading", { level: 2, name: "Due this week" }),
    ).toBeInTheDocument();
  });
});
