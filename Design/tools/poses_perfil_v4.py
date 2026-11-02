"""Põe as 4 poses de perfil v4 (olhar para cima, olhar para baixo, pegar do chão, sentado) na folha de quadros.
Mesma escala da vista de perfil do modelo; sem versão com garra (o jogo desenha a garra na mão: hold + held).
Uso: python3 -I poses_perfil_v4.py <poses.png> <pasta antonio-frames> [braco-mecanico-aberto.png]"""
import sys, json
import numpy as np, cv2
from PIL import Image
src, D = sys.argv[1:3]
fj = f'{D}/frames.json'; meta = json.load(open(fj)); frames = meta['frames']
MODEL_SIDE_H = 832                                  # altura do perfil no antonio-modelo.png
s = frames['side']['size'][1] / MODEL_SIDE_H        # a folha veio na mesma escala do modelo
a = np.array(Image.open(src).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((a[..., 3] > 40).astype(np.uint8), 8)
figs = sorted([i for i in range(1, n) if st[i, 4] > 3000], key=lambda i: st[i, 0])
for name, i in zip(['lookUp', 'lookDown', 'pickUp', 'kneel'], figs):   # kneel: ajoelhado (sentado na cadeira é o boneco)
    x, y, w, h = st[i, :4]
    m = cv2.dilate((lab[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = a[y:y + h, x:x + w].copy(); c[~m, 3] = 0
    hsv = cv2.cvtColor(np.ascontiguousarray(c[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    skin = ((hsv[..., 0] <= 20) & (hsv[..., 1] > 50) & (hsv[..., 2] > 140) & (c[..., 3] > 100)).astype(np.uint8)
    # mão da frente: pele abaixo da cabeça; em pé = ponta de baixo do braço; agachado/sentado = ponta da frente
    skin[: int(h * (0.4 if name in ('lookUp', 'lookDown') else 0.55))] = 0
    if name in ('lookUp', 'lookDown'): skin[int(h * 0.72):] = 0
    nn, ll, ss, cc = cv2.connectedComponentsWithStats(skin, 8)
    arm = ll == 1 + int(np.argmax(ss[1:, 4]))
    ys, xs = np.where(arm)
    if name in ('lookUp', 'lookDown'):
        sel = ys >= np.percentile(ys, 75)
    else:
        sel = xs >= np.percentile(xs, 85)
    hand = (float(xs[sel].mean()), float(ys[sel].mean()))
    im = Image.fromarray(c).resize((round(w * s), round(h * s)), Image.LANCZOS)
    im.save(f'{D}/{name}.png')
    A = np.array(im)[..., 3] > 0; hh, ww = A.shape
    if name == 'kneel':
        # pivô = bumbum: ponto mais baixo da metade de trás (o pé fica à frente)
        back = A[:, : int(ww * 0.45)]; by = np.where(back.any(1))[0].max()
        bx = float(np.where(back[by - 3:by + 1].any(0))[0].mean())
        pivot = [round(bx / ww, 4), round(1 - by / hh, 4)]
    else:
        pivot = [round(float(np.where(A[int(hh * 0.12):int(hh * 0.5)])[1].mean()) / ww, 4), 0.0]
    frames[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'size': [ww, hh], 'pivot': pivot,
                    'hold': {'hand': [round(hand[0] * s, 1), round(hand[1] * s, 1)], 'flip': False, 'angle': 0}}
    print(name, [ww, hh], pivot, frames[name]['hold']['hand'])
json.dump(meta, open(fj, 'w'), indent=1, ensure_ascii=False)

# ---- versões com o braço mecânico: garra na frente da perna, a mão por cima da pegada ----
# (nas poses de perfil a garra atrás do corpo ficaria escondida pela perna)
ANGLE = {'lookUp': 0, 'lookDown': 0, 'pickUp': 80}     # graus, positivo = ponta da garra vai para a frente
garra_p = sys.argv[3] if len(sys.argv) > 3 else None
if garra_p:
    G0 = Image.open(garra_p).convert('RGBA'); Ga = np.array(G0)
    hsvg = cv2.cvtColor(np.ascontiguousarray(Ga[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    bys, bxs = np.where((hsvg[..., 0] >= 95) & (hsvg[..., 0] <= 130) & (hsvg[..., 1] > 100) & (Ga[..., 3] > 100))
    gx, gy = float(bxs.mean()), float(bys.mean())
    REF = meta['refHeight']; gs = 0.27 * REF / (Ga.shape[0] - gy)
    G0 = G0.resize((round(G0.width * gs), round(G0.height * gs)), Image.LANCZOS); gx *= gs; gy *= gs
    for name, ang in ANGLE.items():
        f = frames[name]; im = Image.open(f'{D}/{name}.png'); A = np.array(im)
        hx, hy = f['hold']['hand']
        # garra girada em volta da pegada
        pad = int(max(G0.size) * 1.2)
        big = Image.new('RGBA', (pad * 2, pad * 2)); big.alpha_composite(G0, (int(pad - gx), int(pad - gy)))
        big = big.rotate(ang, resample=Image.BICUBIC, center=(pad, pad))      # PIL: anti-horário; o quadro olha para a direita
        bb = big.getbbox(); big = big.crop(bb); ox, oy = pad - bb[0], pad - bb[1]   # pegada dentro do recorte
        x0, y0 = int(round(hx - ox)), int(round(hy - oy))
        L, T = max(0, -x0), max(0, -y0); R = max(0, x0 + big.width - im.width); B = max(0, y0 + big.height - im.height)
        canvas = Image.new('RGBA', (im.width + L + R, im.height + T + B)); canvas.alpha_composite(im, (L, T))
        canvas.alpha_composite(big, (x0 + L, y0 + T))
        # a mão (pele perto do ponto da mão) volta por cima
        hsv = cv2.cvtColor(np.ascontiguousarray(A[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
        skin = (hsv[..., 0] <= 20) & (hsv[..., 1] > 50) & (hsv[..., 2] > 140) & (A[..., 3] > 100)
        yy, xx = np.mgrid[0:A.shape[0], 0:A.shape[1]]
        near = (xx - hx) ** 2 + (yy - hy) ** 2 <= 26 ** 2
        hm = cv2.dilate((skin & near).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
        hand = A.copy(); hand[~hm, 3] = 0
        canvas.alpha_composite(Image.fromarray(hand), (L, T))
        canvas.save(f'{D}/{name}-garra.png')
        px, py = f['pivot']
        f['fileHolding'] = f'assets/characters/antonio-frames/{name}-garra.png'
        f['pivotHolding'] = [round((px * im.width + L) / canvas.width, 4), round((py * im.height + B) / canvas.height, 4)]
        f.pop('hold', None)          # a garra já está no desenho
        print(name, 'com garra', canvas.size)
    json.dump(meta, open(fj, 'w'), indent=1, ensure_ascii=False)
