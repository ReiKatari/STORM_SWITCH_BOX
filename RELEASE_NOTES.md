> 💡 **Релиз 8.0.8** - *Гарантированная сборка всех частей многосоставных раздач RuTracker (`Aggelos 1-2` и др.) и сохранение готовых файлов в конечную выходную папку*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.8)

- 🌟 **[Исправление многосоставных раздач RuTracker: сборка 100% игр в раздаче (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TasksViewModel.cs`)]**:
  - Устранена критическая ошибка раннего удаления общего каталога раздачи: ранее при включённом автоудалении исходников после сборки 1-й части (`Aggelos`) проверка сиблингов через асинхронный `RunOnUiSyncOrAsync` оставляла флаг `anySiblingStillPending == false`, а условие `SlotNumber >= TotalSlots - 1` срабатывало уже на 1-м слоте при 1-based нумерации (`1 >= 2 - 1`), удаляя файлы 2-й части (`Aggelos 2`) до её запуска, из-за чего 2-я задача завершалась без обработки.
  - Реализован трёхэтапный конвейер `ExecuteMultiTaskPipelineAsync` / `ExecuteMultiGameRuTrackerPipelineAsync`: **Phase 1** заранее раскладывает контейнеры по всем папкам `Part_k_...` до старта 1-й сборки (копированием для общих файлов и перемещением для последнего владельца), **Phase 2** последовательно собирает каждую игру в отдельном блоке `try/catch` с изоляцией сбоев, **Phase 3** удаляет временные папки только после завершения всех частей и выставляет точный статус пакета (`ГОТОВО` или `ОШИБКА`).
  - Расширен список распознаваемых форматов контейнеров (Switch, Switch 2 `.nup`, Nintendo 3DS и Azahar Z3DS), исправлена фильтрация модов (`МОДЫ` и `МОД / РУСИФИКАТОР`) и допуск `TargetGameSlot == 0`, а при пустом списке входных файлов в режиме автосборки теперь фиксируется явная ошибка вместо фиктивного статуса `Успешно`.
- 🌟 **[Сохранение итоговых файлов напрямую в конечную папку назначения вместо `RuTracker_Downloads` (`RuTrackerModDownloaderDialog.xaml.cs`, `RuTrackerDownloadQueueService.cs`)]**:
  - Добавлены методы `NormalizeNonTempOutputDir`, `IsTempRuTrackerStagingDir` и `ResolveRuTrackerFinalOutputFolder`: временные сегменты (`RuTracker_Downloads`, `RuTracker_Staging`, `_Staging_`) автоматически отсекаются при определении итоговой директории.
  - Временная загрузка торрента и распаковка модов изолированы во вложенном каталоге `<конечная папка>/RuTracker_Downloads/<релиз>_<id>`, тогда как итоговые собранные файлы записываются напрямую в выбранную пользователем конечную папку (`OutputFolder`, либо `OutputFolderSwitch2` / `OutputFolder3ds` для игр Switch 2 / 3DS), а пустая папка `RuTracker_Downloads` удаляется автоматически после очистки временных файлов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Добавлены `IsTempRuTrackerStagingDir`, `IsDedicatedPartDir`, `IsSupportedGameContainerPath`, `NormalizeNonTempOutputDir` и `ResolveRuTrackerFinalOutputFolder`; обновлены `LoadSavedSettings`, `SaveCurrentSettings`, `ExecuteDownloadPipelineAsync`, `ExecuteRuTrackerPipelineAsync`, `BindDownloadedFilesToExistingTask`, `CleanupRuTrackerReleaseDirectory`, `ExecuteMultiGameRuTrackerPipelineAsync` и `FilterDownloadedContainersForSlot`.
- 🔹 **[Services/RuTrackerDownloadQueueService.cs]**: Переработан конвейер `ExecuteMultiTaskPipelineAsync` (Phase 1 pre-staging, Phase 2 изолированный цикл сборки, Phase 3 итоговая очистка), исправлены `ExecuteSingleTaskPipelineAsync` (защита `SharedReleaseDir` при `TotalSlots > 1`, нормализация `outDir`, ошибка при пустых входах), `BindRestoredTasksFromViewModel` и `LoadPersistedState`.
- 🔹 **[ViewModels/TasksViewModel.cs]**: Усилена пост-задачная очистка `ExecuteTaskSafelyAsync` с использованием `ResolveRuTrackerFinalOutputFolder` / `CleanupRuTrackerReleaseDirectory` и корректной проверкой активных/ожидающих сиблингов.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.8)

- 🔸 **[Multi-Game RuTracker Torrent Pipeline Fix (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TasksViewModel.cs`)]**:
  - Fixed premature deletion of shared torrent directories when assembling multi-game releases such as `Aggelos 1-2`: `ExecuteSingleTaskPipelineAsync` previously ran an un-awaited UI check leaving `anySiblingStillPending == false` and evaluated `SlotNumber >= TotalSlots - 1` against 1-based slot numbers, wiping Part 2 files right after Part 1 completed.
  - Rewrote multi-slot execution into a 3-phase pipeline: pre-staging all slots' containers into `Part_k_...` folders (reference-counted copy/move) before building slot 1, building every game sequentially with per-slot error isolation, and cleaning up `SharedReleaseDir` only after all slots finish, with explicit error reporting when `AutoBuild` has no input files and full Switch / Switch 2 / 3DS / Z3DS container extension support.
- 🔸 **[Direct Output to Target Game Folder Instead of `RuTracker_Downloads` (`RuTrackerModDownloaderDialog.xaml.cs`, `RuTrackerDownloadQueueService.cs`)]**:
  - Added `NormalizeNonTempOutputDir` and `ResolveRuTrackerFinalOutputFolder` so torrent/cloud downloads stage inside `<target>/RuTracker_Downloads/<release>_<id>` while built containers write directly to the user's configured destination folder (`OutputFolder`, or platform-specific `OutputFolderSwitch2` / `OutputFolder3ds`), automatically migrating legacy persisted tasks and cleaning up empty `RuTracker_Downloads` folders.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.8_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.8.zip` / `STORM_SWITCH_BOX_8.0.8_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.8.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.8_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.8_Setup.exe`** (219.68 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.8.zip`** / **`_win-x64.zip`** (151.24 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.8.apk`** (14.40 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.8_LEGACY.apk`** (25.97 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.8_Setup.exe** (219.68 МБ)
  - `SHA-256: AF38A07F2EEA5C3E185485BD2EB04B5D334134BCC3134D186CD2901CB709AC6E`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.8_win-x64.zip** / **STORM_SWITCH_BOX_8.0.8.zip** (151.24 МБ)
  - `SHA-256: D6A5E506554B9F49A5888369AA35EC9E5CDF7CFEEA7E213775221B815613E9F2`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.8.apk** (14.40 МБ)
  - `SHA-256: F764AB3128B625B1E4DA97C104774024C894CA9BABC2C7AD20CD2E2C076A7040`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.8_LEGACY.apk** (25.97 МБ)
  - `SHA-256: A01102E843D39BCBB0D131F67F09F82B8462F376358E19335285E7DC2A099ED5`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
