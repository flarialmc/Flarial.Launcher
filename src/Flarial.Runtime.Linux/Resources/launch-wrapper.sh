#!/bin/bash
# Run by `xodus-cli run <game_dir> <this script> ` after it decrypted the game executable into memfds
# and published them in WINE_DLL_FILE_MAP="<fd>:<NT path>|...". The Steam Linux Runtime container cannot see our
# fds, so each one is copied to a private 0600 file in RAM (the engine unlinks it as soon as it opens it), the
# PE stack reserve is raised to 16 MiB (settings/pause stack overflow) and the map is rewritten to those paths.
# Env: FLARIAL_UMU_RUN (umu-run path), FLARIAL_REAL_HOME, FLARIAL_STAGE_DIR (default /dev/shm), FLARIAL_EXE_NAME.
ts() { [[ -n "${FLARIAL_LAUNCH_LOG:-}" ]] && echo "$(date +%FT%T.%3N)               wrapper: $1" >> "$FLARIAL_LAUNCH_LOG"; }
ts "started (xodus decrypt + licence check done)"
stage="${FLARIAL_STAGE_DIR:-/dev/shm}"
exe="${FLARIAL_EXE_NAME:-Minecraft.Windows.exe}"
mkdir -p -m 700 "$stage"
IFS='|' read -ra entries <<< "${WINE_DLL_FILE_MAP:-}"
new=""; target="${1:-}"
for e in "${entries[@]}"; do
  fd="${e%%:*}"; nt="${e#*:}"
  [[ "$fd" =~ ^[0-9]+$ && -n "$nt" ]] || continue
  f=$(mktemp "$stage/flarial-stage-XXXXXX") || exit 1
  chmod 600 "$f"
  cat < "/proc/self/fd/$fd" > "$f" || { rm -f "$f"; echo "flarial: cannot stage decrypted game in $stage" >&2; exit 1; }
  base="${nt##*\\}"
  if [[ "${base,,}" == "${exe,,}" ]]; then
    target="$nt"
    lfanew=$(od -An -tu4 -j60 -N4 "$f" | tr -d ' ')
    if [[ "$(head -c2 "$f")" == "MZ" && "$(od -An -tx2 -j$((lfanew + 24)) -N2 "$f" | tr -d ' ')" == "020b" ]]; then
      off=$((lfanew + 24 + 72))
      cur=$(od -An -tu8 -j"$off" -N8 "$f" | tr -d ' ')
      (( cur < 16777216 )) && printf '\x00\x00\x00\x01\x00\x00\x00\x00' | dd of="$f" bs=1 seek="$off" conv=notrunc status=none
    fi
  fi
  new+="$f:$nt|"
done
[[ -n "$target" ]] || { echo "flarial: no executable passed by xodus-cli run" >&2; exit 1; }
[[ -n "$new" ]] && export WINE_DLL_FILE_MAP="${new%|}"
ts "staged decrypted exe, exec umu-run"
export HOME="${FLARIAL_REAL_HOME:-$HOME}"
for v in CONFIG_HOME CACHE_HOME DATA_HOME STATE_HOME; do
  r="FLARIAL_REAL_XDG_$v"
  if [[ -n "${!r+x}" ]]; then export "XDG_$v=${!r}"; else unset "XDG_$v"; fi
  unset "$r"
done
unset FLARIAL_REAL_HOME
exec python3 "$FLARIAL_UMU_RUN" "$target"
