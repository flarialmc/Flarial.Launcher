#!/bin/bash
# Builds injector.exe (x64, static CRT) from injector.c. Prefers mingw, falls back to msvc-wine.
set -e
cd "$(dirname "$0")"
if command -v x86_64-w64-mingw32-gcc >/dev/null; then
  x86_64-w64-mingw32-gcc -O2 -municode -s injector.c -o injector.exe
else
  M="/run/media/bariman/url/Other Programs/msvc/bin/x64"
  export WINEDLLOVERRIDES="mountmgr.sys=d"
  "$M/cl" /nologo /O2 /MT /DUNICODE /D_UNICODE injector.c /Fe:injector.exe /link /SUBSYSTEM:CONSOLE
  rm -f injector.obj
fi
