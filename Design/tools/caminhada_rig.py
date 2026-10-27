"""Ciclo de caminhada do rig (mesma conta que vai para o CutoutRig.ApplyWalk)."""
import math
KNEE = [-45, -20, -5, -8, -3, -5, -20, -55]      # joelho em 8 pontos do passo (0 = passagem com a perna no ar)
def periodic(keys, phase):
    """interpolação Catmull-Rom periódica entre os 8 pontos"""
    n = len(keys); u = (phase / (2 * math.pi)) % 1.0 * n; i = int(u); f = u - i
    p0, p1, p2, p3 = (keys[(i + k) % n] for k in (-1, 0, 1, 2))
    return 0.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f ** 3)
def walk_angles(phase, amount=1.0, thigh=27, arm=20):
    a = {}
    for leg, ph in (('front', phase), ('back', phase + math.pi)):
        a[leg + 'LegUpper'] = thigh * math.sin(ph) * amount
        a[leg + 'LegLower'] = periodic(KNEE, ph) * amount
    for side, ph in (('front', phase + math.pi), ('back', phase)):   # braço ao contrário da perna do mesmo lado
        s = math.sin(ph)
        a[side + 'ArmUpper'] = arm * s * amount
        a[side + 'ArmLower'] = (8 + 16 * max(0.0, s)) * amount
    a['body'] = -3 * amount
    a['head'] = (2 + 1.5 * math.cos(2 * phase)) * amount
    return a
