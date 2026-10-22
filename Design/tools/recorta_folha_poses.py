"""Recorta a folha de poses v3 (5x4, PNG com transparência, com a garra) em quadros com e sem garra + frames.json.
Uso: python3 -I cut_frames3.py <folha_solida.png> <measure.json> <pasta_saida>
(a folha passa antes por solidify.py, que deixa o interior opaco)"""
import sys, json, os
import numpy as np, cv2
from PIL import Image
src, mjson, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
sheet = np.array(Image.open(src).convert('RGBA'))
boxes = json.load(open(mjson))
ROWS = {'front':0,'front34':0,'side':0,'back34':0,'back':0,'walk1':1,'walk2':1,'run1':1,'run2':1,'blink':1,
        'jump':2,'land':2,'crouch':2,'lookUp':2,'lookDown':2,'sit':3,'walk3':3,'walk4':3,'talk':3,'pickUp':3}
# a folha desenha cada linha num tamanho: escala pela altura das poses em pé (blink, lookUp, talk = mesma altura do front)
ROW_SCALE = [1.0, 241 / 204, 241 / 195, 241 / 191]
FRAME_SCALE = {}
# onde a garra cobria muito do corpo (perfil: na frente do short e da perna), fecha um vão maior
CLOSE = {'side': 35, 'back': 21}
UP = 2.0
def yellow_of(hsv, a, s=100, v=130):
    return (hsv[..., 0] >= 17) & (hsv[..., 0] <= 34) & (hsv[..., 1] > s) & (hsv[..., 2] > v) & (a > 0)
def biggest(mask):
    n, lab, st, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    return lab == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])) if n > 1 else mask
def soften(a):
    a2 = cv2.GaussianBlur(a, (3, 3), 0); a2[cv2.erode(a, np.ones((3, 3), np.uint8)) > 0] = 255; return a2
meta = {}
for name, b in boxes.items():
    x, y, w, h = b['box']; pad = 8
    cr = sheet[max(0, y - pad):y + h + pad, max(0, x - pad):x + w + pad].copy()
    c = np.ascontiguousarray(cr[..., :3]); a0 = cr[..., 3]
    # só o personagem (tira letras e sujeira): componentes grandes
    n, lab, st, _ = cv2.connectedComponentsWithStats((a0 > 100).astype(np.uint8), 8)
    keep = [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= 150]
    a = np.where(np.isin(lab, keep) | ((a0 > 0) & (cv2.dilate(np.isin(lab, keep).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0)), a0, 0).astype(np.uint8)
    I = c.astype(int)
    hsv = cv2.cvtColor(c, cv2.COLOR_RGB2HSV).astype(int)
    # --- versão sem garra: tira amarelo + contorno; preenche onde ela passava na frente do corpo
    yellow = yellow_of(hsv, a)
    n_, lab_, st_, _ = cv2.connectedComponentsWithStats(yellow.astype(np.uint8), 8)
    yellow = np.isin(lab_, [i for i in range(1, n_) if st_[i, cv2.CC_STAT_AREA] >= 25])
    core = yellow.astype(np.uint8)
    near_wide = cv2.dilate(core, np.ones((19, 19), np.uint8)) > 0
    near = cv2.dilate(core, np.ones((9, 9), np.uint8)) > 0
    yellowish = (hsv[..., 0] >= 13) & (hsv[..., 0] <= 38) & (hsv[..., 1] > 45) & (hsv[..., 2] > 95)
    skin = (hsv[..., 0] >= 3) & (hsv[..., 0] <= 16) & (hsv[..., 1] > 25) & (hsv[..., 1] < 150) & (hsv[..., 2] > 160)
    grab = (yellow | (near_wide & yellowish & ~skin) | (near & (I.sum(2) < 400) & ~skin)) & (a > 0)
    grab = cv2.dilate(grab.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & (a > 0) & ~(skin & ~near)
    body = biggest((a > 0) & ~grab)
    opened = cv2.morphologyEx(body.astype(np.uint8), cv2.MORPH_OPEN, np.ones((7, 7), np.uint8)) > 0
    body = biggest(np.where(near_wide, opened, body))
    closed = cv2.morphologyEx(body.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((CLOSE.get(name, 7),) * 2, np.uint8)) > 0
    fill = grab & closed & ~body
    # preenche só com cores do corpo: tudo que não é corpo entra na máscara (o fundo transparente tem cor qualquer)
    solid_body = body & (a > 200)
    c2 = c.copy()
    c2[fill] = cv2.inpaint(c, (~solid_body).astype(np.uint8) * 255, 4, cv2.INPAINT_TELEA)[fill]
    a_ng = np.where(body, a, np.where(fill, 255, 0)).astype(np.uint8)
    a_ng[~biggest(a_ng > 0)] = 0
    a_ng = cv2.morphologyEx(a_ng, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    s = FRAME_SCALE.get(name, ROW_SCALE[ROWS[name]]) * UP
    with_g = Image.fromarray(np.dstack([c, a]), 'RGBA')
    no_g = Image.fromarray(np.dstack([c2, soften(a_ng)]), 'RGBA')
    ys, xs = np.where(a > 0); box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    with_g, no_g = with_g.crop(box), no_g.crop(box)
    size = (round(with_g.width * s), round(with_g.height * s))
    with_g = with_g.resize(size, Image.LANCZOS); no_g = no_g.resize(size, Image.LANCZOS)
    A = np.array(no_g)[..., 3] > 0; hh = A.shape[0]
    band = A[int(hh * 0.12):int(hh * 0.5)]; px = float(np.where(band)[1].mean())
    with_g.save(f'{out}/{name}-garra.png'); no_g.save(f'{out}/{name}.png')
    meta[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'fileHolding': f'assets/characters/antonio-frames/{name}-garra.png',
                  'size': list(size), 'pivot': [round(px / size[0], 4), 0.0]}
# restos amarelos nas versões sem garra: pinta com a cor vizinha
for name in meta:
    path = f'{out}/{name}.png'
    im2 = np.array(Image.open(path).convert('RGBA')); rgb = np.ascontiguousarray(im2[..., :3]); hsv2 = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV).astype(int)
    left = yellow_of(hsv2, im2[..., 3], 70, 120)
    if left.any():
        rgb = cv2.inpaint(rgb, cv2.dilate(left.astype(np.uint8), np.ones((3, 3), np.uint8)) * 255, 3, cv2.INPAINT_TELEA)
        Image.fromarray(np.dstack([rgb, im2[..., 3]]), 'RGBA').save(path)
# pose de frente: a garra cruzava a perna esquerda -> copia a perna direita, espelhada (eixo x = 318)
f = np.array(Image.open(f'{out}/front.png').convert('RGBA')); g = f.copy(); A = 318
for yy in range(350, 406):
    g[yy, 95:150] = 0
    for xx in range(168, 224):
        if 95 <= A - xx < 150: g[yy, A - xx] = f[yy, xx]
Image.fromarray(g, 'RGBA').save(f'{out}/front.png')
# pose de costas: a garra cobria a perna direita -> copia a esquerda, espelhada (eixo x = 182)
f = np.array(Image.open(f'{out}/back.png').convert('RGBA')); g = f.copy(); A = 182
for yy in range(362, 434):
    g[yy, 92:160] = 0
    for xx in range(22, 91):
        if 92 <= A - xx < 160: g[yy, A - xx] = f[yy, xx]
Image.fromarray(g, 'RGBA').save(f'{out}/back.png')
# sentado: pivô = ponto do bumbum sobre o assento (medido no quadro)
meta['sit']['pivot'] = [round(80 / meta['sit']['size'][0], 4), round(1 - 351 / meta['sit']['size'][1], 4)]
json.dump({'note': 'Quadros do Antônio (folha de poses v3, 5x4). file = sem garra, fileHolding = com o braço mecânico. '
                   'pivot = (x do tronco, base) normalizado 0..1. refHeight = altura em px do quadro "front". Os quadros olham para a direita.',
           'refHeight': meta['front']['size'][1], 'walkCycle': ['walk3', 'side', 'walk4', 'side'], 'frames': meta}, open(f'{out}/frames.json', 'w'), indent=1)
print({k: v['size'] for k, v in meta.items()})
