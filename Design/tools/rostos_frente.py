"""Troca só o rosto da vista de frente do modelo pelos rostos de outra imagem (piscando, falando...).
O resto do corpo continua o do modelo, então os quadros ficam alinhados pixel a pixel com a frente.
Uso: python3 -I rostos_frente.py <modelo_frente.png> <imagem_com_variacoes.png> <pasta_saida> nome1 nome2 ... [--shift dx dy]
--shift: onde fica o rosto quando a imagem base não é a frente do modelo (ex.: a frente segurando a garra)."""
import sys
import numpy as np, cv2
from PIL import Image
args = sys.argv[1:]; SHIFT = (0, 0)
if '--shift' in args:
    k = args.index('--shift'); SHIFT = (int(args[k + 1]), int(args[k + 2])); args = args[:k] + args[k + 3:]
model_p, var_p, out = args[:3]; names = args[3:]
M = np.array(Image.open(model_p).convert('RGBA')); H, W = M.shape[:2]
V = np.array(Image.open(var_p).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((V[..., 3] > 40).astype(np.uint8), 8)
figs = sorted([i for i in range(1, n) if st[i, 4] > 3000], key=lambda i: st[i, 0])
FACE = (72 + SHIFT[0], 92 + SHIFT[1], 238 + SHIFT[0], 228 + SHIFT[1])   # rosto (sobrancelhas até o queixo) na frente do modelo
HEAD_Y = 250 + SHIFT[1]
yy, xx = np.mgrid[0:H, 0:W]
cx, cy = (FACE[0] + FACE[2]) / 2, (FACE[1] + FACE[3]) / 2; rx, ry = (FACE[2] - FACE[0]) / 2, (FACE[3] - FACE[1]) / 2
ell = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
face_mask = np.clip((1.15 - ell) / 0.3, 0, 1)          # 1 no meio do rosto, some suave na borda
# piscar troca só os olhos (com as sobrancelhas); falar troca só a boca — o resto do rosto fica o da frente
REGIONS = {'eyes': (155 + SHIFT[0], 146 + SHIFT[1], 92, 56), 'mouth': (154 + SHIFT[0], 206 + SHIFT[1], 44, 24)}   # cx, cy, rx, ry
def region_mask(name):
    cxr, cyr, rxr, ryr = REGIONS['eyes' if name == 'blink' else 'mouth']
    e = ((xx - cxr) / rxr) ** 2 + ((yy - cyr) / ryr) ** 2
    return np.clip((1.15 - e) / 0.3, 0, 1)
for name, i in zip(names, figs):
    x, y, w, h = st[i, :4]
    c = V[y:y + h, x:x + w].copy(); c[lab[y:y + h, x:x + w] != i, 3] = 0
    best = None
    for s in np.linspace(0.96, 1.04, 9) * (H / h):
        cs = cv2.resize(c, (round(w * s), round(h * s)), interpolation=cv2.INTER_AREA)
        T = cs[:HEAD_Y].astype(np.float32); R = M[:HEAD_Y + 40].astype(np.float32)
        Rp = np.zeros((R.shape[0] + 40, max(R.shape[1], T.shape[1]) + 40, 4), np.float32); Rp[20:20 + R.shape[0], 20:20 + R.shape[1]] = R
        mk = ((T[..., 3] > 200) & ~(cv2.resize(face_mask[:HEAD_Y], (T.shape[1], T.shape[0])) > 0.01)).astype(np.uint8) * 255
        r = cv2.matchTemplate(Rp[..., :3], T[..., :3], cv2.TM_SQDIFF, mask=np.dstack([mk] * 3))
        mn, _, loc, _ = cv2.minMaxLoc(r); mn /= max(1, (mk > 0).sum())
        if best is None or mn < best[0]: best = (mn, s, loc[0] - 20, loc[1] - 20, cs)
    err, s, ox, oy, cs = best
    print(name, 'escala', round(s, 3), 'desloc', ox, oy, 'erro', round(err))
    # variação no espaço do modelo
    P = np.zeros_like(M)
    ys0, xs0 = max(0, oy), max(0, ox); ys1, xs1 = min(H, oy + cs.shape[0]), min(W, ox + cs.shape[1])
    P[ys0:ys1, xs0:xs1] = cs[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox]
    # acerta a cor da pele pela média do anel em volta do rosto
    ring = (ell > 0.85) & (ell < 1.15) & (M[..., 3] > 200) & (P[..., 3] > 200)
    diff = M[ring][:, :3].astype(float).mean(0) - P[ring][:, :3].astype(float).mean(0)
    Pc = np.clip(P[..., :3].astype(float) + diff, 0, 255)
    k = region_mask(name)[..., None] * (P[..., 3:] > 200)
    outp = M.copy(); outp[..., :3] = (M[..., :3] * (1 - k) + Pc * k).astype(np.uint8)
    Image.fromarray(outp).save(f'{out}/{name}.png')

# Depois, no jogo: cada imagem é reduzida para o tamanho do quadro "front" do frames.json e ganha o mesmo pivô e mão
# (blink, talk, talkHalf); "talkCycle" no frames.json define a ordem da boca falando.
