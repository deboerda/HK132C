# -*- coding: utf-8 -*-
"""Warp the Web-Mercator mosaic to equirectangular so linear lat mapping hits the airport."""
from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

SRC = Path(r"E:\UNITYproject\HK132CWIN7\Assets\Pictures\卫星地图_SW97.2_NE110.5.jpg")
PREVIEW = Path(r"E:\UNITYproject\HK132CWIN7\Assets\Pictures\卫星地图_SW97.2_NE110.5_preview.jpg")
LON_MIN, LAT_MIN = 97.2, 26.2
LON_MAX, LAT_MAX = 110.5, 34.9


def mercator_y(lat: float) -> float:
    lat = max(min(lat, 85.05112878), -85.05112878)
    r = math.radians(lat)
    return math.log(math.tan(math.pi / 4.0 + r / 2.0))


def main() -> None:
    src = Image.open(SRC).convert("RGB")
    w, h = src.size
    arr = np.asarray(src)
    y_north = mercator_y(LAT_MAX)
    y_south = mercator_y(LAT_MIN)
    lats = np.linspace(LAT_MAX, LAT_MIN, h, endpoint=False) - (LAT_MAX - LAT_MIN) / (2.0 * h)
    my = np.log(np.tan(np.pi / 4.0 + np.radians(lats) / 2.0))
    src_y = (my - y_north) / (y_south - y_north) * (h - 1)
    src_y = np.clip(src_y, 0, h - 1)
    y0 = np.floor(src_y).astype(np.int32)
    y1 = np.clip(y0 + 1, 0, h - 1)
    fy = (src_y - y0).astype(np.float32)[:, None, None]
    out = (
        arr[y0].astype(np.float32) * (1.0 - fy)
        + arr[y1].astype(np.float32) * fy
    ).astype(np.uint8)
    Image.fromarray(out, "RGB").save(SRC, "JPEG", quality=92, optimize=True, subsampling=1)
    preview = Image.fromarray(out, "RGB")
    preview.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
    preview.save(PREVIEW, "JPEG", quality=88, optimize=True)

    lon, lat = 103.9653479, 30.69831289
    px = (lon - LON_MIN) / (LON_MAX - LON_MIN) * w
    py = (LAT_MAX - lat) / (LAT_MAX - LAT_MIN) * h
    print(f"saved {SRC} {w}x{h}")
    print(f"Wenjiang pixel {px:.1f},{py:.1f}  frac {px/w:.4f},{1-py/h:.4f}")


if __name__ == "__main__":
    main()
