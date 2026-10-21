#!/bin/sh
# Builds N1TA_MOFLOF.exe with Mono's mcs (runs on Linux/macOS/Windows with Mono installed).
# The result is a plain .NET Framework 4.x executable and runs on stock Windows 7/10/11.
set -e
cd "$(dirname "$0")"
mkdir -p dist
mcs -target:winexe -optimize+ -sdk:4.5 \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll \
    -win32icon:res/n1ta.ico \
    -resource:res/coast.txt,coast.txt \
    -resource:res/cty.dat,cty.dat \
    -out:dist/N1TA_MOFLOF.exe src/*.cs
echo "Built dist/N1TA_MOFLOF.exe"
