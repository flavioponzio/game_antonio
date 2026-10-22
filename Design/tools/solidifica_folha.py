"""Conserta a transparência da folha do ChatGPT: interior opaco, furos com cor fechados, borda suave.
Uso: python3 -I solidify.py <entrada.png> <saida.png>"""
import sys, numpy as np, cv2
from PIL import Image
im = np.array(Image.open(sys.argv[1]).convert('RGBA')); al = im[..., 3].astype(int); rgb = im[..., :3]
H, W = al.shape
sil = (al > 40).astype(np.uint8)
has_color = rgb.astype(int).sum(2) > 60            # o fundo transparente guarda preto; o personagem guarda a cor
ff = sil.copy(); m = np.zeros((H + 2, W + 2), np.uint8); cv2.floodFill(ff, m, (0, 0), 2)
holes = ff == 0                                     # regiões transparentes cercadas pela silhueta
near = cv2.morphologyEx(sil, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
solid = (sil > 0) | (has_color & (holes | near))
solid = cv2.morphologyEx(solid.astype(np.uint8), cv2.MORPH_OPEN, np.ones((2, 2), np.uint8))
inner = cv2.erode(solid, np.ones((3, 3), np.uint8)) > 0
edge = (solid > 0) & ~inner
a2 = np.where(inner, 255, np.where(edge, np.clip(np.maximum(al, 120) * 255 // 235, 0, 255), 0)).astype(np.uint8)
Image.fromarray(np.dstack([rgb, a2]), 'RGBA').save(sys.argv[2])
