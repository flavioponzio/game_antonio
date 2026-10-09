import sys, numpy as np, cv2
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
im = np.array(Image.open(src).convert('RGB'))
H, W = im.shape[:2]
OX, OY = 0.05 * W, 0.28 * H
def box(zx0, zy0, zx1, zy1, m=2):  # coordenadas do zoom 3x -> pixels
    return (int(OX + zx0 / 3) - m, int(OY + zy0 / 3) - m, int(OX + zx1 / 3) + m, int(OY + zy1 / 3) + m)
L1, L2, L3 = (90, 190), (250, 340), (380, 485)
g = {'2': box(148, *L1[:1], 202, L1[1]), '3': box(292, L1[0], 348, L1[1]), '5': box(292, L2[0], 342, L2[1])}
src_img = im.copy()
def chalk(region):
    r = region.astype(int)
    return (r.sum(2) > 470) & (r[..., 0] > 140)
# 1) apaga: respostas das 3 linhas + "5" da linha 2 + "8" e "7" da linha 3
erase = [box(425, L1[0], 480, L1[1]), box(425, L2[0], 522, L2[1]), box(425, L3[0], 525, L3[1]),
         box(292, L2[0], 342, L2[1]), box(146, L3[0], 205, L3[1]), box(292, L3[0], 345, L3[1])]
mask = np.zeros((H, W), np.uint8)
for (x0, y0, x1, y1) in erase:
    mask[y0:y1, x0:x1][chalk(im[y0:y1, x0:x1])] = 255
mask = cv2.dilate(mask, np.ones((5, 5), np.uint8))
im = cv2.inpaint(im, mask, 4, cv2.INPAINT_TELEA)
# 2) cola algarismos de giz copiados da própria lousa
def paste(glyph, dest_x0, dest_y0):
    x0, y0, x1, y1 = g[glyph]
    piece = src_img[y0:y1, x0:x1]
    m = chalk(piece).astype(np.uint8) * 255
    m = cv2.GaussianBlur(cv2.dilate(m, np.ones((2, 2), np.uint8)), (3, 3), 0).astype(float)[..., None] / 255
    tgt = im[dest_y0:dest_y0 + (y1 - y0), dest_x0:dest_x0 + (x1 - x0)].astype(float)
    im[dest_y0:dest_y0 + (y1 - y0), dest_x0:dest_x0 + (x1 - x0)] = (tgt * (1 - m) + piece * m).astype(np.uint8)
def at(zx, line): b = box(zx, line[0], zx + 50, line[1]); return b[0], b[1]
paste('2', *at(292, L2))   # 4 x 2 =
paste('3', *at(148, L3))   # 3 x 5 =
paste('5', *at(292, L3))
Image.fromarray(im).save(out)
