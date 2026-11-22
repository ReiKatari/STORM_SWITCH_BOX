> 💡 **Релиз 8.0.11** - *Скоростной P2P-движок без лимитов скорости и подключений, синхронизация чекбоксов игр и файлов раздачи, формат NSZ по умолчанию и полный паритет на Android*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.0.11)

- 🌟 **[Исправление подключения к сидам и снятие всех лимитов скорости/соединений (`storm_lt_turbo.py`, `RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerSeedingService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Встроенный C++ движок `libtorrent-rasterbar 2.1.1.0` (`storm_lt_turbo.py`) переведён в основной режим работы (Tier 1), чтобы закачки не «зависали» на `0,0 МБ/с • Сиды: 0` при работающем в фоне отключённом или ограниченном внешнем клиенте `qBittorrent`.
  - Для резервного `qBittorrent` (Tier 2) добавлены мгновенный пропуск при статусе `disconnected` и 0 DHT-узлах, принудительный сброс лимитов скорости на торренте и 20-секундный сторожевой таймер автопереключения при нулевой активности.
  - Полностью сняты ограничения числа подключений, слотов и очереди: `connections_limit = 10000`, `max_peerlist_size = 10000`, `connection_speed = 2000`, `active_downloads = -1`, `active_seeds = -1`, `active_limit = -1`, `unchoke_slots_limit = -1`, `download_rate_limit = 0`, `upload_rate_limit = 0`, сняты лимиты на хендлах торрента, `MaxConcurrentDownloads` увеличен до 16 (`MaxTotalConcurrentDownloads = 64`), а на Android лимит подключений увеличен до 2000 с безлимитным числом пиров на раздачу.
  - Сохранены оригинальные трекеры `bt*.t-ru.org` (включая `?pk=`) с добавлением публичных трекеров (`numwant = 500`), добавлено безопасное кэширование таблицы DHT (`--dht-state` / `save_dht_state`) без перезаписи лимитов сессии и ускорена повторная инъекция пиров.
- 🌟 **[Синхронизация «Игры и части в раздаче» и «Файлы в раздаче» (`RuTrackerModDownloaderDialog`)]**:
  - При отключении игры или части в списке «Игры и части в раздаче» автоматически снимаются галочки со всех относящихся к ней файлов и ссылок обсуждения в блоке «Файлы в раздаче». При повторном включении возвращается прежний выбор файлов (или умный выбор), а при отметке файла выключенной игры её слот включается автоматически.
- 🌟 **[Формат NSZ по умолчанию для Nintendo Switch 1 и Nintendo Switch 2]**:
  - Во всех параметрах по умолчанию, задачах конвертации и загрузчике RuTracker для Nintendo Switch 1 и Nintendo Switch 2 установлен формат сжатия `NSZ` (с однократной миграцией существующих настроек и без изменения форматов Nintendo 3DS).
- 🌟 **[Полный паритет функций на Android (`MainActivity.kt`, `RuTrackerCloudEngine.kt`, `StormNczEngine.kt`)]**:
  - В диалоге скачивания RuTracker на Android добавлен блок «0. Игры и части в раздаче» с двусторонней синхронизацией файлов, каноническая группировка файлов по 8 категориям (`ИГРА`, `ОБНОВЛЕНИЕ`, `ДОПОЛНЕНИЯ`, `UNLOCKER`, `МОДЫ`, `ОБНОВЛЕНИЕ (РУС)`, `СЭЙВЫ`, `ИНФО`), исправление классификации обновлённых DLC `[v>0]`, автоматическое отключение модов, русификаторов, сэйвов и инфо по умолчанию и кнопка копирования чистого названия DLC.
  - Реализованы разделение многоигровых раздач (например, *Aggelos 1-2*) в отдельные задачи с маршрутизацией в целевые папки Switch 1 и Switch 2 (`NSZ` по умолчанию), автоочистка завершённых задач при удалении исходных файлов и постоянный блок `📡 Раздачи` с живой статистикой и управлением сидированием (`libtorrent4j` `UPLOAD_MODE` + JSON-персистентность).
  - На вкладках задач и инспектора на Android добавлены нерекурсивный режим сканирования папок по умолчанию с чекбоксом «Включать подпапки (рекурсивный поиск)» и проверка обновлений версий и новых DLC по каталогу RuTracker (#1605).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Безлимитный профиль настроек сессии и хендла торрента, сохранение трекеров `t-ru.org`, безопасное чтение/запись только DHT-маршрутизации (`load_dht_state_only` / `save_dht_state_only`) и ускоренный опрос/переподключение пиров.
- 🔹 **[Services/RuTrackerModService.cs, Services/RuTrackerDownloadQueueService.cs, Services/RuTrackerSeedingService.cs]**: Повышение embedded `libtorrent-rasterbar` до Tier 1, защитные проверки и фоллбэк из отключённого `qBittorrent`, передача `--dht-state`, `numwant=500`, расширение очереди `MaxConcurrentDownloads=16` и лимитов MonoTorrent.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml, .xaml.cs, Models/AppSettings.cs, Services/SettingsService.cs, Models/ProcessingTask.cs, ViewModels/TasksViewModel.cs, Services/MultiContentService.cs, Views/TasksPage.xaml.cs, Views/SettingsPage.xaml.cs]**: Двусторонняя синхронизация галочек слотов игр и файлов, установка формата `NSZ` по умолчанию для Switch 1 и Switch 2 с однократной миграцией.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt, MainActivity.kt, StormNczEngine.kt, StormBinaryAndLocTest.kt]**: Полный паритет классификации 8 категорий, чистого копирования имён DLC, многоигровых слотов и их синхронизации, автоочистки и P2P-раздачи `libtorrent4j`, снятия лимитов подключений, нерекурсивного сканирования по умолчанию и проверки обновлений RuTracker; добавлены юнит-тесты в `StormBinaryAndLocTest.kt` (7/7 PASS).

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.11)

- 🔸 **[P2P Seed Connectivity Fix and Unlimited Speed/Connections (`storm_lt_turbo.py`, `RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerSeedingService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Promoted embedded C++ `libtorrent-rasterbar 2.1.1.0` (`storm_lt_turbo.py`) to Tier 1 primary downloader so downloads never stall at `0.0 MB/s • Seeds: 0` when an external disconnected or throttled `qBittorrent` instance is open.
  - Added immediate `disconnected` with `0` DHT-node bypass, per-torrent rate limit clearing, and a 20-second zero-activity fallback watchdog for the optional qBittorrent Tier 2 path.
  - Removed speed and connection caps (`connections_limit = 10000`, `active_downloads = -1`, `active_seeds = -1`, `active_limit = -1`, `download_rate_limit = 0`, `upload_rate_limit = 0`, unlimited handle connections, `MaxConcurrentDownloads = 16`, Android `connections_limit = 2000` with `-1` per-torrent limits), preserved `t-ru.org` passkey trackers, added `--dht-state` DHT-only routing persistence, raised `numwant = 500`, and accelerated peer injection.
- 🔸 **[Game Slot to File Checkbox Synchronization (`RuTrackerModDownloaderDialog`)]**:
  - Unchecking a game slot in «Games and parts in torrent» automatically unchecks all files and discussion links belonging to that game; re-checking restores prior or smart selection, and checking a file on a disabled slot re-enables the slot automatically.
- 🔸 **[Default NSZ Format for Nintendo Switch 1 and Nintendo Switch 2]**:
  - Standardized default output container format to `NSZ` across Switch 1 and Switch 2 settings, task queues, and RuTracker downloader (via one-time `SwitchDefaultNszInitialized` migration while leaving 3DS defaults untouched).
- 🔸 **[Full Android Feature Parity (`MainActivity.kt`, `RuTrackerCloudEngine.kt`, `StormNczEngine.kt`)]**:
  - Added game slot checkboxes with two-way file sync, canonical 8-category grouping (`ИГРА`, `ОБНОВЛЕНИЕ`, `ДОПОЛНЕНИЯ`, `UNLOCKER`, `МОДЫ`, `ОБНОВЛЕНИЕ (РУС)`, `СЭЙВЫ`, `ИНФО`), DLC `[v>0]` classification fix, default disabling of mods/RUS patches/saves/info, clean DLC name copy buttons, multi-game splitting, Switch 1/2 output routing, auto-clear vs persistent `libtorrent4j` seeding panel (`UPLOAD_MODE` + JSON state), non-recursive scan default with subfolder checkbox, and RuTracker catalog update/DLC checker on the Inspector tab.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.11_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.11.zip` / `STORM_SWITCH_BOX_8.0.11_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.11.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.11_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.11_Setup.exe`** (219.72 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.11.zip`** / **`_win-x64.zip`** (151.27 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.11.apk`** (14.43 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.11_LEGACY.apk`** (26.00 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.11_Setup.exe** (219.72 МБ)
  - `SHA-256: 152253970DF4D0FC985149EA61C6658683E471BB59DD69F6E6D50F44EDA092C9`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.11_win-x64.zip** / **STORM_SWITCH_BOX_8.0.11.zip** (151.27 МБ)
  - `SHA-256: 3BF49A62CEB4B55BBEFB353112BF2F314E197169CAE9428FC2416BB2B93592BD`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.11.apk** (14.43 МБ)
  - `SHA-256: F1D734C76CD528F5F80BFD4F2638B19DC81DAD786E22C23F8FC1A6BC56E7A1B8`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.11_LEGACY.apk** (26.00 МБ)
  - `SHA-256: 8D27F174AE04A65C575433DD7C475D79819C7F7201854A38E10B6FF07F7FDEB2`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
