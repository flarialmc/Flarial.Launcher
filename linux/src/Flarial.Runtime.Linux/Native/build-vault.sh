#!/bin/bash
# Builds flarial_vault.dll (x64, static CRT, C++/WinRT) from flarial_vault.cpp with msvc-wine.
set -e
cd "$(dirname "$0")"
M="/run/media/bariman/url/Other Programs/msvc/bin/x64"
export WINEDLLOVERRIDES="mountmgr.sys=d"
"$M/cl" /nologo /O2 /MT /std:c++20 /EHsc /DUNICODE /D_UNICODE /LD flarial_vault.cpp /Fe:flarial_vault.dll \
  /link /EXPORT:DllGetActivationFactory,PRIVATE /EXPORT:DllCanUnloadNow,PRIVATE runtimeobject.lib ole32.lib oleaut32.lib
rm -f flarial_vault.obj flarial_vault.lib flarial_vault.exp
