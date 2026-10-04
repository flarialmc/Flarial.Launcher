#!/bin/bash
# Walk every screen of the Linux build and dump window frames to $1 (default /tmp/flarial-linux-frames).
export DOTNET_ROOT="${DOTNET_ROOT:-/run/media/bariman/url/Other Programs/dotnet}"
export PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")/.."
OUT=${1:-/tmp/flarial-linux-frames}
export FLARIAL_SHOT=/tmp/flarial-linux-state; rm -rf $FLARIAL_SHOT
tools/watch.sh $OUT $FLARIAL_SHOT &
timeout 120 dotnet run --project src/Flarial.Launcher -c Debug; wait
