> 💡 **Релиз 8.0.12** - *Исправление целостности P2P-скачивания и сборки контейнеров, актуальная последняя версия обновлений (Brotato 1.1.15.4), лимит скорости «Авто (макс - 2 МБ/с)», устранение фризов UI, автоперезапуск незавершённых задач и табличный интерфейс с разделителями*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.0.12)

- 🌟 **[Целостность скачанных файлов и исправление ошибок сборки (`storm_lt_turbo.py`, `RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Устранено преждевременное завершение загрузок и повреждение разделов NSZ/NSP (`ResultFsPartitionSignatureVerificationFailed`, `ZstdError: Data corruption detected`): встроенный `libtorrent-rasterbar` и Android `libtorrent4j` теперь требуют достижения `finished`/`seeding` и 100% прогресса по каждому выбранному файлу, выполняют `pause()` - `flush_cache()` - `remove_torrent()`, проверяют точный размер файлов и заголовки `PFS0`/`HEAD` (исключая виртуальные BEP-47 `.pad/` файлы).
  - Исправлены генерация `--skip-indices`, нормализованная фильтрация исключённых путей и атомарный подбор локальных портов (`Interlocked.Increment`), чтобы старые обновления и снятые файлы не смешивались со сборкой.
- 🌟 **[Актуальная последняя версия обновлений и очистка кэша (`TitleDbService.cs`, `CatalogScannerService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `MainActivity.kt`, `RuTrackerCloudEngine.kt`)]**:
  - Исправлено определение отображаемой версии игры в раздачах со снятыми старыми патчами (например, *Brotato*: вместо устаревшего `Old Update 1.0.1.3` `[v327680]` отображается последняя версия `1.1.15.4` для `[v851968]`).
  - Добавлена миграция кэша на `known_display_versions_v2.json` с автоматической очисткой старого кэша, защитой от отката версии и приоритетом авторитетного NACP, выбранного `[vN]` и версии из темы RuTracker.
- 🌟 **[Глобальный ограничитель скорости и профиль «Авто (макс - 2 МБ/с)» (`AppSettings.cs`, `RuTrackerDownloadQueueService.cs`, `storm_lt_turbo.py`, `RuTrackerModDownloaderDialog`, `SettingsPage`, Android)]**:
  - Добавлено живое управление общей скоростью скачивания (`AutoHeadroom` по умолчанию оставляет запас 2 МБ/с для браузера и системы, также доступны «Без ограничений» и ручной лимит МБ/с) через выпадающий список в загрузчике RuTracker, на странице настроек и в Android-клиенте.
  - Одновременная очередь сбалансирована до `MaxConcurrentDownloads = 5` (`MaxTotalConcurrentDownloads = 12`) и дискового кэша `64 MiB` (`aio_threads = 4`) с динамическим весовым перераспределением через `--limit-file` и `EngineSettingsBuilder` / `UpdateSettingsAsync`.
- 🌟 **[Устранение фризов интерфейса при добавлении загрузок (`TasksViewModel.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `MainWindow.xaml.cs`)]**:
  - Исключено раздувание `persisted_tasks.json` (ранее свыше 9 МБ): иконки, бинарные NACP-метаданные и буферы `.torrent` больше не сериализуются в base64 и кэшируются на диск асинхронно.
  - Запись очереди `SaveQueueState()` переведена в фоновый дебаунс (250 мс) с гарантированным сбросом `FlushQueueStateSync()` при закрытии окна, а создание директорий и чтение/запись `.torrent` в `ExecuteDownloadPipelineAsync` вынесены из UI-потока.
- 🌟 **[Автоочистка и автоматический перезапуск незавершённых задач при старте (`RuTrackerDownloadQueueService.cs`, `TasksViewModel.cs`, `MainWindow.xaml.cs`, `MainActivity.kt`)]**:
  - При полном закрытии и следующем запуске программы (Windows и Android) незавершённые задачи автоматически очищаются от зависшего состояния, создаются заново с `0%` и сразу запускаются в обработку с сохранением уже готовых результатов.
- 🌟 **[Таблицы с видимыми разделителями и без обрезания элементов (`RuTrackerModDownloaderDialog.xaml`, `.xaml.cs`, `RuTrackerSeedingService.cs`, `LocalizationService.UiTree.cs`)]**:
  - Нижний блок загрузок (7 столбцов) и таблицы на вкладке «Закачки» (загрузки и «Раздачи», 8 столбцов) получили фиксированные заголовки столбцов и чёткие `1px` вертикальные и горизонтальные разделители ячеек без обрезания текста и кнопок (добавлены 18 ключей локализации для 10 языков).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/storm_lt_turbo.py, Services/RuTrackerModService.cs]**: Добавлены `finalize_torrent_and_flush`, `verify_downloaded_files_integrity`, поддержка `--download-limit` и `--limit-file`, безаварийное закрытие сессии, `VerifyDownloadedTorrentFilesIntegrity`, `HasValidSwitchContainerHeader`, исправление `--skip-indices` и атомарный подбор портов.
- 🔹 **[Services/TitleDbService.cs, Services/CatalogScannerService.cs]**: Миграция на `known_display_versions_v2.json`, удаление legacy-кэша, монотонная защита от понижения версии и флаг авторитетности `isAuthoritative`.
- 🔹 **[Models/AppSettings.cs, Models/ProcessingTask.cs, Services/RuTrackerDownloadQueueService.cs, ViewModels/TasksViewModel.cs, MainWindow.xaml.cs]**: Лимитер скорости `AutoHeadroom` / `Custom` / `Unlimited`, дисковый кэш `.torrent` и иконок, асинхронное сохранение очереди, очистка и автоперезапуск незавершённых задач при запуске.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml, .xaml.cs, Views/SettingsPage.xaml, .xaml.cs, Services/RuTrackerSeedingService.cs, Services/LocalizationService.UiTree.cs]**: Таблицы с разделителями 1px для нижнего блока и вкладки «Закачки», элементы выбора лимита скорости, фильтрация старых обновлений (`IsOldOrLegacyUpdatePath`) и 18 новых ключей локализации.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt, MainActivity.kt]**: Проверка `State.FINISHED`/`SEEDING`, `fileProgress()`, `flushCache()` и `PFS0`, пропуск `Old Update` в версии, живой лимитер `AUTO_HEADROOM` (`max - 2 MB/s`) и автоперезапуск незавершённых задач при перезапуске приложения.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.12)

- 🔸 **[P2P Download Completeness and Container Build Fix (`storm_lt_turbo.py`, `RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Fixed premature download completion causing `ResultFsPartitionSignatureVerificationFailed` and `ZstdError: Data corruption detected` when building NSZ/NSP containers: both desktop `libtorrent-rasterbar` and Android `libtorrent4j` now enforce `finished`/`seeding` state plus full `file_progress()` across wanted files, run explicit `pause()` / `flush_cache()` / `remove_torrent()`, and validate exact file sizes and `PFS0`/`HEAD` headers (while skipping BEP-47 `.pad/` virtual entries).
  - Fixed `--skip-indices`, path filtering, and atomic port allocation (`Interlocked.Increment`) so unchecked or older update packages never pollute multi-content builds.
- 🔸 **[Latest Game Display Version Resolution (`TitleDbService.cs`, `CatalogScannerService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `MainActivity.kt`, `RuTrackerCloudEngine.kt`)]**:
  - Fixed stale update version display (such as *Brotato* showing old update `1.0.1.3` from `[v327680]` instead of `1.1.15.4` for `[v851968]`): added `known_display_versions_v2.json` migration, monotonic anti-downgrade protection, `Old Update` path exclusion, and authoritative NACP / `[vN]` / topic HTML resolution.
- 🔸 **[Global Download Speed Limit and Auto Headroom (`AppSettings.cs`, `RuTrackerDownloadQueueService.cs`, `storm_lt_turbo.py`, `RuTrackerModDownloaderDialog`, `SettingsPage`, Android)]**:
  - Added live global download bandwidth limiting defaulting to `AutoHeadroom` (peak minus 2 MB/s reserve), plus `Unlimited` and custom MB/s modes in both Windows and Android UIs (`--limit-file` + MonoTorrent `UpdateSettingsAsync`), with concurrency balanced to `MaxConcurrentDownloads = 5` (`MaxTotalConcurrentDownloads = 12`).
- 🔸 **[UI Freeze Elimination (`TasksViewModel.cs`, `RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `MainWindow.xaml.cs`)]**:
  - Removed inline base64 icon, NACP, and `.torrent` blobs from `persisted_tasks.json`, switched queue state persistence to 250 ms debounced background writes with exit flush, and moved target directory creation and `.torrent` file caching off the UI thread.
- 🔸 **[Startup Auto-Clear and Auto-Restart of Unfinished Tasks (`RuTrackerDownloadQueueService.cs`, `TasksViewModel.cs`, `MainWindow.xaml.cs`, `MainActivity.kt`)]**:
  - On full app close and relaunch (Windows and Android), unfinished tasks are automatically cleared and recreated cleanly from `0%` and restarted automatically while preserving finished items.
- 🔸 **[Bordered Table UI Without Clipped Elements (`RuTrackerModDownloaderDialog.xaml`, `.xaml.cs`, `RuTrackerSeedingService.cs`, `LocalizationService.UiTree.cs`)]**:
  - Redesigned both the bottom active downloads dock (7 columns) and the Transfers tab downloads and seeding lists (8 columns) with sticky column headers, visible 1px vertical and horizontal cell borders, and non-clipped controls.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.12_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.12.zip` / `STORM_SWITCH_BOX_8.0.12_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.12.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.12_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.12_Setup.exe`** (219.74 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.12.zip`** / **`_win-x64.zip`** (151.30 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.12.apk`** (14.43 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.12_LEGACY.apk`** (26.00 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.12_Setup.exe** (219.74 МБ)
  - `SHA-256: 738A0F372851AD959B830CBCAC7DFE2A4874199AE15AF69DD563431833B3E0FF`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.12_win-x64.zip** / **STORM_SWITCH_BOX_8.0.12.zip** (151.30 МБ)
  - `SHA-256: D4E8D912D91FC8D11DF04499A027033E163768FB417722D245128D56D94C5C78`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.12.apk** (14.43 МБ)
  - `SHA-256: 57EE4FCD4D01C746D99A5FAE7CFCB7346D6661FC415EB5450304380EEE4F9E32`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.12_LEGACY.apk** (26.00 МБ)
  - `SHA-256: 6A350F38C6F79461A6052CA1D219AB96273CF8E192659FDDFDA41AB6E1938A65`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
