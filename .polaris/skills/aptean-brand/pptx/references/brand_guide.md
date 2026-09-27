# Aptean 2026 Brand Guide (for slide generation)

Derived from the official "2026 Company PPT Template". Follow this exactly -
consistency with the corporate template is the whole point of this skill.

## Canvas

16:9, 13.333 x 7.5 in. Content margin 0.5 in on all sides. Titles start at
y ≈ 0.55 in; content zone runs y ≈ 1.9–6.9 in; footer sits at y = 7.11 in.

## Color palette

| Token | Hex | Usage |
|---|---|---|
| Ink | `#121212` | Headlines and body on light slides, footer text |
| White | `#FFFFFF` | Text on gradient, card fill, chart panel |
| Fog | `#EAEAEA` | Light panels, pill buttons, table banding |
| Gray | `#B3B3B3` | Secondary text, axis lines, kickers |
| Navy | `#1D3251` | Gradient stop 1, primary series, table headers |
| Teal | `#34718C` | Gradient stop 2, secondary series, progress fills |
| Coral | `#DA7758` | Gradient stop 3, accent/highlight, third series |
| Sky | `#54A4DB` | Fourth series color |
| Gold | `#E5C179` | Fifth series color, quote marks, section numbers on gradient |

Chart/categorical order: Navy → Teal → Coral → Sky → Gold → Gray. Never
introduce colors outside this palette.

## The gradient

The signature brand element. Two forms:

1. **Photographic full-bleed backgrounds** (in `assets/`): rich,
   softly-blurred gradients used as full-slide backgrounds.
   - `gradient_bg.jpg` - navy→teal→coral. Title slides, section dividers,
     hero/statement slides, thank-you.
   - `gradient_bg_deep.jpg` - blue→purple→coral. Roadmaps, data-heavy and
     stat slides (matches template roadmap slides).
   - `gradient_bg_vertical.jpg` - vertical variant for tall panels/sidebars.
2. **Native vector gradient** for shapes/cards/headers: linear 45 degrees,
   `#1D3251` at 0% → `#34718C` at 50% → `#DA7758` at 100%
   (`aptean_brand.gradient_fill(shape)`). Used for gradient cards
   (quotes, highlighted stats, banner headers).

Rhythm rule: alternate gradient and light slides. Openers, dividers,
hero-stat and closer slides go gradient; dense reading content (tables,
multi-card grids, charts) usually goes light. Everything on a gradient
background is white (text) or a white card.

## Typography

| Role | Font | Size |
|---|---|---|
| Deck title | Inter ExtraBold | 40–44 pt |
| Section title | Inter ExtraBold | 36–40 pt |
| Slide title | Inter ExtraBold | 26–30 pt |
| Card heading | Inter ExtraBold | 13–15 pt |
| Body | Inter | 10–12.5 pt |
| Chip/kicker labels | Inter | 6.5–9 pt |
| Footer | Inter | 7 pt |

Inter ExtraBold is set with bold=True so systems without the ExtraBold cut
fall back to Inter Bold. Body copy is never bold, never justified.

## Footer (required on every slide)

Bottom-left at (0.5, 7.11) in, 7 pt:
`Copyright © Aptean 2026. All rights reserved. Confidential do not distribute.`
Ink on light slides, white on gradient. Wordmark bottom-right on content
slides; on slides where the wordmark is already top-left (all builders place
it), no second logo is added. `add_footer()` handles all of this.

## Logo rules

- White wordmark (`aptean_logo_white.png`) on gradient/dark; ink wordmark
  (`aptean_logo_ink.png`) on light. Never recolor, stretch, or box it.
- Top-left at (0.5, 0.32) in, ~0.9–1.1 in wide on content slides.

## Light-slide chrome (mandatory)

A light slide must never be a bare white page. The standard treatments,
in order of preference:

1. **Gradient banner title** (`title_banner`) - full-width rounded box with
   the brand gradient and white title; the template's "Gradient Box" layout.
   This is the default header on light slides.
2. **Tinted card strips** (`card_strip`) - a thin navy/teal/coral/sky/gold
   strip across the top of each white card.
3. **Fog panel** - body content grouped on an `#EAEAEA` rounded panel.
4. **Gradient side rail** (`accent_rail`) - a thin vertical brand-gradient
   bar on the slide edge.
5. **Vertical gradient side panel** - `gradient_bg_vertical.jpg` as a
   half-slide image block (agenda motif).

## Cards, chips, pills

- Cards: white rounded rectangles (corner radius ≈ 5.5%), subtle drop
  shadow, 0.25–0.3 in internal padding. Heading in ExtraBold, body 10–10.5 pt.
- KPI chips: small white rounded cards, bold value over a 6.5 pt label.
- Pills: fully-rounded CTA buttons ("Let's get started →", "Learn more →"),
  fog fill on light, white fill on gradient, 9.5 pt ink text with a → arrow.

## Don'ts

- No colors outside the palette; no default Office blue charts.
- No gridline-heavy charts - value-axis gridlines off, ticks off.
- No footer-less slides, no black text on gradient backgrounds.
- Don't put a photographic gradient background behind a native chart
  directly - charts sit on a white card panel.
