> 💡 **Релиз 8.0.13** - *Исключение перезапуска готовых игр и скрытие «ГОТОВО» из нижней панели, исправление запуска сборки на 100%, оптимизация дискового ввода-вывода libtorrent (`always_pwrite`) и восстановление полной скорости скачивания*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.0.13)

- 🌟 **[Готовые игры («ГОТОВО») больше не перезапускаются и переносятся на вкладку «Закачки» (`RuTrackerDownloadQueueService.cs`, `RuTrackerSeedingService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TasksViewModel.cs`, `MainActivity.kt`)]**:
  - Устранено дублирование и повторный запуск уже завершённых раздач (например, *Brotato*): при восстановлении очереди (`LoadPersistedState`, `BindRestoredTasksFromViewModel`) сначала фиксируются все завершённые и сидируемые идентификаторы (`IsAlreadyCompletedOrSeeding`, `HasSeedingEntryMatching`, `HasBuiltOutputOnDisk`), а старые дубликаты удаляются.
  - Нижняя панель очереди теперь привязана к отдельной коллекции `BottomDockItems`, из которой автоматически скрываются элементы в статусе `ГОТОВО`. Готовые раздачи отображаются только на вкладке «Закачки» (включая перенос активных торрент-раздач в блок «Раздачи»).
- 🌟 **[Исправление запуска сборки при 100% скачивания (`RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `storm_lt_turbo.py`)]**:
  - В `VerifyDownloadedTorrentFilesIntegrity` исправлена проверка путей многофайловых торрентов (`saveDirectory/<torrentName>/<relativePath>`): ранее проверка не находила файлы в подпапке торрента, отклоняла полностью скачанные релизы и отправляла их на повторную загрузку в резервный движок.
  - Добавлен семафор `_buildSemaphore` (строго 1 сборка одновременно при `MaxConcurrentDownloads = 4`, `MaxTotalConcurrentDownloads = 5`), чтобы параллельные операции `yanu-cli` и `nsz` не перегружали диск, и синхронизирован статус `Успешно` перед финализацией пакета.
- 🌟 **[Высокая скорость скачивания и устранение зависания на 99,9-100% (`storm_lt_turbo.py`, `RuTrackerDownloadQueueService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - В встроенном `libtorrent` для загрузки отключены `always_mmap_write` и синхронный `write_through` в пользу `disk_write_mode = 0` (`always_pwrite`) и `disk_io_write_mode = 0` (`enable_os_cache`), увеличен `max_queued_disk_bytes` до `256 MiB`, включены `strict_end_game_mode = True` и `peer_turnover = 4`. Это устранило перерасход ОЗУ (до 6-9 ГБ на торрент), 100% загрузку диска ReFS и зависания последних блоков на 100%.
  - Убрано преждевременное округление до `100.0%` до завершения торрента (`finished`/`seeding`), добавлен спасательный механизм при застревании последних блоков после `99%`, а `--skip-indices` корректно пересчитывается при наличии виртуальных BEP-47 `.pad/` файлов.
  - Ограничитель `AutoHeadroom` больше не душит скорость ниже `88%` целевого бюджета (до достижения пика все закачки работают без ограничений, отбрасываются заниженные старые пики `< 6 МБ/с`, не тратится квота на раздачи с 0 МБ/с, а `limit.txt` обновляется с гистерезисом и дебаунсом).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Поиск скачанных файлов в `VerifyDownloadedTorrentFilesIntegrity` дополнен путём `Path.Combine(parsedTorrent.Name, tf.Path)`, вариантом с `Trim()` и резервным сопоставлением по размеру и относительному суффиксу среди `downloadedPaths`.
- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Переход на `default_settings()` при загрузке с `disk_write_mode = 0` (`always_pwrite`), `disk_io_write_mode = 0`, `disk_io_read_mode = 0`, `max_queued_disk_bytes = 256 MiB`, `aio_threads = 8`, `hashing_threads = 4`, `strict_end_game_mode = True`, `peer_turnover = 4`, ремаппинг `--skip-indices` с учётом `.pad/`, дебаунс `--limit-file`, защита от преждевременного `100.0%`, реску-переобъявление при застое на `>= 99%` и 10-секундный таймаут `file_progress()` с принудительным `flush_cache()`.
- 🔹 **[Services/RuTrackerDownloadQueueService.cs, Services/RuTrackerSeedingService.cs, ViewModels/TasksViewModel.cs, Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Коллекция `BottomDockItems` без `ГОТОВО`, двухпроходная дедупликация в `LoadPersistedState`, `_completedIdentityKeys`, `HasSeedingEntryMatching`, `HasBuiltOutputOnDisk`, исправление `!b.IsCompleted` в `BindRestoredTasksFromViewModel`, семафор `_buildSemaphore`, баланс слотов `4/5` и неблокирующий `AutoHeadroom` в `ApplyGlobalSpeedLimitDistribution`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt, MainActivity.kt]**: Неблокирующий лимитер `AUTO_HEADROOM` (не режет скорость ниже 88% бюджета, прогрев 15 сек), запрет показа 100% до `FINISHED`/`SEEDING` и исключение перезапуска уже завершённых или сидируемых задач в `loadPersistedBgTasks`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.13)

- 🔸 **[Prevent Restarting Completed Games and Hide Finished Rows from Bottom Dock (`RuTrackerDownloadQueueService.cs`, `RuTrackerSeedingService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TasksViewModel.cs`, `MainActivity.kt`)]**:
  - Fixed completed games (such as *Brotato*) re-appearing as active downloads after restart by checking `IsAlreadyCompletedOrSeeding`, `HasSeedingEntryMatching`, `HasBuiltOutputOnDisk`, and two-pass deduplication across `rutracker_download_queue.json`, `persisted_tasks.json`, and `bg_tasks_state.json`.
  - Split `BottomDockItems` from `Items` so completed (`ГОТОВО`) batches stay hidden from the bottom dock and appear exclusively on the «Закачки» tab (with torrent releases promoted into the «Раздачи» seeding panel).
- 🔸 **[Fix Container Build Not Starting at 100% (`RuTrackerModService.cs`, `RuTrackerDownloadQueueService.cs`, `storm_lt_turbo.py`)]**:
  - Fixed `VerifyDownloadedTorrentFilesIntegrity` rejecting completed multi-file downloads because `libtorrent` stores them under `saveDirectory/<torrentName>/<relativePath>`.
  - Added sequential build locking (`_buildSemaphore`) and balanced active download slots (`MaxConcurrentDownloads = 4`, `MaxTotalConcurrentDownloads = 5`) so finished packages immediately transition into container assembly without starving disk I/O.
- 🔸 **[Restore Full Download Speed and Fix 100% Stall (`storm_lt_turbo.py`, `RuTrackerDownloadQueueService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Switched `storm_lt_turbo.py` downloads to `disk_write_mode = 0` (`always_pwrite`) and `disk_io_write_mode = 0` (`enable_os_cache`), raised `max_queued_disk_bytes` to `256 MiB`, and enabled `strict_end_game_mode` and `peer_turnover = 4`, eliminating multi-GB mmap RAM bloat, disk queue saturation, and end-game hangs at `99.9-100%`.
  - Fixed `AutoHeadroom` throttling below `88%` of target bandwidth and discarded stale low peak estimates so multi-torrent transfers run at full link speed.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.13_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.13.zip` / `STORM_SWITCH_BOX_8.0.13_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.13.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.13_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.13_Setup.exe`** (219.75 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.13.zip`** / **`_win-x64.zip`** (151.30 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.13.apk`** (14.44 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.13_LEGACY.apk`** (26.01 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.13_Setup.exe** (219.75 МБ)
  - `SHA-256: 06F93EC025A3775BFA9C4DBE2F4CD7DE7DB8394E1A35A88AAFBD6BF586EBA7C4`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.13_win-x64.zip** / **STORM_SWITCH_BOX_8.0.13.zip** (151.30 МБ)
  - `SHA-256: D5C384CC1C4421EAAE9C52B3098451B1229CB41E5D2277D08B8A5403D4658029`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.13.apk** (14.44 МБ)
  - `SHA-256: F1027A7E7B1FB9257ECB1BC3C1558BFCC19C5645706F6A0ADBB70E13EF792C58`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.13_LEGACY.apk** (26.01 МБ)
  - `SHA-256: 212293BDAEB8CC05AA01725A9898FCC047EDFE1C9268BD65F503E4B7D9AF7AAD`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
