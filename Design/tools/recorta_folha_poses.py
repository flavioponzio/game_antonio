"""Recorta os quadros da folha de poses do Antônio, normaliza a escala e grava frames.json.
Uso: python3 -I cut_frames.py <folha.png|webp> <pasta_saida>"""
import sys, json, os
import numpy as np, cv2
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
im = np.array(Image.open(src).convert('RGB'))
# (nome, caixa x,y,w,h já com margem de 4px, linha da folha)
boxes = {
 'front': (149,22,136,332,1), 'front34': (427,22,136,331,1), 'side': (685,24,128,328,1), 'back34': (953,25,131,329,1), 'back': (1229,24,137,332,1),
 'walk1': (96,403,145,252,2), 'walk2': (395,404,151,253,2), 'run1': (678,409,179,248,2), 'run2': (946,414,176,246,2), 'blink': (1303,406,119,259,2),
 'jump': (84,704,163,220,3), 'land': (398,752,152,206,3), 'crouch': (696,784,130,175,3), 'lookUp': (979,729,116,232,3), 'lookDown': (1286,749,131,212,3),
}
# escala: as linhas 2 e 3 estão menores; normaliza pela altura de "front" (linha 1) vs "blink" (pose parada da linha 2)
ROW_SCALE = {1: 1.0, 2: (332 - 8) / (259 - 8), 3: (332 - 8) / (259 - 8)}
UP = 1.5  # amplia tudo para ficar mais nítido no jogo
meta = {}
for name, (x, y, w, h, row) in boxes.items():
    pad = 6
    c = im[max(0, y - pad):y + h + pad, max(0, x - pad):x + w + pad].copy()
    H, W = c.shape[:2]
    nw = (c.astype(int).sum(2) > 735).astype(np.uint8)
    m = np.zeros((H + 2, W + 2), np.uint8)
    for p in [(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)]:
        if nw[p[1], p[0]]: cv2.floodFill(nw, m, p, 2)
    alpha = np.where(nw == 2, 0, 255).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats((alpha > 0).astype(np.uint8), 8)
    keep = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    alpha[lab != keep] = 0
    a2 = cv2.GaussianBlur(alpha, (3, 3), 0); a2[cv2.erode(alpha, np.ones((3, 3), np.uint8)) > 0] = 255
    ys, xs = np.where(a2 > 0)
    rgba = np.dstack([c, a2])[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    img = Image.fromarray(rgba, 'RGBA')
    s = ROW_SCALE[row] * UP
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    A = np.array(img)[..., 3] > 0
    hh = A.shape[0]
    # pivô: x = centro do tronco/cabeça (linhas de 15% a 55% da altura), y = base (pés)
    band = A[int(hh * 0.15):int(hh * 0.55)]
    px = float(np.where(band)[1].mean())
    img.save(f'{out}/{name}.png')
    meta[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'size': [img.width, img.height],
                  'pivot': [round(px / img.width, 4), 0.0]}
ref = meta['front']['size'][1]
json.dump({'note': 'Quadros do Antônio (folha de poses). pivot = (x do tronco, base) normalizado 0..1. '
                   'refHeight = altura em px do quadro "front" (define a escala de todos).',
           'refHeight': ref, 'facing': 1, 'frames': meta}, open(f'{out}/frames.json', 'w'), indent=1)
print(json.dumps({k: v['size'] for k, v in meta.items()}))
