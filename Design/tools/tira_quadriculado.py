"""Tira o quadriculado desenhado (falso fundo transparente) de uma imagem do ChatGPT.
Uso: python3 -I unchecker.py <entrada> <saida.png>"""
import sys, numpy as np, cv2
from PIL import Image
im = np.array(Image.open(sys.argv[1]).convert('RGB')); I = im.astype(int)
hsv = cv2.cvtColor(im, cv2.COLOR_RGB2HSV).astype(int)
light = ((I.sum(2) > 660) & (hsv[..., 1] < 18)).astype(np.uint8)
H, W = light.shape; m = np.zeros((H + 2, W + 2), np.uint8); ff = light.copy()
for x in range(0, W, 7):
    for y in (0, H - 1):
        if ff[y, x] == 1: cv2.floodFill(ff, m, (x, y), 2)
for y in range(0, H, 7):
    for x in (0, W - 1):
        if ff[y, x] == 1: cv2.floodFill(ff, m, (x, y), 2)
bg = ff == 2
fg = ~bg
n, lab, st, _ = cv2.connectedComponentsWithStats(fg.astype(np.uint8), 8)
fg = np.isin(lab, [i for i in range(1, n) if st[i, cv2.CC_STAT_AREA] > 500])
fg = cv2.morphologyEx(fg.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
a = np.where(fg, 255, 0).astype(np.uint8)
a2 = cv2.GaussianBlur(a, (3, 3), 0); a2[cv2.erode(a, np.ones((3, 3), np.uint8)) > 0] = 255
Image.fromarray(np.dstack([im, a2]), 'RGBA').save(sys.argv[2])
