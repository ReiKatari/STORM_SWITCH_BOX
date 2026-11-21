> 💡 **Релиз 8.0.7** - *Полноценная нативная поддержка сжатых форматов Nintendo 3DS Azahar Z3DS (`.zcci`, `.zcxi`, `.zcia`, `.z3dsx`) и `.3dsx` на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.0.7)

- 🌟 **[Нативный движок Azahar Z3DS v1 Seekable Zstandard для Windows и Android (`Z3dsService.cs`, `StormNczEngine.kt`)]**:
  - Реализована полноценная побитовая поддержка спецификации **Azahar Z3DS v1**: 32-байтовый заголовок `Z3DS` (`NCSD` -> `.zcci`, `NCCH` -> `.zcxi`, `CIA\0` -> `.zcia`, `3DSX` -> `.z3dsx`), область метаданных с выравниванием на 16 байт (`compressor`, `date`, `maxframesize`, `titleinfo` 24 байта и `smdh` 0x36C0 байт) и независимые кадры **Seekable Zstandard** (`256 КиБ` для `NCSD`/`NCCH`/`3DSX` и `32 МиБ` для `CIA`) с завершающей таблицей смещений `Seek Table` (`0x184D2A5E` / `0x8F92EAB1`).
  - Перед сжатием выполняется обязательная автоматическая дешифровка `NCCH`/`CIA` (через `KeyScrambler` и `SeedDB`) и безопасная обрезка пустых байтов `0xFF` картриджа `NCSD` на временной копии.
  - Реализованы потоковые классы произвольного чтения кадров `Z3dsReadStream` (Windows, кэш LRU на 8 кадров) и `Z3dsReadHandle` (Android), позволяющие читать метаданные, иконки `SMDH` и дерево `RomFS`/`ExeFS` напрямую из сжатых контейнеров без полной распаковки.
- 🌟 **[Сквозная интеграция Z3DS и `.3dsx` во все конвейеры, UI и аудит]**:
  - Добавлены форматы вывода `ZCCI`, `ZCIA`, `ZCXI`, `Z3DSX`, `Z3DS (Авто)` и двойной вывод `3DS + ZCCI` в Задачник, Параметры, «Умную папку» 3DS, Мульти-контент 3DS, проверку целостности (`VerifyZ3dsIntegrity`), Аудитор библиотек, виртуальный инспектор RomFS, Каталог игр, Историю и прямую отправку по Wi-Fi/FTP на Windows и Android (форматы `8..12` и авто-распаковка в режиме `3`).
- 🌟 **[Обновление документации, встроенных инструкций и 11-язычной локализации (`README.md`, `InstructionPage.xaml.cs`, `LocalizationService.UiTree.cs`, `StormLocalization.kt`)]**:
  - Актуализированы руководства по форматам 3DS/Z3DS, расширен интерактивный конвертер форматов в приложении, добавлена благодарность **Azahar Emulator Team**, а все новые строки переведены на 11 языков (`ru`, `en`, `de`, `fr`, `zh`, `ko`, `ja`, `es`, `it`, `pt`, `hi`).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/Z3dsService.cs, Services/Nintendo3dsService.cs, Services/Nintendo3dsCryptoService.cs]**: Новый движок Azahar Z3DS v1 (`Z3dsHeader`, `Z3dsMetadata`, `Z3dsReadStream`, `OpenTransparentReadStream`, `CompressToZ3dsAsync`, `DecompressZ3dsAsync`, `VerifyZ3dsIntegrity`), поддержка `.3dsx` с расширенным заголовком `SMDH` (`0x20`/`0x24`), прозрачное чтение, распаковка и сборка `.zcci`/`.zcxi`/`.zcia`/`.z3dsx`.
- 🔹 **[Models/ProcessingTask.cs, ViewModels/TasksViewModel.cs, Views/TasksPage.xaml, Views/TasksPage.xaml.cs, Views/SettingsPage.xaml, Views/SettingsPage.xaml.cs]**: Расширены списки целевых форматов 3DS (`ZCCI`, `ZCIA`, `ZCXI`, `Z3DSX`, `Z3DS (Авто)`, `3DS + ZCCI`), настройка уровня сжатия Zstd и «Умная папка» 3DS.
- 🔹 **[Services/VirtualRomFsInspectorService.cs, Services/CatalogScannerService.cs, Services/LibraryAuditorService.cs, Services/WatchFolderService.cs, Services/HistoryService.cs, Services/DirectConsoleSenderService.cs, Services/DbiInstallService.cs]**: Полная поддержка `.zcci`, `.zcxi`, `.zcia`, `.z3dsx` и `.3dsx` во всех сервисах сканирования, инспектирования, аудита и передачи.
- 🔹 **[android/.../StormNczEngine.kt, android/.../MainActivity.kt, android/.../StormBinaryAndLocTest.kt]**: Полная реализация Z3DS v1 (`Z3dsReadHandle`, `compressToZ3ds`, `decompressZ3ds`, прозрачное инспектирование, режимы `8..12` и тест `verifyZ3dsFormatMatchesAzaharSpecAndRoundTrips`).
- 🔹 **[README.md, Views/InstructionPage.xaml.cs, Services/LocalizationService.UiTree.cs, android/.../StormLocalization.kt]**: Обновлена техническая документация, интерактивный маршрутизатор форматов и 11-язычные словари интерфейса.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.7)

- 🔸 **[Full Azahar Z3DS v1 Seekable Zstandard Engine on Windows and Android (`Z3dsService.cs`, `StormNczEngine.kt`)]**:
  - Implemented full compliance with the official **Azahar Z3DS v1** binary specification: 32-byte `Z3DS` header (`NCSD` -> `.zcci`, `NCCH` -> `.zcxi`, `CIA\0` -> `.zcia`, `3DSX` -> `.z3dsx`), 16-byte aligned metadata (`compressor`, `date`, `maxframesize`, 24-byte `titleinfo`, 0x36C0 `smdh`), and independent **Seekable Zstandard** frames (`256 KiB` for `NCSD`/`NCCH`/`3DSX`, `32 MiB` for `CIA`) with a trailing `Seek Table` skippable frame (`0x184D2A5E` / `0x8F92EAB1`).
  - Enforces automatic `NCCH`/`CIA` decryption (via `KeyScrambler` and `SeedDB`) and non-destructive temp-copy `0xFF` NCSD cartridge trimming prior to compression.
  - Added thread-safe random-access streams (`Z3dsReadStream` on Windows, `Z3dsReadHandle` on Android) for direct `Parse3dsFile`, `SMDH`, and `RomFS`/`ExeFS` inspection without full decompression.
- 🔸 **[End-to-End 3DS Workflow Integration and Android UI/Tests]**:
  - Added `ZCCI`, `ZCIA`, `ZCXI`, `Z3DSX`, `Z3DS (Auto)`, and dual `3DS + ZCCI` targets across the Windows queue, Multi-Content 3DS builder, Smart Watch Folder, Virtual RomFS Inspector, Library Auditor, Catalog Scanner, History, and Console Sender, plus Android output modes `8..12`, Mode `3` Z3DS decompression, and JUnit round-trip tests (`StormBinaryAndLocTest`).
- 🔸 **[Documentation, Interactive Guides, and 11-Language Localization]**:
  - Updated `README.md` (with **Azahar Emulator Team** attribution), `InstructionPage.xaml.cs` RU/EN 3DS topics and interactive converter, Android instruction topic #9, and 11-language translation tables (`LocalizationService.UiTree.cs`, `StormLocalization.kt`).

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.7_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.7.zip` / `STORM_SWITCH_BOX_8.0.7_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.7.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.7_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.7_Setup.exe`** (219.68 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.7.zip`** / **`_win-x64.zip`** (151.23 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.7.apk`** (14.40 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.7_LEGACY.apk`** (25.97 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.7_Setup.exe** (219.68 МБ)
  - `SHA-256: 1002D1D668AECFF52C3DE6815BA43D82986F78C68D85260A2CAAA718EE9BE135`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.7_win-x64.zip** / **STORM_SWITCH_BOX_8.0.7.zip** (151.23 МБ)
  - `SHA-256: 71FAFCFDFD1BDD15474E5B46F2621F89E8D1EA0F03AE0B1D23BA8B641BC03A76`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.7.apk** (14.40 МБ)
  - `SHA-256: 27E623EA36E72B7DE977C812EC0E3E1B7D079C2953F5935A8C882E9692DC4AC8`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.7_LEGACY.apk** (25.97 МБ)
  - `SHA-256: 72A2FB752C8A39FF4648E674690AEC919C7AB19273C5DDBEA153FE4B724BF3F9`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
