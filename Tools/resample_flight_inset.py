# -*- coding: utf-8 -*-
"""Warp the flight-range Web-Mercator mosaic to equirectangular for linear overlay."""
from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

SRC = Path(r"E:\UNITYproject\HK132CWIN7\Assets\Pictures\卫星地图_SW103.91916_NE104.25196.jpg")
DST = Path(r"E:\UNITYproject\HK132CWIN7\Assets\Pictures\飞行范围插图.jpg")
LON_MIN, LAT_MIN = 103.91916083, 30.58753045
LON_MAX, LAT_MAX = 104.25195738, 30.75583896


def main() -> None:
    src = Image.open(SRC).convert("RGB")
    w, h = src.size
    arr = np.asarray(src)
    y_north = math.log(math.tan(math.pi / 4.0 + math.radians(LAT_MAX) / 2.0))
    y_south = math.log(math.tan(math.pi / 4.0 + math.radians(LAT_MIN) / 2.0))
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
    Image.fromarray(out, "RGB").save(DST, "JPEG", quality=92, optimize=True, subsampling=1)
    lon, lat = 103.9653479, 30.69831289
    px = (lon - LON_MIN) / (LON_MAX - LON_MIN) * w
    py = (LAT_MAX - lat) / (LAT_MAX - LAT_MIN) * h
    print(f"saved {DST} {w}x{h} bytes={DST.stat().st_size}")
    print(f"Wenjiang pixel {px:.1f},{py:.1f}")


if __name__ == "__main__":
    main()
