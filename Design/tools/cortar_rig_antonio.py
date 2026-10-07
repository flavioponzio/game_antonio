"""Recorta o perfil do Antônio (da folha de turnaround) em partes para um rig 2D.
Uso: python3 -I cut.py <turnaround.jpg> <pasta_saida>
"""
import json, sys, os
import numpy as np, cv2
from PIL import Image, ImageDraw, ImageFilter

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
full = Image.open(src).convert('RGB')
# recorte do perfil (mesmo usado nas medições)
X0, Y0 = 791 - 10, 161 - 10
crop = full.crop((X0, Y0, 997 + 10, 674 + 10))
W, H = crop.size
rgb = np.array(crop)

# --- alfa: flood fill do branco a partir das bordas (a camisa é branca mas fica dentro do contorno)
near_white = (rgb.astype(int).sum(2) > 730).astype(np.uint8)
ff = near_white.copy()
mask = np.zeros((H + 2, W + 2), np.uint8)
for (x, y) in [(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)]:
    if ff[y, x]:
        cv2.floodFill(ff, mask, (x, y), 2)
bg = (ff == 2)
alpha = np.where(bg, 0, 255).astype(np.uint8)
# suaviza 1px a borda
alpha = cv2.GaussianBlur(alpha, (3, 3), 0)
alpha[cv2.erode((~bg).astype(np.uint8), np.ones((3, 3), np.uint8)) == 1] = 255

def poly_mask(points):
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    return np.array(m) > 0

def rect_mask(x0, y0, x1, y1):
    m = np.zeros((H, W), bool); m[y0:y1, x0:x1] = True; return m

opaque = alpha > 0

# --- polígonos (coordenadas do recorte, medidas na grade)
upper_arm = poly_mask([(95, 207), (110, 213), (117, 244), (119, 285), (113, 287), (113, 335), (71, 335), (69, 291), (63, 285), (64, 244), (74, 221)])
fore_arm = poly_mask([(76, 329), (114, 329), (117, 344), (121, 361), (118, 373), (111, 387), (84, 389), (79, 376), (79, 346)])
navy = (rgb[..., 2].astype(int) - rgb[..., 0].astype(int) > 12) & (rgb.astype(int).sum(2) < 400)
grab = rect_mask(134, 340, W, 432) & opaque & ~navy
# só a maior mancha conectada (o braço mecânico), sem lascas da bermuda
n, lab, stats, _ = cv2.connectedComponentsWithStats(grab.astype(np.uint8), 8)
grab = lab == (1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA])))
head = (rect_mask(0, 0, W, 179) | (rect_mask(119, 0, W, 193))) & opaque
leg_zone = (rect_mask(0, 401, 129, H) | rect_mask(0, 433, W, H)) & opaque
leg_up = leg_zone & rect_mask(0, 401, W, 447)
leg_low = leg_zone & rect_mask(0, 437, W, H)
arm_any = (upper_arm | fore_arm) & opaque
body = opaque & ~head & ~leg_zone & ~grab

def save_part(name, m, img_rgb, a=None, extra_alpha=None):
    a_ = alpha.copy() if a is None else a
    al = np.where(m, a_, 0).astype(np.uint8)
    if extra_alpha is not None: al = np.maximum(al, extra_alpha)
    ys, xs = np.where(al > 0)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    rgba = np.dstack([img_rgb, al])[y0:y1, x0:x1]
    Image.fromarray(rgba, 'RGBA').save(os.path.join(out, name + '.png'))
    return [int(x0), int(y0), int(x1 - x0), int(y1 - y0)]

boxes = {}

# corpo: tira o braço e preenche (inpaint) camisa/bermuda por trás dele; estende o pescoço sob a cabeça
body_rgb = rgb.copy()
hole = (arm_any & body).astype(np.uint8) * 255
hole = cv2.dilate(hole, np.ones((5, 5), np.uint8))
hole &= (body.astype(np.uint8) * 255)
# preenchimento chapado: camisa acima da barra, bermuda abaixo (com leve textura vinda do vizinho)
shirt = np.median(rgb[250:280, 125:140].reshape(-1, 3), 0).astype(np.uint8)
shorts = np.median(rgb[355:375, 65:78].reshape(-1, 3), 0).astype(np.uint8)
hy, hx = np.where(hole > 0)
noise = np.random.RandomState(1).randint(-6, 7, size=(len(hy), 1))
fill = np.where((hy >= 346)[:, None], shorts[None, :], shirt[None, :]).astype(int) + noise
body_rgb[hy, hx] = np.clip(fill, 0, 255).astype(np.uint8)
# linha da barra da camisa atravessando o buraco
for x in np.unique(hx):
    col = hy[hx == x]
    if col.min() <= 345 <= col.max(): body_rgb[343:347, x] = rgb[344, 70] if x < 80 else body_rgb[343:347, x]
neck = poly_mask([(83, 150), (117, 150), (119, 190), (81, 190)])
skin = rgb[186, 100]
body_rgb[neck & ~body] = skin
body_alpha = alpha.copy(); body_alpha[neck] = 255
boxes['body'] = save_part('body', body | neck, body_rgb, body_alpha)

boxes['head'] = save_part('head', head, rgb)
boxes['arm-upper'] = save_part('arm-upper', upper_arm & opaque, rgb)
fa = fore_arm & opaque & ~upper_arm & ~navy
n, lab, stats, _ = cv2.connectedComponentsWithStats(fa.astype(np.uint8), 8)
fa = lab == (1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA])))
fa = cv2.morphologyEx(fa.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)) > 0
boxes['arm-lower'] = save_part('arm-lower', fa, rgb)
boxes['grabber'] = save_part('grabber', grab, rgb)

# coxa: estende para cima (escondida pela bermuda) repetindo a linha y=404
leg_rgb = rgb.copy(); leg_alpha = alpha.copy()
row = 404
cols = np.where(leg_zone[row])[0]
ext = np.zeros((H, W), bool)
cols = cols[(cols >= cols.min() + 3) & (cols <= cols.max() - 3)]
for y in range(386, row):
    leg_rgb[y, cols] = rgb[row, cols]; leg_alpha[y, cols] = 255; ext[y, cols] = True
boxes['leg-upper'] = save_part('leg-upper', leg_up | ext, leg_rgb, leg_alpha)
boxes['leg-lower'] = save_part('leg-lower', leg_low, rgb)

# --- rig: pivôs (coordenadas do recorte) e ordem de desenho (de trás para frente)
pivots = {'root': [104, 528], 'body': [104, 384], 'head': [100, 182], 'shoulder': [93, 230], 'elbow': [93, 331],
          'hand': [100, 372], 'hip': [104, 384], 'knee': [104, 442]}
P = pivots
parts = [
    # name, image, parent, pivot, tint
    ('backArmUpper', 'arm-upper', 'body', P['shoulder'], 0.80),
    ('backArmLower', 'arm-lower', 'backArmUpper', P['elbow'], 0.80),
    ('backLegUpper', 'leg-upper', 'body', P['hip'], 0.82),
    ('backLegLower', 'leg-lower', 'backLegUpper', P['knee'], 0.82),
    ('grabber', 'grabber', 'frontArmLower', P['hand'], 1.0),
    ('frontLegLower', 'leg-lower', 'frontLegUpper', P['knee'], 1.0),
    ('frontLegUpper', 'leg-upper', 'body', P['hip'], 1.0),
    ('body', 'body', 'root', P['body'], 1.0),
    ('head', 'head', 'body', P['head'], 1.0),
    ('frontArmLower', 'arm-lower', 'frontArmUpper', P['elbow'], 1.0),
    ('frontArmUpper', 'arm-upper', 'body', P['shoulder'], 1.0),
]
rig = {
    'note': 'Rig do Antônio de perfil. Coordenadas em pixels do recorte original (y para baixo). '
            'box = [x, y, w, h] da imagem da parte; pivot = junta onde a parte gira; order = ordem de desenho.',
    'height': 528 - 10, 'root': P['root'],
    'images': {k: {'file': f'assets/characters/antonio-rig/{k}.png', 'box': v} for k, v in boxes.items()},
    'parts': [{'name': n, 'image': i, 'parent': p, 'pivot': pv, 'tint': t, 'order': k} for k, (n, i, p, pv, t) in enumerate(parts)],
}
json.dump(rig, open(os.path.join(out, 'rig.json'), 'w'), indent=1)
print(json.dumps(boxes))
