import json, sys, math
from PIL import Image
R='/home/user/game_antonio/Assets/Resources/EP01/'
rig=json.load(open(R+'assets/characters/antonio-rig/rig.json'))
imgs={k:Image.open(R+v['file']).convert('RGBA') for k,v in rig['images'].items()}
parts={p['name']:p for p in rig['parts']}
angles=json.loads(sys.argv[1]); hipx, seat_top, flip = float(sys.argv[2]), float(sys.argv[3]), -1
bg=Image.open(R+'assets/scenes/ep01-sala-2d.png').convert('RGBA'); W,H=bg.size
k = 0.46*H/rig['height']   # px de cena por px do rig
W_={}
def tf(n):
    if n in W_: return W_[n]
    p=parts[n]; a=angles.get(n,0)
    if p['parent']=='root': W_[n]=(tuple(p['pivot']),a); return W_[n]
    pp=parts[p['parent']]; (px,py),pa=tf(p['parent'])
    dx=p['pivot'][0]-pp['pivot'][0]; dy=p['pivot'][1]-pp['pivot'][1]; r=math.radians(-pa)
    W_[n]=((px+dx*math.cos(r)-dy*math.sin(r), py+dx*math.sin(r)+dy*math.cos(r)), pa+a); return W_[n]
canvas=Image.new('RGBA',(700,700),(0,0,0,0))
hip=parts['body']['pivot']
for p in sorted(rig['parts'],key=lambda q:q['order']):
    if p['name']=='grabber': continue
    (wx,wy),a=tf(p['name']); im=imgs[p['image']].copy(); bx,by,bw,bh=rig['images'][p['image']]['box']
    if p['tint']<1:
        r_,g,b,al=im.split(); f=lambda v:int(v*p['tint']); im=Image.merge('RGBA',(r_.point(f),g.point(f),b.point(f),al))
    big=Image.new('RGBA',(900,900),(0,0,0,0)); big.paste(im,(int(450-(p['pivot'][0]-bx)),int(450-(p['pivot'][1]-by))),im)
    big=big.rotate(a,resample=Image.BICUBIC,center=(450,450))
    canvas.alpha_composite(big,(int(wx-450+350-hip[0]),int(wy-450+350-hip[1])))
if flip<0: canvas=canvas.transpose(Image.FLIP_LEFT_RIGHT)
canvas=canvas.resize((int(700*k),int(700*k)),Image.LANCZOS)
cx=int(hipx/100*W); cy=int(seat_top/100*H)
bg.alpha_composite(canvas,(cx-canvas.width//2, cy-canvas.height//2))
bg.crop((int(0.15*W),int(0.40*H),int(0.48*W),H)).convert('RGB').save(sys.argv[4])
