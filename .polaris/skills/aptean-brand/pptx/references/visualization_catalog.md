# Visualization Catalog - aptean_viz.py

21 slide builders. Every builder adds branding automatically (background,
logo, footer) and returns the slide. `bg` accepts `"light"`, `"gradient"`
(navy→teal→coral hero), `"deep"` (blue→purple→coral, data/roadmap).

## Choosing the right visualization

| The story is… | Use |
|---|---|
| Opening / closing the deck | `title_slide`, `thank_you_slide` |
| Changing topic | `section_slide` |
| What we'll cover | `agenda_slide` |
| Plain points, prose | `bullets_slide` |
| 2–5 parallel offerings/pillars | `card_grid_slide` |
| Headline + proof-point numbers | `stat_band_slide` (small chips) |
| The numbers ARE the story | `big_stats_slide` (hero columns) |
| Phases over time | `roadmap_slide` |
| History / achievements by year | `milestones_slide` |
| Sequential how-it-works | `process_flow_slide` |
| Narrowing conversion/pipeline | `funnel_slide` |
| Two-dimensional positioning (SWOT, priority) | `matrix_slide` |
| Us vs. them / option A vs. B | `comparison_table_slide` |
| Customer voice | `quote_slide` |
| One % against a target | `gauge_slide` |
| Several % toward goals | `progress_bars_slide` |
| Ranking categories | `bar_chart_slide` |
| Comparing values across groups | `column_chart_slide` |
| Trend over time | `line_chart_slide` |
| Share of a whole | `donut_chart_slide` |

## Signatures and data shapes

```python
title_slide(prs, title, subtitle=None, presenter=None, cta="Let's get started")
section_slide(prs, title, kicker=None, number=None)         # number → "(02)" in gold
agenda_slide(prs, items, title="Agenda", bg="light", image=None)   # items: [str], ≤6
bullets_slide(prs, title, bullets, subtitle=None, bg="light", image=None)

card_grid_slide(prs, title, cards, subtitle=None, bg="gradient", button=None)
    # cards: [{"head","body"}] x2–5; button="Learn more" adds pills
stat_band_slide(prs, title, stats, body=None, bg="gradient", image=None)
    # stats: [{"value":"5.0+","label":"Rate"}] x3–5
big_stats_slide(prs, title, stats, subtitle=None, bg="deep", highlight=None)
    # stats x2–5; highlight=index → that column gets the gradient card

roadmap_slide(prs, title, phases, subtitle=None, bg="deep")
    # phases: [{"phase":"PHASE 01","head","body"}] x2–5
milestones_slide(prs, title, milestones, bg="light", image=None)
    # milestones: [{"year":2026,"body"}] x2–6
process_flow_slide(prs, title, steps, subtitle=None, bg="light")
    # steps: [str] or [{"head","body"}] x3–6, chevron flow
funnel_slide(prs, title, stages, subtitle=None, bg="light")
    # stages: [{"label","value"}] x3–6
matrix_slide(prs, title, quadrants, x_axis=("Low","High"), y_axis=("Low","High"),
             subtitle=None, bg="light")   # quadrants: [tl,tr,bl,br] {"head","body"}
comparison_table_slide(prs, title, columns, rows, subtitle=None, bg="light",
                       highlight_col=None)
    # columns: ["","Option A","Option B"]; rows: [["Feature","✓","–"], ...]

quote_slide(prs, quote, name, role=None, bg="light", image=None)
gauge_slide(prs, title, pct, body=None, bg="light", label=None)   # pct: 0–100
progress_bars_slide(prs, title, items, subtitle=None, bg="gradient")
    # items: [{"label","pct"}] x2–6

bar_chart_slide(prs, title, categories, series, subtitle=None, body=None, bg="light")
column_chart_slide(...)   # same signature
line_chart_slide(...)     # same signature; smoothed brand-colored lines
    # series: [("Series name", [12, 19, 30])]; body → side commentary panel
donut_chart_slide(prs, title, categories, values, subtitle=None, body=None,
                  bg="light", center_label=None)

thank_you_slide(prs, headline="Thank you For Your Time!", url=None, note=None,
                image=None)
```

## Composition guidance

- Open gradient (`title_slide`), close gradient (`thank_you_slide`);
  alternate light/dark in between so the deck breathes (see "Deck rhythm"
  in SKILL.md). Actively pass `bg=` - don't accept every default.
- Light slides are never bare: builders add a gradient banner title box,
  tinted card strips, fog panels, gradient side rails, or a vertical
  gradient side panel (agenda). Custom slides must do the same.
- One idea per slide. If a card grid needs >5 cards, split across slides.
- Charts: single-series bars auto-color per category from the brand ramp;
  multi-series gets Navy/Teal/Coral/Sky with a bottom legend.
- Use `body=` on chart slides to pair every chart with a takeaway - the
  template style always frames data with a headline and short commentary.
- Numbers speak loudest as `big_stats_slide` when there are ≤5 of them and
  they're impressive; use charts when the shape of the data is the point.
- Vary archetypes: a good 12-slide deck uses 8+ different builders.
