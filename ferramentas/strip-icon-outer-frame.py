# -*- coding: utf-8 -*-
"""Remove apenas o branco do CANVAS exterior ao card arredondado.

O flood-fill a partir das bordas só atravessa pixels quase brancos e neutros
(r+g+b >= limiar, baixa saturação). A sombra do card (~249–253) fica abaixo do
limiar e quebra a ligação com o branco interno do card — preservando card,
sombra e R azul.

Uso:
  strip-icon-outer-frame.py <entrada.png> <saida-quadrada.png> [--lum N] [--sat N]
"""
from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

from PIL import Image


def main() -> int:
    if len(sys.argv) < 3:
        print(
            "Uso: strip-icon-outer-frame.py <entrada.png> <saida.png> [--lum N] [--sat N]"
        )
        return 2

    src = Path(sys.argv[1])
    dst = Path(sys.argv[2])
    # 756 = ~252/canal em média: remove 254 exterior, não atravessa sombra ~249
    lum_min = 756
    sat_max = 10
    i = 3
    while i < len(sys.argv):
        if sys.argv[i] == "--lum" and i + 1 < len(sys.argv):
            lum_min = int(sys.argv[i + 1])
            i += 2
            continue
        if sys.argv[i] == "--sat" and i + 1 < len(sys.argv):
            sat_max = int(sys.argv[i + 1])
            i += 2
            continue
        i += 1

    im = Image.open(src).convert("RGBA")
    w, h = im.size
    px = im.load()

    def floodable(x: int, y: int) -> bool:
        r, g, b, a = px[x, y]
        if a < 16:
            return False
        if max(r, g, b) - min(r, g, b) > sat_max:
            return False
        return r + g + b >= lum_min

    seen: set[tuple[int, int]] = set()
    q: deque[tuple[int, int]] = deque()

    for x in range(w):
        for y in (0, h - 1):
            if floodable(x, y):
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if floodable(x, y):
                q.append((x, y))

    while q:
        x, y = q.popleft()
        if (x, y) in seen:
            continue
        seen.add((x, y))
        if not floodable(x, y):
            continue
        px[x, y] = (0, 0, 0, 0)
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen:
                q.append((nx, ny))

    bbox = im.getbbox()
    if not bbox:
        print("ERRO: imagem vazia apos remocao (ajuste --lum / --sat).")
        return 1

    im = im.crop(bbox)
    w, h = im.size
    side = max(w, h)
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    out.paste(im, ((side - w) // 2, (side - h) // 2), im)
    out.save(dst, format="PNG")
    print(f"OK -> {dst} (crop {bbox}, quadrado {side}, lum>={lum_min}, sat<={sat_max})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
