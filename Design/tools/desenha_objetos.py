"""Desenha objetos pequenos no estilo do jogo (contorno marrom irregular + textura de lápis de cor).
Uso: python3 -I draw_props.py <pasta_saida>"""
import sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
out = sys.argv[1]
INK = (59, 38, 24, 255)
rnd = random.Random(7)

def wobble(points, amp=0.9, step=14):
    """Contorno levemente irregular, como traço à mão."""
    res = []
    for i in range(len(points)):
        (x0, y0), (x1, y1) = points[i], points[(i + 1) % len(points)]
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        for k in range(n):
            t = k / n
            res.append((x0 + (x1 - x0) * t + rnd.uniform(-amp, amp), y0 + (y1 - y0) * t + rnd.uniform(-amp, amp)))
    return res

def rrect(x0, y0, x1, y1, r):
    pts = []
    for cx, cy, a0 in [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]:
        for k in range(7):
            a = math.radians(a0 + k * 15)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts

def texture(img, strength=14):
    """Textura de lápis de cor: ruído + hachura diagonal leve dentro das áreas pintadas."""
    a = np.array(img).astype(int)
    h, w = a.shape[:2]
    noise = np.random.RandomState(3).randint(-strength, strength + 1, (h, w, 1))
    yy, xx = np.mgrid[0:h, 0:w]
    hatch = ((xx + yy) % 7 == 0)[..., None] * -10
    mask = (a[..., 3:4] > 0) & (a[..., :3].sum(2, keepdims=True) > 200)
    a[..., :3] = np.where(mask, np.clip(a[..., :3] + noise + hatch, 0, 255), a[..., :3])
    return Image.fromarray(a.astype(np.uint8), 'RGBA')

def shape(d, pts, fill, width=4):
    p = wobble(pts)
    d.polygon(p, fill=fill)
    d.line(p + [p[0]], fill=INK, width=width, joint='curve')

# ---------- Estojo: visto de cima, 3 divisões com 5 lápis cada ----------
W, H = 720, 400
img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
shape(d, rrect(20, 30, W - 20, H - 20, 46), (74, 120, 194, 255), 6)          # tecido azul (cor da gola)
shape(d, rrect(48, 56, W - 48, H - 46, 30), (238, 228, 205, 255), 5)         # forro claro
colors = [(216, 67, 44), (243, 183, 46), (88, 160, 80), (74, 120, 194), (150, 90, 170)]
cw = (W - 96) / 3
for c in range(3):
    x0 = 48 + c * cw
    if c > 0: d.line(wobble([(x0, 62), (x0, H - 52)], 1.2)[::1], fill=INK, width=5)
    for k in range(5):
        px = x0 + 22 + k * (cw - 44) / 4
        top, bot = 82 + rnd.uniform(-6, 6), H - 74 + rnd.uniform(-6, 6)
        col = colors[(k + c) % 5]
        body = [(px - 9, top + 34), (px + 9, top + 34), (px + 9, bot), (px - 9, bot)]
        shape(d, body, col + (255,), 3)
        shape(d, [(px - 9, top + 34), (px + 9, top + 34), (px, top + 6)], (233, 200, 150, 255), 3)   # madeira apontada
        shape(d, [(px - 3.5, top + 17), (px + 3.5, top + 17), (px, top + 6)], col + (255,), 2)       # ponta colorida
d.line(wobble([(70, 34), (W - 70, 34)], 1), fill=(200, 200, 200, 255), width=6)                       # zíper
img = texture(img).filter(ImageFilter.GaussianBlur(0.7))
img.save(f'{out}/prop-estojo.png')

# ---------- Apontador: vermelho, de lado ----------
W, H = 300, 220
img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
shape(d, [(40, 60), (230, 50), (262, 90), (262, 190), (40, 190)], (216, 67, 44, 255), 6)
shape(d, [(150, 75), (238, 70), (238, 110), (150, 112)], (200, 200, 205, 255), 4)                  # lâmina
d.ellipse([70, 95, 130, 155], fill=INK); d.ellipse([84, 109, 116, 141], fill=(90, 40, 30, 255))   # furo
d.line([(160, 92), (228, 89)], fill=(120, 120, 130, 255), width=3)
img = texture(img).filter(ImageFilter.GaussianBlur(0.7))
img.save(f'{out}/prop-apontador.png')
print('ok')
