"""Usa a ilustração do Antônio segurando o braço mecânico (Design/personagens/v4/antonio-poses-com-garra.png) como a
versão "com garra" dos quadros: 1ª figura = frente, 2ª = perfil, 3ª = pegando do chão (a 4ª, sentado, não é usada:
na cadeira ele é o boneco). Os desenhos entram como estão, só recortados e na escala do quadro sem garra.
Uso: python3 -I poses_com_garra.py <antonio-poses-com-garra.png> <pasta antonio-frames>"""
import sys, json
import numpy as np, cv2
from PIL import Image
src, D = sys.argv[1:3]
fj = f'{D}/frames.json'; meta = json.load(open(fj)); frames = meta['frames']
a = np.array(Image.open(src).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((a[..., 3] > 40).astype(np.uint8), 8)
figs = sorted([i for i in range(1, n) if st[i, 4] > 3000], key=lambda i: st[i, 0])
def crop(i):
    x, y, w, h = st[i, :4]
    m = cv2.dilate((lab[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = a[y:y + h, x:x + w].copy(); c[~m, 3] = 0; return Image.fromarray(c)
def pivot_x(im):
    A = np.array(im)[..., 3] > 0; hh = A.shape[0]
    return float(np.where(A[int(hh * 0.12):int(hh * 0.5)])[1].mean()) / im.width
s_side = None
for name, i in zip(['front', 'side', 'pickUp'], figs[:3]):
    im = crop(i)
    if name == 'pickUp': s = s_side                         # agachado: mesma escala do perfil em pé
    else: s = frames[name]['size'][1] / im.height           # em pé: mesma altura do quadro sem garra
    if name == 'side': s_side = s
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    im.save(f'{D}/{name}-garra.png')
    frames[name]['fileHolding'] = f'assets/characters/antonio-frames/{name}-garra.png'
    frames[name]['pivotHolding'] = [round(pivot_x(im), 4), 0.0]
    print(name, im.size, frames[name]['pivotHolding'])
json.dump(meta, open(fj, 'w'), indent=1, ensure_ascii=False)
