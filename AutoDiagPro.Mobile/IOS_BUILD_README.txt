AutoDiag Pro iOS v1.1.0

Что готово:
- Вход по email/паролю и Google через AutoDiag Server.
- Сессия хранится в iOS SecureStorage/Keychain.
- Мои автомобили и выбор активного авто.
- История серверных диагностик.
- Клиентская панель с DTC, последним scan и заказами.
- Диагностика ELM327: VIN, протокол, напряжение, DTC и Live Data.
- Полный scan с сохранением результата на AutoDiag Server.
- Wi-Fi OBD через TCP.
- Bluetooth LE OBD: поиск, выбор, подключение, GATT write/notify и ELM327.
- AutoDiag AI с контекстом автомобиля/последней диагностики и веб-поиском.
- Настройки OBD, региона поиска запчастей и выход из аккаунта.
- iOS разрешения для локальной сети, Bluetooth, камеры и фото.
- Фирменная тёмная graphite/amber тема, app icon и splash screen.

Проверка:
dotnet build AutoDiagPro.Mobile.csproj -f net8.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
Результат последней проверки: 0 warnings, 0 errors.

Важно:
Windows может проверить C#/.NET iOS-проект, но финальная подписанная .ipa требует macOS + Xcode и Apple signing.
Для реального OBD теста нужен физический iPhone и совместимый BLE или Wi-Fi ELM327/Vgate адаптер.

Backend:
https://api-autodiagpro.duckdns.org/