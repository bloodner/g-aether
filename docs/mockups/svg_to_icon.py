"""Rasterize G-Aether's SVG logo into the PNG + ICO the WPF app references.

Uses resvg-py (Rust-backed, no native deps) for crisp rendering at each size,
then packs them into a multi-resolution ICO with Pillow.
"""
import io
from pathlib import Path

import resvg_py
from PIL import Image

RES_DIR = Path(__file__).resolve().parents[2] / "app" / "GHelper.WPF" / "Resources"
SVG_PATH = RES_DIR / "app.svg"
ICON_PNG_PATH = RES_DIR / "icon.png"
ICO_PATH = RES_DIR / "app.ico"

ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)
PNG_SIZE = 256


def render_to_png(size: int) -> Image.Image:
    png_bytes = resvg_py.svg_to_bytes(
        svg_path=str(SVG_PATH),
        width=size,
        height=size,
    )
    img = Image.open(io.BytesIO(bytes(png_bytes))).convert("RGBA")

    if img.size != (size, size):
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ox = (size - img.width) // 2
        oy = (size - img.height) // 2
        canvas.paste(img, (ox, oy), img)
        img = canvas
    return img


def main() -> None:
    main_png = render_to_png(PNG_SIZE)
    main_png.save(ICON_PNG_PATH, format="PNG", optimize=True)
    print(f"Wrote {ICON_PNG_PATH} ({PNG_SIZE}x{PNG_SIZE})")

    images = [render_to_png(s) for s in ICO_SIZES]

    largest = images[-1]
    largest.save(
        ICO_PATH,
        format="ICO",
        sizes=[img.size for img in images],
        append_images=images[:-1],
    )
    print(f"Wrote {ICO_PATH} with sizes {[img.size for img in images]}")


if __name__ == "__main__":
    main()
