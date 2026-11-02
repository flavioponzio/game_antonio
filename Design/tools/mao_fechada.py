"""Versões "segurando o braço mecânico" das vistas do modelo: cola só a MÃO FECHADA de uma imagem nova sobre o quadro que
já está no jogo (o resto do desenho não muda) e põe a garra passando por dentro do punho.
Grava <nome>-garra.png e fileHolding/pivotHolding no frames.json. Também vale para os quadros que usam o corpo da frente
(blink, talk, talkHalf).
Uso: python3 -I mao_fechada.py <vistas_mao_fechada.png> <braco-mecanico-aberto.png> <pasta antonio-frames>"""
import sys, json
import numpy as np, cv2
from PIL import Image
src, garra_p, D = sys.argv[1:4]
fj = f'{D}/frames.json'; meta = json.load(open(fj)); frames = meta['frames']; REF = meta['refHeight']
V = np.array(Image.open(src).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((V[..., 3] > 40).astype(np.uint8), 8)
figs = sorted([i for i in range(1, n) if st[i, 4] > 3000], key=lambda i: st[i, 0])
VIEWS = ['front', 'front34', 'side', 'back']
SAME_BODY = {'front': ['blink', 'talk', 'talkHalf'], 'back': ['back34']}
R_HAND = 34                                          # raio da região da mão, em px do quadro (altura ~484)

# garra na escala do quadro, pegada = centro do cabo azul
G0 = Image.open(garra_p).convert('RGBA'); Ga = np.array(G0)
hsvg = cv2.cvtColor(np.ascontiguousarray(Ga[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
bys, bxs = np.where((hsvg[..., 0] >= 95) & (hsvg[..., 0] <= 130) & (hsvg[..., 1] > 100) & (Ga[..., 3] > 100))
gs = 0.27 * REF / (Ga.shape[0] - bys.mean())
G0 = G0.resize((round(G0.width * gs), round(G0.height * gs)), Image.LANCZOS); ggx, ggy = bxs.mean() * gs, bys.mean() * gs

def align(fig_i, base):
    """recorta a figura nova e acha escala/deslocamento que casam com o quadro (sem olhar a região da mão)"""
    x, y, w, h = st[fig_i, :4]
    c = V[y:y + h, x:x + w].copy(); c[lab[y:y + h, x:x + w] != fig_i, 3] = 0
    B = base.astype(np.float32); H0 = B.shape[0]
    best = None
    for s in np.linspace(0.97, 1.03, 7) * (H0 / h):
        cs = cv2.resize(c, (round(w * s), round(h * s)), interpolation=cv2.INTER_AREA)
        P = np.zeros((H0 + 40, max(B.shape[1], cs.shape[1]) + 40, 4), np.float32); P[20:20 + H0, 20:20 + B.shape[1]] = B
        T = cs[: int(cs.shape[0] * 0.5)].astype(np.float32)          # cabeça e tronco: iguais nas duas imagens
        mk = (T[..., 3] > 200).astype(np.uint8) * 255
        if T.shape[0] > P.shape[0] or T.shape[1] > P.shape[1]: continue
        r = cv2.matchTemplate(P[..., :3], T[..., :3], cv2.TM_SQDIFF, mask=np.dstack([mk] * 3))
        mn, _, loc, _ = cv2.minMaxLoc(r); mn /= max(1, (mk > 0).sum())
        if best is None or mn < best[0]: best = (mn, cs, loc[0] - 20, loc[1] - 20)
    _, cs, ox, oy = best
    out = np.zeros_like(base)
    ys0, xs0 = max(0, oy), max(0, ox); ys1, xs1 = min(base.shape[0], oy + cs.shape[0]), min(base.shape[1], ox + cs.shape[1])
    out[ys0:ys1, xs0:xs1] = cs[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox]
    return out

def fist_center(img, near):
    hsv = cv2.cvtColor(np.ascontiguousarray(img[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    skin = (hsv[..., 0] <= 20) & (hsv[..., 1] > 50) & (hsv[..., 2] > 140) & (img[..., 3] > 100)
    yy, xx = np.mgrid[0:img.shape[0], 0:img.shape[1]]
    m = skin & ((xx - near[0]) ** 2 + (yy - near[1]) ** 2 <= (R_HAND * 1.6) ** 2)
    ys, xs = np.where(m); return float(xs.mean()), float(ys.mean())

def holding(base, newfig, hand):
    """quadro com a mão fechada e a garra: garra na frente do corpo, mão fechada por cima"""
    hx, hy = hand
    fx, fy = fist_center(newfig, hand)
    H0, W0 = base.shape[:2]; yy, xx = np.mgrid[0:H0, 0:W0]
    d2 = (xx - fx) ** 2 + (yy - fy) ** 2
    region = d2 <= R_HAND ** 2
    soft = np.clip((R_HAND ** 2 - d2) / (R_HAND ** 2 * 0.35), 0, 1)
    # dedos abertos do desenho antigo somem na região; a mão nova entra (borda suave onde as duas são opacas: o pulso)
    out = base.copy()
    both = (base[..., 3] > 200) & (newfig[..., 3] > 200)
    k = np.where(both, soft, region.astype(float))[..., None]
    out = (base * (1 - k) + newfig * k).astype(np.uint8)
    # garra: pegada no centro do punho, desenhada na frente do corpo; o punho volta por cima
    pad = G0.height
    canvas = Image.new('RGBA', (W0 + 2 * pad, H0 + 2 * pad)); canvas.alpha_composite(Image.fromarray(out), (pad, pad))
    canvas.alpha_composite(G0, (int(round(pad + fx - ggx)), int(round(pad + fy - ggy))))
    hsv = cv2.cvtColor(np.ascontiguousarray(newfig[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    skin = (hsv[..., 0] <= 22) & (hsv[..., 1] > 40) & (hsv[..., 2] > 110) & (newfig[..., 3] > 100)
    fist = cv2.dilate((skin & region).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    # o furo do punho (onde o cabo passa) fica de fora: a garra aparece ali
    top = newfig.copy(); top[~fist, 3] = 0
    canvas.alpha_composite(Image.fromarray(top), (pad, pad))
    bb = canvas.getbbox(); return canvas.crop(bb), (bb[0] - pad, bb[1] - pad)

for view, fi in zip(VIEWS, figs):
    base = np.array(Image.open(f'{D}/{view}.png').convert('RGBA'))
    newfig = align(fi, base)
    for name in [view] + SAME_BODY.get(view, []):
        f = frames[name]; img = np.array(Image.open(f'{D}/{f["file"].split("/")[-1]}').convert('RGBA'))
        im, (dx, dy) = holding(img, newfig, f['hold']['hand'])
        im.save(f'{D}/{name}-garra.png')
        px, py = f['pivot']; W0, H0 = img.shape[1], img.shape[0]
        f['fileHolding'] = f'assets/characters/antonio-frames/{name}-garra.png'
        f['pivotHolding'] = [round((px * W0 - dx) / im.width, 4), round(1 - (H0 * (1 - py) - dy) / im.height, 4)]
        print(name, im.size, f['pivotHolding'])
json.dump(meta, open(fj, 'w'), indent=1, ensure_ascii=False)
