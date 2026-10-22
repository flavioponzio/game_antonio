"""Monta o ciclo de caminhada de 8 quadros a partir da folha de caminhada (2 linhas x 8, perfil, fundo transparente).
Linha 1 = sem garra. A versão com garra usa os MESMOS desenhos da linha 1 com a garra da linha 2 colada na mão de trás
(a linha 2 do ChatGPT não repete as mesmas pernas, então trocar de versão faria as pernas pularem).
Uso: python3 -I build_walk.py <folha_solida.png> <pasta_saida> <frames.json existente>"""
import sys, json, os
import numpy as np, cv2
from PIL import Image
src, out, fjson = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
sheet = np.array(Image.open(src).convert('RGBA'))
H, W = sheet.shape[:2]
SCALE = 1.37          # cabeça/altura iguais às outras poses (quadro mais alto do ciclo ~ altura em pé)
# ordem do ciclo (2 passos): contato, descida, passagem, subida — escolhida pela abertura dos pés e pela altura
ORDER = [1, 3, 4, 8, 5, 2, 7, 6]

def figures(y0, y1):
    a = (sheet[y0:y1, :, 3] > 100).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    figs = sorted([i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > 3000], key=lambda i: st[i, 0])
    return [(lab == i, st[i, :4], y0) for i in figs]
row1, row2 = figures(0, H // 2), figures(H // 2, H)
assert len(row1) == 8 and len(row2) == 8, (len(row1), len(row2))

def crop(fig, pad=6):
    m, (x, y, w, h), y0 = fig
    c = sheet[y0 + y - pad:y0 + y + h + pad, x - pad:x + w + pad].copy()
    mm = np.zeros(c.shape[:2], bool); mm[pad:pad + h, pad:pad + w] = m[y:y + h, x:x + w]
    mm = cv2.dilate(mm.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c[~mm, 3] = 0
    return c
def hsv_of(c): return cv2.cvtColor(np.ascontiguousarray(c[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
def back_hand(c):
    """centro da mão de trás: pele à esquerda do tronco, na altura do quadril"""
    h, w = c.shape[:2]; hsv = hsv_of(c)
    skin = (hsv[..., 0] <= 18) & (hsv[..., 1] > 40) & (hsv[..., 1] < 170) & (hsv[..., 2] > 150) & (c[..., 3] > 128)
    zone = np.zeros_like(skin); zone[int(h * 0.38):int(h * 0.66), :int(w * 0.5)] = True
    m = (skin & zone).astype(np.uint8)
    n, lab, st, cen = cv2.connectedComponentsWithStats(m, 8)
    i = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))
    return cen[i], lab == i

# --- garra recortada da linha 2, quadro 1, com a posição relativa à mão
g = crop(row2[0]); hsv = hsv_of(g)
yel = (hsv[..., 0] >= 15) & (hsv[..., 0] <= 36) & (hsv[..., 1] > 80) & (hsv[..., 2] > 110) & (g[..., 3] > 0)
n, lab, st, _ = cv2.connectedComponentsWithStats(yel.astype(np.uint8), 8)
yel = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] >= 30])
ring = cv2.dilate(yel.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
dark = g[..., :3].astype(int).sum(2) < 330
skin_g = (hsv[..., 0] <= 18) & (hsv[..., 1] > 40) & (hsv[..., 2] > 150)
gmask = (yel | (ring & dark & ~skin_g)) & (g[..., 3] > 0)
gmask = cv2.morphologyEx(gmask.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)) > 0
hand_g, _ = back_hand(g)
grab = g.copy(); grab[~gmask, 3] = 0
gy, gx = np.where(gmask)
print('garra bbox', gx.min(), gy.min(), gx.max(), gy.max(), 'mão', hand_g)

meta = json.load(open(fjson))
frames = meta['frames']
for k in [k for k in list(frames) if k.startswith('walk8_')]: del frames[k]
names = []
for slot, idx in enumerate(ORDER):
    c = crop(row1[idx - 1])
    hand, hand_m = back_hand(c)
    # com garra: cola a garra na mesma posição relativa à mão; ela fica na frente do corpo, e a mão por cima dela
    h0, w0 = c.shape[:2]
    gx0, gy0 = int(round(hand[0] - hand_g[0])) + gx.min(), int(round(hand[1] - hand_g[1])) + gy.min()
    gc = grab[gy.min():gy.max() + 1, gx.min():gx.max() + 1]; gh, gw = gc.shape[:2]
    padL, padT = max(0, -gx0), max(0, -gy0)
    padR, padB = max(0, gx0 + gw - w0), max(0, gy0 + gh - h0)
    big = np.zeros((h0 + padT + padB, w0 + padL + padR, 4), np.uint8); big[padT:padT + h0, padL:padL + w0] = c
    lay = np.zeros_like(big); lay[padT + gy0:padT + gy0 + gh, padL + gx0:padL + gx0 + gw] = gc
    hand_big = np.pad(hand_m, ((padT, padB), (padL, padR)))
    hand_big = cv2.dilate(hand_big.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    arr = big.copy()
    front = (lay[..., 3] > 0) & ~hand_big
    arr[front] = lay[front]
    withg = Image.fromarray(arr)
    nog = Image.fromarray(big)
    # mesma moldura e escala para as duas versões; pivô no x da cabeça/tronco, base nos pés
    A = np.array(withg)[..., 3] > 0; ys, xs = np.where(A); box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    withg, nog = withg.crop(box), nog.crop(box)
    size = (round(withg.width * SCALE), round(withg.height * SCALE))
    withg = withg.resize(size, Image.LANCZOS); nog = nog.resize(size, Image.LANCZOS)
    An = np.array(nog)[..., 3] > 0; hh = An.shape[0]
    band = An[int(hh * 0.12):int(hh * 0.5)]; px = float(np.where(band)[1].mean())
    # base = pé mais baixo da versão sem garra (a garra pode passar dos pés)
    bottom = np.where(An.any(1))[0].max() + 1
    name = f'walk8_{slot + 1}'
    nog.save(f'{out}/{name}.png'); withg.save(f'{out}/{name}-garra.png')
    frames[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'fileHolding': f'assets/characters/antonio-frames/{name}-garra.png',
                    'size': list(size), 'pivot': [round(px / size[0], 4), round(1 - bottom / size[1], 4)]}
    names.append(name)
meta['walkCycle'] = names
meta['stride'] = 0.33   # abertura média dos pés na passada / altura
json.dump(meta, open(f'{out}/frames.json', 'w'), indent=1, ensure_ascii=False)
print({n: frames[n]['size'] for n in names})
