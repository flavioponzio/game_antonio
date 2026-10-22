"""Recorta a folha de poses (5x4, com a garra) em quadros com e sem garra e grava frames.json.
Uso: python3 -I cut_frames2.py <folha> <measure.json> <pasta_saida>"""
import sys, json, os
import numpy as np, cv2
from PIL import Image
src, mjson, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
im = np.array(Image.open(src).convert('RGB'))
boxes = json.load(open(mjson))
ROWS = {'front':0,'front34':0,'side':0,'back34':0,'back':0,'walk1':1,'walk2':1,'run1':1,'run2':1,'blink':1,
        'jump':2,'land':2,'crouch':2,'lookUp':2,'lookDown':2,'sit':3,'walk3':3,'walk4':3,'talk':3,'pickUp':3}
# escala por linha: meio-termo (média geométrica) entre igualar a altura em pé e igualar a largura da cabeça
ROW_SCALE = [1.0, 1.136, 1.178, 1.219]
UP = 2.0
def alpha_of(c):
    H, W = c.shape[:2]
    nw = (c.astype(int).sum(2) > 735).astype(np.uint8); m = np.zeros((H + 2, W + 2), np.uint8)
    for p in [(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)]:
        if nw[p[1], p[0]]: cv2.floodFill(nw, m, p, 2)
    a = np.where(nw == 2, 0, 255).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats((a > 0).astype(np.uint8), 8)
    # fundo branco preso entre braço/garra/pernas (não alcançado pelo flood fill): áreas grandes e uniformes
    ssum = c.astype(int).sum(2)
    holes = ((ssum >= 754) & (a > 0)).astype(np.uint8)
    nh, labh, sth, _ = cv2.connectedComponentsWithStats(holes, 8)
    for i in range(1, nh):
        if sth[i, cv2.CC_STAT_AREA] >= 60 and ssum[labh == i].mean() >= 758:
            a[cv2.dilate((labh == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0] = 0
    n, lab, st, _ = cv2.connectedComponentsWithStats((a > 0).astype(np.uint8), 8)
    hsv0 = cv2.cvtColor(c, cv2.COLOR_RGB2HSV).astype(int)
    yel = (hsv0[..., 0] >= 17) & (hsv0[..., 0] <= 34) & (hsv0[..., 1] > 100) & (hsv0[..., 2] > 130)
    keep = {1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))}
    keep |= {i for i in range(1, n) if (yel & (lab == i)).sum() >= 20}   # garra solta do corpo também fica
    a[~np.isin(lab, list(keep))] = 0
    return a
def soften(a):
    a2 = cv2.GaussianBlur(a, (3, 3), 0); a2[cv2.erode(a, np.ones((3, 3), np.uint8)) > 0] = 255; return a2
meta = {}
for name, b in boxes.items():
    x, y, w, h = b['box']; pad = 8
    c = im[max(0, y - pad):y + h + pad, max(0, x - pad):x + w + pad].copy()
    a = alpha_of(c)
    I = c.astype(int)
    # --- versão sem garra: tira amarelo + contorno; preenche onde ela passava na frente do corpo
    hsv = cv2.cvtColor(c, cv2.COLOR_RGB2HSV).astype(int)
    yellow = (hsv[..., 0] >= 17) & (hsv[..., 0] <= 34) & (hsv[..., 1] > 100) & (hsv[..., 2] > 130) & (a > 0)
    n_, lab_, st_, _ = cv2.connectedComponentsWithStats(yellow.astype(np.uint8), 8)
    yellow = np.isin(lab_, [i for i in range(1, n_) if st_[i, cv2.CC_STAT_AREA] >= 25])
    core = yellow.astype(np.uint8)
    near_wide = cv2.dilate(core, np.ones((23, 23), np.uint8)) > 0
    near = cv2.dilate(core, np.ones((11, 11), np.uint8)) > 0
    # amarelado fraco (bordas borradas do cabo) e contorno escuro colado na garra
    yellowish = (hsv[..., 0] >= 13) & (hsv[..., 0] <= 38) & (hsv[..., 1] > 45) & (hsv[..., 2] > 95)
    skin = (hsv[..., 0] >= 5) & (hsv[..., 0] <= 16) & (hsv[..., 1] < 140) & (hsv[..., 2] > 170)
    grab = (yellow | (near_wide & yellowish & ~skin) | (near & (I.sum(2) < 430))) & (a > 0)
    grab = cv2.dilate(grab.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & (a > 0) & ~(skin & ~near)
    body = (a > 0) & ~grab
    n, lab, st, _ = cv2.connectedComponentsWithStats(body.astype(np.uint8), 8)
    body = lab == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    # restos finos do cabo grudados na mão: abertura só perto da garra
    opened = cv2.morphologyEx(body.astype(np.uint8), cv2.MORPH_OPEN, np.ones((7, 7), np.uint8)) > 0
    body = np.where(near_wide, opened, body)
    n, lab, st, _ = cv2.connectedComponentsWithStats(body.astype(np.uint8), 8)
    body = lab == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    closed = cv2.morphologyEx(body.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8)) > 0
    fill = grab & closed & ~body
    c2 = cv2.inpaint(c, fill.astype(np.uint8) * 255, 4, cv2.INPAINT_TELEA)
    fill &= c2.astype(int).sum(2) < 690   # preenchimento quase branco = era fundo: fica transparente
    # halo cinza-claro do borrado da garra, do lado de fora do corpo
    sat = hsv[..., 1]
    inner = cv2.erode((body | fill).astype(np.uint8), np.ones((9, 9), np.uint8)) > 0
    halo = near_wide & (I.sum(2) > 600) & (sat < 45) & ~inner
    body &= ~halo; fill &= ~halo
    a_ng = np.where(body | fill, 255, 0).astype(np.uint8)
    n2, lab2, st2, _ = cv2.connectedComponentsWithStats((a_ng > 0).astype(np.uint8), 8)
    a_ng[lab2 != 1 + int(np.argmax(st2[1:, cv2.CC_STAT_AREA]))] = 0   # só o personagem (tira objeto do chão etc.)
    a_ng = cv2.morphologyEx(a_ng, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    s = ROW_SCALE[ROWS[name]] * UP
    def save(rgb, al, fname):
        al = soften(al); ys, xs = np.where(al > 0)
        img = Image.fromarray(np.dstack([rgb, al]), 'RGBA')
        return img
    with_g = save(c, a, None); no_g = save(c2, a_ng, None)
    # mesma moldura para as duas versões (a da versão com garra), redimensionada igual
    ys, xs = np.where(np.array(with_g)[..., 3] > 0); box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    with_g, no_g = with_g.crop(box), no_g.crop(box)
    size = (round(with_g.width * s), round(with_g.height * s))
    with_g = with_g.resize(size, Image.LANCZOS); no_g = no_g.resize(size, Image.LANCZOS)
    A = np.array(no_g)[..., 3] > 0; hh = A.shape[0]
    band = A[int(hh * 0.12):int(hh * 0.5)]; px = float(np.where(band)[1].mean())
    with_g.save(f'{out}/{name}-garra.png'); no_g.save(f'{out}/{name}.png')
    meta[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'fileHolding': f'assets/characters/antonio-frames/{name}-garra.png',
                  'size': list(size), 'pivot': [round(px / size[0], 4), 0.0]}
# ---- acabamento das versões sem garra ----
def mirror_hand(path, A, sx0, sx1, y0, y1, dx0, dx1):
    im2 = np.array(Image.open(path).convert('RGBA')); out2 = im2.copy(); J = im2[..., :3].astype(int)
    navy = (J[..., 2] - J[..., 0] > 12) & (J.sum(2) < 400)
    reg = np.zeros(im2.shape[:2], bool); reg[y0:y1, dx0:dx1] = True
    out2[reg & ~navy, 3] = 0
    for yy in range(y0, y1):
        for xx in range(sx0, sx1):
            if im2[yy, xx, 3] > 0 and not navy[yy, xx] and 0 <= A - xx < im2.shape[1]: out2[yy, A - xx] = im2[yy, xx]
    Image.fromarray(out2, 'RGBA').save(path)
# pose de frente: o punho tinha o furo do cabo -> usa a mão aberta do outro lado, espelhada
mirror_hand(f'{out}/front.png', 316, 219, 259, 298, 358, 52, 101)
for name in meta:
    path = f'{out}/{name}.png'
    im2 = np.array(Image.open(path).convert('RGBA')); rgb = np.ascontiguousarray(im2[..., :3]); hsv2 = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV).astype(int)
    left = (hsv2[..., 0] >= 17) & (hsv2[..., 0] <= 36) & (hsv2[..., 1] > 70) & (hsv2[..., 2] > 120) & (im2[..., 3] > 0)
    if left.any():
        rgb = cv2.inpaint(rgb, cv2.dilate(left.astype(np.uint8), np.ones((3, 3), np.uint8)) * 255, 3, cv2.INPAINT_TELEA)
        Image.fromarray(np.dstack([rgb, im2[..., 3]]), 'RGBA').save(path)
# sentado: pivô = ponto do bumbum sobre o assento (medido no quadro)
meta['sit']['pivot'] = [round(75 / meta['sit']['size'][0], 4), round(1 - 352 / meta['sit']['size'][1], 4)]
json.dump({'note': 'Quadros do Antônio (folha de poses 5x4). file = sem garra, fileHolding = com o braço mecânico. '
                   'pivot = (x do tronco, base) normalizado 0..1. refHeight = altura em px do quadro "front". Os quadros olham para a direita.',
           'refHeight': meta['front']['size'][1], 'walkCycle': ['walk3', 'walk4'], 'frames': meta}, open(f'{out}/frames.json', 'w'), indent=1)
print({k: v['size'] for k, v in meta.items()})
