"""Draw the SprintImprovements package icon with Pillow.

    python tools/make_icon.py

Writes thunderstore/SprintImprovements/icon.png (256x256), which is also
shown in the Risk of Options mod list.

Glyph: a key cap with a double chevron (sprint) on its face. The style
and the background(), bracket_mask() and compose() helpers are copied
from DroneImprovements' tools/make_icons.py (a dark glitch background,
glowing corner brackets and a glowing one-colour glyph); keep the copies
identical. The glyph is drawn as a white-on-black mask at WORK_SIZE and
coloured by compose(). The output only depends on the Pillow version:
Pillow 12.3.0 reproduces the committed PNG byte for byte.
"""
from __future__ import annotations

import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

WORK_SIZE = 1024  # drawn large, then downscaled for antialiasing
ICON_SIZE = 256
REPO = Path(__file__).resolve().parent.parent
PACKAGE_ICON = REPO / "thunderstore" / "SprintImprovements" / "icon.png"

Color = tuple[int, int, int]


def background(seed: int, color: Color = (255, 255, 255)) -> Image.Image:
    """Return the near-black background with faint glitch blocks."""
    rng = random.Random(seed)
    image = Image.new("RGB", (WORK_SIZE, WORK_SIZE), (6, 6, 8))
    draw = ImageDraw.Draw(image)
    # Dark pixel/glitch blocks like the vanilla drone icons, slightly
    # tinted with the glyph colour.
    for _ in range(260):
        w = rng.choice([24, 32, 48, 64, 96, 128])
        h = rng.choice([16, 24, 32, 48])
        x = rng.randrange(0, WORK_SIZE, 16)
        y = rng.randrange(0, WORK_SIZE, 16)
        value = rng.randint(10, 30)
        tint = rng.random() * 0.12
        fill = tuple(int(value + c * tint) for c in color)
        draw.rectangle([x, y, x + w, y + h], fill=fill)
    # Grey scanlines across the whole width.
    for _ in range(40):
        y = rng.randrange(0, WORK_SIZE, 8)
        value = rng.randint(22, 40)
        draw.rectangle([0, y, WORK_SIZE, y + rng.choice([4, 8])],
                       fill=(value, value, value))
    return image


def bracket_mask() -> Image.Image:
    """Return the mask of the double-line chamfered corner brackets."""
    mask = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(mask)
    for inset, width in ((34, 64), (140, 40)):
        arm = 330 - inset // 3
        chamfer = 90
        for sign_x, sign_y in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            corner_x = inset if sign_x == 1 else WORK_SIZE - inset
            corner_y = inset if sign_y == 1 else WORK_SIZE - inset
            points = [
                (corner_x + sign_x * arm, corner_y),
                (corner_x + sign_x * chamfer, corner_y),
                (corner_x, corner_y + sign_y * chamfer),
                (corner_x, corner_y + sign_y * arm),
            ]
            draw.line(points, fill=255, width=width, joint="curve")
    # Small notches in the middle of each edge, like the circuit tabs on
    # the vanilla icons: (centre x, centre y, width, height).
    middle, edge = WORK_SIZE // 2, WORK_SIZE - 30
    for cx, cy, w, h in ((middle, 30, 160, 26), (middle, edge, 160, 26),
                         (30, middle, 26, 160), (edge, middle, 26, 160)):
        draw.rectangle([cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2],
                       fill=255)
    return mask


def compose(glyph: Image.Image, color: Color, seed: int,
            scale: float = 1.0) -> Image.Image:
    """Colour a glyph mask and put it on the background and brackets.

    scale enlarges the glyph around the centre before composing.
    """
    if scale != 1:
        big = glyph.resize((int(WORK_SIZE * scale), int(WORK_SIZE * scale)),
                           Image.Resampling.BICUBIC)
        offset = (big.width - WORK_SIZE) // 2
        glyph = big.crop((offset, offset, offset + WORK_SIZE,
                          offset + WORK_SIZE))
    size = (WORK_SIZE, WORK_SIZE)
    base = background(seed, color).convert("RGBA")
    solid = Image.new("RGBA", size, color + (255,))

    # Frame: a slightly darker tint of the glyph colour.
    dark = tuple(int(c * 0.72) for c in color)
    base.paste(Image.new("RGBA", size, dark + (255,)), (0, 0), bracket_mask())

    # Glow: the blurred glyph, added on top.
    glow = glyph.filter(ImageFilter.GaussianBlur(38))
    glow_rgb = Image.merge("RGB", [
        ImageChops.multiply(glow, Image.new("L", size, c)) for c in color])
    lit = ImageChops.add(base.convert("RGB"), glow_rgb)
    base = Image.merge("RGBA", (*lit.split(), Image.new("L", size, 255)))

    # The solid glyph with a brighter core.
    base.paste(solid, (0, 0), glyph)
    core = glyph.filter(ImageFilter.MinFilter(21)).filter(
        ImageFilter.GaussianBlur(10))
    highlight = tuple(min(255, int(c + (255 - c) * 0.6)) for c in color)
    base.paste(Image.new("RGBA", size, highlight + (255,)), (0, 0),
               ImageChops.multiply(core, Image.new("L", size, 150)))
    return base.resize((ICON_SIZE, ICON_SIZE), Image.Resampling.LANCZOS)


def chevron(draw: ImageDraw.ImageDraw, x: int, cy: int, half_height: int,
            depth: int, thickness: int) -> None:
    """Draw a solid right-pointing chevron with its back edge at x."""
    draw.polygon([
        (x, cy - half_height),
        (x + thickness, cy - half_height),
        (x + thickness + depth, cy),
        (x + thickness, cy + half_height),
        (x, cy + half_height),
        (x + depth, cy),
    ], fill=255)


def key_glyph() -> Image.Image:
    """Package icon: a key cap with a double chevron on its face."""
    glyph = Image.new("L", (WORK_SIZE, WORK_SIZE), 0)
    draw = ImageDraw.Draw(glyph)
    # Key cap: an outline with a thicker bottom edge, like a keyboard
    # key seen from the front.
    left, top, right, bottom = 250, 260, 774, 784
    draw.rounded_rectangle([left, top, right, bottom], radius=90, fill=255)
    draw.rounded_rectangle([left + 44, top + 44, right - 44, bottom - 104],
                           radius=56, fill=0)
    # Double chevron (sprint) centred on the key face.
    cy = (top + 44 + bottom - 104) // 2
    chevron(draw, 346, cy, 150, 130, 80)
    chevron(draw, 496, cy, 150, 130, 80)
    return glyph


def main() -> None:
    """Draw the icon and save it."""
    PACKAGE_ICON.parent.mkdir(parents=True, exist_ok=True)
    compose(key_glyph(), (255, 190, 60), seed=5).save(PACKAGE_ICON)
    print(f"wrote {PACKAGE_ICON.relative_to(REPO).as_posix()}")


if __name__ == "__main__":
    main()
