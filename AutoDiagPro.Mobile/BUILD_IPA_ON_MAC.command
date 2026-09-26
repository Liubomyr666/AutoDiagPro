#!/bin/zsh
set -e
cd "$(dirname "$0")"

echo "AutoDiag Pro iOS v2.0.0"
echo "Unsigned IPA for AltStore / AltServer"

dotnet --version
dotnet workload restore AutoDiagPro.Mobile.csproj

dotnet build AutoDiagPro.Mobile.csproj \
  -f net8.0-ios \
  -c Release \
  -r ios-arm64 \
  -p:EnableCodeSigning=false \
  -p:ArchiveOnBuild=false

APP_PATH="$(find bin/Release/net8.0-ios/ios-arm64 -type d -name '*.app' | head -n 1)"
if [[ -z "$APP_PATH" ]]; then
  echo "No .app bundle found"
  exit 1
fi

rm -rf ipa
mkdir -p ipa/Payload
cp -R "$APP_PATH" ipa/Payload/
cd ipa
/usr/bin/zip -qry ../AutoDiagPro_iOS_v2.0.0_AltStore.ipa Payload
cd ..
echo "READY: AutoDiagPro_iOS_v2.0.0_AltStore.ipa"