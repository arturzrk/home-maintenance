---
name: aptean-2026-pptx
description: Use when creating Aptean-branded PowerPoint decks, slides, or slide visualizations that must match the 2026 Company PPT Template - gradient backgrounds, Inter typography, branded footer, and 21 ready-made slide archetypes (roadmaps, KPI stats, funnels, matrices, charts, quotes). Trigger phrases: 'make a deck', 'build slides in our template', 'Aptean presentation', 'visualize this as slides'.
version: 1.1.0
---

# Aptean 2026 PPTX Builder

Generate on-brand Aptean presentations programmatically with python-pptx.
Faithful to the official 2026 Company PPT Template: the navy→teal→coral
gradient, Inter/Inter ExtraBold type, white rounded cards, the wordmark, and
the mandatory confidentiality footer on every slide.

## Header & footer (v1.1)

- **Full-width flush header bar.** On LIGHT content slides the title sits in a
  gradient bar that spans the entire slide width, flush to the top and both
  edges (no inset box, no rounded corners, no shadow). `title_banner()`.
- **White title, no bar, on dark pages.** Slides that already have a full
  gradient/deep background use `title_white()` - plain white title text at the
  top, so the gradient isn't doubled. `_scaffold` routes to this automatically
  based on `bg`.
- **Footer.** aptean wordmark + copyright bottom-left;
  "Company Confidential Do Not Distribute | <page>" bottom-right; thin rule
  above. White details on dark slides, ink on light. Page numbers
  auto-increment across the deck (reset in `deck()`). **Do not use** the
  retired tagline "Ready for What's Next, Now" (Brand Guidelines 2026).
- **No top-left wordmark on content slides** - the footer carries it. Hero
  slides (title/section/thank-you) keep their own wordmark.

## Bundle map

| File | Read/use when |
|---|---|
| `scripts/aptean_brand.py` | Brand core: palette, gradient fills, footer, logo, cards, pills, chips. Import - don't reimplement. |
| `scripts/aptean_viz.py` | 21 slide builders. This is the API you call. |
| `references/visualization_catalog.md` | **Read before building any deck** - full builder menu, signatures, data shapes, and how to pick the right visualization per story. |
| `references/brand_guide.md` | Read when composing custom slides outside the builders, or when unsure about colors/spacing/typography. |
| `examples/demo_deck.py` | Working end-to-end example (12-slide showcase). Copy patterns from here. |
| `assets/` | Gradient backgrounds + logo PNGs. Never substitute other assets. |

Do NOT read the asset binaries; reference them via `aptean_brand` constants.

## Quick start

```python
import sys; sys.path.insert(0, "<bundle>/scripts")
import aptean_viz as av

prs = av.deck()
av.title_slide(prs, "FY26 Growth Plan", subtitle="Commercial review",
               presenter="Chad Ludwig")
av.agenda_slide(prs, ["Where we are", "Where we're going", "Investments"])
av.big_stats_slide(prs, "2025 by the numbers",
                   [{"value": "134K", "label": "Day 1"},
                    {"value": "157K", "label": "Day 2"},
                    {"value": "285K", "label": "Day 4"}], highlight=2)
av.roadmap_slide(prs, "Delivery roadmap",
                 [{"head": "Discover", "body": "Scope and success metrics."},
                  {"head": "Deploy", "body": "Core rollout, data migration."},
                  {"head": "Scale", "body": "Second-site expansion."}])
av.thank_you_slide(prs, url="www.aptean.com")
av.save(prs, "deck.pptx")
```

Run with `pip install -r requirements.txt` satisfied (python-pptx ≥ 1.0).

## Rules (non-negotiable)

1. **No bare white slides - ever.** Every light slide must carry brand
   chrome. The builders do this automatically: light slides get a full-width
   flush gradient header bar; cards get tinted top strips; list/gauge/funnel
   slides get a gradient side rail or fog panel. If you compose a custom
   slide, give it at least one of: `title_banner()`, `accent_rail()`, a
   gradient card, or a fog panel. A white page with floating black text is a
   defect.
2. **Footer on every slide.** All builders add it automatically. For custom
   slides call `aptean_brand.add_footer(slide, dark_bg=...)`.
3. **Use the gradient.** Open and close on full-bleed gradient slides;
   use `bg="gradient"`/`bg="deep"` on hero, roadmap, and stat slides. Use
   `aptean_brand.gradient_fill(shape)` for gradient cards/banners.
4. **Palette discipline.** Only the brand colors (see `aptean_brand.SERIES`).
   Never ship a default-blue Office chart - builders style this for you.
5. **Pick visualizations from the catalog** based on the data story, not
   habit. Never emit two of the same archetype back-to-back, and never
   three bullet slides in a row.

## Deck rhythm (how to mix it up)

Backgrounds follow a light/dark cadence - a run of same-background slides
reads flat. Working pattern for a 10-slide deck:

`gradient` (title) → `light` (agenda, gets banner + side panel) → `gradient`
(section) → `deep` (big stats) → `light` (card grid) → `deep` (roadmap) →
`light` (chart) → `gradient` (progress/quote) → `light` (table) →
`gradient` (thank-you).

Practical heuristics: at most two consecutive light slides; every 3rd–4th
slide should be a full-bleed gradient moment; charts/tables prefer `light`
(banner supplies the color), stats/roadmaps/quotes prefer `deep`/`gradient`.
Most builders take `bg=` - actively alt