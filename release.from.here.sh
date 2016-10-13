#!/bin/sh
# Assemble a release in "bin" from this repo

mkdir bin
cp NewMagellan/bin/x86/Release/*.dll bin/
# do not have permission to distribute these
rm bin/AxDSI*.dll
rm bin/DSI*.dll
cp NewMagellan/bin/x86/Release/NewMagellan.exe bin/
# copy as older name too for compatibility
cp NewMagellan/bin/x86/Release/NewMagellan.exe bin/pos.exe
date -u "+%s" > bin/BUILD

