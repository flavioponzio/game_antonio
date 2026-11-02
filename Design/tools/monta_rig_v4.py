"""Monta o rig de perfil do Antônio (v4) a partir da folha de peças do ChatGPT.
A folha tem: cabeça, tronco (de frente, não usado), short (de frente, não usado), braço de cima/baixo (frente e trás),
coxa e canela+pé (frente e trás) e, à direita, o Antônio de perfil inteiro.
O tronco e o quadril saem do perfil inteiro, apagando o braço; o resto são as peças soltas.
Uso: python3 -I build_rig4.py <folha.png> <tronco_e_short.png> <bracos.png> <pasta_saida> [garra.png] [antonio-poses-com-garra.png]
O tronco e o short de perfil vêm de uma segunda imagem (a folha trouxe os dois de frente) e o braço de cima
(manga com o ombro arredondado) de uma terceira, já sem o quadriculado (unchecker.py).
Grava as imagens das partes e rig.json (formato do CutoutRig: coordenadas no espaço do perfil montado, y para baixo)."""
import sys, os, json
import numpy as np, cv2
from PIL import Image
src, src2, src3, out = sys.argv[1:5]
grabber_src = sys.argv[5] if len(sys.argv) > 5 else None   # braço mecânico (braco-mecanico-ilustracao.png)
fist_src = sys.argv[6] if len(sys.argv) > 6 else None      # ilustração com a garra (antonio-poses-com-garra.png)
os.makedirs(out, exist_ok=True)
A = np.array(Image.open(src).convert('RGBA'))
n, lab, st, _ = cv2.connectedComponentsWithStats((A[..., 3] > 40).astype(np.uint8), 8)

def comp_at(x, y):
    """peça cuja caixa contém (x, y) — evita depender da ordem dos componentes"""
    for i in range(1, n):
        bx, by, bw, bh = st[i, :4]
        if st[i, 4] > 300 and bx <= x < bx + bw and by <= y < by + bh and lab[y, x] == i: return i
    raise SystemExit(f'nenhuma peça em {x},{y}')
def piece(i, scale=1.0):
    x, y, w, h = st[i, :4]
    m = cv2.dilate((lab[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = A[y:y + h, x:x + w].copy(); c[~m, 3] = 0
    im = Image.fromarray(c)
    if scale != 1.0: im = im.resize((round(w * scale), round(h * scale)), Image.LANCZOS)
    return im, (x, y)

# ---- medidas (pixels da folha) ----
ARM = (0.9, 0.78)              # antebraço (largura, altura): mais curto para a mão parar no mesmo lugar do perfil
# juntas nas peças soltas (centro das bolinhas), em pixels da folha
J = {
    'armUpF': ((683, 122), (697, 274)), 'armUpB': ((887, 124), (901, 276)),
    'armLoF': ((705, 331), None), 'armLoB': ((906, 333), None),
    'legUpF': ((230, 605), (233, 722)), 'legUpB': ((530, 600), (538, 722)),
    'legLoF': ((227, 775), None), 'legLoB': ((533, 777), None),
}
SEEDS = {'head': (160, 150), 'profile': (1290, 600), 'armUpF': (690, 200), 'armUpB': (895, 200), 'armLoF': (710, 400),
         'armLoB': (912, 400), 'legUpF': (230, 650), 'legUpB': (530, 650), 'legLoF': (230, 850), 'legLoB': (530, 850)}
C = {k: comp_at(*v) for k, v in SEEDS.items()}

# espaço do rig = coordenadas do perfil na folha
SHOULDER = (1272, 420)         # ombro (centro da cúpula da manga) no perfil
NECK = (1290, 340)             # base da cabeça
HIP = (1283, 665)              # quadril (pivô do tronco)
FLOOR = 989
STRIDE = 0.358                 # quanto o pé de apoio anda para trás num passo / altura (coxa a 27°)
BACK_DX = {'arm': 6, 'leg': -8}   # braço de trás escondido atrás do tronco; perna de trás um pouco atrás

images, parts = {}, []
def add_image(key, im, box_xy):
    im.save(f'{out}/{key}.png')
    images[key] = {'file': f'assets/characters/antonio-rig4/{key}.png', 'box': [int(box_xy[0]), int(box_xy[1]), im.width, im.height]}
def place(key, scale, joint_in_piece, joint_in_rig):
    """põe a peça de forma que a junta dela caia em joint_in_rig; scale = número ou (largura, altura)"""
    sx, sy = scale if isinstance(scale, tuple) else (scale, scale)
    im, (x0, y0) = piece(C[key])
    im = im.resize((round(im.width * sx), round(im.height * sy)), Image.LANCZOS)
    jx, jy = (joint_in_piece[0] - x0) * sx, (joint_in_piece[1] - y0) * sy
    bx, by = joint_in_rig[0] - jx, joint_in_rig[1] - jy
    add_image(key, im, (round(bx), round(by)))
    return lambda p: (joint_in_rig[0] + (p[0] - joint_in_piece[0]) * sx, joint_in_rig[1] + (p[1] - joint_in_piece[1]) * sy)

# cabeça: mesma posição da cabeça do perfil (casamento de molde: deslocamento 17,5 na caixa do perfil)
px0, py0 = st[C['profile'], :2]
head, (hx, hy) = piece(C['head'])
add_image('head', head, (px0 + 17, py0 + 5))

# braços: imagem com o braço MONTADO (gabarito) e as 2 peças, em 2 linhas (frente em cima, trás embaixo).
# O cotovelo fica logo abaixo da barra da manga (ombro→punho dividido ao meio no braço montado); a peça de cima vem com
# pele demais, então é cortada ali com a ponta arredondada. A escala vem da barra da camiseta do braço montado.
Cimg = np.array(Image.open(src3).convert('RGBA'))
n3, lab3, st3, _ = cv2.connectedComponentsWithStats((Cimg[..., 3] > 40).astype(np.uint8), 8)
big3 = [i for i in range(1, n3) if st3[i, 4] > 3000]
rows3 = [sorted([i for i in big3 if (st3[i, 1] < Cimg.shape[0] / 2) == top], key=lambda i: st3[i, 0]) for top in (True, False)]
hsv3 = cv2.cvtColor(np.ascontiguousarray(Cimg[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
blue3 = (hsv3[..., 0] >= 95) & (hsv3[..., 0] <= 125) & (hsv3[..., 1] > 80) & (Cimg[..., 3] > 40)
skin3 = (hsv3[..., 0] <= 22) & (hsv3[..., 1] > 40) & (hsv3[..., 2] > 120) & (Cimg[..., 3] > 40)
INK = None
def crop3(i):
    x, y, w, h = st3[i, :4]
    m = cv2.dilate((lab3[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = Cimg[y:y + h, x:x + w].copy(); c[~m, 3] = 0; return c, x, y
def cuff_bottom(c, x, y, zone=(0.0, 1.0)):
    """última linha da barra azul da manga (mais da metade da linha azul), dentro da faixa de altura zone"""
    b = blue3[y:y + c.shape[0], x:x + c.shape[1]]; w = (c[..., 3] > 40).sum(1)
    rows = np.where(b.sum(1) > 0.5 * np.maximum(w, 1))[0]
    h = c.shape[0]; rows = rows[(rows >= zone[0] * h) & (rows <= zone[1] * h)]
    return int(rows.max())
def arm_from_sheet(row):
    asm, up, fo = rows3[row]
    A, ax, ay = crop3(asm); U, ux, uy = crop3(up); F, fx, fy = crop3(fo)
    # gabarito: barra da camiseta (canto da frente) e o braço
    aal = A[..., 3] > 40
    sk = skin3[ay:ay + A.shape[0], ax:ax + A.shape[1]]
    cuffA = cuff_bottom(A[:, :int(A.shape[1] * 0.75)], ax, ay, (0.2, 0.6))   # sem a gola e o short
    hand_rows = np.where(sk[cuffA:].any(1))[0] + cuffA
    # pontas dos dedos: última linha de pele antes do vão da perna (a pele do joelho aparece de novo embaixo)
    gaps = np.where(np.diff(hand_rows) > 3)[0]
    tips = int(hand_rows[gaps[0]] if len(gaps) else hand_rows.max())
    # peça de cima: cúpula = topo; pivô do ombro no centro da cúpula
    ual = U[..., 3] > 40; uw = ual.shape[1]
    utop = int(np.where(ual.any(1))[0].min()); ucuff = cuff_bottom(U, ux, uy)
    xs = np.where(ual[utop + uw // 2])[0]; shoulder_u = (float(xs.mean()), utop + np.ptp(xs) / 2)
    # no gabarito a peça de cima está com a cúpula no mesmo lugar (a manga casa): distância cúpula→barra igual
    dome_to_cuff = ucuff - utop
    domeA = cuffA - dome_to_cuff
    shoulderA_y = domeA + (shoulder_u[1] - utop)
    elbowA_y = shoulderA_y + (tips - shoulderA_y) * 0.43     # ombro→cotovelo ≈ cotovelo→punho (mão ≈ 0,14 do braço)
    elbow_u_y = shoulder_u[1] + (elbowA_y - shoulderA_y)
    # corta a peça de cima no cotovelo com ponta redonda + contorno novo
    sk_u = skin3[uy:uy + U.shape[0], ux:ux + U.shape[1]]
    xs_e = np.where(sk_u[int(elbow_u_y)])[0]; cx = float(xs_e.mean()); R = np.ptp(xs_e) / 2 + 2
    yy, xx = np.mgrid[0:U.shape[0], 0:U.shape[1]]
    keep = (yy <= elbow_u_y) | ((xx - cx) ** 2 + (yy - elbow_u_y) ** 2 <= R ** 2)
    U[~keep, 3] = 0
    # a ponta fica sempre por cima do antebraço: sem contorno, só pele (o contorno lateral do braço continua)
    # (só o arco de baixo; os lados da ponta mantêm o contorno do braço)
    low = yy > elbow_u_y + 0.45 * R
    solid = (U[..., 3] > 40).astype(np.uint8)
    edge = (solid > 0) & ~(cv2.erode(solid, np.ones((7, 7), np.uint8)) > 0) & low
    skin_col = np.median(U[sk_u & (yy < elbow_u_y) & (yy > elbow_u_y - 25)][:, :3], axis=0)
    U[edge, :3] = skin_col.astype(np.uint8)
    dark = (U[..., :3].astype(int).sum(2) < 330) & (U[..., 3] > 0) & low
    U[dark, :3] = skin_col.astype(np.uint8)
    elbow_u = (cx, float(elbow_u_y))
    # antebraço: pivô no centro da ponta de cima; escala para a ponta dos dedos cair onde está no gabarito
    fal = F[..., 3] > 40; ftop = int(np.where(fal.any(1))[0].min()); fbot = int(np.where(fal.any(1))[0].max())
    xs_f = np.where(fal[ftop + 30])[0]; elbow_f = (float(xs_f.mean()), ftop + np.ptp(xs_f) / 2)
    f_scale = (tips - elbowA_y) / (fbot - elbow_f[1])
    # escala do gabarito para o rig: largura da barra da camiseta
    hem_rows = np.where(aal.any(1))[0]
    return dict(U=Image.fromarray(U), shoulder_u=shoulder_u, elbow_u=elbow_u, F=Image.fromarray(F), elbow_f=elbow_f, f_scale=f_scale)
ARM_S = 0.87                   # gabarito -> rig (largura da barra da camiseta: 146 px no rig, 170 no gabarito)

# braços e pernas
elbow = {}
for side, dx in (('F', 0), ('B', BACK_DX['arm'])):
    sh = (SHOULDER[0] + dx, SHOULDER[1])
    d = arm_from_sheet(0 if side == 'F' else 1)
    s = ARM_S
    im = d['U'].resize((round(d['U'].width * s), round(d['U'].height * s)), Image.LANCZOS)
    add_image('armUp' + side, im, (round(sh[0] - d['shoulder_u'][0] * s), round(sh[1] - d['shoulder_u'][1] * s)))
    elbow[side] = (float(sh[0] + (d['elbow_u'][0] - d['shoulder_u'][0]) * s), float(sh[1] + (d['elbow_u'][1] - d['shoulder_u'][1]) * s))
    fs = s * d['f_scale']
    im = d['F'].resize((round(d['F'].width * fs), round(d['F'].height * fs)), Image.LANCZOS)
    add_image('armLo' + side, im, (round(elbow[side][0] - d['elbow_f'][0] * fs), round(elbow[side][1] - d['elbow_f'][1] * fs)))
    print('braço', side, 'escala antebraço', round(d['f_scale'], 3))
knee = {}
for side, dx in (('F', 0), ('B', BACK_DX['leg'])):
    hp = (HIP[0] + dx, HIP[1])
    to_rig = place('legUp' + side, 1.0, J['legUp' + side][0], hp)
    knee[side] = to_rig(J['legUp' + side][1])
    place('legLo' + side, 1.0, J['legLo' + side][0], knee[side])

# tronco + quadril (segunda imagem): escala para caber no perfil
B = np.array(Image.open(src2).convert('RGBA'))
n2, lab2, st2, _ = cv2.connectedComponentsWithStats((B[..., 3] > 40).astype(np.uint8), 8)
big2 = sorted([i for i in range(1, n2) if st2[i, 4] > 3000], key=lambda i: st2[i, 1])
def crop2(i):
    x, y, w, h = st2[i, :4]; m = cv2.dilate((lab2[y:y + h, x:x + w] == i).astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    c = B[y:y + h, x:x + w].copy(); c[~m, 3] = 0; return c
TORSO_S = (0.56, 0.50)         # largura igual à barra da camiseta do perfil, altura igual do pescoço à barra
HEM = (1376, 632)              # canto da frente da barra da camiseta no perfil
torso = Image.fromarray(crop2(big2[0])); torso = torso.resize((round(torso.width * TORSO_S[0]), round(torso.height * TORSO_S[1])), Image.LANCZOS)
T = np.array(torso); ys, xs = np.where(T[..., 3] > 40)
tx, ty = HEM[0] - xs.max(), HEM[1] - ys.max()
SHORTS_S = 0.667               # largura do short do perfil
HIP_ROWS = 115                 # só a parte de cima do short (as pernas do short são as coxas)
sh = crop2(big2[1])[:HIP_ROWS]
hh, hw = sh.shape[:2]
yy, xx = np.mgrid[0:hh, 0:hw]
# fundo arredondado: meia elipse no terço de baixo
cy, ry = hh * 0.55, hh * 0.45
round_mask = (yy < cy) | (((xx - hw / 2) / (hw / 2)) ** 2 + ((yy - cy) / ry) ** 2 <= 1)
sh[~round_mask, 3] = 0
shorts = Image.fromarray(sh); shorts = shorts.resize((round(hw * SHORTS_S), round(hh * SHORTS_S)), Image.LANCZOS)
S2 = np.array(shorts); sys_, sxs = np.where(S2[..., 3] > 40)
sx, sy = 1360 - sxs.max(), 612
# junta tronco + short numa imagem só (o short fica atrás da camiseta)
x0, y0 = min(tx, sx), min(ty, sy); x1 = max(tx + torso.width, sx + shorts.width); y1 = max(ty + torso.height, sy + shorts.height)
canvas = Image.new('RGBA', (int(x1 - x0), int(y1 - y0)))
canvas.alpha_composite(shorts, (int(sx - x0), int(sy - y0))); canvas.alpha_composite(torso, (int(tx - x0), int(ty - y0)))
add_image('body', canvas, (x0, y0))
NECK = (tx + (xs[ys < ys.min() + 10].mean()), ty + ys.min() + 22)

# acabamento das juntas: tira o contorno das pontas que ficam por cima de outra peça
def skin_of(c):
    hsv = cv2.cvtColor(np.ascontiguousarray(c[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    return (hsv[..., 0] <= 22) & (hsv[..., 1] > 35) & (hsv[..., 2] > 140) & (c[..., 3] > 0)
def soften_end(key, joint_rig, radius, below=True):
    """pinta de pele o contorno escuro da metade de baixo (ou de cima) da bolinha da junta"""
    b = images[key]['box']; path = f'{out}/{key}.png'; c = np.array(Image.open(path))
    jx, jy = joint_rig[0] - b[0], joint_rig[1] - b[1]
    yy, xx = np.mgrid[0:c.shape[0], 0:c.shape[1]]
    near = ((xx - jx) ** 2 + (yy - jy) ** 2 <= (radius * 1.25) ** 2) & ((yy > jy) if below else (yy < jy))
    dark = (c[..., :3].astype(int).sum(2) < 330) & (c[..., 3] > 0)
    sk = skin_of(c) & ~dark
    col = np.median(c[sk & near][:, :3], axis=0) if (sk & near).sum() > 20 else np.median(c[sk][:, :3], axis=0)
    fix = near & dark
    c[fix, :3] = col.astype(np.uint8)
    Image.fromarray(c).save(path)
for side in 'FB':
    soften_end('legUp' + side, knee[side], 30)
# topo da coxa (perna do short) sem contorno, para emendar no short do quadril
for side in 'FB':
    path = f'{out}/legUp{side}.png'; c = np.array(Image.open(path))
    rows = np.where((c[..., 3] > 0).any(1))[0]; top = rows.min()
    band = np.zeros(c.shape[:2], bool); band[top:top + 16] = True
    navy = (c[..., 2].astype(int) - c[..., 0] > 15) & (c[..., :3].astype(int).sum(2) > 150) & (c[..., 3] > 0)
    col = np.median(c[navy & ~band][:, :3], axis=0)
    dark = (c[..., :3].astype(int).sum(2) < 200) & (c[..., 3] > 0) & band
    c[dark, :3] = col.astype(np.uint8)
    # a bolinha de pele do topo some (fica escondida no short)
    skn = skin_of(c) & band; c[skn, 3] = 0
    Image.fromarray(c).save(path)

def part(name, image, parent, pivot, order):
    parts.append({'name': name, 'image': image, 'parent': parent, 'pivot': [round(float(pivot[0]), 1), round(float(pivot[1]), 1)], 'tint': 1.0, 'order': order})
bsh, bhp = (SHOULDER[0] + BACK_DX['arm'], SHOULDER[1]), (HIP[0] + BACK_DX['leg'], HIP[1])
part('backArmUpper', 'armUpB', 'body', bsh, 0)
part('backArmLower', 'armLoB', 'backArmUpper', elbow['B'], 1)
part('backLegLower', 'legLoB', 'backLegUpper', knee['B'], 2)
part('backLegUpper', 'legUpB', 'body', bhp, 3)
part('frontLegLower', 'legLoF', 'frontLegUpper', knee['F'], 5)
part('body', 'body', 'root', HIP, 6)
part('frontLegUpper', 'legUpF', 'body', HIP, 7)
part('head', 'head', 'body', NECK, 8)
part('frontArmLower', 'armLoF', 'frontArmUpper', elbow['F'], 10)
part('frontArmUpper', 'armUpF', 'body', SHOULDER, 11)
GARRA_LEN = 0.305              # da pegada (topo do cabo azul) até a ponta da garra, em fração da altura (como na ilustração)
held_arm = False
if fist_src:
    # COM O ITEM NA MÃO: o antebraço da frente com a mão fechada e o braço mecânico vem pronto do desenho de perfil
    # segurando a garra (2ª figura de antonio-poses-com-garra.png) e fica parado ("when": "holding"); a mão aberta
    # some ("when": "empty"). A escala e a posição saem de casar a cabeça do desenho com a cabeça do boneco.
    Fi = np.array(Image.open(fist_src).convert('RGBA'))
    n4, lab4, st4, _ = cv2.connectedComponentsWithStats((Fi[..., 3] > 40).astype(np.uint8), 8)
    side_i = sorted([i for i in range(1, n4) if st4[i, 4] > 3000], key=lambda i: st4[i, 0])[1]
    x, y, w, h = st4[side_i, :4]
    fig = Fi[y:y + h, x:x + w].copy(); fig[lab4[y:y + h, x:x + w] != side_i, 3] = 0
    hsv4 = cv2.cvtColor(np.ascontiguousarray(fig[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    a4 = fig[..., 3] > 40
    yel = (hsv4[..., 0] >= 15) & (hsv4[..., 0] <= 35) & (hsv4[..., 1] > 100) & a4
    blue = (hsv4[..., 0] >= 95) & (hsv4[..., 0] <= 130) & (hsv4[..., 1] > 120) & (hsv4[..., 2] > 150) & a4
    gray = (hsv4[..., 1] < 60) & (hsv4[..., 2] > 50) & (hsv4[..., 2] < 140) & a4
    skin = (hsv4[..., 0] <= 20) & (hsv4[..., 1] > 40) & (hsv4[..., 2] > 130) & a4
    yy4, xx4 = np.mgrid[0:h, 0:w]
    # antebraço: pele entre a barra da manga e o fim do short (abaixo é a perna)
    cuff = int(np.where(((hsv4[..., 0] >= 95) & (hsv4[..., 0] <= 130) & (hsv4[..., 1] > 120) & a4)[: h // 2].sum(1) > 20)[0].max())
    shorts_bottom = int(h * 0.71)
    arm = skin & (yy4 > cuff - 4) & (yy4 < shorts_bottom - 30)
    n5, l5, s5, _ = cv2.connectedComponentsWithStats(arm.astype(np.uint8), 8); arm = l5 == 1 + int(np.argmax(s5[1:, 4]))
    garra = (yel | blue) & (yy4 > cuff + 40)            # só a garra (a gola e a manga também são azuis)
    n6, l6, s6, _ = cv2.connectedComponentsWithStats(garra.astype(np.uint8), 8)
    garra = np.isin(l6, [i for i in range(1, n6) if s6[i, 4] > 80])
    garra = cv2.morphologyEx(garra.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
    garra |= gray & (cv2.dilate(garra.astype(np.uint8), np.ones((15, 15), np.uint8)) > 0)
    core = arm | garra
    ink = (fig[..., :3].astype(int).sum(2) < 230) & a4
    m = core | (ink & (cv2.dilate(core.astype(np.uint8), np.ones((7, 7), np.uint8)) > 0))
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)) > 0
    piece4 = fig.copy(); piece4[~m, 3] = 0
    # casa a cabeça do desenho com a cabeça do boneco (escala e deslocamento)
    head_im = np.array(Image.open(f'{out}/head.png').convert('RGBA')).astype(np.float32)
    hb = images['head']['box']; best = None
    for s in np.linspace(0.95, 1.25, 31):
        fs = cv2.resize(fig, (round(w * s), round(h * s)), interpolation=cv2.INTER_AREA).astype(np.float32)
        Pd = np.zeros((fs.shape[0] + 80, fs.shape[1] + 80, 4), np.float32); Pd[40:-40, 40:-40] = fs
        mk = (head_im[..., 3] > 200).astype(np.uint8) * 255
        if head_im.shape[0] > Pd.shape[0] or head_im.shape[1] > Pd.shape[1]: continue
        r = cv2.matchTemplate(Pd[..., :3], np.ascontiguousarray(head_im[..., :3]), cv2.TM_SQDIFF, mask=np.dstack([mk] * 3))
        mn, _, loc, _ = cv2.minMaxLoc(r); mn /= max(1, (mk > 0).sum())
        if best is None or mn < best[0]: best = (mn, s, loc[0] - 40, loc[1] - 40)
    _, s4, lx, ly = best
    # pixel do desenho (u,v) -> rig: (u*s4 - lx + hb[0], v*s4 - ly + hb[1])
    im4 = Image.fromarray(piece4).resize((round(w * s4), round(h * s4)), Image.LANCZOS)
    bb = im4.getbbox(); im4 = im4.crop(bb)
    add_image('armHold', im4, (round(bb[0] - lx + hb[0]), round(bb[1] - ly + hb[1])))
    part('frontArmHold', 'armHold', 'frontArmUpper', elbow['F'], 10)
    parts[-1]['when'] = 'holding'
    for pt in parts:
        if pt['name'] == 'frontArmLower': pt['when'] = 'empty'
    held_arm = True
    print('braço com a garra: escala', round(s4, 3), 'erro', round(best[0]), im4.size)
if grabber_src and not held_arm:
    # braço mecânico pendurado na palma da mão da frente (a 72% do antebraço), por trás da mão:
    # a mão fica logo acima do cabo azul, como na ilustração com a garra
    b = images['armLoF']['box']; al = np.array(Image.open(f'{out}/armLoF.png'))[..., 3] > 100
    gy = int(al.shape[0] * 0.72); xs = np.where(al[gy])[0]
    grip = (b[0] + float(xs.mean()), b[1] + gy)
    knob = b[1] + al.shape[0] * 0.86     # topo do cabo azul fica na ponta dos dedos (o azul aparece embaixo da mão)
    g = Image.open(grabber_src).convert('RGBA'); G = np.array(g)
    hsvg = cv2.cvtColor(np.ascontiguousarray(G[..., :3]), cv2.COLOR_RGB2HSV).astype(int)
    blue = (hsvg[..., 0] >= 95) & (hsvg[..., 0] <= 130) & (hsvg[..., 1] > 100) & (G[..., 3] > 100)
    bys, bxs = np.where(blue); gx0, gy0 = float(bxs.mean()), float(bys.min()) + 4
    height = FLOOR - min(images[k]['box'][1] for k in images)
    gs = GARRA_LEN * height / (G.shape[0] - gy0)   # (giro em volta da palma: grip)
    g = g.resize((round(g.width * gs), round(g.height * gs)), Image.LANCZOS)
    add_image('grabber', g, (round(grip[0] - gx0 * gs), round(knob - gy0 * gs)))
    part('grabber', 'grabber', 'frontArmLower', grip, 9)
# sola (calcanhar e ponta do pé) de cada canela+pé, no espaço do rig: o jogo usa para manter o pé no chão
for pt in parts:
    if pt['name'] in ('frontLegLower', 'backLegLower'):
        b = images[pt['image']]['box']; al = np.array(Image.open(f"{out}/{pt['image']}.png"))[..., 3] > 100
        ys, xs = np.where(al); bottom = ys.max(); low = ys >= bottom - 14
        heel = (xs[low].min(), ys[low][np.argmin(xs[low])]); toe = (xs[low].max(), ys[low][np.argmax(xs[low])])
        pt['sole'] = [[int(b[0] + heel[0]), int(b[1] + heel[1])], [int(b[0] + toe[0]), int(b[1] + toe[1])]]
top_y = min(images[k]['box'][1] for k in images)
rig = {'note': 'Rig do Antônio de perfil (v4, folha de peças do ChatGPT). Coordenadas em pixels do perfil montado (y para baixo). '
               'box = [x, y, w, h] da imagem; pivot = junta onde a parte gira; order = ordem de desenho.',
       'height': int(FLOOR - top_y), 'root': [HIP[0], FLOOR], 'stride': STRIDE, 'images': images, 'parts': parts}
json.dump(rig, open(f'{out}/rig.json', 'w'), indent=1, ensure_ascii=False)
print('altura', FLOOR - top_y, 'cotovelo', elbow, 'joelho', knee)
