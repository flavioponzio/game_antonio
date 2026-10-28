"""Põe as 4 vistas do modelo v4 (frente, três-quartos, perfil, costas) na folha de quadros do Antônio.
Elas não têm versão com a garra: o braço mecânico é desenhado na mão pelo jogo ("held" + "hold" no frames.json).
Uso: python3 -I frames_v4.py <antonio-modelo.png> <garra.png> <pasta antonio-frames>"""
import sys, json
import numpy as np, cv2
from PIL import Image
model, garra, D = sys.argv[1:4]
fj = f'{D}/frames.json'; meta = json.load(open(fj)); frames = meta['frames']
REF = meta['refHeight']                          # mantém a escala dos outros quadros
a = np.array(Image.open(model).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((a[..., 3] > 40).astype(np.uint8), 8)
figs = sorted([i for i in range(1, n) if st[i, 4] > 3000], key=lambda i: st[i, 0])
s = REF / st[figs[0], 3]                          # altura da frente = refHeight
for name, i in zip(['front', 'front34', 'side', 'back'], figs):
    x, y, w, h = st[i, :4]
    m = cv2.dilate((lab[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = a[y:y + h, x:x + w].copy(); c[~m, 3] = 0
    hsv = cv2.cvtColor(np.ascontiguousarray(c[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    skin = (hsv[..., 0] <= 20) & (hsv[..., 1] > 50) & (hsv[..., 2] > 140) & (c[..., 3] > 100)
    zone = np.zeros_like(skin); zone[int(h * 0.55):int(h * 0.75), :int(w * 0.5)] = True
    nn, ll, ss, cc = cv2.connectedComponentsWithStats((skin & zone).astype(np.uint8), 8)
    hand = cc[1 + int(np.argmax(ss[1:, 4]))]
    im = Image.fromarray(c).resize((round(w * s), round(h * s)), Image.LANCZOS)
    im.save(f'{D}/{name}.png')
    A = np.array(im)[..., 3] > 0; hh = A.shape[0]
    px = float(np.where(A[int(hh * 0.12):int(hh * 0.5)])[1].mean())
    frames[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'size': list(im.size),
                    'pivot': [round(px / im.width, 4), 0.0],
                    'hold': {'hand': [round(hand[0] * s, 1), round(hand[1] * s, 1)], 'flip': False, 'angle': 0}}
# costas olhando o quadro: mesma imagem das costas
frames['back34'] = dict(frames['back'])
# piscar e falar ainda são do desenho antigo: saem até chegar a folha de poses nova (sem eles, fica a frente)
for k in ('blink', 'talk'): frames.pop(k, None)
# braço mecânico na mão (provisório, recortado do elenco: Antônio com 613 px)
g = Image.open(garra).convert('RGBA'); gs = REF / 613
g = g.resize((round(g.width * gs), round(g.height * gs)), Image.LANCZOS); g.save(f'{D}/garra.png')
ga = np.array(g)[..., 3] > 100; gx = float(np.where(ga[2])[0].mean())
meta['held'] = {'file': 'assets/characters/antonio-frames/garra.png', 'grip': [round(gx / g.width, 4), round(1 - 2 / g.height, 4)]}
meta['note'] = ('Quadros do Antônio. front/front34/side/back = modelo v4 (Design/personagens/v4/antonio-modelo.png), com a garra '
                'desenhada pelo jogo na mão (hold + held); os outros ainda são da folha de poses v3 (file = sem garra, '
                'fileHolding = com o braço mecânico). pivot = (x do tronco, base) 0..1. Os quadros olham para a direita.')
json.dump(meta, open(fj, 'w'), indent=1, ensure_ascii=False)
print({k: frames[k]['size'] for k in ['front', 'front34', 'side', 'back']}, meta['held'])
