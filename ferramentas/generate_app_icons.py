# -*- coding: utf-8 -*-
"""Gerador oficial dos icones do Ribanense Solucoes.

O launcher usa uma fita azul dobrada em forma de "R" sobre um card branco squircle.
Cada aplicativo do catalogo recebe um monograma de duas letras desenhado com a mesma
gramatica: fitas de largura constante, terminais recortados ou acabados, gradiente
discreto e sombra de dobra onde uma fita passa por cima da outra.

Uso:
    python ferramentas/generate_app_icons.py [--preview <dir>]

O parametro opcional --preview grava versoes de 512px em um diretorio para inspecao.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

# ---------------------------------------------------------------------------
# Catalogo de aplicativos
# ---------------------------------------------------------------------------

APPS = [
    {
        "slug": "winget",
        "name": "Gestor WinGet",
        "letters": "Wg",
        "light": (64, 166, 255),
        "dark": (6, 62, 168),
        "fold": (0, 20, 60),
        "crease_strength": 112,
        "app_dir": "src/aplicativos/Ribanense.Solucoes.App.Winget",
        "catalog_icon": "catalog/icons/winget.png",
    },
    {
        "slug": "chocolatey",
        "name": "Gestor Chocolatey",
        "letters": "Ch",
        "light": (255, 150, 54),
        "dark": (154, 52, 0),
        "fold": (74, 26, 0),
        "app_dir": "src/aplicativos/Ribanense.Solucoes.App.Chocolatey",
        "catalog_icon": "catalog/icons/chocolatey.png",
    },
    {
        "slug": "sistema",
        "name": "Gestor de Sistema",
        "letters": "Si",
        "light": (66, 222, 160),
        "dark": (3, 105, 74),
        "fold": (4, 52, 38),
        "app_dir": "src/aplicativos/Ribanense.Solucoes.App.Sistema",
        "catalog_icon": "catalog/icons/sistema.png",
    },
    {
        "slug": "balanca",
        "name": "Testador de Balancas",
        "letters": "Ba",
        "light": (180, 124, 255),
        "dark": (85, 32, 190),
        "fold": (35, 10, 80),
        "app_dir": "src/aplicativos/Ribanense.Solucoes.App.Balanca",
        "catalog_icon": "catalog/icons/balanca.png",
    },
    {
        "slug": "farol",
        "name": "Farol",
        "letters": "Fa",
        "light": (56, 208, 214),
        "dark": (4, 92, 110),
        "fold": (2, 40, 52),
        "app_dir": "src/aplicativos/Ribanense.Solucoes.App.Farol",
        "catalog_icon": "catalog/icons/farol.png",
    },
]

# Proporcoes do card, herdadas do icone do launcher.
CARD_MARGIN = 0.058
CARD_RADIUS = 0.205

# Proporcoes tipograficas do monograma, relativas ao lado do canvas.
RIBBON_WIDTH = 0.084
CAP_HEIGHT = 0.420
X_HEIGHT_RATIO = 0.70
LETTER_GAP = 0.045

# Caixa maxima ocupada pela tinta dentro do card.
INK_MAX_WIDTH = 0.74
INK_MAX_HEIGHT = 0.56

GRADIENT_ANGLE = -45.0


# ---------------------------------------------------------------------------
# Primitivas de desenho
# ---------------------------------------------------------------------------


def mix(c0, c1, t):
    return tuple(int(round(a + (b - a) * t)) for a, b in zip(c0, c1))


def ellipse_points(cx, cy, rx, ry, a0, a1, step=1.5):
    """Amostra um arco eliptico. Angulos em graus, eixo Y para baixo."""
    count = max(2, int(abs(a1 - a0) / step))
    pts = []
    for i in range(count + 1):
        angle = math.radians(a0 + (a1 - a0) * i / count)
        pts.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle)))
    return pts


def arc_points(cx, cy, radius, a0, a1, step=1.5):
    return ellipse_points(cx, cy, radius, radius, a0, a1, step)


def densify(points, max_step):
    """Reamostra a polilinha para que nenhum segmento seja maior que max_step."""
    dense = [points[0]]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        distance = math.hypot(x1 - x0, y1 - y0)
        steps = max(1, int(distance / max_step))
        for i in range(1, steps + 1):
            dense.append((x0 + (x1 - x0) * i / steps, y0 + (y1 - y0) * i / steps))
    return dense


def _extend_path_ends(points, start_extension, end_extension):
    """Estende o eixo sem alterar sua direcao nas extremidades."""
    extended = list(points)
    if start_extension:
        x0, y0 = extended[0]
        x1, y1 = extended[1]
        length = math.hypot(x1 - x0, y1 - y0)
        extended[0] = (
            x0 - (x1 - x0) / length * start_extension,
            y0 - (y1 - y0) / length * start_extension,
        )
    if end_extension:
        x0, y0 = extended[-2]
        x1, y1 = extended[-1]
        length = math.hypot(x1 - x0, y1 - y0)
        extended[-1] = (
            x1 + (x1 - x0) / length * end_extension,
            y1 + (y1 - y0) / length * end_extension,
        )
    return extended


def _clip_terminal_locally(mask, point, neighbour, width):
    """Achata somente a vizinhanca da ponta, sem cortar outras partes da curva."""
    size = mask.width
    px, py = point
    nx, ny = neighbour
    tx, ty = nx - px, ny - py
    length = math.hypot(tx, ty)
    tx, ty = tx / length, ty / length
    normal_x, normal_y = -ty, tx
    reach = size * 3

    half_plane = Image.new("L", mask.size, 0)
    ImageDraw.Draw(half_plane).polygon(
        [
            (px - normal_x * reach, py - normal_y * reach),
            (px + normal_x * reach, py + normal_y * reach),
            (px + normal_x * reach + tx * reach, py + normal_y * reach + ty * reach),
            (px - normal_x * reach + tx * reach, py - normal_y * reach + ty * reach),
        ],
        fill=255,
    )

    zone = Image.new("L", mask.size, 0)
    zone_radius = width * 1.35
    ImageDraw.Draw(zone).ellipse(
        [px - zone_radius, py - zone_radius, px + zone_radius, py + zone_radius],
        fill=255,
    )
    outside = ImageChops.multiply(mask, ImageChops.invert(zone))
    inside = ImageChops.multiply(ImageChops.multiply(mask, zone), half_plane)
    return ImageChops.lighter(outside, inside)


def stroke_mask(size, points, width, start_cap="round", end_cap="round"):
    """Fita de largura constante com terminais controlados.

    ``round`` representa uma ponta deliberadamente moldada. ``cut`` produz um
    corte reto, como uma faixa fisicamente seccionada. ``fold`` tambem e reto,
    mas avanca um pouco alem do eixo para que duas faces se sobreponham no vinco,
    sem criar o bulbo circular que havia na versao anterior.
    """
    original = list(points)
    fold_extension = width * 0.08
    working = _extend_path_ends(
        original,
        fold_extension if start_cap == "fold" else 0,
        fold_extension if end_cap == "fold" else 0,
    )
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    radius = width / 2.0
    dense = densify(working, radius)

    for (x0, y0), (x1, y1) in zip(dense, dense[1:]):
        dx, dy = x1 - x0, y1 - y0
        length = math.hypot(dx, dy)
        if length < 1e-6:
            continue
        nx, ny = -dy / length * radius, dx / length * radius
        draw.polygon(
            [(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)],
            fill=255,
        )
    # Os discos internos unem os quadrilateros sem farpas. Os discos das pontas
    # so existem quando o terminal foi deliberadamente definido como redondo.
    for x, y in dense[1:-1]:
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=255)
    if start_cap == "round":
        x, y = dense[0]
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=255)
    if end_cap == "round":
        x, y = dense[-1]
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=255)
    if start_cap != "round":
        mask = _clip_terminal_locally(mask, working[0], working[1], width)
    if end_cap != "round":
        mask = _clip_terminal_locally(mask, working[-1], working[-2], width)
    return mask


def _line_intersection(origin_a, direction_a, origin_b, direction_b):
    """Intersecao de duas retas usada para construir juntas em meia-esquadria."""
    ax, ay = origin_a
    adx, ady = direction_a
    bx, by = origin_b
    bdx, bdy = direction_b
    denominator = adx * bdy - ady * bdx
    if abs(denominator) < 1e-6:
        return ((ax + bx) / 2.0, (ay + by) / 2.0)
    distance = ((bx - ax) * bdy - (by - ay) * bdx) / denominator
    return (ax + adx * distance, ay + ady * distance)


def folded_polyline_masks(size, points, width):
    """Divide uma fita angular continua em faces que se encontram sem saliencias."""
    radius = width / 2.0
    directions = []
    normals = []
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        dx, dy = x1 - x0, y1 - y0
        length = math.hypot(dx, dy)
        direction = (dx / length, dy / length)
        directions.append(direction)
        normals.append((-direction[1] * radius, direction[0] * radius))

    left = [
        (points[0][0] + normals[0][0], points[0][1] + normals[0][1])
    ]
    right = [
        (points[0][0] - normals[0][0], points[0][1] - normals[0][1])
    ]
    for index, point in enumerate(points[1:-1], start=1):
        previous_normal = normals[index - 1]
        next_normal = normals[index]
        previous_direction = directions[index - 1]
        next_direction = directions[index]

        left.append(
            _line_intersection(
                (point[0] + previous_normal[0], point[1] + previous_normal[1]),
                previous_direction,
                (point[0] + next_normal[0], point[1] + next_normal[1]),
                next_direction,
            )
        )
        right.append(
            _line_intersection(
                (point[0] - previous_normal[0], point[1] - previous_normal[1]),
                previous_direction,
                (point[0] - next_normal[0], point[1] - next_normal[1]),
                next_direction,
            )
        )

    left.append(
        (points[-1][0] + normals[-1][0], points[-1][1] + normals[-1][1])
    )
    right.append(
        (points[-1][0] - normals[-1][0], points[-1][1] - normals[-1][1])
    )

    segments = []
    for index in range(len(points) - 1):
        mask = Image.new("L", (size, size), 0)
        ImageDraw.Draw(mask).polygon(
            [left[index], left[index + 1], right[index + 1], right[index]],
            fill=255,
        )
        segments.append(mask)
    return segments


def chamfer_polyline(points, distance):
    """Substitui cada vertice agudo por uma pequena face reta de dobra."""
    chamfered = [points[0]]
    for previous, current, following in zip(points, points[1:-1], points[2:]):
        incoming_x, incoming_y = current[0] - previous[0], current[1] - previous[1]
        outgoing_x, outgoing_y = following[0] - current[0], following[1] - current[1]
        incoming_length = math.hypot(incoming_x, incoming_y)
        outgoing_length = math.hypot(outgoing_x, outgoing_y)
        chamfered.extend(
            [
                (
                    current[0] - incoming_x / incoming_length * distance,
                    current[1] - incoming_y / incoming_length * distance,
                ),
                (
                    current[0] + outgoing_x / outgoing_length * distance,
                    current[1] + outgoing_y / outgoing_length * distance,
                ),
            ]
        )
    chamfered.append(points[-1])
    return chamfered


def ring_mask(size, cx, cy, r_out, r_in):
    """Fita fechada em anel."""
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse([cx - r_out, cy - r_out, cx + r_out, cy + r_out], fill=255)
    draw.ellipse([cx - r_in, cy - r_in, cx + r_in, cy + r_in], fill=0)
    return mask


def diamond_mask(size, cx, cy, diagonal, corner=0.30):
    """Losango de cantos arredondados, usado no pingo do 'i'."""
    side = diagonal / math.sqrt(2.0)
    pad = int(side)
    box = int(side) + 2 * pad
    tile = Image.new("L", (box, box), 0)
    ImageDraw.Draw(tile).rounded_rectangle(
        [pad, pad, pad + side, pad + side], radius=side * corner, fill=255
    )
    tile = tile.rotate(45, resample=Image.BICUBIC, expand=True)
    mask = Image.new("L", (size, size), 0)
    mask.paste(tile, (int(cx - tile.width / 2), int(cy - tile.height / 2)))
    return mask


def linear_gradient(width, height, angle_deg):
    """Rampa linear em tons de cinza, orientada pelo angulo informado."""
    diagonal = int(math.hypot(width, height)) + 4
    ramp = Image.linear_gradient("L").resize((diagonal, diagonal), Image.BILINEAR)
    ramp = ramp.rotate(angle_deg, resample=Image.BILINEAR)
    left = (diagonal - width) // 2
    top = (diagonal - height) // 2
    return ramp.crop((left, top, left + width, top + height))


def paint_ribbon(mask, color_light, color_dark):
    """Preenche a fita com um gradiente discreto restrito ao seu proprio bbox."""
    bbox = mask.getbbox()
    layer = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    if bbox is None:
        return layer
    width = max(1, bbox[2] - bbox[0])
    height = max(1, bbox[3] - bbox[1])
    ramp = linear_gradient(width, height, GRADIENT_ANGLE)
    layer.paste(ImageOps.colorize(ramp, color_light, color_dark), (bbox[0], bbox[1]))
    layer.putalpha(mask)
    return layer


# ---------------------------------------------------------------------------
# Construcao das letras
#
# Cada construtor devolve (camadas, avanco). As camadas vao da mais ao fundo
# para a mais a frente, e cada uma declara a face de iluminacao que recebe.
# ---------------------------------------------------------------------------


def build_w(size, x, baseline, geo):
    cap, w = geo["cap"], geo["w"]
    advance = 1.22 * cap
    top = baseline - cap + 0.10 * w
    bottom = baseline - 0.10 * w
    apex = baseline - 0.68 * cap

    t1 = (x + 0.08 * advance, top)
    v1 = (x + 0.265 * advance, bottom)
    ap = (x + 0.500 * advance, apex)
    v2 = (x + 0.735 * advance, bottom)
    t2 = (x + 0.92 * advance, top)

    axis = chamfer_polyline([t1, v1, ap, v2, t2], w * 0.34)
    segments = folded_polyline_masks(size, axis, w)
    outer_right = ImageChops.lighter(segments[6], segments[5])
    inner_right = ImageChops.lighter(segments[4], segments[3])
    inner_left = ImageChops.lighter(segments[2], segments[1])
    bands = [
        (outer_right, "a"),
        (inner_right, "a"),
        (inner_left, "a"),
        (segments[0], "a"),
    ]
    return bands, advance


def build_g(size, x, baseline, geo):
    cap, xh, w = geo["cap"], geo["xh"], geo["w"]
    r_out = xh / 2.0
    bowl = ring_mask(size, x + r_out, baseline - r_out, r_out, r_out - w)

    stem_x = x + xh - w / 2
    descender = 0.36 * cap
    tail_y = baseline + descender - w / 2
    elbow = 0.42 * xh

    path = [(stem_x, baseline - xh + w / 2), (stem_x, tail_y - elbow)]
    path += arc_points(stem_x - elbow, tail_y - elbow, elbow, 0, 90)
    path += [(x + 0.08 * xh, tail_y)]

    # A cauda recebe acabamento arredondado, ecoando a ponta inferior do R.
    return [(stroke_mask(size, path, w, start_cap="fold", end_cap="round"), "b"), (bowl, "a")], xh


def build_c(size, x, baseline, geo):
    cap, w = geo["cap"], geo["w"]
    r_out = cap / 2.0
    center = (x + r_out, baseline - r_out)
    path = arc_points(center[0], center[1], r_out - w / 2, 42, 318)
    return [(stroke_mask(size, path, w, start_cap="cut", end_cap="cut"), "a")], cap


def build_h(size, x, baseline, geo):
    cap, xh, w = geo["cap"], geo["xh"], geo["w"]
    advance = 0.80 * cap
    stem_x = x + w / 2
    leg_x = x + advance - w / 2
    shoulder_r = (leg_x - stem_x) / 2.0
    shoulder_y = baseline - xh + w / 2 + shoulder_r

    path = arc_points(stem_x + shoulder_r, shoulder_y, shoulder_r, 180, 360)
    path += [(leg_x, baseline)]
    leg = stroke_mask(size, path, w, start_cap="fold", end_cap="round")
    stem = stroke_mask(
        size,
        [(stem_x, baseline - cap), (stem_x, baseline)],
        w,
        start_cap="cut",
        end_cap="cut",
    )

    return [(leg, "b"), (stem, "a")], advance


def build_s(size, x, baseline, geo):
    """Dois meios lacos ligados por uma espinha diagonal, dobrada na cintura."""
    cap, w = geo["cap"], geo["w"]
    ry = (cap - w) / 4.0
    rx = ry * 1.28
    cx = x + rx + w / 2
    cy_top = baseline - cap + w / 2 + ry
    cy_bottom = baseline - w / 2 - ry

    top = ellipse_points(cx, cy_top, rx, ry, 330, 150)
    bottom = ellipse_points(cx, cy_bottom, rx, ry, 330, 510)

    waist_start, waist_end = top[-1], bottom[0]
    overlap = (
        waist_start[0] + (waist_end[0] - waist_start[0]) * 0.34,
        waist_start[1] + (waist_end[1] - waist_start[1]) * 0.34,
    )

    lower = stroke_mask(size, [waist_start] + bottom, w, start_cap="fold", end_cap="round")
    upper = stroke_mask(size, top + [overlap], w, start_cap="cut", end_cap="fold")

    return [(lower, "b"), (upper, "a")], 2 * rx + w


def build_i(size, x, baseline, geo):
    xh, w = geo["xh"], geo["w"]
    stem_x = x + w / 2
    foot = 0.26 * xh
    elbow = 0.22 * xh
    tail_y = baseline - w / 2

    path = [(stem_x, baseline - xh), (stem_x, tail_y - elbow)]
    path += arc_points(stem_x + elbow, tail_y - elbow, elbow, 180, 90)
    path += [(stem_x + foot, tail_y)]

    diagonal = 1.22 * w
    dot_y = baseline - xh - 0.34 * w - diagonal / 2
    dot = diamond_mask(size, stem_x, dot_y, diagonal)

    return [
        (stroke_mask(size, path, w, start_cap="cut", end_cap="round"), "a"),
        (dot, "a"),
    ], w / 2 + foot


def _bowl_path(stem_x, right_x, y_top, y_bottom):
    """Bojo em D: barra reta, quina arredondada, lombo vertical e retorno."""
    radius = min((y_bottom - y_top) / 2.0, (right_x - stem_x) * 0.62)
    path = [(stem_x, y_top), (right_x - radius, y_top)]
    path += arc_points(right_x - radius, y_top + radius, radius, 270, 360)
    path += [(right_x, y_bottom - radius)]
    path += arc_points(right_x - radius, y_bottom - radius, radius, 0, 90)
    path += [(stem_x, y_bottom)]
    return path


def build_b(size, x, baseline, geo):
    cap, w = geo["cap"], geo["w"]
    counter_total = cap - 3 * w
    counter_top = counter_total * 0.46

    stem_x = x + w / 2
    y_cap = baseline - cap + w / 2
    y_waist = y_cap + w + counter_top
    y_base = baseline - w / 2

    upper_right = x + 0.76 * cap - w / 2
    lower_right = x + 0.82 * cap - w / 2

    upper = stroke_mask(
        size,
        _bowl_path(stem_x, upper_right, y_cap, y_waist),
        w,
        start_cap="fold",
        end_cap="fold",
    )
    lower = stroke_mask(
        size,
        _bowl_path(stem_x, lower_right, y_waist, y_base),
        w,
        start_cap="fold",
        end_cap="fold",
    )
    stem = stroke_mask(
        size,
        [(stem_x, y_cap - w / 2), (stem_x, y_base + w / 2)],
        w,
        start_cap="cut",
        end_cap="cut",
    )

    return [(lower, "b"), (upper, "b"), (stem, "a")], 0.82 * cap


def build_a(size, x, baseline, geo):
    xh, w = geo["xh"], geo["w"]
    r_out = xh / 2.0
    bowl = ring_mask(size, x + r_out, baseline - r_out, r_out, r_out - w)
    stem_x = x + xh - w / 2
    stem = stroke_mask(
        size,
        [(stem_x, baseline - xh), (stem_x, baseline)],
        w,
        start_cap="cut",
        end_cap="cut",
    )
    # A haste fica por cima do bojo: e o que diferencia o "a" de um "o".
    return [(bowl, "b"), (stem, "a")], xh


def build_f(size, x, baseline, geo):
    """Haste vertical com dois bracos horizontais, ambos recolhidos atras dela."""
    cap, w = geo["cap"], geo["w"]
    advance = 0.70 * cap

    stem_x = x + w / 2
    y_cap = baseline - cap + w / 2
    y_waist = baseline - cap * 0.50

    arm_top = stroke_mask(
        size,
        [(stem_x, y_cap), (x + advance, y_cap)],
        w,
        start_cap="fold",
        end_cap="cut",
    )
    arm_waist = stroke_mask(
        size,
        [(stem_x, y_waist), (x + advance * 0.78, y_waist)],
        w,
        start_cap="fold",
        end_cap="cut",
    )
    stem = stroke_mask(
        size,
        [(stem_x, y_cap - w / 2), (stem_x, baseline)],
        w,
        start_cap="cut",
        end_cap="cut",
    )

    return [(arm_top, "b"), (arm_waist, "b"), (stem, "a")], advance


LETTER_BUILDERS = {
    "F": build_f,
    "W": build_w,
    "g": build_g,
    "C": build_c,
    "h": build_h,
    "S": build_s,
    "i": build_i,
    "B": build_b,
    "a": build_a,
}


# ---------------------------------------------------------------------------
# Composicao do icone
# ---------------------------------------------------------------------------


def draw_card(size):
    """Card branco squircle com sombra suave, identico ao do launcher."""
    margin = int(size * CARD_MARGIN)
    radius = int(size * CARD_RADIUS)
    box = [margin, margin, size - margin, size - margin]

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    offset = int(size * 0.012)
    ImageDraw.Draw(shadow).rounded_rectangle(
        [box[0], box[1] + offset, box[2], box[3] + offset], radius=radius, fill=(0, 0, 0, 36)
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=size * 0.022))

    canvas = Image.alpha_composite(canvas, shadow)
    ImageDraw.Draw(canvas).rounded_rectangle(box, radius=radius, fill=(255, 255, 255, 255))
    return canvas


def layout_monogram(size, letters):
    """Constroi as fitas das duas letras e as centraliza dentro do card."""
    geo = {
        "cap": size * CAP_HEIGHT,
        "xh": size * CAP_HEIGHT * X_HEIGHT_RATIO,
        "w": size * RIBBON_WIDTH,
    }
    gap = size * LETTER_GAP
    baseline = size * 0.66

    layers = []
    cursor = size * 0.16
    for index, char in enumerate(letters):
        built, advance = LETTER_BUILDERS[char](size, cursor, baseline, geo)
        layers.extend(built)
        cursor += advance + (gap if index == 0 else 0)

    union = layers[0][0].copy()
    for mask, _ in layers[1:]:
        union = ImageChops.lighter(union, mask)
    bbox = union.getbbox()

    ink_w = bbox[2] - bbox[0]
    ink_h = bbox[3] - bbox[1]
    scale = min(1.0, size * INK_MAX_WIDTH / ink_w, size * INK_MAX_HEIGHT / ink_h)

    target_w = max(1, int(round(ink_w * scale)))
    target_h = max(1, int(round(ink_h * scale)))
    left = (size - target_w) // 2
    top = (size - target_h) // 2

    placed = []
    for mask, face in layers:
        cropped = mask.crop(bbox).resize((target_w, target_h), Image.LANCZOS)
        canvas = Image.new("L", (size, size), 0)
        canvas.paste(cropped, (left, top))
        placed.append((canvas, face))

    return placed, geo["w"] * scale


def compose_icon(app, size):
    """Card + sombra projetada do monograma + fitas + sombras de dobra."""
    icon = draw_card(size)
    layers, ribbon_w = layout_monogram(size, app["letters"])

    light, dark, fold = app["light"], app["dark"], app["fold"]
    crease_strength = app.get("crease_strength", 170)
    faces = {
        "a": (light, mix(light, dark, 0.58)),
        "b": (mix(light, dark, 0.24), dark),
    }

    union = layers[0][0].copy()
    for mask, _ in layers[1:]:
        union = ImageChops.lighter(union, mask)

    cast = Image.new("L", (size, size), 0)
    cast.paste(union, (0, int(ribbon_w * 0.10)))
    cast = cast.filter(ImageFilter.GaussianBlur(radius=ribbon_w * 0.30))
    cast = cast.point(lambda v: (v * 46) // 255)
    drop = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    drop.putalpha(cast)
    icon = Image.alpha_composite(icon, drop)

    offset = max(1, int(round(ribbon_w * 0.20)))
    blur = ribbon_w * 0.26
    drawn = Image.new("L", (size, size), 0)

    for mask, face in layers:
        shade = Image.new("L", (size, size), 0)
        shade.paste(mask, (offset, offset))
        shade = shade.filter(ImageFilter.GaussianBlur(radius=blur))
        shade = ImageChops.multiply(shade, drawn)
        shade = ImageChops.multiply(shade, ImageChops.invert(mask))
        shade = shade.point(lambda v: (v * crease_strength) // 255)

        crease = Image.new("RGBA", (size, size), fold + (255,))
        crease.putalpha(shade)
        icon = Image.alpha_composite(icon, crease)

        icon = Image.alpha_composite(icon, paint_ribbon(mask, *faces[face]))
        drawn = ImageChops.lighter(drawn, mask)

    return icon


# ---------------------------------------------------------------------------
# Icone do launcher
# ---------------------------------------------------------------------------


def rebuild_launcher_icon(src_path, size=1024):
    """Recompoe o card do launcher preservando a fita 'R' e a transparencia externa."""
    source = Image.open(src_path).convert("RGBA")
    src_w, src_h = source.size
    pixels = source.load()

    icon = draw_card(size)
    ribbon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ribbon_px = ribbon.load()

    scale_x = src_w / size
    scale_y = src_h / size
    for y in range(int(size * 0.14), int(size * 0.86)):
        for x in range(int(size * 0.16), int(size * 0.84)):
            r, g, b, a = pixels[int(x * scale_x), int(y * scale_y)]
            if a > 0 and not (r >= 248 and g >= 248 and b >= 248):
                ribbon_px[x, y] = (r, g, b, a)

    return Image.alpha_composite(icon, ribbon)


# ---------------------------------------------------------------------------
# Execucao
# ---------------------------------------------------------------------------


def main():
    preview_dir = None
    if "--preview" in sys.argv:
        preview_dir = Path(sys.argv[sys.argv.index("--preview") + 1])
        preview_dir.mkdir(parents=True, exist_ok=True)

    root = Path(__file__).resolve().parent.parent
    render_size = 2048

    print("Launcher")
    launcher_src = root / "design" / "branding" / "ribanense-icon-source.png"
    launcher = rebuild_launcher_icon(launcher_src, size=1024)
    launcher.save(launcher_src, format="PNG")

    launcher_256 = launcher.resize((256, 256), Image.LANCZOS)
    launcher_256.save(root / "src/Ribanense.Solucoes.Launcher/Assets/app-256.png", format="PNG")
    launcher_256.save(root / "catalog/icons/launcher.png", format="PNG")
    launcher.save(
        root / "src/Ribanense.Solucoes.Launcher/Assets/app.ico",
        format="ICO",
        sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)],
    )
    print("  assets do launcher atualizados")

    for app in APPS:
        print(f"{app['name']} [{app['letters']}]")
        master = compose_icon(app, render_size)
        icon_256 = master.resize((256, 256), Image.LANCZOS)
        icon_256.save(root / app["app_dir"] / "icon.png", format="PNG")
        icon_256.save(root / app["catalog_icon"], format="PNG")
        if preview_dir is not None:
            master.resize((512, 512), Image.LANCZOS).save(preview_dir / f"{app['slug']}.png")
        print(f"  {app['app_dir']}/icon.png e {app['catalog_icon']}")


if __name__ == "__main__":
    main()
