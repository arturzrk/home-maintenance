"""Aptean 2026 visualization library - one function per slide archetype.

Every builder adds a fully-branded slide (gradient/light background, footer,
wordmark) and returns it. Data goes in as plain lists/dicts.

See references/visualization_catalog.md for the full menu and data shapes.
"""
import math

from pptx.util import Inches, Pt, Emu
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from pptx.chart.data import CategoryChartData
from pptx.enum.chart import XL_CHART_TYPE, XL_LEGEND_POSITION, XL_TICK_MARK

from aptean_brand import (
    deck, blank_slide, add_footer, add_logo, title_block, title_banner,
    title_white,
    accent_rail, card_strip, card, pill,
    kpi_chip, textbox, set_text, gradient_fill, soft_shadow, is_dark, save,
    INK, WHITE, FOG, GRAY, NAVY, TEAL, CORAL, SKY, GOLD, SERIES,
    HEAD_FONT, BODY_FONT, SLIDE_W, SLIDE_H, GRADIENT_BG_VERT,
)

__all__ = [
    "deck", "save",
    "title_slide", "section_slide", "agenda_slide", "bullets_slide",
    "card_grid_slide", "stat_band_slide", "big_stats_slide",
    "roadmap_slide", "milestones_slide", "process_flow_slide",
    "funnel_slide", "matrix_slide", "comparison_table_slide",
    "quote_slide", "gauge_slide", "progress_bars_slide",
    "bar_chart_slide", "column_chart_slide", "line_chart_slide",
    "donut_chart_slide", "thank_you_slide",
]

FG = lambda dark: WHITE if dark else INK


def _scaffold(prs, bg, title=None, subtitle=None, page=None, banner=True):
    """Slide + footer + header.

    The footer carries the aptean wordmark, so content slides no longer get a
    separate top-left logo. LIGHT slides get a full-width flush gradient
    header bar; already-dark (gradient/deep) slides get a plain white title so
    the gradient isn't doubled. Pass banner=False to force a plain title."""
    s = blank_slide(prs, bg)
    dark = is_dark(bg)
    add_footer(s, dark_bg=dark, page=page, logo=False)
    if title:
        if dark:
            title_white(s, title, subtitle)
        elif not banner:
            title_block(s, title, subtitle, dark_bg=dark)
        else:
            title_banner(s, title, subtitle)
    return s, dark


# ================================================================ openers
def title_slide(prs, title, subtitle=None, presenter=None, cta="Let's get started"):
    """Full-bleed gradient hero title."""
    s = blank_slide(prs, "gradient")
    add_logo(s, dark_bg=True, corner="top-left", width=1.1)
    textbox(s, 0.5, 2.55, 9.8, 1.9, title, font=HEAD_FONT, size=44,
            bold=True, color=WHITE)
    if subtitle:
        textbox(s, 0.5, 4.45, 8.5, 0.6, subtitle, size=14, color=WHITE)
    if presenter:
        textbox(s, 0.5, 6.35, 6.0, 0.3,
                [(f"Presenter: ", {"size": 11, "color": WHITE}),
                 ], size=11, color=WHITE)
        s.shapes[-1].text_frame.paragraphs[0].add_run().text = presenter
        r = s.shapes[-1].text_frame.paragraphs[0].runs[-1]
        r.font.bold = True; r.font.size = Pt(11); r.font.color.rgb = WHITE
        r.font.name = BODY_FONT
    if cta:
        pill(s, 11.0, 6.7, cta, w=1.85, dark_bg=True)
    add_footer(s, dark_bg=True, logo=False)
    return s


def section_slide(prs, title, kicker=None, number=None):
    """Gradient section divider."""
    s = blank_slide(prs, "gradient")
    add_logo(s, dark_bg=True, corner="top-left", width=1.0)
    if number:
        textbox(s, 0.5, 2.15, 3.0, 0.5, f"({number:02d})" if isinstance(number, int) else str(number),
                font=HEAD_FONT, size=16, bold=True, color=GOLD)
    if kicker:
        textbox(s, 0.5, 2.7, 9.0, 0.4, kicker.upper(), size=12, color=WHITE)
    textbox(s, 0.5, 3.15, 11.5, 1.6, title, font=HEAD_FONT, size=40,
            bold=True, color=WHITE)
    add_footer(s, dark_bg=True, logo=False)
    return s


def agenda_slide(prs, items, title="Agenda", bg="light", image=None):
    """Numbered agenda list, template '(01) Agenda Item' motif. On light
    slides a vertical-gradient side panel fills the right half."""
    s, dark = _scaffold(prs, bg, title)
    y = 2.15
    step = min(0.85, 4.6 / max(len(items), 1))
    for i, it in enumerate(items):
        textbox(s, 0.7, y, 0.7, 0.3, f"({i + 1:02d})", size=10,
                color=CORAL if not dark else GOLD, font=BODY_FONT)
        textbox(s, 1.45, y - 0.02, 6.4, 0.35, it, font=HEAD_FONT, size=14,
                bold=True, color=FG(dark))
        y += step
    if image:
        s.shapes.add_picture(image, Inches(8.6), Inches(2.15),
                             Inches(4.2), Inches(4.2))
    elif not dark:
        panel = s.shapes.add_picture(GRADIENT_BG_VERT, Inches(8.6),
                                     Inches(2.15), Inches(4.23), Inches(4.55))
        soft_shadow(panel)
        textbox(s, 8.95, 5.55, 3.6, 0.9,
                f"{len(items):02d} topics today", font=HEAD_FONT, size=16,
                bold=True, color=WHITE)
    return s


def bullets_slide(prs, title, bullets, subtitle=None, bg="light", image=None):
    """Title + chevron bullets. On light slides without an image, bullets sit
    on a fog panel with a gradient side rail so the page is never bare."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    w = 6.6 if image else 11.2
    y0 = 2.15
    if image:
        s.shapes.add_picture(image, Inches(7.6), Inches(y0),
                             Inches(5.2), Inches(4.4))
    elif not dark:
        card(s, 0.5, y0 - 0.05, 12.33, 4.6, fill=FOG, radius=0.04,
             shadow=False)
        accent_rail(s)
        w = 11.4
    runs = []
    for b in bullets:
        runs.append((f"»  {b}", {"size": 12.5, "color": FG(dark)}))
    textbox(s, 0.9 if not (image or dark) else 0.5, y0 + 0.3, w, 4.0, runs,
            line_spacing=1.15, space_after=10)
    return s


# ================================================================ cards & stats
def card_grid_slide(prs, title, cards, subtitle=None, bg="gradient",
                    button=None):
    """1-row grid of white cards (2–5). cards: [{'head','body'}]."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    n = len(cards)
    gap = 0.28
    x0, x1 = 0.5, 12.83
    w = (x1 - x0 - gap * (n - 1)) / n
    y, h = 2.15, 3.9 if button else 3.4
    strip_colors = [NAVY, TEAL, CORAL, SKY, GOLD]
    for i, c in enumerate(cards):
        x = x0 + i * (w + gap)
        shp = card(s, x, y, w, h, fill=WHITE)
        if not dark:  # tinted top strip so white cards pop on light slides
            card_strip(s, shp, strip_colors[i % len(strip_colors)])
        textbox(s, x + 0.25, y + 0.34, w - 0.5, 0.7, c["head"],
                font=HEAD_FONT, size=14, bold=True, color=INK)
        textbox(s, x + 0.25, y + 1.09, w - 0.5, h - 1.55, c["body"],
                size=10.5, color=INK, line_spacing=1.15)
        if button:
            pill(s, x + 0.25, y + h - 0.62, button, w=min(1.5, w - 0.5))
    return s


def stat_band_slide(prs, title, stats, body=None, bg="gradient", image=None):
    """Headline + row of small KPI chips ('5.0+ Rate | 189 Clients | 80% Approve').

    stats: [{'value','label'}] (3–5 chips)."""
    s, dark = _scaffold(prs, bg, None)
    textbox(s, 0.5, 1.5, 7.2, 1.4, title, font=HEAD_FONT, size=30, bold=True,
            color=FG(dark))
    if body:
        textbox(s, 0.5, 3.0, 6.8, 1.2, body, size=12, color=FG(dark),
                line_spacing=1.2)
    x = 0.5
    for st in stats:
        kpi_chip(s, x, 4.5, st["value"], st["label"])
        x += 1.05
    if image:
        pic = s.shapes.add_picture(image, Inches(7.9), Inches(1.5),
                                   Inches(4.9), Inches(3.4))
        soft_shadow(pic)
    return s


def big_stats_slide(prs, title, stats, subtitle=None, bg="deep",
                    highlight=None):
    """Hero numbers on tall white column cards, staggered like the template.

    stats: [{'value','label'}] (2–5). highlight: index drawn on gradient card."""
    s, dark = _scaffold(prs, bg, None)
    textbox(s, 0.5, 2.2, 4.6, 2.2, title, font=HEAD_FONT, size=30, bold=True,
            color=FG(dark))
    if subtitle:
        textbox(s, 0.5, 4.3, 4.4, 1.0, subtitle, size=12, color=FG(dark))
    n = len(stats)
    x0, x1 = 5.6, 12.83
    gap = 0.25
    w = (x1 - x0 - gap * (n - 1)) / n
    tops = [2.6, 1.4, 3.1, 0.9, 2.0]  # staggered heights
    for i, st in enumerate(stats):
        x = x0 + i * (w + gap)
        top = tops[i % len(tops)]
        hi = (highlight == i)
        c = card(s, x, top, w, 6.85 - top, fill="gradient" if hi else WHITE)
        textbox(s, x + 0.12, top + 0.25, w - 0.24, 0.35, st["label"],
                size=9, color=WHITE if hi else GRAY)
        textbox(s, x + 0.12, top + 0.6, w - 0.24, 0.8, st["value"],
                font=HEAD_FONT, size=min(28, int(w * 16)), bold=True,
                color=WHITE if hi else INK)
    return s


# ================================================================ time
def roadmap_slide(prs, title, phases, subtitle=None, bg="deep"):
    """Numbered dots on a line with phase cards above (Layout 13 motif).

    phases: [{'phase','head','body'}] (2–5)."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    n = len(phases)
    gap = 0.3
    x0, x1 = 0.5, 12.83
    w = (x1 - x0 - gap * (n - 1)) / n
    y, h = 2.3, 3.3
    line_y = 6.1
    # connector line
    ln = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(x0 + w / 2),
                            Inches(line_y + 0.13), Inches((n - 1) * (w + gap)),
                            Pt(1.2))
    ln.fill.solid(); ln.fill.fore_color.rgb = WHITE if dark else GRAY
    ln.line.fill.background(); ln.shadow.inherit = False
    for i, p in enumerate(phases):
        x = x0 + i * (w + gap)
        card(s, x, y, w, h, fill=WHITE)
        textbox(s, x + 0.22, y + 0.28, w - 0.44, 0.3,
                p.get("phase", f"PHASE {i + 1:02d}").upper(), size=8.5,
                color=GRAY)
        textbox(s, x + 0.22, y + 0.62, w - 0.44, 0.75, p["head"],
                font=HEAD_FONT, size=13.5, bold=True, color=INK)
        textbox(s, x + 0.22, y + 1.42, w - 0.44, h - 1.7, p["body"],
                size=10, color=INK, line_spacing=1.15)
        dot = s.shapes.add_shape(MSO_SHAPE.OVAL, Inches(x + w / 2 - 0.17),
                                 Inches(line_y), Inches(0.34), Inches(0.34))
        dot.fill.solid(); dot.fill.fore_color.rgb = WHITE
        dot.line.fill.background(); dot.shadow.inherit = False
        tf = dot.text_frame
        tf.margin_left = tf.margin_right = 0
        tf.margin_top = tf.margin_bottom = 0
        set_text(tf, f"{i + 1:02d}", size=9, bold=True, color=INK,
                 align=PP_ALIGN.CENTER)
        tf.word_wrap = False  # after set_text (which enables wrap)
    return s


def milestones_slide(prs, title, milestones, bg="light", image=None):
    """Staggered year blocks in navy/teal tints (template 'Milestones' slide).

    milestones: [{'year','body'}] (2–6)."""
    s, dark = _scaffold(prs, bg, title)
    palette = [NAVY, TEAL, CORAL, SKY, GOLD, GRAY]
    cols = 3
    bw, bh, gap = 2.15, 1.75, 0.22
    x0, y0 = 4.6 if image else 0.7, 1.95
    if image:
        s.shapes.add_picture(image, Inches(0.5), Inches(1.95),
                             Inches(3.7), Inches(4.6))
    for i, m in enumerate(milestones):
        x = x0 + (i % cols) * (bw + gap) + (0.5 * bw if (i // cols) % 2 else 0)
        y = y0 + (i // cols) * (bh + gap)
        c = card(s, x, y, bw, bh, fill=palette[i % len(palette)], radius=0.09)
        textbox(s, x + 0.2, y + 0.2, bw - 0.4, 0.35, str(m["year"]),
                font=HEAD_FONT, size=15, bold=True, color=WHITE)
        textbox(s, x + 0.2, y + 0.62, bw - 0.4, bh - 0.8, m["body"], size=9.5,
                color=WHITE, line_spacing=1.1)
    return s


# ================================================================ flow
def process_flow_slide(prs, title, steps, subtitle=None, bg="light"):
    """Left-to-right chevron flow. steps: [{'head','body'}] or [str] (3–6)."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    steps = [{"head": st} if isinstance(st, str) else st for st in steps]
    n = len(steps)
    x0, x1 = 0.5, 12.83
    w = (x1 - x0) / n
    y, h = 3.0, 1.5
    for i, st in enumerate(steps):
        x = x0 + i * w
        shp = s.shapes.add_shape(
            MSO_SHAPE.PENTAGON if i == 0 else MSO_SHAPE.CHEVRON,
            Inches(x), Inches(y), Inches(w - 0.08), Inches(h))
        shp.adjustments[0] = 0.28
        shp.fill.solid(); shp.fill.fore_color.rgb = SERIES[i % len(SERIES)]
        shp.line.fill.background(); shp.shadow.inherit = False
        set_text(shp.text_frame, st["head"], font=HEAD_FONT, size=12,
                 bold=True, color=WHITE, align=PP_ALIGN.CENTER)
        if st.get("body"):
            textbox(s, x + 0.15, y + h + 0.25, w - 0.3, 1.4, st["body"],
                    size=9.5, color=FG(dark), align=PP_ALIGN.CENTER,
                    line_spacing=1.1)
    return s


def funnel_slide(prs, title, stages, subtitle=None, bg="light"):
    """Top-down funnel. stages: [{'label','value'}] (3–6)."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    if not dark:
        accent_rail(s)
    n = len(stages)
    y0, total_h = 2.15, 4.4
    h = total_h / n - 0.1
    max_w, min_w = 7.6, 2.6
    cx = 4.7
    for i, st in enumerate(stages):
        w = max_w - (max_w - min_w) * i / max(n - 1, 1)
        x = cx - w / 2
        shp = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x),
                                 Inches(y0 + i * (h + 0.1)), Inches(w), Inches(h))
        shp.adjustments[0] = 0.5
        shp.fill.solid(); shp.fill.fore_color.rgb = SERIES[i % len(SERIES)]
        shp.line.fill.background(); shp.shadow.inherit = False
        set_text(shp.text_frame, st["label"], font=HEAD_FONT, size=11.5,
                 bold=True, color=WHITE, align=PP_ALIGN.CENTER)
        textbox(s, 9.2, y0 + i * (h + 0.1) + h / 2 - 0.17, 3.0, 0.35,
                str(st.get("value", "")), font=HEAD_FONT, size=14, bold=True,
                color=FG(dark))
    return s


def matrix_slide(prs, title, quadrants, x_axis=("Low", "High"),
                 y_axis=("Low", "High"), subtitle=None, bg="light"):
    """2×2 matrix (SWOT, priority grid). quadrants: [tl, tr, bl, br] as
    {'head','body'}."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    x0, y0, w, h, gap = 2.1, 2.1, 4.6, 2.15, 0.15
    colors = [NAVY, TEAL, CORAL, GOLD]
    pos = [(x0, y0), (x0 + w + gap, y0), (x0, y0 + h + gap),
           (x0 + w + gap, y0 + h + gap)]
    for i, q in enumerate(quadrants):
        x, y = pos[i]
        card(s, x, y, w, h, fill=colors[i], radius=0.06)
        txt = INK if colors[i] == GOLD else WHITE  # contrast on gold
        textbox(s, x + 0.25, y + 0.2, w - 0.5, 0.35, q["head"],
                font=HEAD_FONT, size=13, bold=True, color=txt)
        textbox(s, x + 0.25, y + 0.62, w - 0.5, h - 0.8, q["body"], size=9.5,
                color=txt, line_spacing=1.1)
    # axis labels
    textbox(s, x0, y0 + 2 * h + gap + 0.15, 2 * w + gap, 0.3,
            f"{x_axis[0]}  →  {x_axis[1]}", size=9, color=FG(dark),
            align=PP_ALIGN.CENTER)
    lab = textbox(s, x0 - 1.6, y0 + h - 0.15, 1.45, 0.3,
                  f"{y_axis[0]}  →  {y_axis[1]}", size=9, color=FG(dark),
                  align=PP_ALIGN.RIGHT)
    lab.rotation = 270
    return s


def comparison_table_slide(prs, title, columns, rows, subtitle=None,
                           bg="light", highlight_col=None):
    """Styled comparison table. columns: [''] + option names.
    rows: [[feature, val, val, ...]]. highlight_col: 1-based data column."""
    s, dark = _scaffold(prs, bg, title, subtitle)
    n_r, n_c = len(rows) + 1, len(columns)
    tbl_shape = s.shapes.add_table(n_r, n_c, Inches(0.5), Inches(2.05),
                                   Inches(12.33), Inches(min(4.6, 0.5 * n_r)))
    tbl = tbl_shape.table
    tbl.first_row = True
    tbl.horz_banding = True
    for j, col in enumerate(columns):
        cell = tbl.cell(0, j)
        cell.fill.solid()
        cell.fill.fore_color.rgb = CORAL if j == highlight_col else NAVY
        set_text(cell.text_frame, col, font=HEAD_FONT, size=11, bold=True,
                 color=WHITE, align=PP_ALIGN.LEFT if j == 0 else PP_ALIGN.CENTER)
    for i, row in enumerate(rows):
        for j, val in enumerate(row):
            cell = tbl.cell(i + 1, j)
            cell.fill.solid()
            if j == highlight_col:
                cell.fill.fore_color.rgb = FOG
            else:
                cell.fill.fore_color.rgb = WHITE if i % 2 == 0 else FOG
            set_text(cell.text_frame, str(val), size=10,
                     bold=(j == 0), color=INK,
                     align=PP_ALIGN.LEFT if j == 0 else PP_ALIGN.CENTER)
    return s


# ================================================================ voice
def quote_slide(prs, quote, name, role=None, bg="light", image=None):
    """Customer quote on a gradient card."""
    s, dark = _scaffold(prs, bg, None)
    if not dark:
        accent_rail(s, side="left")
    c = card(s, 3.4, 1.7, 6.5, 4.1, fill="gradient", radius=0.05)
    # ASCII-rule exemption: the curly quotes below are intentional rendered
    # quotation glyphs on the slide card, not prose punctuation.
    textbox(s, 3.8, 1.95, 5.7, 0.8, "“", font=HEAD_FONT, size=54,
            bold=True, color=GOLD)
    textbox(s, 3.8, 2.9, 5.7, 1.9, f"“{quote}”", size=15,
            color=WHITE, line_spacing=1.25)
    who = [(f"- {name}", {"font": HEAD_FONT, "size": 12, "bold": True,
                          "color": WHITE})]
    if role:
        who.append((role, {"size": 10, "color": WHITE}))
    textbox(s, 3.8, 4.95, 5.7, 0.7, who)
    if image:
        s.shapes.add_picture(image, Inches(10.3), Inches(1.7),
                             Inches(2.5), Inches(4.1))
    return s


# ================================================================ data - custom
def gauge_slide(prs, title, pct, body=None, bg="light", label=None):
    """Segmented arc gauge with big % (template gauge motif)."""
    s, dark = _scaffold(prs, bg, None)
    if not dark:
        accent_rail(s)
    textbox(s, 8.0, 2.4, 4.6, 1.6, title, font=HEAD_FONT, size=26, bold=True,
            color=FG(dark))
    if body:
        textbox(s, 8.0, 4.0, 4.5, 1.6, body, size=11.5, color=FG(dark),
                line_spacing=1.2)
    panel = card(s, 0.6, 1.5, 6.8, 4.6, fill="gradient" if not is_dark(bg) else WHITE,
                 radius=0.06)
    seg_fg = WHITE if not is_dark(bg) else NAVY
    cx, cy, r = 3.95, 4.55, 1.9
    n_seg = 12
    filled = round(pct / 100 * n_seg)
    for i in range(n_seg):
        ang = math.radians(200 - i * (220 / (n_seg - 1)))
        x = cx + r * math.cos(ang)
        y = cy - r * math.sin(ang)
        seg = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE,
                                 Inches(x - 0.3), Inches(y - 0.21),
                                 Inches(0.6), Inches(0.42))
        seg.adjustments[0] = 0.25
        seg.rotation = (90 - math.degrees(ang)) % 360
        seg.shadow.inherit = False
        if i < filled:
            seg.fill.solid(); seg.fill.fore_color.rgb = seg_fg
            seg.line.fill.background()
        else:
            seg.fill.background()
            seg.line.color.rgb = seg_fg
            seg.line.width = Pt(1)
    textbox(s, cx - 1.4, cy - 1.0, 2.8, 0.9, f"{pct}%", font=HEAD_FONT,
            size=40, bold=True, color=seg_fg, align=PP_ALIGN.CENTER)
    if label:
        textbox(s, cx - 1.4, cy - 0.1, 2.8, 0.4, label, size=10,
                color=seg_fg, align=PP_ALIGN.CENTER)
    return s


def progress_bars_slide(prs, title, items, subtitle=None, bg="gradient"):
    """Horizontal progress bars, white track on gradient.

    items: [{'label','pct'}] (2–6)."""
    s, dark = _scaffold(prs, bg, None)
    textbox(s, 0.5, 2.2, 4.4, 2.0, title, font=HEAD_FONT, size=28, bold=True,
            color=FG(dark))
    if subtitle:
        textbox(s, 0.5, 4.2, 4.2, 1.2, subtitle, size=11.5, color=FG(dark))
    x0, bw, bh = 5.6, 6.9, 0.42
    n = len(items)
    gap = min(1.1, 4.8 / n)
    y = 1.7
    track_line = WHITE if dark else GRAY
    for it in items:
        textbox(s, x0, y - 0.28, 4.0, 0.25, it["label"], size=9,
                color=FG(dark))
        track = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x0),
                                   Inches(y), Inches(bw), Inches(bh))
        track.adjustments[0] = 0.5
        track.fill.background()
        track.line.color.rgb = track_line; track.line.width = Pt(1)
        track.shadow.inherit = False
        fillw = max(bw * it["pct"] / 100, bh)
        bar = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(x0),
                                 Inches(y), Inches(fillw), Inches(bh))
        bar.adjustments[0] = 0.5
        bar.fill.solid(); bar.fill.fore_color.rgb = WHITE if dark else TEAL
        bar.line.fill.background(); bar.shadow.inherit = False
        textbox(s, x0 + bw + 0.15, y + 0.05, 0.8, 0.3, f"{it['pct']}%",
                size=10, bold=True, color=FG(dark))
        y += gap
    return s


# ================================================================ data - native charts
def _style_chart(chart, color_per_point=False, legend=False):
    chart.has_title = False
    chart.has_legend = legend
    if legend:
        chart.legend.position = XL_LEGEND_POSITION.BOTTOM
        chart.legend.include_in_layout = False
        chart.legend.font.size = Pt(10)
        chart.legend.font.name = BODY_FONT
    for i, series in enumerate(chart.plots[0].series):
        if color_per_point:
            for j, pt in enumerate(series.points):
                pt.format.fill.solid()
                pt.format.fill.fore_color.rgb = SERIES[j % len(SERIES)]
        else:
            series.format.fill.solid()
            series.format.fill.fore_color.rgb = SERIES[i % len(SERIES)]
    try:
        for ax in (chart.category_axis, chart.value_axis):
            ax.tick_labels.font.size = Pt(9.5)
            ax.tick_labels.font.name = BODY_FONT
            ax.format.line.color.rgb = GRAY
            ax.major_tick_mark = XL_TICK_MARK.NONE
        chart.value_axis.has_major_gridlines = False
    except ValueError:
        pass


def _chart_slide(prs, title, subtitle, bg, chart_type, categories, series,
                 body=None, legend=None, color_per_point=False):
    s, dark = _scaffold(prs, bg, title, subtitle)
    data = CategoryChartData()
    data.categories = categories
    for name, vals in series:
        data.add_series(name, vals)
    cw = 7.6 if body else 12.3
    panel = card(s, 0.5, 2.1, cw, 4.5, fill=WHITE, radius=0.035)
    gf = s.shapes.add_chart(chart_type, Inches(0.8), Inches(2.35),
                            Inches(cw - 0.6), Inches(4.0), data)
    chart = gf.chart
    _style_chart(chart, color_per_point=color_per_point,
                 legend=(len(series) > 1 if legend is None else legend))
    chart.font.name = BODY_FONT
    chart.font.size = Pt(10)
    chart.font.color.rgb = INK
    if body:
        textbox(s, 8.5, 2.4, 4.2, 3.8, body, size=11.5, color=FG(dark),
                line_spacing=1.25)
    return s, chart


def bar_chart_slide(prs, title, categories, series, subtitle=None,
                    body=None, bg="light"):
    """Horizontal bars. series: [(name, [vals])]."""
    s, _ = _chart_slide(prs, title, subtitle, bg, XL_CHART_TYPE.BAR_CLUSTERED,
                        categories, series, body,
                        color_per_point=(len(series) == 1))
    return s


def column_chart_slide(prs, title, categories, series, subtitle=None,
                       body=None, bg="light"):
    """Vertical columns. series: [(name, [vals])]."""
    s, _ = _chart_slide(prs, title, subtitle, bg,
                        XL_CHART_TYPE.COLUMN_CLUSTERED, categories, series,
                        body)
    return s


def line_chart_slide(prs, title, categories, series, subtitle=None,
                     body=None, bg="light"):
    """Trend lines. series: [(name, [vals])]."""
    s, chart = _chart_slide(prs, title, subtitle, bg,
                            XL_CHART_TYPE.LINE_MARKERS, categories, series,
                            body)
    for i, ser in enumerate(chart.plots[0].series):
        ser.format.line.color.rgb = SERIES[i % len(SERIES)]
        ser.format.line.width = Pt(2.25)
        ser.smooth = True
    return s


def donut_chart_slide(prs, title, categories, values, subtitle=None,
                      body=None, bg="light", center_label=None):
    """Donut share-of-total. values: [nums]."""
    s, chart = _chart_slide(prs, title, subtitle, bg, XL_CHART_TYPE.DOUGHNUT,
                            categories, [("share", values)], body,
                            legend=True, color_per_point=True)
    plot = chart.plots[0]
    plot.has_data_labels = True
    dl = plot.data_labels
    dl.show_percentage = True
    dl.show_value = False
    dl.font.size = Pt(10); dl.font.bold = True
    dl.font.color.rgb = WHITE; dl.font.name = BODY_FONT
    if center_label:
        cw = 7.6 if body else 12.3
        textbox(s, 0.5 + cw / 2 - 1.25, 4.1, 2.5, 0.6, center_label,
                font=HEAD_FONT, size=18, bold=True, color=INK,
                align=PP_ALIGN.CENTER)
    return s


# ================================================================ closer
def thank_you_slide(prs, headline="Thank you For Your Time!", url=None,
                    note=None, image=None):
    s = blank_slide(prs, "gradient")
    add_logo(s, dark_bg=True, corner="top-left", width=1.1)
    textbox(s, 0.5, 2.3, 6.4, 1.9, headline, font=HEAD_FONT, size=38,
            bold=True, color=WHITE)
    if url:
        pill(s, 0.5, 4.35, url, w=2.3, dark_bg=True)
    if note:
        c = card(s, 0.5, 5.1, 5.9, 1.1, fill=None, line=WHITE, radius=0.1,
                 shadow=False)
        textbox(s, 0.75, 5.3, 5.4, 0.7, note, size=10, color=WHITE,
                line_spacing=1.15)
    if image:
        pic = s.shapes.add_picture(image, Inches(7.6), Inches(1.2),
                                   Inches(5.2), Inches(5.0))
        soft_shadow(pic)
    add_footer(s, dark_bg=True, logo=False)
    return s
