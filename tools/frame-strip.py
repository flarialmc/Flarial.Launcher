import os,re,subprocess,sys
R,L,label,out=sys.argv[1:5]; offs=[int(x) for x in sys.argv[5:]]
def load(d):
    return [(int(m.group(1)),f) for f in sorted(os.listdir(d)) if (m:=re.match(r'\d+-(\d+)-(.*)\.png$',f)) and m.group(2)==label]
rows=[]
for d in (R,L):
    fs=load(d); t0=fs[0][0]
    for o in offs:
        t,f=min(fs,key=lambda x:abs(x[0]-t0-o)); rows.append(f'{d}/{f}')
subprocess.run(['montage',*rows,'-tile',f'{len(offs)}x','-geometry','300x188+2+2','-background','#555',out])
