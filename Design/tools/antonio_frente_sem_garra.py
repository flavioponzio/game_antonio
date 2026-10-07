import sys, numpy as np, cv2
from PIL import Image
src, outdir = sys.argv[1], sys.argv[2]
c = Image.open(src).convert('RGB'); rgb = np.array(c); H, W = rgb.shape[:2]
I = rgb.astype(int)
# alfa por flood fill do branco a partir das bordas
nw = (I.sum(2) > 730).astype(np.uint8); m = np.zeros((H+2, W+2), np.uint8)
for p in [(0,0),(W-1,0),(0,H-1),(W-1,H-1)]:
    if nw[p[1], p[0]]: cv2.floodFill(nw, m, p, 2)
bg = nw == 2
alpha = np.where(bg, 0, 255).astype(np.uint8)
results = {}
def save(name, rgb_, a_):
    a2 = cv2.GaussianBlur(a_, (3,3), 0); a2[cv2.erode((a_>0).astype(np.uint8), np.ones((3,3),np.uint8))==1] = 255
    results[name] = np.dstack([rgb_, a2])
save('front_grab', rgb, alpha)

yy, xx = np.mgrid[0:H, 0:W]
yellow = (I[...,0] > 170) & (I[...,1] > 130) & (I[...,2] < 120) & (I[...,0] - I[...,2] > 70)
zone = (xx < 122) & (yy > 322)
grab = cv2.dilate((yellow & zone).astype(np.uint8), np.ones((7,7),np.uint8)) > 0
grab &= zone
navy = (I[...,2] - I[...,0] > 12) & (I.sum(2) < 400)
whiteish = I.sum(2) > 650
# antebraço+mão esquerdos (da imagem) a substituir
left_hand = (xx >= 66) & (xx <= 120) & (yy >= 321) & (yy <= 395) & ~bg & ~navy & ~whiteish
# mão direita (aberta), recortada e espelhada
rh = (xx >= 240) & (xx <= 292) & (yy >= 318) & (yy <= 395) & ~bg & ~navy & ~whiteish
n, lab, st, _ = cv2.connectedComponentsWithStats(rh.astype(np.uint8), 8)
rh = lab == (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))
out = rgb.copy(); a = alpha.copy()
remove = grab | left_hand
# o que sai: fora do corpo vira transparente; dentro do corpo (camisa/bermuda) é preenchido
body_zone = (xx >= 100)
fill = remove & body_zone & ~bg
out = cv2.inpaint(out, fill.astype(np.uint8)*255, 5, cv2.INPAINT_TELEA)
a[remove & ~body_zone] = 0
# cola a mão espelhada: x' = A - x, alinhando o punho
A = int(sys.argv[3]) if len(sys.argv) > 3 else 360
ys, xs = np.where(rh)
nx = A - xs
ok = (nx >= 0) & (nx < W)
out[ys[ok], nx[ok]] = rgb[ys[ok], xs[ok]]; a[ys[ok], nx[ok]] = 255
# só a figura principal (tira pontinhos soltos do contorno da garra)
n, lab, st, _ = cv2.connectedComponentsWithStats((a > 0).astype(np.uint8), 8)
a[lab != (1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])))] = 0
save('front_nograb', out, a)

# mesma tela para as duas versões, centrada no corpo (versão sem garra), para não "pular" ao trocar
ys, xs = np.where(results['front_nograb'][..., 3] > 0)
cx = (xs.min() + xs.max()) / 2
ys2, xs2 = np.where(results['front_grab'][..., 3] > 0)
half = int(np.ceil(max(cx - min(xs.min(), xs2.min()), max(xs.max(), xs2.max()) - cx))) + 1
x0, x1 = int(round(cx - half)), int(round(cx + half))
y0, y1 = min(ys.min(), ys2.min()), max(ys.max(), ys2.max()) + 1
for name, arr in results.items():
    pad = np.zeros((H, W + 2 * half, 4), np.uint8); pad[:, half:half + W] = arr
    Image.fromarray(pad[y0:y1, x0 + half:x1 + half], 'RGBA').save(f'{outdir}/{name}.png')
