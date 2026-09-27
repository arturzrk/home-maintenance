"""Aptean 2026 brand core for python-pptx decks.

Colors, fonts, gradient recipes, footer, logo, cards, pills.
All visualization builders in aptean_viz.py sit on top of this module.
"""
from pathlib import Path

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from pptx.oxml.ns import qn
import copy

ASSETS = Path(__file__).parent.parent / "assets"

# ---------------------------------------------------------------- palette
INK = RGBColor(0x12, 0x12, 0x12)      # dk1 - headlines / body on light
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
FOG = RGBColor(0xEA, 0xEA, 0xEA)      # accent1 - panels on light slides
GRAY = RGBColor(0xB3, 0xB3, 0xB3)     # dk2 - secondary text
NAVY = RGBColor(0x1D, 0x32, 0x51)     # accent2
TEAL = RGBColor(0x34, 0x71, 0x8C)     # accent3
CORAL = RGBColor(0xDA, 0x77, 0x58)    # accent4
SKY = RGBColor(0x54, 0xA4, 0xDB)      # accent5
GOLD = RGBColor(0xE5, 0xC1, 0x79)     # accent6

# categorical order for charts / repeated elements
SERIES = [NAVY, TEAL, CORAL, SKY, GOLD, GRAY]

HEAD_FONT = "Inter ExtraBold"   # falls back to Inter Bold / system if absent
BODY_FONT = "Inter"

SLIDE_W = Inches(13.333)
SLIDE_H = Inches(7.5)
MARGIN = Inches(0.5)
FOOTER_TEXT = "Copyright © Aptean 2026. All rights reserved. Confidential do not distribute."
# Tagline retired in Brand Guidelines 2026 - do not use in new materials.
FOOTER_TAGLINE = ""
FOOTER_CONF = "Company Confidential Do Not Distribute"
CHARCOAL = RGBColor(0x3A, 0x3A, 0x3A)
_PAGE = {"n": 0}   # running footer page number, reset per deck()

GRADIENT_BG = str(ASSETS / "gradient_bg.jpg")            # navy→teal→coral, hero
GRADIENT_BG_DEEP = str(ASSETS / "gradient_bg_deep.jpg")  # blue→purple→coral, data/roadmap
GRADIENT_BG_VERT = str(ASSETS / "gradient_bg_vertical.jpg")
LOGO_WHITE = str(ASSETS / "aptean_logo_white.png")
LOGO_INK = str(ASSETS / "aptean_logo_ink.png")
LOGO_AR = 298.71 / 948.59  # logo height / width


# ---------------------------------------------------------------- deck
def deck() -> Presentation:
    """New 16:9 presentation."""
    prs = Presentation()
    prs.slide_width = SLIDE_W
    prs.slide_height = SLIDE_H
    _PAGE["n"] = 0
    return prs


def blank_slide(prs, bg="light"):
    """Add a blank slide. bg: 'light' | 'gradient' | 'deep' | 'vertical'."""
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    if bg == "gradient":
        _full_bleed(slide, GRADIENT_BG)
    elif bg == "deep":
        _full_bleed(slide, GRADIENT_BG_DEEP)
    elif bg == "vertical":
        _full_bleed(slide, GRADIENT_BG_VERT)
    return slide


def _full_bleed(slide, img):
    pic = slide.shapes.add_picture(img, 0, 0, SLIDE_W, SLIDE_H)
    slide.shapes._spTree.remove(pic._element)
    slide.shapes._spTree.insert(2, pic._element)
    return pic


def is_dark(bg):
    return bg in ("gradient", "deep", "vertical", "ink")


# ---------------------------------------------------------------- fills
def gradient_fill(shape, stops=None, angle_deg=45):
    """Native Aptean brand gradient on any autoshape.

    Default stops: navy 0% → teal 50% → coral 100% at 45° (Layout-13 recipe).
    stops: list of (pos_0to1, RGBColor).
    """
    stops = stops or [(0.0, NAVY), (0.5, TEAL), (1.0, CORAL)]
    shape.fill.solid()  # ensure spPr/fill exists, then swap
    spPr = shape.fill._xPr
    for tag in ("a:solidFill", "a:noFill", "a:gradFill", "a:blipFill", "a:pattFill"):
        for el in spPr.findall(qn(tag)):
            spPr.remove(el)
    grad = spPr.makeelement(qn("a:gradFill"), {"flip": "none", "rotWithShape": "1"})
    gsLst = grad.makeelement(qn("a:gsLst"), {})
    for pos, color in stops:
        gs = grad.makeelement(qn("a:gs"), {"pos": str(int(pos * 100000))})
        clr = grad.makeelement(qn("a:srgbClr"), {"val": "%02X%02X%02X" % (color[0], color[1], color[2])})
        gs.append(clr)
        gsLst.append(gs)
    grad.append(gsLst)
    lin = grad.makeelement(qn("a:lin"), {"ang": str(int(angle_deg * 60000)), "scaled": "1"})
    grad.append(lin)
    ln = spPr.find(qn("a:ln"))
    if ln is not None:
        spPr.insert(list(spPr).index(ln), grad)
    else:
        spPr.append(grad)


def soft_shadow(shape, blur=0.09, dist=0.045, alpha=28):
    """Subtle card shadow (matches template card treatment)."""
    spPr = shape._element.spPr
    for el in spPr.findall(qn("a:effectLst")):
        spPr.remove(el)
    eff = spPr.makeelement(qn("a:effectLst"), {})
    shd = spPr.makeelement(qn("a:outerShdw"), {
        "blurRad": str(Emu(Inches(blur))), "dist": str(Emu(Inches(dist))),
        "dir": "5400000", "rotWithShape": "0"})
    clr = spPr.makeelement(qn("a:srgbClr"), {"val": "121212"})
    a = spPr.makeelement(qn("a:alpha"), {"val": str(alpha * 1000)})
    clr.append(a)
    shd.append(clr)
    eff.append(shd)
    spPr.append(eff)


# ---------------------------------------------------------------- text
def set_text(tf, runs, font=BODY_FONT, size=12, color=INK, bold=False,
             align=PP_ALIGN.LEFT, line_spacing=None, space_after=None):
    """Fill a text frame. runs: str, or list of str (one per paragraph),
    or list of (str, dict-of-run-overrides)."""
    if isinstance(runs, str):
        runs = [runs]
    tf.word_wrap = True
    for i, item in enumerate(runs):
        text, over = (item if isinstance(item, tuple) else (item, {}))
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = over.get("align", align)
        if line_spacing:
            p.line_spacing = line_spacing
        if space_after is not None:
            p.space_after = Pt(space_after)
        r = p.add_run()
        r.text = text
        f = r.font
        f.name = over.get("font", font)
        f.size = Pt(over.get("size", size))
        f.bold = over.get("bold", bold)
        f.color.rgb = over.get("color", color)


def textbox(slide, x, y, w, h, runs, anchor=MSO_ANCHOR.TOP, **kw):
    tb = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    tb.text_frame.vertical_anchor = anchor
    tb.text_frame.margin_left = tb.text_frame.margin_right = 0
    tb.text_frame.margin_top = tb.text_frame.margin_bottom = 0
    set_text(tb.text_frame, runs, **kw)
    return tb


# ---------------------------------------------------------------- chrome
def add_logo(slide, dark_bg=False, corner="top-left", width=0.9):
    """Aptean wordmark. White on gradient/dark, ink on light."""
    img = LOGO_WHITE if dark_bg else LOGO_INK
    w = Inches(width)
    h = Emu(int(w * LOGO_AR))
    pos = {
        "top-left": (MARGIN, Inches(0.32)),
        "bottom-right": (SLIDE_W - w - MARGIN, SLIDE_H - h - Inches(0.18)),
    }[corner]
    return slide.shapes.add_picture(img, pos[0], pos[1], w, h)


def add_footer(slide, dark_bg=False, page=None, logo=True):
    """Footer band: aptean wordmark bottom-left,
    'Company Confidential Do Not Distribute | <page>' at bottom-right, over a
    thin rule. White details on dark/gradient slides, ink on light. Page
    numbers auto-increment across the deck (reset in deck()); pass page= to
    override. The `logo` arg is kept for backwards compatibility (ignored -
    the wordmark is always drawn).

    Brand Guidelines 2026: do not print the retired tagline.
    """
    _PAGE["n"] += 1
    n = page if page is not None else _PAGE["n"]
    conf_color = WHITE if dark_bg else INK
    rule = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(0.3), Inches(7.02),
                                  Inches(12.73), Pt(0.75))
    rule.fill.solid()
    rule.fill.fore_color.rgb = RGBColor(0xB0, 0xB0, 0xB0) if dark_bg else RGBColor(0xCF, 0xCF, 0xCF)
    rule.line.fill.background()
    rule.shadow.inherit = False
    img = LOGO_WHITE if dark_bg else LOGO_INK
    w = Inches(0.72)
    h = Emu(int(w * LOGO_AR))
    slide.shapes.add_picture(img, Inches(0.3), Inches(7.11), w, h)
    # Optional left-side copyright (no retired tagline)
    textbox(slide, 1.12, 7.13, 5.5, 0.3, FOOTER_TEXT,
            font=BODY_FONT, size=8.5, color=conf_color)
    textbox(slide, SLIDE_W.inches - 5.35, 7.13, 5.05, 0.3,
            f"{FOOTER_CONF}   |   {n}",
            font=BODY_FONT, size=9.5, color=conf_color, align=PP_ALIGN.RIGHT)


def title_block(slide, title, subtitle=None, dark_bg=False, y=0.85, size=28):
    """Plain header: ExtraBold title + optional 12pt subhead. On light slides
    prefer title_banner() - plain headers on white read as unfinished."""
    color = WHITE if dark_bg else INK
    textbox(slide, 0.5, y, 12.33, 0.9, title,
            font=HEAD_FONT, size=size, bold=True, color=color)
    if subtitle:
        textbox(slide, 0.5, y + 0.62, 11.5, 0.4, subtitle,
                font=BODY_FONT, size=12, color=color)


def title_banner(slide, title, subtitle=None, h=1.25):
    """Full-width flush gradient header bar with a white title. The default
    header for LIGHT content slides. Spans the entire slide width, flush to
    the top and both edges (no inset, no rounded corners, no shadow). Already
    dark (gradient/deep) slides should use title_white() instead - a gradient
    bar over a gradient page is redundant."""
    shp = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, Inches(h))
    gradient_fill(shp)
    shp.line.fill.background()
    shp.shadow.inherit = False
    textbox(slide, 0.55, 0.28 if subtitle else 0.40, 12.2, 0.6, title,
            font=HEAD_FONT, size=24, bold=True, color=WHITE)
    if subtitle:
        textbox(slide, 0.57, 0.87, 12.2, 0.35, subtitle,
                font=BODY_FONT, size=11.5, color=WHITE)
    return shp


def title_white(slide, title, subtitle=None):
    """Plain white title (no bar) for slides that already have a full
    gradient/deep background. Sits flush at the top like the banner, so light
    and dark headers line up; avoids doubling the gradient."""
    textbox(slide, 0.55, 0.34 if subtitle else 0.46, 12.3, 0.7, title,
            font=HEAD_FONT, size=26, bold=True, color=WHITE)
    if subtitle:
        textbox(slide, 0.57, 1.02, 12.3, 0.4, subtitle,
                font=BODY_FONT, size=12, color=WHITE)
    return None


def accent_rail(slide, side="right", w=0.16):
    """Thin vertical gradient rail on a slide edge - subtle brand accent for
    light slides that have no other color block."""
    x = SLIDE_W - Inches(w) if side == "right" else 0
    shp = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, x, 0, Inches(w), SLIDE_H)
    gradient_fill(shp, angle_deg=90)
    shp.line.fill.background()
    shp.shadow.inherit = False
    return shp


def card_strip(slide, card_shape, color=None):
    """Tinted header strip across the top of a card (I5000-style card top,
    rendered in 2026 palette). color=None → brand gradient."""
    from pptx.util import Emu as _Emu
    x, y = card_shape.left, card_shape.top
    w = card_shape.width
    h = Inches(0.14)
    shp = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, x, y, w, h)
    shp.adjustments[0] = 0.5
    if color is None:
        gradient_fill(shp)
    else:
        shp.fill.solid()
        shp.fill.fore_color.rgb = color
    shp.line.fill.background()
    shp.shadow.inherit = False
    return shp


def card(slide, x, y, w, h, fill=WHITE, line=None, radius=0.055, shadow=True):
    """Rounded-corner card, the template's core container."""
    shp = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE,
                                 Inches(x), Inches(y), Inches(w), Inches(h))
    shp.adjustments[0] = radius
    if fill == "gradient":
        gradient_fill(shp)
    elif fill is None:
        shp.fill.background()
    else:
        shp.fill.solid()
        shp.fill.fore_color.rgb = fill
    if line:
        shp.line.color.rgb = line
        shp.line.width = Pt(0.75)
    else:
        shp.line.fill.background()
    if shadow:
        soft_shadow(shp)
    shp.shadow.inherit = False
    return shp


def pill(slide, x, y, text, w=1.6, dark_bg=False, arrow=True):
    """CTA pill button ('Let's get started →' style)."""
    shp = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE,
                                 Inches(x), Inches(y), Inches(w), Inches(0.32))
    shp.adjustments[0] = 0.5
    shp.fill.solid()
    shp.fill.fore_color.rgb = WHITE if dark_bg else FOG
    shp.line.fill.background()
    shp.shadow.inherit = False
    label = text + ("   →" if arrow else "")
    set_text(shp.text_frame, label, font=BODY_FONT, size=9.5, color=INK,
             align=PP_ALIGN.CENTER)
    return shp


def kpi_chip(slide, x, y, value, label, w=0.85, h=0.62, dark_value=True):
    """Small stat chip: bold value over 6.5pt label (template '5.0+ Rate' motif)."""
    shp = card(slide, x, y, w, h, fill=WHITE, radius=0.12, shadow=True)
    tf = shp.text_frame
    tf.vertical_anchor = MSO_ANCHOR.MIDDLE
    tf.margin_left = tf.margin_right = Pt(2)
    set_text(tf, [(value, {"font": HEAD_FONT, "size": 13, "bold": True, "color": INK}),
                  (label, {"size": 6.5, "color": INK})],
             align=PP_ALIGN.CENTER)
    return shp


def save(prs, path):
    prs.save(path)
    return path
