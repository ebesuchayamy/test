# Build: SuperVPN single EXE

Этот каталог содержит сборку single EXE, захардкоженную под SuperVPN.

## Что находится в папке

- `build-single-exe.ps1` — основной скрипт сборки.
- `single_launcher/` — .NET launcher, в который встраивается `payload.zip`.
- `launcher.cmd` и `single_exe.sed` — legacy-вариант через IExpress.

## Требования

- Windows.
- Установленный .NET SDK.
- Доступный в PATH `tar` (bsdtar в Windows 10/11).
- В корне проекта должны быть:
  - `super_vpn.exe`
  - `flutter_windows.dll`
  - `audioplayers_windows_plugin.dll`
  - `screen_retriever_windows_plugin.dll`
  - `window_manager_plugin.dll`
  - `core/`
  - `data/`

## Как собрать

Из корня репозитория выполните:

```powershell
powershell -ExecutionPolicy Bypass -File .\build\build-single-exe.ps1
```

После успешной сборки появится:

- `dist\\SuperVPN_Single.exe`

## Что делает launcher при запуске

1. Выбирает путь установки:
    - `%ProgramFiles%\\SuperVPN`, если процесс запущен от администратора.
    - `%LOCALAPPDATA%\\SuperVPN`, если без прав администратора.
2. Распаковывает payload (если `super_vpn.exe` отсутствует).
3. Копирует launcher в папку приложения и создаёт `uninstall.exe`.
4. Создаёт ярлыки:
    - Desktop: `SuperVPN.lnk`
    - Start Menu: `Programs\\SuperVPN\\SuperVPN.lnk`
    - Start Menu: `Programs\\SuperVPN\\Uninstall SuperVPN.lnk`
5. Пишет запись удаления в реестр:
    - HKLM или HKCU: `Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\SuperVPN`
6. Запускает `super_vpn.exe`.

## Что делает деинсталлятор

- Удаляет ярлыки Start Menu и Desktop.
- Удаляет запись из Uninstall в HKLM/HKCU.
- Останавливает `super_vpn.exe` (best-effort).
- Удаляет папку установки через отложенный cleanup-скрипт.

## Troubleshooting

- Если не хватает `MSVCP140.dll`, установите Microsoft Visual C++ Redistributable 2015-2022 x64.
- Если `tar` недоступен, проверьте PATH.
- Если блокирует политика PowerShell, используйте `-ExecutionPolicy Bypass`.
