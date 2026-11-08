import { render } from "@testing-library/react";
import { DueBucketIcon } from "@/components/due-bucket-icon";

describe("DueBucketIcon", () => {
  it("rendersAnSvg_ForEveryBucket", () => {
    for (const bucket of ["overdue", "dueThisWeek", "comingUp"] as const) {
      const { container } = render(<DueBucketIcon bucket={bucket} />);
      expect(container.querySelector("svg")).toBeInTheDocument();
    }
  });

  it("appliesTheGivenClassName", () => {
    const { container } = render(
      <DueBucketIcon bucket="overdue" className="h-3.5 w-3.5 text-rose-500" />,
    );
    expect(container.querySelector("svg")).toHaveClass("text-rose-500");
  });

  it("isDecorative_HiddenFromAssistiveTech", () => {
    const { container } = render(<DueBucketIcon bucket="overdue" />);
    expect(container.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
  });

  it("overdueAndDueThisWeek_RenderDifferentIcons", () => {
    const { container: overdueContainer } = render(<DueBucketIcon bucket="overdue" />);
    const { container: dueThisWeekContainer } = render(<DueBucketIcon bucket="dueThisWeek" />);
    expect(overdueContainer.querySelector("svg")?.innerHTML).not.toBe(
      dueThisWeekContainer.querySelector("svg")?.innerHTML,
    );
  });
});
