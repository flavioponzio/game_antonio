"""Acrescenta os 2 quadros de passagem (pernas juntas, uma levantada) ao ciclo de caminhada.
Entrada: duas imagens com 2 figuras cada (sem garra e com garra), fundo transparente, já passadas por solidifica_folha.py.
Uso: python3 -I add_pass.py <sem_solida.png> <com_solida.png> <pasta_saida> <frames.json existente>"""
import sys, json, os
import numpy as np, cv2
from PIL import Image
sem_p, com_p, out, fjson = sys.argv[1:5]
os.makedirs(out, exist_ok=True)
# escala: meio-termo entre igualar a cabeça e não deixar o corpo subir demais na passagem
SCALE = 0.575
# os quadros de passagem entram no lugar de walk8_3 e walk8_7 (meio de cada passo)
SLOTS = {'walk8_3': 'pass1', 'walk8_7': 'pass2'}

def figures(path):
    im = np.array(Image.open(path).convert('RGBA'))
    n, lab, st, _ = cv2.connectedComponentsWithStats((im[..., 3] > 100).astype(np.uint8), 8)
    figs = sorted([i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > 3000], key=lambda i: st[i, 0])
    res = []
    for i in figs:
        x, y, w, h = st[i, :4]
        m = cv2.dilate((lab == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
        c = im.copy(); c[~m, 3] = 0
        res.append(Image.fromarray(c[y - 2:y + h + 2, x - 2:x + w + 2]))
    return res
sem, com = figures(sem_p), figures(com_p)
assert len(sem) == 2 and len(com) == 2
meta = json.load(open(fjson)); frames = meta['frames']
for k, (s_im, c_im) in enumerate(zip(sem, com)):
    name = f'pass{k + 1}'
    def fit(im, height=None):
        ys, xs = np.where(np.array(im)[..., 3] > 0); im = im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
        s = SCALE if height is None else height / im.height
        return im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    s_im = fit(s_im); c_im = fit(c_im, s_im.height)   # versão com garra na mesma altura da sem garra
    def pivot(im):
        A = np.array(im)[..., 3] > 0; hh = A.shape[0]
        return [round(float(np.where(A[int(hh * 0.12):int(hh * 0.5)])[1].mean()) / im.width, 4), 0.0]
    # as duas versões são desenhos diferentes: cada uma com seu tamanho e pivô (a versão com garra fica num arquivo à parte)
    s_im.save(f'{out}/{name}.png'); c_im.save(f'{out}/{name}-garra.png')
    frames[name] = {'file': f'assets/characters/antonio-frames/{name}.png', 'fileHolding': f'assets/characters/antonio-frames/{name}-garra.png',
                    'size': list(s_im.size), 'pivot': pivot(s_im), 'sizeHolding': list(c_im.size), 'pivotHolding': pivot(c_im)}
meta['walkCycle'] = [SLOTS.get(n, n) for n in meta['walkCycle']]
json.dump(meta, open(f'{out}/frames.json', 'w'), indent=1, ensure_ascii=False)
print(meta['walkCycle'], {k: frames[k] for k in ['pass1', 'pass2']})
