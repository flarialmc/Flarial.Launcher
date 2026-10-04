import os,re,subprocess,sys
R,L=sys.argv[1:3]
def load(d,label):
    fs=[]
    for f in sorted(os.listdir(d)):
        m=re.match(r'\d+-(\d+)-(.*)\.png$',f)
        if m and m.group(2)==label: fs.append((int(m.group(1)),f))
    return fs
for label in ['t-to-settings','t-to-versions','t-to-general','t-to-home','perf-t-to-settings','perf-t-to-home']:
    a=load(R,label); b=load(L,label)
    a0=a[0][0]; b0=b[0][0]; res=[]
    for t,f in a:
        t-=a0
        bt,bf=min(b,key=lambda x:abs(x[0]-b0-t))
        v=subprocess.run(['compare','-metric','AE','-fuzz','3%',f'{R}/{f}',f'{L}/{bf}','/dev/null'],capture_output=True,text=True).stderr.split()[0]
        res.append((t,int(float(v))))
    print(label,len(a),len(b),'max',max(r[1] for r in res),'mean',int(sum(r[1] for r in res)/len(res)),[r[1] for r in res][::4])
