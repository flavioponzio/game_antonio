import sys, json
from PIL import Image
import numpy as np, cv2
im=np.array(Image.open(sys.argv[1]).convert('RGB')); I=im.astype(int); H,W=im.shape[:2]
names=[['front','front34','side','back34','back'],['walk1','walk2','run1','run2','blink'],['jump','land','crouch','lookUp','lookDown'],['sit','walk3','walk4','talk','pickUp']]
rows=[(0,290),(290,540),(540,780),(780,1024)]
ink=(I.sum(2)<700).astype(np.uint8)
res={}
for r,(y0,y1) in enumerate(rows):
    for c in range(5):
        x0,x1=int(c*W/5),int((c+1)*W/5)
        cell=ink[y0:y1,x0:x1]
        n,lab,st,_=cv2.connectedComponentsWithStats(cv2.dilate(cell,np.ones((3,3),np.uint8)),8)
        k=1+int(np.argmax(st[1:,4])); bx,by,bw,bh=st[k,:4]
        # cabelo: castanho (r>g>b, escuro-médio)
        sub=I[y0+by:y0+by+bh, x0+bx:x0+bx+bw]; m=(lab[by:by+bh,bx:bx+bw]==k)
        hair=m&(sub[...,0]-sub[...,2]>45)&(sub[...,0]<185)&(sub[...,0]>90)&(sub[...,1]<130)&(sub[...,1]>55)
        ys,xs=np.where(hair)
        # largura do cabelo na faixa mais larga (percentil para evitar fios soltos)
        hw=float(np.percentile(xs,98)-np.percentile(xs,2)) if len(xs) else 0
        hh=float(np.percentile(ys,98)-np.percentile(ys,0.5)) if len(ys) else 0
        res[names[r][c]]={'box':[int(x0+bx),int(y0+by),int(bw),int(bh)],'hairW':round(hw,1),'hairH':round(hh,1)}
json.dump(res,open(sys.argv[2],'w'),indent=1)
for k,v in res.items(): print(f"{k:9s} box={v['box']} hairW={v['hairW']:6.1f} hairH={v['hairH']:6.1f}")
