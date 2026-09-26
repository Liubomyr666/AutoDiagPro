#!/bin/zsh
set -e
cd "$(dirname "$0")"

echo "AutoDiag Pro iOS v1.3.1"
echo "Проверяю .NET..."
dotnet --info >/dev/null

read "SIGN_KEY?Apple signing key (пример: Apple Development: Name (TEAMID)): "
read "PROVISION?Provisioning profile name/UUID: "

if [[ -z "$SIGN_KEY" || -z "$PROVISION" ]]; then
  echo "Не указана подпись или provisioning profile."
  exit 1
fi

dotnet publish AutoDiagPro.Mobile.csproj \
  -f net8.0-ios \
  -c Release \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:ArchiveOnBuild=true \
  -p:CodesignKey="$SIGN_KEY" \
  -p:CodesignProvision="$PROVISION"

echo ""
echo "Готово. Ищи .ipa в:"
echo "bin/Release/net8.0-ios/ios-arm64/publish/"