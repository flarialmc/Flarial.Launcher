#!/usr/bin/env python3
"""Drive the launcher window with XTEST (hover/press/click) and dump frames. usage: interact.py <outdir> [window title]
Run the app first (without FLARIAL_SHOT). Works on XWayland: the pointer is injected into the X server."""
import ctypes, subprocess, sys, time, os, re
X = ctypes.CDLL('libX11.so.6'); T = ctypes.CDLL('libXtst.so.6')
X.XOpenDisplay.restype = ctypes.c_void_p
d = X.XOpenDisplay(None)
out = sys.argv[1]; title = sys.argv[2] if len(sys.argv) > 2 else 'Flarial Launcher'
os.makedirs(out, exist_ok=True)

def find():
    ids = re.findall(r'0x[0-9a-f]+', subprocess.run(['xprop', '-root', '_NET_CLIENT_LIST'], capture_output=True, text=True).stdout)
    for i in ids:
        n = subprocess.run(['xprop', '-id', i, '_NET_WM_NAME'], capture_output=True, text=True).stdout
        if n.strip().endswith('"%s"' % title): return int(i, 16)
win = None
while not win: win = find(); time.sleep(.3)
root = X.XDefaultRootWindow(ctypes.c_void_p(d))
def origin():
    x = ctypes.c_int(); y = ctypes.c_int(); c = ctypes.c_ulong()
    X.XTranslateCoordinates(ctypes.c_void_p(d), ctypes.c_ulong(win), ctypes.c_ulong(root), 0, 0, ctypes.byref(x), ctypes.byref(y), ctypes.byref(c))
    return x.value, y.value
def move(px, py):
    ox, oy = origin()
    T.XTestFakeMotionEvent(ctypes.c_void_p(d), -1, ox + px, oy + py, 0); X.XFlush(ctypes.c_void_p(d))
def button(down):
    T.XTestFakeButtonEvent(ctypes.c_void_p(d), 1, 1 if down else 0, 0); X.XFlush(ctypes.c_void_p(d))
n = [0]
T0 = time.time()
def shot(name):
    n[0] += 1; subprocess.run(['magick', 'x:%d' % win, '%s/%03d-%05d-%s.png' % (out, n[0], int((time.time()-T0)*1000), name)])
def burst(name, secs):
    t = time.time()
    while time.time() - t < secs: shot(name)
def settle(s=.5): time.sleep(s)

time.sleep(12)  # startup + network
move(5, 5); settle(); shot('home-idle')
for name, (x, y) in {'launch': (400, 260), 'settings-btn': (400, 315), 'minimize': (733, 38), 'close': (767, 38), 'user': (620, 38)}.items():
    move(x, y); settle(); shot('home-hover-' + name)
move(400, 315); settle(.3); button(True); settle(.4); shot('home-press-settings')
button(False); burst('t-to-settings', 1.6); shot('settings-general')
pts = {'versions': (115, 185), 'general': (115, 130), 'return': (115, 440), 'open-client': (377, 181), 'open-launcher': (638, 181),
       'toggle-auto': (740, 252), 'toggle-perf': (740, 283), 'seg-release': (335, 357), 'seg-beta': (507, 357), 'seg-custom': (680, 357), 'login': (730, 92)}
for name, (x, y) in pts.items():
    move(x, y); settle(); shot('settings-hover-' + name)
move(740, 283); settle(.3); button(True); settle(.4); shot('settings-press-toggle-perf'); button(False); settle(.6); shot('settings-perf-on')
move(680, 357); settle(.3); button(True); settle(.4); shot('settings-press-seg-custom'); button(False); settle(.8); shot('settings-custom-selected')
move(335, 357); settle(.3); button(True); button(False); settle(.8)
move(115, 185); settle(.3); button(True); button(False); burst('t-to-versions', 1.6); shot('settings-versions')
move(115, 130); settle(.3); button(True); button(False); burst('t-to-general', 1.6)
move(115, 440); settle(.3); button(True); button(False); burst('t-to-home', 1.6); shot('home-after')
# performance mode transitions (toggle was left on)
move(400, 315); settle(.3); button(True); button(False); burst('perf-t-to-settings', 1.2)
move(115, 440); settle(.3); button(True); button(False); burst('perf-t-to-home', 1.2)
# launch dialog (game not found on Linux / wine ref has no game either)
move(400, 260); settle(.3); button(True); button(False); burst('launch-click', 2.5)
move(5, 5)
