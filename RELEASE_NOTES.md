> 💡 **Релиз 8.0.10** - *Автоочистка готовых закачек RuTracker при удалении исходников и полноценное BitTorrent-сидирование в разделе «Раздачи»*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.0.10)

- 🌟 **[Автоматическая очистка завершённых закачек при включённом удалении исходников (`RuTrackerDownloadQueueService.cs`)]**:
  - Когда включён пункт «Удалять исходные файлы после успешной проверки контейнера» (`DeleteSourceAfterVerify`), успешно скачанная, собранная и проверенная раздача теперь автоматически очищается из списка очереди (аналогично кнопке «Очистить готовые»).
  - Если же закачка завершилась с ошибкой или была отменена пользователем, карточка остаётся видимой в списке, чтобы ошибка не потерялась.
- 🌟 **[Полноценный блок «Раздачи» на вкладке «Закачки» и постоянное сидирование (`RuTrackerSeedingService.cs`, `storm_lt_turbo.py`)]**:
  - При отключённом удалении исходных файлов готовая раздача автоматически скрывается из нижней компактной панели на других вкладках и переносится в выделенный блок **«Раздачи»** на вкладке **«Закачки»**.
  - Реализован постоянный фоновый сид-демон `--seed-daemon` на базе встроенного C++ движка `libtorrent-rasterbar 2.1.1.0` (`upload_mode` без докачки невыбранных файлов, сохранение оригинальных passkey-трекеров `t-ru.org`, FastResume `.resume` и состояния DHT), а также резервный движок `MonoTorrent 3.0.2`.
  - Для многосоставных раздач с несколькими играми (`PreMatchedContainerPaths`) при сидировании файлы больше не перемещаются из исходной папки торрента `sharedReleaseDir` в `Part_...`, благодаря чему хэши торрента полностью сохраняются для отдачи пирам.
  - В интерфейсе «Раздачи» отображается живая телеметрия каждой раздачи: статус (`РАЗДАЧА`, `ПРОВЕРКА`, `ПАУЗА`, `ОШИБКА`, `НЕТ ФАЙЛОВ`), размер, текущая скорость отдачи, накопительный объём отданного между запусками (`Отдано`), рейтинг (`Рейтинг`), число личей и сидов, кнопки `Пауза / Возобновить`, `Обновить трекеры и пиров`, `Открыть папку раздачи`, `Убрать из раздач` (файлы на диске сохраняются), а также общие кнопки `Запустить все` и `Пауза все`.
  - Добавлен автозапуск сохранённых раздач при старте приложения (`App.xaml.cs`), периодическое сохранение статистики и корректное сохранение FastResume при закрытии (`MainWindow.xaml.cs`), а также перевод всех новых элементов на 11 языков (`LocalizationService.UiTree.cs`).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Добавлены флаг `--seed-daemon`, `--resume-dir`, хелперы `save_torrent_info_file`, `_resolve_seed_save_dir`, `merge_seed_trackers`, `run_seed_daemon` (JSON-управление через `stdin`, телеметрия каждую секунду через `stdout`, FastResume `.resume` и DHT state) и сохранение `.torrent` при обычном скачивании через `--save-torrent`.
- 🔹 **[Services/RuTrackerSeedingService.cs]**: Создан сервис управления раздачами (`RuTrackerSeedingItem`, `RuTrackerSeedingService`) с персистентностью `rutracker_seeding_state.json`, учётом отданного трафика и рейтинга, управлением сид-демоном и резервным `MonoTorrent` движком.
- 🔹 **[Services/RuTrackerDownloadQueueService.cs, Models/ProcessingTask.cs]**: Внедрено `HandleSuccessfulBatchCompletionAsync` (автоудаление при `DeleteSourceAfterVerify` или передача в `RuTrackerSeedingService`), убран ранний выход из `OnUiTimerTick` при пустой очереди закачек, сохранены исходные пути многосоставных раздач через `PreMatchedContainerPaths`.
- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `GetCachedTorrentFilePath`, `TryResolveCachedTorrentBytes`, `TrySaveCachedTorrentBytes`, `TryResolveTopicHashAndMagnet` и автоматическое кэширование `.torrent` метаданных при загрузке.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml, .xaml.cs, App.xaml.cs, MainWindow.xaml.cs, Services/LocalizationService.UiTree.cs]**: Размещён блок «Раздачи» на вкладке «Закачки» (скрытый в нижней док-панели на остальных вкладках), обновлены счётчики шапки `Закачки (A | ↑S)`, добавлены обработчики управления раздачами, автозапуск/завершение сидинга и 24 ключа локализации.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.10)

- 🔸 **[Auto-Clear Completed Downloads When Source Deletion Is Enabled (`RuTrackerDownloadQueueService.cs`)]**:
  - When «Delete source files after container verification» (`DeleteSourceAfterVerify`) is enabled and all tasks in a batch succeed, the finished entry now clears automatically from the queue bar just like «Clear finished», while errored and cancelled items remain visible.
- 🔸 **[Dedicated «Seeding» Panel on the Downloads Tab and Persistent BitTorrent Seeding (`RuTrackerSeedingService.cs`, `storm_lt_turbo.py`)]**:
  - When source deletion is disabled, finished torrent batches hide from the bottom bar across other tabs and automatically move to the new **Seeding** section inside the **Downloads** tab.
  - Added persistent multi-torrent `--seed-daemon` powered by embedded C++ `libtorrent-rasterbar 2.1.1.0` (`upload_mode` to seed selected files without re-downloading skipped entries, preserving original `t-ru.org` passkey trackers, FastResume `.resume`, and DHT state) with an automatic `MonoTorrent 3.0.2` fallback.
  - Preserved original multi-game torrent directory layouts in-place (`PreMatchedContainerPaths`) when seeding is kept enabled, and added live stats (status badge, upload speed, cumulative uploaded bytes across restarts, share ratio, connected leeches/seeds), per-item and batch controls, startup auto-resume, graceful shutdown persistence, and 11-language localization.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.10_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.10.zip` / `STORM_SWITCH_BOX_8.0.10_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.10.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.10_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.10_Setup.exe`** (219.72 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.10.zip`** / **`_win-x64.zip`** (151.27 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.10.apk`** (14.40 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.10_LEGACY.apk`** (25.97 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.10_Setup.exe** (219.72 МБ)
  - `SHA-256: B2B16D20925D5440B9F68CB4D08C5658CD64DA352B699E381CE51B785040653F`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.10_win-x64.zip** / **STORM_SWITCH_BOX_8.0.10.zip** (151.27 МБ)
  - `SHA-256: 1901E119C341B5C6428A27CB600F4B95AE9BF57B2BA56240D88022C1CFE56794`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.10.apk** (14.40 МБ)
  - `SHA-256: 7DC56B6BD1755B8A5AD450C90168419C774FAEAC003D0B8F4C45B387D65E9EDA`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.10_LEGACY.apk** (25.97 МБ)
  - `SHA-256: 1425BE3F9DAEDBE2F234B55934346B3F7161479EAD42C13D33DC2EAEEA7EBCFE`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
