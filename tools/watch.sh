#!/bin/bash
# usage: watch.sh <outdir> <shotdir>; captures window frames until state==done
OUT=$1; SD=$2; rm -rf $OUT; mkdir -p $OUT
until [ -f $SD/state ]; do sleep 0.2; done
sleep 1.5
W=""
for i in $(seq 100); do for w in $(xprop -root _NET_CLIENT_LIST | sed 's/.*# //;s/,//g'); do
 n=$(xprop -id $w _NET_WM_NAME 2>/dev/null); case "$n" in *'"Flarial Launcher"') W=$w;; esac; done; [ -n "$W" ] && break; sleep 0.2; done
echo win=$W
while [ "$(cat $SD/state)" != done ]; do
  s=$(cat $SD/state); t=$(date +%s%3N)
  magick x:$W $OUT/$t-$s.png 2>/dev/null
done
