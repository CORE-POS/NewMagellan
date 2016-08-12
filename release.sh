#!/bin/sh

# do not have permission to distribute these
rm AxDSI*.dll
rm DSI*.dll

rm -rf ss-output
rm ports.conf

# general cruft
rm logo.bmp
rm log.xml
rm posdriver-sph*
rm pos.service
rm posd.sh
rm posSVC.InstallState
rm .gitignore
rm sscom.sh
rm "newer pos.bat"
rm pos_1.ico
rm README

# older build scripts
rm Makefile
rm make.bat

mkdir bin
cp *.dll bin/
rm *.dll
mv pos.exe bin/

mkdir src
cp *.cs src/
rm *.cs
mv NewMagellan.csproj src/

mkdir packages
mv Newtonsoft.Json packages/
mv HidSharp packages/
mv rabbitmq packages/

date -u "+%s" > bin/BUILD
