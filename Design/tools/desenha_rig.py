"""Desenha o rig (rig.json do CutoutRig) numa pose, para prévia. Mesma convenção do Unity:
ângulo positivo = anti-horário com o personagem olhando para a direita."""
import json, math
import numpy as np, cv2
from PIL import Image
class Rig:
    def __init__(self, path, root_dir):
        self.r = json.load(open(path)); self.parts = {p['name']: p for p in self.r['parts']}
        self.img = {k: Image.open(f"{root_dir}/{v['file'].split('/')[-1]}").convert('RGBA') for k, v in self.r['images'].items()}
        # pontos da borda de cada imagem (para achar o pé mais baixo)
        self.edge = {}
        for k, im in self.img.items():
            a = (np.array(im)[..., 3] > 100).astype(np.uint8)
            cs, _ = cv2.findContours(a, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            self.edge[k] = np.vstack([c[:, 0, :] for c in cs]).astype(float)
    def world(self, angles):
        """matriz 2x3 (pixel do rig -> mundo) para cada parte"""
        M = {}
        def rot(t):
            t = math.radians(t); c, s = math.cos(t), math.sin(t)
            return np.array([[c, s], [-s, c]])
        def get(name):
            if name in M: return M[name]
            p = self.parts[name]; piv = np.array(p['pivot'], float)
            if p['parent'] == 'root' or p['parent'] not in self.parts:
                L, o = np.eye(2), np.array(self.r['root'], float) * 0 + piv
            else:
                PL, Po = get(p['parent']); o = PL @ piv + Po; L = PL
            R = L @ rot(angles.get(name, 0.0))
            # x_mundo = R (x - piv) + o
            M[name] = (R, o - R @ piv); return M[name]
        for n in self.parts: get(n)
        return M
    def lowest(self, M, names=('frontLegLower', 'backLegLower')):
        y = -1e9
        for n in names:
            R, t = M[n]; b = self.r['images'][self.parts[n]['image']]['box']
            pts = self.edge[self.parts[n]['image']] + [b[0], b[1]]
            y = max(y, (pts @ R.T + t)[:, 1].max())
        return y
    def render(self, angles, size, origin, scale=1.0, dy=0.0):
        """origin = onde o root (chão) cai na imagem de saída"""
        M = self.world(angles); out = Image.new('RGBA', size, (0, 0, 0, 0))
        root = np.array(self.r['root'], float)
        for p in sorted(self.r['parts'], key=lambda p: p['order']):
            R, t = M[p['name']]; b = self.r['images'][p['image']]['box']
            # pixel da imagem (u,v) -> rig (u+bx, v+by) -> mundo -> saída
            A = scale * R; c = scale * (R @ np.array([b[0], b[1]], float) + t - root) + np.array(origin, float) + [0, dy * scale]
            Ai = np.linalg.inv(A); ci = -Ai @ c
            im = self.img[p['image']].transform(size, Image.AFFINE, (Ai[0, 0], Ai[0, 1], ci[0], Ai[1, 0], Ai[1, 1], ci[1]), resample=Image.BICUBIC)
            out = Image.alpha_composite(out, im)
        return out, M
