"""Recorta o braço mecânico da 1ª figura da ilustração com a garra (antonio-poses-com-garra.png), que fica pendurado
fora do corpo. A mão (pele em cima do cabo azul) e o tênis ficam de fora.
Uso: python3 -I garra_da_ilustracao.py <antonio-poses-com-garra.png> <saida.png>"""
import sys
import numpy as np, cv2
from PIL import Image
a = np.array(Image.open(sys.argv[1]).convert('RGBA'))
x0, y0, x1, y1 = 22, 595, 125, 870          # caixa da garra na 1ª figura
c = a[y0:y1, x0:x1].copy()
hsv = cv2.cvtColor(np.ascontiguousarray(c[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
yel = (hsv[..., 0] >= 15) & (hsv[..., 0] <= 35) & (hsv[..., 1] > 100) & (c[..., 3] > 40)
blue = (hsv[..., 0] >= 95) & (hsv[..., 0] <= 130) & (hsv[..., 1] > 120) & (hsv[..., 2] > 150) & (c[..., 3] > 40)
blue_top = np.where(blue.any(1))[0].min()
yy = np.mgrid[0:c.shape[0], 0:c.shape[1]][0]
skin = (hsv[..., 0] <= 20) & (hsv[..., 1] > 40) & (hsv[..., 2] > 130) & (yy < blue_top + 6)   # só a mão, em cima
red = ((hsv[..., 0] <= 8) | (hsv[..., 0] >= 170)) & (hsv[..., 1] > 120) & (yy > c.shape[0] * 0.8)  # tênis, embaixo
m = (c[..., 3] > 40) & ~skin & ~red
part = yel | blue
for y in range(c.shape[0]):                    # por linha: entre as pontas da garra, mais o contorno
    xs = np.where(part[max(0, y - 3):y + 4].any(0))[0]
    if len(xs) == 0: m[y] = False; continue
    m[y, :max(0, xs.min() - 5)] = False; m[y, xs.max() + 6:] = False
m[:max(0, blue_top - 2)] = False
n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8); m = lab == 1 + np.argmax(st[1:, 4])
c[~m, 3] = 0
im = Image.fromarray(c); im = im.crop(im.getbbox()); im.save(sys.argv[2]); print(im.size)
