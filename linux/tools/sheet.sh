#!/bin/bash
# one (last) frame per state -> sheet
D=$1; mkdir -p $D/pick; rm -f $D/pick/*
for s in home home-logged-in settings-general settings-general-logged-in settings-versions settings-configs notification dialog; do
 f=$(ls $D | grep -E "^[0-9]+-$s.png$" | tail -1); [ -n "$f" ] && cp $D/$f $D/pick/$s.png
done
cd $D/pick; montage -label '%t' *.png -tile 3x -geometry 520x325+3+3 -background '#555' -fill white ../sheet.png
