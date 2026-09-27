"""Showcase deck — one slide per major archetype. Run from bundle root:
    python examples/demo_deck.py out.pptx
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "scripts"))
import aptean_viz as av  # noqa: E402

prs = av.deck()

av.title_slide(prs, "2026 Visualization Showcase",
               subtitle="Every archetype in the Aptean deck-builder skill",
               presenter="Chad Ludwig")

av.agenda_slide(prs, ["Momentum & proof points", "Product pillars",
                      "Delivery roadmap", "Market position",
                      "Performance data", "Next steps"])

av.section_slide(prs, "Momentum", kicker="Where we are today", number=1)

av.stat_band_slide(
    prs, "Customers keep choosing Aptean",
    [{"value": "5.0+", "label": "Rate"},
     {"value": "189", "label": "Clients"},
     {"value": "80%", "label": "Approve"}],
    body=("Lorem-free proof: adoption, satisfaction, and retention all "
          "moved up and to the right in FY25."))

av.big_stats_slide(
    prs, "Launch week by the numbers",
    [{"value": "134K", "label": "Day 1"},
     {"value": "157K", "label": "Day 2"},
     {"value": "47K", "label": "Day 3"},
     {"value": "285K", "label": "Day 4"}],
    subtitle="Product units sold out in four days.", highlight=3)

av.card_grid_slide(
    prs, "Three pillars for FY26",
    [{"head": "Automate", "body": "AI ways of working across every team, "
      "with AppCentral as the front door."},
     {"head": "Integrate", "body": "One data plane across ERP, TMS, and "
      "compliance products."},
     {"head": "Expand", "body": "Land-and-expand motion in food & beverage "
      "and logistics verticals."}],
    subtitle="What we invest in, and why it wins.", button="Learn more")

av.roadmap_slide(
    prs, "Delivery roadmap",
    [{"head": "Discover", "body": "Scope, success metrics, executive "
      "alignment."},
     {"head": "Deploy", "body": "Core rollout, data migration, champion "
      "training."},
     {"head": "Adopt", "body": "Playbooks, office hours, usage telemetry."},
     {"head": "Scale", "body": "Second-site expansion and automation."}],
    subtitle="Four phases from kickoff to scaled value.")

av.process_flow_slide(
    prs, "How an order flows",
    [{"head": "Capture", "body": "Omnichannel intake"},
     {"head": "Validate", "body": "Credit & compliance checks"},
     {"head": "Fulfill", "body": "Warehouse orchestration"},
     {"head": "Deliver", "body": "Route-optimized transport"},
     {"head": "Settle", "body": "Automated invoicing"}])

av.funnel_slide(
    prs, "Pipeline conversion",
    [{"label": "Leads", "value": "12,400"},
     {"label": "Qualified", "value": "3,800"},
     {"label": "Evaluations", "value": "940"},
     {"label": "Closed won", "value": "212"}])

av.progress_bars_slide(
    prs, "Goal attainment",
    [{"label": "Week 1", "pct": 68}, {"label": "Week 2", "pct": 38},
     {"label": "Week 3", "pct": 55}, {"label": "Week 4", "pct": 82}],
    subtitle="Weekly attainment against the launch plan.")

av.matrix_slide(
    prs, "Initiative priorities",
    [{"head": "Quick wins", "body": "AppCentral rollout, footer automation."},
     {"head": "Big bets", "body": "Unified data plane, AI copilots."},
     {"head": "Fill-ins", "body": "Template refresh, training refresh."},
     {"head": "Reconsider", "body": "Legacy connectors, on-prem tooling."}],
    x_axis=("Low impact", "High impact"), y_axis=("Hard", "Easy"))

av.comparison_table_slide(
    prs, "Aptean vs. status quo",
    ["", "Spreadsheets", "Generic ERP", "Aptean"],
    [["Industry fit", "–", "Partial", "Purpose-built"],
     ["Time to value", "Months", "12–18 mo", "Weeks"],
     ["AI ways of working", "–", "Add-on", "Built in"],
     ["Compliance", "Manual", "Configurable", "Out of the box"]],
    highlight_col=3)

av.milestones_slide(
    prs, "Milestones",
    [{"year": 2023, "body": "Platform unification begins."},
     {"year": 2024, "body": "AI copilot beta across 40 customers."},
     {"year": 2025, "body": "AppCentral ships to every employee."},
     {"year": 2026, "body": "100% AI-empowered workforce."}])

av.column_chart_slide(
    prs, "Revenue by quarter", ["Q1", "Q2", "Q3", "Q4"],
    [("FY25", [110, 125, 140, 170]), ("FY26 plan", [130, 150, 175, 210])],
    body=("FY26 plan compounds the Q4 exit rate; the swing factor is "
          "attach rate on AI modules."))

av.donut_chart_slide(
    prs, "Revenue mix", ["Food & Bev", "Logistics", "Manufacturing", "Other"],
    [42, 28, 21, 9], center_label="FY25",
    body="Two verticals now drive 70% of revenue.")

av.line_chart_slide(
    prs, "Adoption trend", ["W1", "W2", "W3", "W4", "W5", "W6"],
    [("Active users", [1.2, 2.1, 3.4, 4.2, 5.6, 7.1]),
     ("Power users", [0.2, 0.5, 0.9, 1.6, 2.4, 3.3])],
    body="Power-user share doubles every three weeks.", bg="deep")

av.gauge_slide(prs, "Customer approval", 80,
               body="Post-rollout CSAT across 189 accounts.",
               label="approve")

av.quote_slide(
    prs, "Sed ut perspiciatis unde omnis iste natus error sit voluptatem "
    "accusantium doloremque laudantium.", "Jordan Reyes",
    role="VP Operations, Northwind Foods")

av.thank_you_slide(prs, url="www.aptean.com",
                   note="Questions? The team is available all week at "
                        "appcentral/office-hours.")

out = sys.argv[1] if len(sys.argv) > 1 else "showcase.pptx"
av.save(prs, out)
print("wrote", out, "-", len(prs.slides.__iter__.__self__._sldIdLst), "slides")
