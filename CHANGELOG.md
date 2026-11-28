## **8.2.8** (28.11.2026)

> 💡 **Релиз 8.2.8** - *Исправление определения папок `base` + `update` для Nintendo Switch 2 (NS2) и корректная сборка целевого формата `.nup` вместо `.nsp` на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.8)

- 🌟 **[Распознавание `base` + `update` / `updates` / `patch` для Switch 2 (`Switch2DumpCompilerService.cs`, `NupContainerService.cs`, `TasksViewModel.cs`, `StormNczEngine.kt`)]**:
  - Устранён баг, из-за которого при выборе папки с `base` и `update` подтягивался только `update` (или отбрасывался второй компонент на вкладке «Конвертация»): папка `update`/`patch` больше не может распознаваться как отдельный корень дампа, добавлен единый резолвер `ResolveDumpComponents(...)` с поддержкой `base`/`basedata`, `updates`/`update`/`patch`/`patches` (как прямых, так и версионных подпапок) и `dlc`/`dlcdata`.
  - Для `.nsp`/`.nsz`-контейнеров внутри папок `base`/`update`/`dlc` в `TasksViewModel.cs` компоненты объединяются в одну задачу (`topFolder = "ROOT"`, с гарантированным порядком `Base` -> `Update` -> `DLC`), а ветка `Convert` при нескольких входных файлах корректно запускает мульти-сборку вместо взятия только первого файла.
- 🌟 **[Исправление вывода в формат `NUP` вместо `.nsp` (`MultiContentService.cs`, `Switch2DumpCompilerService.cs`, `TasksViewModel.cs`, `StormNczEngine.kt`)]**:
  - При выборе конечного формата `NUP` во всех режимах («Конвертация», «Упаковка», «Мульти-контент», Switch 2 дамп и `.nup` -> `.nup`) больше не остаётся/не выходит `.nsp` - выполняется прямая или промежуточная упаковка в `.nup` через `NupContainerService` (а в Android при `targetFormatMode == 7` - напрямую через `packToNupContainer`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/Switch2DumpCompilerService.cs]**: Добавлен `ResolveDumpComponents`, защита от ложного определения `update`/`patch`/`basedata`/`dlcdata` как корня дампа, прямая ветка `targetFormat == "NUP"` до сборки `.nsp` и поддержка `NUP -> NUP` в `CompileNupToSwitch2ContainerAsync`.
- 🔹 **[Services/NupContainerService.cs]**: `PackSwitchDumpToNupAsync` переведён на `ResolveDumpComponents`, а `ConvertContainerToNupAsync` учитывает `OutputFileName`, настройки сжатия Switch 2 и финальные метрики задачи.
- 🔹 **[Services/MultiContentService.cs]**: Добавлена полная поддержка `isTargetNup` (`ConvertIntermediateToNupAsync`) во все ветки завершения `BuildMultiContentAsync` с удалением временного `.nsp`.
- 🔹 **[ViewModels/TasksViewModel.cs]**: Группировка `base`/`update`/`dlc`, сортировка `Application` -> `Patch`, `Pack` в `.nup`, а `Convert` с несколькими файлами маршрутизируется в `BuildMultiContentAsync`.
- 🔹 **[Android (`StormNczEngine.kt`, `MainActivity.kt`)]**: Паритет по исключению подпапок компонентов (`update`/`base`/`dlc`), поддержка `update/` + `updates/` и прямой вывод `.nup` при `targetFormatMode == 7`.
- 🔹 **[Релиз 8.2.8]**: Обновлены версии Windows (`8.2.8`) и Android (`versionCode = 8208`, `versionName = "8.2.8"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed Switch 2 (`NS2`) folder detection when both `base` and `update` folders are present (`ResolveDumpComponents`, component subfolder guards preventing `update/` from being mistaken for the dump root, unified `ROOT` folder grouping and multi-file `Convert` routing in `TasksViewModel.cs`, plus Android parity in `StormNczEngine.kt` and `MainActivity.kt`).
- 🔸 Fixed selecting `NUP` as the target output format producing `.nsp` instead of `.nup` across `Switch2DumpCompilerService.cs`, `MultiContentService.cs`, `NupContainerService.cs`, `TasksViewModel.cs`, and Android `StormNczEngine.kt`, and bumped version to `8.2.8` (`versionCode = 8208`).

</details>

---

## **8.2.7** (28.11.2026)

> 💡 **Релиз 8.2.7** - *Устранение OOM-краша и вечного зависания `nsz.exe` при блочном сжатии NSZ на уровне 22 (снижение расхода памяти в 37 раз) и умное планирование воркеров*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.7)

- 🌟 **[Исправление OOM и deadlock в `BlockCompressor.py` (`tools/nsz/Lib/site-packages/nsz/BlockCompressor.py`)]**:
  - В функции `compressBlockTask` добавлено кеширование экземпляра `ZstdCompressor` на процесс воркера и передача `source_size=blockSize` (1 МБ) в `ZstdCompressionParameters.from_level(...)`. На уровне сжатия 22 это снижает объём памяти контекста Zstd с `641.3 МБ` до `17.3 МБ` на процесс (в 37 раз меньше при идентичной степени сжатия блоков 1 МБ), полностью устраняя ошибки `zstd.ZstdError: not enough memory` и `MemoryError`.
  - В `blockCompressContainer` размер очереди чанка `TasksPerChunk` ограничен до `max(threads * 4, 32)` (вместо `200` блоков на задачу), добавлены проверки живости процессов (`_wait_until_workers_ready` и `_put_work_checked`) и блок `finally` с корректным завершением воркеров и `manager.shutdown()`, что исключает вечное зависание при падении дочернего процесса.
- 🌟 **[Умное планирование потоков и памяти для `nsz.exe` (`Services/NszCompressionService.cs`)]**:
  - Добавлен расчёт свободной физической/виртуальной памяти (`GlobalMemoryStatusEx`) и метод `GetOptimalExternalNszThreads`, делящий квоту процессов `nsz.exe` с учётом `ConcurrentTasks`, уровня сжатия и доступного объёма ОЗУ (первая задача больше не занимает сразу 15 дочерних Python-процессов при `ConcurrentTasks > 1`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/Lib/site-packages/nsz/BlockCompressor.py]**: Кеширование `ZstdCompressor`, `source_size=blockSize`, ограничение `TasksPerChunk`, `_wait_until_workers_ready`, `_put_work_checked`, таймаут завершения и `try...finally` очистка процессов и `Manager`.
- 🔹 **[Services/NszCompressionService.cs]**: Добавлены `MEMORYSTATUSEX` / `GlobalMemoryStatusEx`, `GetAvailableMemoryMb()`, `GetOptimalExternalNszThreads()` и информативное логирование выделенных процессов и доступной ОЗУ/commit.
- 🔹 **[Релиз 8.2.7]**: Синхронизированы версии Windows (`8.2.7`) и Android (`versionCode = 8207`, `versionName = "8.2.7"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `nsz.exe` level-22 out-of-memory crashes and infinite worker-wait deadlocks in `BlockCompressor.py` by passing `source_size=blockSize` (1 MB) to `ZstdCompressionParameters.from_level` (cutting per-worker Zstd context RAM from `641.3 MB` to `17.3 MB` - 37x reduction), reusing per-worker `ZstdCompressor` instances, capping `TasksPerChunk`, adding `_wait_until_workers_ready` / `_put_work_checked` dead-worker guards, and cleaning up worker pools in `try...finally`.
- 🔸 Added `GlobalMemoryStatusEx`-backed `GetOptimalExternalNszThreads` in `Services/NszCompressionService.cs` to budget external `nsz.exe` worker processes across `ConcurrentTasks` and available system RAM/commit headroom, and bumped version to `8.2.7` (`versionCode = 8207`).

</details>

---

## **8.2.6** (25.11.2026)

> 💡 **Релиз 8.2.6** - *Устранение перезаписи уже загруженных из кэша реальных `.torrent`-файлов синтетической заглушкой `MAGNET` в `LoadSelectedTopicAsync`*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.6)

- 🌟 **[Исправление бесконечного «Magnet-разрешение активно» при открытии кэшированных тем (`RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранён критический баг в `LoadSelectedTopicAsync`: флаг `filesApplied` не инициализировался значением `instantCachedFiles != null && instantCachedFiles.Count > 0`. Из-за этого для любой раздачи из мгновенного кэша (включая `Mario Party Superstars`, `t=6126947`) `filesTask` пропускался, а блок `if (!filesApplied)` немедленно перезаписывал уже отображённые реальные файлы `.nsp`/`.nsz` фейковой строкой `MAGNET` и замораживал заголовок в состоянии «файлы будут получены через P2P».
  - В `ApplyResolvedFilesOnUi`, `StartMagnetMetadataBackground` и резервный фолбэк добавлена жёсткая защита `if (_activeTopicHasRealFiles && !hasReal) return;`, гарантирующая, что реальный список файлов никогда не будет затёрт заглушкой `MAGNET`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Инициализация `filesApplied = instantCachedFiles != null && instantCachedFiles.Count > 0`, защита `_activeTopicHasRealFiles && !hasReal` в `ApplyResolvedFilesOnUi`, фильтр `Any(f => f.IsRealTorrentFile)` в `StartMagnetMetadataBackground` и условие `!filesApplied && !_activeTopicHasRealFiles`.
- 🔹 **[Релиз 8.2.6]**: Обновлены версии Windows (`8.2.6`) и Android (`versionCode = 8206`, `versionName = "8.2.6"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed a critical bug in `LoadSelectedTopicAsync` (`RuTrackerModDownloaderDialog.xaml.cs`) where `filesApplied` remained `false` when `instantCachedFiles` loaded real `.torrent` entries, causing the subsequent `if (!filesApplied)` fallback to overwrite all real `.nsp`/`.nsz` files with a synthetic `MAGNET` placeholder forever; guarded `ApplyResolvedFilesOnUi`, `StartMagnetMetadataBackground`, and the fallback branch against overwriting `_activeTopicHasRealFiles`, and bumped version to `8.2.6` (`versionCode = 8206`).

</details>

---

## **8.2.5** (25.11.2026)

> 💡 **Релиз 8.2.5** - *Мгновенное получение списка файлов раздач RuTracker, обход лимитов HTTP 503 и ТСПУ через TLS ClientHello Split и Cloudflare Edge, FastResume скачивания и устранение простоев `0,00 МБ/с`*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.5)

- 🌟 **[Мгновенное получение списка `.torrent`-файлов и анти-503 защита (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена перегрузка зеркала `itorrents.net` (HTTP 503): предзагрузка ограничена до 4 тем с очередью 1 поток и 250 мс паузой, уступает активным запросам пользователя и уходит на 10-секундный бэкофф при 429/503; убрана двойная атака на `itorrents.org` (301 на `itorrents.net`).
  - Добавлен аппаратный обход блокировок ТСПУ и лимитов `FetchItorrentsViaCloudflareEdgeTlsSplitAsync` (`TlsSplitStream`: фрагментация TLS ClientHello напрямую через граничные IP Cloudflare `188.114.96.3..98.3`).
  - В `InspectTorrentFilesAsync` параллельно с HTTP-зеркалом через 1.0 с (или сразу при отказе HTTP) запускается быстрая гонка P2P BEP-9 `ut_metadata`; `TryGetInstantCachedTorrentFiles` использует локальный буфер без гонок при переключении тем; убран race в `LoadSelectedTopicAsync`.
- 🌟 **[Мгновенное возобновление скачивания FastResume и встроенный обход ТСПУ трекеров (`storm_lt_turbo.py`, `RuTrackerDownloadQueueService.cs`)]**:
  - В `storm_lt_turbo.py` внедрено сохранение и загрузка `--resume-file` (`.resume` каждые 25 с и мгновенно при паузе/остановке/закрытии), что полностью исключает многоминутный перехэш всех гигабайтных файлов на HDD после перезапуска программы.
  - Во время дисковой проверки `checking_files` / `checking_resume_data` движок честно передаёт состояние `"checking"` (бейдж `ПРОВЕРКА`, слот учитывается в лимите одновременных закачек без плодения лишних процессов), а сразу по окончании проверки делает мгновенный залп подключений ко всем пирам.
  - Внутрь `storm_lt_turbo.py` добавлен автономный обход ТСПУ методом Cloudflare HTTP/1.1 Pipelining (`harvest_cf_bt_peers`), который автоматически подключает живых сидов с `bt..bt4.t-ru.org`, если `seeds == 0` или не хватает пиров для BEP-9.
- 🌟 **[Android-движок (`RuTrackerCloudEngine.kt`)]**:
  - Обновлён порядок зеркал `.torrent` кэша на `itorrents.net` -> `itorrents.org` с удалением нерабочего `torrage.info`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Анти-503 троттлинг `_mirrorFetchThrottle`/`_prefetchThrottle`, `TlsSplitStream` + `FetchItorrentsViaCloudflareEdgeTlsSplitAsync`, потокобезопасный `TryGetInstantCachedTorrentFiles`, гонка BEP-9 с 1.0с форой в `InspectTorrentFilesAsync`, распределение `bt..bt4.t-ru.org` по разным Cloudflare Edge IP, аргумент `--resume-file` и разбор состояния `"checking"`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Предзагрузка 4 тем без 503, удаление гонки `filesTask.ContinueWith`, защита активного заголовка P2P при `discoveredNewHash`.
- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Поддержка `--resume-file` (`read_resume_data`/`save_resume_data`), `hashing_threads = 2`, состояние `"checking"`, пост-проверочный реконнект пиров и автономный `harvest_cf_bt_peers` (Cloudflare HTTP/1.1 Pipelining).
- 🔹 **[Services/RuTrackerDownloadQueueService.cs]**: Этап `ПРОВЕРКА`, корректный учёт в `CountsTowardConcurrencyLimit` и синхронизация прогресса.
- 🔹 **[Релиз 8.2.5]**: Версии синхронизированы до `8.2.5` (`versionCode = 8205`), публикуются установщик Windows, портативный архив и APK для Android (без Legacy).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Eliminated `itorrents.net` HTTP 503 self-throttling via priority-aware foreground/background throttling (prefetch capped to 4 topics sequentially with foreground yield), removed redundant concurrent `itorrents.org` calls, and added a Cloudflare Edge TLS ClientHello fragmentation bypass (`TlsSplitStream`) plus a 1.0s-staggered BEP-9 `ut_metadata` race in `InspectTorrentFilesAsync` and race-free local torrent loading in `TryGetInstantCachedTorrentFiles`.
- 🔸 Fixed active download `0.00 MB/s | 0 / 0` stalls on restart by adding full download FastResume (`--resume-file` saved periodically and on pause/exit) in `storm_lt_turbo.py`, honest `checking` state reporting (`ПРОВЕРКА` badge counted in queue concurrency slots), post-check peer burst connect, and built-in Cloudflare HTTP/1.1 Pipelining tracker peer harvesting (`bt..bt4.t-ru.org`).
- 🔸 Removed dead `torrage.info` from Android (`RuTrackerCloudEngine.kt`, using `itorrents.net` -> `itorrents.org`), and bumped version to `8.2.5` (`versionCode = 8205`).

</details>

---

## **8.2.4** (25.11.2026)

> 💡 **Релиз 8.2.4** - *Мгновенная подгрузка `Title ID`, метаданных и списка `.torrent`-файлов по Magnet/HTTP (`Mario + Rabbids: Sparks of Hope`), всегда видимый чип `Title ID` слева от расширенной версии и глобальное ускорение загрузки тем RuTracker*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.4)

- 🌟 **[Мгновенное определение `Title ID`, издателя, обложки и `.torrent`-файлов (`RuTrackerModDownloaderDialog.xaml.cs`, `RuTrackerModService.cs`)]**:
  - Устранено ошибочное чтение текста Magnet-заглушек (`IsRealTorrentFile = false`) в `EnrichGameSlotsFromFilesAndTitleDb`, из-за которого срывался мгновенный поиск в TitleDB; добавлен прямой фолбэк по `slot.GameTitle` и чистому заголовку темы (`Mario plus Rabbids Sparks of Hope` сразу определяет `0100317013770000`, `Ubisoft` и подтягивает версию).
  - Исправлен баг в `LoadSelectedTopicAsync`: фоновый резолв метаданных (`StartMagnetMetadataBackground` с параллельной гонкой зеркал кэша `itorrents` + P2P `ut_metadata`) теперь запускается сразу по `!_activeTopicHasRealFiles`, не дожидаясь таймаутов HTML/Cloudflare страницы темы.
- 🌟 **[Исправленная вёрстка строки слота с всегда видимым `Title ID` (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModService.cs`)]**:
  - Добавлено свойство `TitleIdChipDisplay` - бейдж `Title ID` слева от версии больше никогда не пропадает; строка разнесена в 3-колоночную сетку (`1*`, `Auto`, `0.22*`), поле «Доп. облачные ссылки» стало заметно компактнее, `Title ID`, версия и внутренняя версия сдвинуты левее, поле версии расширено (`MinWidth="124"`), а ширина `Title ID` и внутренней версии оставлена фиксированной.
- 🌟 **[Глобальное ускорение и стабильность загрузчика RuTracker (`RuTrackerModService.cs`, `StormHttpClientFactory.cs`)]**:
  - Удалено нерабочее HTML-зеркало `torrage.info`, увеличен таймаут гонки кэша `.torrent` (`12` с), запрещено кэширование временных сетевых таймаутов как 404, убрана блокирующая UI 5.2-секундная пауза STEP 4 внутри `InspectTorrentFilesAsync`, `PrefetchTopicsMetadata` сохраняет дерево файлов заранее (топ-8 тем открываются мгновенно), `FetchValidatedTopicPageHtmlAsync` ведёт параллельную гонку с облачными рендерами и 5-минутным ТСПУ-кулдауном, а пул `StormHttpClientFactory` расширен до 64 соединений на сервер.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Фильтр `f.IsRealTorrentFile` в `EnrichGameSlotsFromFilesAndTitleDb`, фолбэк поиска TitleDB по `slot.GameTitle`, запуск `StartMagnetMetadataBackground` по `!_activeTopicHasRealFiles`, предзагрузка топ-8 раздач.
- 🔹 **[Services/RuTrackerModService.cs, Services/StormHttpClientFactory.cs]**: `TitleIdChipDisplay`, параллельный `ResolveMagnetMetadataBackgroundAsync`, прогрев `api.rutracker.cc` + кэш дерева файлов в `PrefetchTopicsMetadata`, 5-минутный ТСПУ-кулдаун и параллельная гонка `FetchValidatedTopicPageHtmlAsync`, `MaxConnectionsPerServer = 64`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml]**: 3-колоночная строка дополнительных ссылок (`1*`, `Auto`, `0.22*`) со сдвигом группы чипов влево, суженным полем ссылок, всегда видимым фиксированным `Title ID` и расширенной кнопкой версии.
- 🔹 **[Релиз 8.2.4]**: Обновлены версии Windows (`8.2.4`) и Android (`versionCode = 8204`, `versionName = "8.2.4"`) без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `EnrichGameSlotsFromFilesAndTitleDb` ignoring Magnet/DHT placeholder strings (`IsRealTorrentFile = false`) with TitleDB fallback on `slot.GameTitle`, so `Mario + Rabbids: Sparks of Hope` (`t=6271728`) resolves `0100317013770000` and metadata immediately, and fixed `LoadSelectedTopicAsync` launching background torrent/BEP-9 resolution without waiting for RuTracker HTML/Cloudflare timeouts.
- 🔸 Added always-visible `TitleIdChipDisplay` and converted the GameSlot extra-links row to a 3-column grid (`1*`, `Auto`, `0.22*`), narrowing the extra-links box, shifting `Title ID`, `Version`, and `Internal Version` leftward, widening `Version`, and keeping `Title ID` and `Internal Version` fixed.
- 🔸 Sped up all RuTracker topic & torrent loads (`FetchTorrentFromCacheMirrorsCoreAsync`, `ResolveMagnetMetadataBackgroundAsync` parallel race, `PrefetchTopicsMetadata` filetree caching for top 8 results, `FetchValidatedTopicPageHtmlAsync` 5-minute TSPU cooldown, and `StormHttpClientFactory` 64 connections) and bumped version to `8.2.4` (`versionCode = 8204`).

</details>

---

## **8.2.3** (25.11.2026)

> 💡 **Релиз 8.2.3** - *Исправление разбиения двуязычных раздач и скачивания через Magnet-заглушки (`Mario + Rabbids: Sparks of Hope`), расширенная строка версии в слоте и оптимизация плавности интерфейса*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.3)

- 🌟 **[Точное распознавание двуязычных раздач RuTracker (`RuTrackerModService.cs`, `TitleDbService.cs`)]**:
  - Устранено ошибочное разделение двуязычных тем вида `[Nintendo Switch] English Title / Русское название + N DLC` (включая `Mario plus Rabbids Sparks of Hope / Марио и Кролики: Искры Надежды + 7 DLC`, `t=6271728`) на 2 отдельные игры: такие раздачи теперь корректно определяются как 1 игра, при этом реальные сборники (`Bayonetta / Bayonetta 2`, `South Park: Stick of Truth + Fractured But Whole / Южный парк...`) продолжают разделяться корректно.
  - В `TitleDbService.TryFindTitleByName` добавлена нормализация слова `plus` и предкомпиляция регулярок, чтобы названия вроде `Mario plus Rabbids Sparks of Hope` сразу находили `TitleID` `0100317013770000` в локальной базе.
- 🌟 **[Исправление скачивания раздач при активной Magnet-заглушке (`RuTrackerModService.cs`, `storm_lt_turbo.py`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Исправлена критическая ошибка в `ComputeSkippedTorrentEntries`: синтетические строки-заглушки (`IsRealTorrentFile = false`) больше не участвуют в фильтрации реальных файлов `.torrent`, из-за чего раньше могли пропускаться все файлы раздачи и загрузка не стартовала.
  - Добавлено резервное зеркало кэша `.torrent` (`torrage.info`), увеличены таймауты гонки зеркал, гидратация `InfoHash` из локального индекса для живых результатов и защитная проверка в `storm_lt_turbo.py`.
- 🌟 **[Обновлённая вёрстка слота игры (`RuTrackerModDownloaderDialog.xaml`)]**:
  - Поле «Доп. облачные ссылки только для этой части» аккуратно сужено, бейджи `Title ID`, версии и внутренней версии сдвинуты левее, поле версии расширено для длинных буквенных версий, а ширина чипов `Title ID` и внутренней версии зафиксирована.
- 🌟 **[Оптимизация скорости и плавности интерфейса (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `CatalogPage.xaml.cs`, `GameLibraryPage.xaml.cs`)]**:
  - Исправлено лишнее срабатывание `OnPropertyChanged` в `CommitUiUpdates()`, включён пропуск тиков таймера очереди при полном простое, кеширование строк заголовка очереди, пакетное обновление `ItemsSource` в «Информации» и «Библиотеке игр», вынос `CoverCacheService.ResolveCoverUrl` в фон, ранний выход при центрировании полей ввода и `DecodePixelWidth` в диалогах превью.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлена проверка `IsBilingualSingleGameSlashPair` в `DetectMultiGameTitlesFromRelease`, фолбэк `InfoHash` из `_topicsById` в `ExtractInfoHashHex` и `SearchTopicsAsync`, фильтр `f.IsRealTorrentFile` в `ComputeSkippedTorrentEntries`, третье зеркало `torrage.info` и обращение к зеркалам в начале `ResolveMagnetMetadataBackgroundAsync`.
- 🔹 **[tools/nsz/storm_lt_turbo.py, Services/TitleDbService.cs]**: Защита от полного пропуска не-pad файлов в `storm_lt_turbo.py`, предкомпиляция регулярок и `plus`-фолбэк в `TitleDbService`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml(.cs)]**: Расширена кнопка версии в слоте, сужено поле доп. ссылок, сокращены лишние вызовы `RebuildFileAndLinkGroups()`, добавлен кэш текста заголовка очереди и оптимизирован `CenteredInput_Loaded`.
- 🔹 **[Оптимизация производительности и релиз 8.2.3]**: Оптимизированы `RuTrackerDownloadQueueService.cs`, `CatalogPage.xaml.cs`, `GameLibraryPage.xaml.cs`, `ControlEditorDialog.xaml.cs`, `DirectConsoleSenderDialog.xaml.cs`, `RomFsInspectorDialog.xaml.cs`; версия повышена до `8.2.3` (`versionCode = 8203`).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed bilingual `English / Russian` RuTracker topic parsing (`DetectMultiGameTitlesFromRelease`) so single games like `Mario plus Rabbids Sparks of Hope / Марио и Кролики: Искры Надежды + 7 DLC` (`t=6271728`) are no longer split into 2 games, and improved TitleDB lookup for `plus` titles (`0100317013770000`).
- 🔸 Fixed `ComputeSkippedTorrentEntries` and `storm_lt_turbo.py` so Magnet/DHT placeholder rows (`IsRealTorrentFile = false`) never cause all real torrent files to be skipped at download start, added `torrage.info` cache mirror, and improved live search hash hydration.
- 🔸 Refined the GameSlot extra-links row layout in `RuTrackerModDownloaderDialog.xaml` (narrower extra-links box, wider version button, Title ID and internal version kept fixed and shifted slightly left).
- 🔸 Improved app speed and UI smoothness (`RuTrackerDownloadQueueService`, `RuTrackerModDownloaderDialog`, `CatalogPage`, `GameLibraryPage`, and `DecodePixelWidth` in preview dialogs) and bumped version to `8.2.3` (`versionCode = 8203`).

</details>

---

## **8.2.2** (24.11.2026)

> 💡 **Релиз 8.2.2** - *Мгновенное определение отображаемой версии игр в загрузчике RuTracker (включая буквенные версии вида `3.0.5b` без ввода имени) и переход на единую сборку Android*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.2)

- 🌟 **[Точное и мгновенное определение версии игр в RuTracker (`TitleDbService.cs`, `RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `CatalogScannerService.cs`)]**:
  - Устранено обрезание буквенных и хотфикс-версий вроде `3.0.5b` до `3.0`: обновлён парсер `ExtractGameVersionsFromTopicHtml`, `ScanVerTextSource`, `ExtractFilePatchCode`, `IsValidSemanticVersion`, `CompareSemanticVersions`, `StormTitleTripleRegex` и `CatalogScannerService` с запретом бэктрекинга на разделитель `.\d` и поддержкой суффиксов `[a-zA-Z]{1,2}` / `-hf...`.
  - Поиск отображаемой версии через API `stormgamesworld.ru` (`TryResolveDisplayVersionOnlineAsync`) теперь стартует немедленно параллельно с чтением темы (сразу после получения списка файлов `.torrent`, не дожидаясь окончания загрузки HTML), ищет в первую очередь по точному `TitleID`, а затем по названию, и не блокирует повторные попытки при временных сетевых сбоях.
  - Повышена версия схемы кэша тем RuTracker (`SchemaVersion = 3`), чтобы ранее кэшированные темы без версии или с усечённой версией автоматически перечитались.
- 🌟 **[Единая сборка Android (`STORM_SWITCH_BOX_8.2.2.apk`)]**:
  - Убрана лишняя устаревшая сборка `_LEGACY.apk`; в релизе публикуются установщик Windows, портативный архив Windows и актуальная сборка Android `8.2.2` (`versionCode = 8202`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/TitleDbService.cs]**: Добавлена поддержка буквенных/хотфикс-суффиксов (`3.0.5b`, `1.0.2a`, `1.2.3-hf1`) в `StormTitleTripleRegex`, `IsValidSemanticVersion`, `ParseSemanticParts` и `CompareSemanticVersions`; `TryResolveDisplayVersionOnlineAsync` переведена на поиск в первую очередь по `baseTid` с 30-секундным окном повтора вместо вечной блокировки ошибки.
- 🔹 **[Services/RuTrackerModService.cs]**: Устранена ошибка бэктрекинга регулярных выражений в `ExtractGameVersionsFromTopicHtml`, расширено распознавание заголовков версий и повышена `SchemaVersion` кэша тем до `3`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs, Services/CatalogScannerService.cs]**: Обновлены `ScanVerTextSource`, `ExtractFilePatchCode`, `FnPairVerRegex`, `FnVerRegex`; добавлен параллельный запуск `TryResolveDisplayVersionOnlineAsync` через `_pendingOnlineVersionLookups` с индикатором `...` до завершения фонового запроса.
- 🔹 **[Релиз и документация]**: Синхронизирована версия `8.2.2` (`versionCode = 8202`), обновлены `README.md`, `CHANGELOG.md` и `RELEASE_NOTES.md` под выпуск 3 основных сборок без `_LEGACY.apk`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed semantic version parsing and comparison (`TitleDbService`, `RuTrackerModService`, `RuTrackerModDownloaderDialog`, `CatalogScannerService`) so alphanumeric versions like `3.0.5b` never truncate back to `3.0`, and bumped RuTracker parsed topic cache `SchemaVersion` to `3`.
- 🔸 Enabled immediate parallel `stormgamesworld.ru` TitleID version lookup in the RuTracker downloader right when torrent files arrive without waiting for manual output name input or HTML completion.
- 🔸 Bumped version to `8.2.2` (`versionCode = 8202`) and streamlined Android releases to the Modern `STORM_SWITCH_BOX_8.2.2.apk` build only (no Legacy APK).

</details>

---

## **8.2.1** (24.11.2026)

> 💡 **Релиз 8.2.1** - *Приоритет внутренних названий из метаданных игры во вкладке «Информация», адаптивная шапка без наложения поиска и обновлённый стилизованный справочник кодов ошибок*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.1)

- 🌟 **[Внутренние русские и английские названия игр во вкладке «Информация» (`CatalogScannerService.cs`, `Nintendo3dsService.cs`, `CatalogItem.cs`, `CatalogPage.xaml(.cs)`) ]**:
  - На Switch исправлено чтение `control.nacp` (полное потоковое чтение всех `0x4000` байт без обрыва на `NCZ` и открытие `Control NCA` с `IntegrityCheckLevel.None`): теперь извлекается и русский слот `[11]`, и английский слот `[0]/[1]`, отдельно учитываются Base и Update Control NCA, а `TitleDbService.EnrichCatalogItem` больше не перетирает реальное вшитое название.
  - На Nintendo 3DS исправлен порядок слотов SMDH в `TryReadSmdhAtOffset` (и в Android `StormNczEngine.kt`): русский (`11`) и английский (`1`) слоты извлекаются отдельно с приоритетом языка интерфейса, а в окне деталей и подсказке карточки показывается второе вшитое название игры. Поиск во вкладке «Информация» теперь ищет одновременно по русскому и английскому внутренним названиям.
- 🌟 **[Исправлена вёрстка шапки вкладки «Информация» (`CatalogPage.xaml`)]**:
  - Верхняя панель разделена на две адаптивные строки: кнопки «Папки сканирования», прокручиваемый переключатель платформ и индикатор TitleDB больше не сжимают строку поиска, а поле поиска с гибкой шириной (`*`) и кнопка `Коды ошибок (2xxx-xxxx)` разнесены с интервалом `12px` и никогда не налезают друг на друга.
- 🌟 **[Новый широкий стилизованный справочник кодов ошибок (`Views/Dialogs/ErrorCodesDialog.xaml(.cs)`) ]**:
  - Справочник системных кодов ошибок Switch и Atmosphère (`2xxx-xxxx`) вынесен в отдельное широкое адаптивное окно (до `1120x760`), оформленное под все 8 тем оформления STORM SOFT, с фильтрами по категориям (`Все коды`, `Файлы и SD`, `Вылеты и моды`, `Подписи и ключи`, `Сеть и блокировки`), живым поиском по кодам и симптомам, 10 подробными карточками с цветными акцентами, кнопкой копирования решения и поддержкой 11 языков.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Models/CatalogItem.cs, Models/Nintendo3dsInfo.cs, Services/CatalogScannerService.cs, Services/Nintendo3dsService.cs, Services/TitleDbService.cs]**: Добавлены `InternalTitleRussian`, `InternalTitleEnglish`, `InternalTitleFallback`, `HasEmbeddedInternalTitle`, `ResolvePreferredInternalTitle(...)`, `AlternateInternalTitle` и `TitleTooltip`; добавлено полное чтение `control.nacp` через `ReadStreamFull`, приоритет русского языка для `NACP` слота `11`, `icon_Russian.dat` и `SMDH` слота `11`, а также защита от перезаписи вшитых названий базой TitleDB (при сохранении поиска eShop по `InternalTitleEnglish`).
- 🔹 **[Views/CatalogPage.xaml, Views/CatalogPage.xaml.cs]**: Обновлена разметка шапки (устранено наложение поля поиска на кнопку `Коды ошибок (2xxx-xxxx)`), добавлен подзаголовок второго внутреннего названия в окне деталей `DetailAltTitle`, поиск по обоим внутренним названиям и автообновление названий при смене языка интерфейса; подключён новый `ErrorCodesDialog`.
- 🔹 **[Views/Dialogs/ErrorCodesDialog.xaml, Views/Dialogs/ErrorCodesDialog.xaml.cs, Services/LocalizationService.UiTree.cs]**: Создан широкий стилизованный диалог-справочник с 10 карточками ошибок (`2002-4153`, `2168-0002`, `2016-0641`, `2162-0002`, `2155-8007`, `2002-3502`, `2005-0003`, `MasterKey / KeyGeneration`, `2002-2060`, `2144-0001`), темо-адаптивными кистями, копированием в буфер обмена и полным 11-язычным переводом без длинных тире.
- 🔹 **[Android: StormNczEngine.kt]**: Обновлено чтение SMDH-метаданных (функция `extractSmdhMeta` с приоритетом слотов `11 -> 1 -> 0` вместо смещённого слота `0`), синхронизированы номера сборок `8.2.1` и `versionCode = 8201`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed the Info view (`CatalogScannerService`, `Nintendo3dsService`, `TitleDbService`, `CatalogItem`) to display the exact game titles embedded in Switch `control.nacp` (Russian slot `11` and English slots `0`/`1`, reading all `0x4000` bytes reliably) and 3DS `SMDH` (slots `11` and `1`), preventing TitleDB from overwriting embedded titles while searching across both internal titles and showing the alternate internal title in tooltips and detail headers.
- 🔸 Redesigned the Info view header toolbar (`CatalogPage.xaml`) into two responsive rows so the search box and `Error codes (2xxx-xxxx)` button stay cleanly separated at every window size.
- 🔸 Replaced the narrow inline error code popup with a wide, theme-aware styled `ErrorCodesDialog` (up to `1120x760`) featuring category filter pills, live search across 10 detailed Switch/Atmosphère error entries, one-click copy buttons, and 11-language localization.

</details>

---

## **8.2.0** (24.11.2026)

> 💡 **Релиз 8.2.0** - *Полная поддержка всех 17+1 нативных форматов Nintendo 3DS (включая единые бандлы без установки `.bcci`, `.bcxi` и `.bcia`), справочник и умные рекомендации форматов на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.2.0)

- 🌟 **[Бандлы без установки `.bcci`, `.bcxi`, `.bcia` (`ThreeDsBundleService.cs`, `Nintendo3dsService.cs`, `StormNczEngine.kt`)]**:
  - На Windows (C#) и Android (Kotlin) реализован бинарно-точный движок `microtar` + `Z3DSMetadata`, совместимый с `STORM 3DS` и `Azahar`: запись `"metadata"` идёт первой (`bundler`, `date`, `mainfile`, `titleinfo` без дублирования `smdh`), имена записей санитизируются до 99 байт UTF-8, поддерживаются файлы `> 8 ГБ` (GNU base-256) и проверка всех вложенных Z3DS-секций (`Seek Table 0x8F92EAB1`).
  - **`.bcci` (рекомендуется №1)** объединяет сжатую игру `.zcci` + официальные обновления `.zcia` + все DLC `.zcia` (и вшитые моды/русификаторы) в 1 файл с мгновенным запуском без установки в `sdmc` и без 2x расхода места. **`.bcxi`** дополнительно убирает лишние разделы мануала/DLP (только Partition 0 NCCH `.zcxi` + `.zcia` патчей и DLC) для абсолютного минимума размера, а **`.bcia`** собирает пакеты `.cia`/`.zcia` для групповой установки.
  - Реализован быстрый режим сборки `.bcci` / `.bcxi` без распаковки RomFS через `ctrtool`, а также потоковое извлечение `Partition 0 NCCH` (`NCSD / CIA <-> CXI` и `CXI/APP -> CCI`).
- 🌟 **[Все 17+1 форматов 3DS, описания и умные рекомендации в интерфейсе и Инструкции]**:
  - Во все выпадающие списки, файловые пикеры, Умные папки, Аудитор библиотеки, Инспектор RomFS/TAR, Историю задач, локализацию (11 языков Windows и 10 языков Android), Инструкцию (RU/EN и интерактивный симулятор конвертера) и фильтр RuTracker добавлены все 18 расширений: `.bcci`, `.bcxi`, `.bcia`, `.zcci`, `.zcxi`, `.zcia`, `.z3dsx`, `.z3ds`, `.cci`, `.3ds`, `.cxi`, `.cia`, `.3dsx`, `.app`, `.cfa`, `.elf`, `.axf`, `.artic`.
  - В интерфейсе задач, параметров (Windows) и главного экрана (Android) добавлены описания каждого формата, панель рекомендаций (с выделением `.bcci` и `.bcxi` как лучших форматов) и встроенный справочник форматов 3DS.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Windows: Services/ThreeDsBundleService.cs, Services/Z3dsService.cs, Services/Nintendo3dsService.cs]**: Реализован каталог `ThreeDsFormatCatalog` (все 17+1 форматов с описаниями и рекомендациями RU/EN), `SubRangeStream` и потоковый движок `microtar` (`TryReadBundleInfo`, `TryOpenMainEntryTransparentStream`, `CreateBundleAsync`, `ExtractBundleAsync`, `VerifyBundleIntegrity`), быстрая вырезка Partition 0 `ExtractNcsdPartition0ToCxiAsync` / `ExtractCiaContent0ToCxiAsync`, `WrapCxiToCciReliableAsync`, потоковый `Z3dsService.VerifyZ3dsIntegrity(Stream)` и маршрутизация всех 18 расширений в `Build3dsMultiContentAsync`, `Convert3dsContainerAsync`, `Unpack3dsContainerAsync`, `Pack3dsContainerAsync`.
- 🔹 **[Windows: ViewModels, Services и Views]**: Обновлены `ProcessingTask.cs`, `AppSettings.cs` (`DefaultFormat3ds = "BCCI"`), `TasksViewModel.cs`, `WatchFolderService.cs`, `CatalogScannerService.cs`, `LibraryAuditorService.cs`, `DirectConsoleSenderService.cs`, `VirtualRomFsInspectorService.cs` (`/bundle/...`), `HistoryService.cs`, `RuTrackerModService.cs`, `TasksPage.xaml(.cs)` (кнопка-справочник форматов), `SettingsPage.xaml(.cs)` (живая карточка рекомендаций и WatchFolder 0..13), `LocalizationService.cs`, `LocalizationService.UiTree.cs` и `InstructionPage.xaml.cs`.
- 🔹 **[Android: StormNczEngine.kt, MainActivity.kt, RuTrackerCloudEngine.kt, StormLocalization.kt, ThreeDsBundleAndFormatsTest.kt]**: Добавлены `is3dsExtension` / `is3dsBundleExtension`, группировка TitleID 3DS (`00040000` / `0004000E` / `0004008C`), чистый Kotlin `microtar` + `Z3DSMetadata` движок, режимы `13=BCCI ★`, `14=BCXI ★`, `15=BCIA`, `16=CXI`, `17=3DSX`, быстрая вырезка Partition 0 NCCH, карточка рекомендаций и диалог-справочник 3DS, тема справки №9 и юнит-тесты `ThreeDsBundleAndFormatsTest.kt`.
- 🔹 **[Версия 8.2.0]**: Все сборки Windows (`8.2.0`) и Android (`versionCode = 8200`, `versionName = "8.2.0"`) синхронизированы; обновлены `README.md`, `CHANGELOG.md` и `RELEASE_NOTES.md`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Implemented bit-exact `microtar` + `Z3DSMetadata` bundles (`.bcci`, `.bcxi`, `.bcia`) on both Windows (C#) and Android (Kotlin) matching `STORM 3DS` and `Azahar`: writes `"metadata"` first (`bundler`, `date`, `mainfile`, `titleinfo` without `smdh`), sanitizes entry names up to 99 UTF-8 bytes, supports >8 GiB TAR headers, zero-unpack assembly when no RomFS/ExeFS mods are attached, and transparent bundle inspection and integrity verification.
- 🔸 Added all remaining Nintendo 3DS native formats (`17+1`: `.bcci`, `.bcxi`, `.bcia`, `.zcci`, `.zcxi`, `.zcia`, `.z3dsx`, `.z3ds`, `.cci`, `.3ds`, `.cxi`, `.cia`, `.3dsx`, `.app`, `.cfa`, `.elf`, `.axf`, `.artic`), fast `NCSD/CIA -> CXI` Partition 0 extraction, and `CXI/APP -> CCI` wrapping.
- 🔸 Added live per-format descriptions, recommendation banners highlighting `.bcci` and `.bcxi` as #1 zero-install choices, and full interactive 3DS format guides in both Windows and Android UIs plus 11-language localizations.

</details>

## **8.1.3** (24.11.2026)

> 💡 **Релиз 8.1.3** - *Надёжная прямая конвертация крупных образов 3DS/CCI (>= 2 ГБ и 4 ГБ) в CIA/ZCIA через нарезку игровых разделов NCSD, защита от сбоев по длине пути `MAX_PATH` во всех операциях 3DS и синхронизированное обновление версий*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.3)

- 🌟 **[Надёжная конвертация крупных образов 3DS/CCI -> CIA/ZCIA (`Nintendo3dsService.cs`)]**:
  - Устранено падение `makerom.exe -ccitocia` (`Access Violation 0xC0000005`) на картриджных дампах Nintendo 3DS / New Nintendo 3DS объёмом `>= 1.875 ГБ` (включая 4 ГБ образы уровня *Xenoblade Chronicles 3D*), а также отказ `[NCCH ERROR] AccessDesc Sigcheck Failed` на расшифрованных No-Intro дампах без флага `-ignoresign`.
  - Внедрён новый движок `ConvertCciToCiaReliableAsync`: для образов `< 1.875 ГБ` с коротким путём сначала пробуется быстрый `-ignoresign -ccitocia`, а для крупных образов (`>= 1.875 ГБ`) и при любом отказе/краше выполняется прямое чтение заголовка NCSD (`0x120`) и потоковое извлечение только игровых разделов `0..2` (`p0.cxi`, `p1.cfa`, `p2.cfa` без разделов обновления прошивки `6..7`) с прямой сборкой через `makerom -f cia -ignoresign -target p` (с резервом `-target t`).
  - В `ConvertCiaToCciReliableAsync` добавлена многоступенчатая конвертация CIA -> CCI/3DS с приоритетом флага `-ignoresign`, а в `Build3dsFromExtractedDirAsync` флаг `-ignoresign` и резерв через `ConvertCciToCiaReliableAsync` защищают двойную сборку `3DS + CIA`.
- 🌟 **[Короткие временные пути 3DS и защита от `MAX_PATH` (`Nintendo3dsService.cs`, `TempCleanupService.cs`)]**:
  - Добавлены хелперы `CreateShortTempDirectory` и `CleanupShortTempDirectory`, создающие компактные рабочие каталоги формата `<диск>:\STORM_TMP\<tag>_<6hex>` на быстром фиксированном диске с достаточным свободным местом (с безопасным резервом `%TEMP%\S3D_<tag>_<6hex>` и автоудалением пустого `STORM_TMP` и паттерна `S3D_*` в `TempCleanupService`).
  - Короткие пути и сокращённые имена подпапок (`b`, `u1`, `d1`, `c_...`) внедрены в `Build3dsMultiContentAsync`, `Extract3dsContainerToDirAsync`, `Convert3dsContainerAsync`, `Unpack3dsContainerAsync` и `Pack3dsContainerAsync`, исключая ошибку `ctrtool.exe` `[tc::io::VirtualFileSystem::openFile() ERROR] File does not exist.` на глубоких деревьях RomFS.
- 🌟 **[Синхронизированное обновление Windows и Android 8.1.3]**:
  - Версия обновлена до `8.1.3` (`versionCode = 8103` на Android) для всех сборок Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/Nintendo3dsService.cs]**: Добавлены `HasValidOutputFile`, `TryDeleteFileSilent`, `CreateShortTempDirectory`, `CleanupShortTempDirectory`, `ConvertCciToCiaReliableAsync` и `ConvertCiaToCciReliableAsync`; все прямые вызовы `-ccitocia` и `-ciatocci` в `Convert3dsContainerAsync` и `Build3dsFromExtractedDirAsync` переведены на надёжную логику с разбором NCSD (`0..2`) и `-ignoresign`; временные каталоги `Build3dsMultiContentAsync`, `Convert3dsContainerAsync`, `Unpack3dsContainerAsync` и `Pack3dsContainerAsync` переведены на короткие пути `STORM_TMP`.
- 🔹 **[Services/TempCleanupService.cs]**: В `tempDirPatterns` добавлен шаблон `"S3D_*"` для автоматической очистки резервных временных папок 3DS.
- 🔹 **[StormSwitchBox.csproj, App.xaml.cs, Build_Release.ps1, Services/Z3dsService.cs, app.manifest, installer/StormInstaller/*, android/app/build.gradle.kts, StormNczEngine.kt, README.md]**: Версия продукта обновлена до `8.1.3` (`versionCode = 8103`).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `makerom -ccitocia` crashes (`0xC0000005` on `>= 1.875 GB` / 4 GB NCSD images such as *Xenoblade Chronicles 3D*) and `AccessDesc Sigcheck Failed` errors on decrypted No-Intro dumps by introducing `ConvertCciToCiaReliableAsync`: extracts game NCSD partitions `0..2` (`NCCH`) directly from `0x120` (skipping firmware update partitions `6..7`) and packages via `makerom -f cia -ignoresign -target p`.
- 🔸 Added `ConvertCiaToCciReliableAsync` and `-ignoresign` handling for `CIA -> CCI/3DS` and dual `3DS + CIA` builds in `Build3dsFromExtractedDirAsync`.
- 🔸 Added `CreateShortTempDirectory` (`<drive>:\STORM_TMP\<tag>_<6hex>` with `%TEMP%\S3D_*` fallback) and shortened 3DS extraction folder names across all 3DS multi-content, convert, unpack, and pack pipelines to eliminate Windows `MAX_PATH` failures in `ctrtool.exe`.
- 🔸 Updated unified Windows x64 and Android Modern/Legacy releases to version `8.1.3` (`versionCode 8103`).

</details>

## **8.1.2** (23.11.2026)

> 💡 **Релиз 8.1.2** - *Ускорение сборки и многопоточного Zstd-сжатия (`FastPassthrough` без пересжатия Block NSZ и снятие лимита CPU/4), выбор лимита одновременных сборок и скачиваний в интерфейсе RuTracker, исправление обратной декомпрессии Block NSZ -> NSP (совместимость с Eden) и поддержка 4-сегментных версий игр (`1.0.6.1267`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.2)

- 🌟 **[Многократное ускорение сборки и сжатия NSZ/Z3DS (`MultiContentService.cs`, `NszCompressionService.cs`, `StormNczCompressor.cs`, `Z3dsService.cs`, `RuTrackerDownloadQueueService.cs`)]**:
  - Снято жёсткое ограничение очереди RuTracker в `1` одновременную сборку (`SemaphoreSlim(1, 1)`): внедрён потокобезопасный динамический планировщик `AcquireBuildSlotAsync` / `ReleaseBuildSlot` с поддержкой до `8` параллельных сборок и мгновенным пробуждением задач из очереди при смене лимита.
  - Снято жёсткое ограничение потоков сжатия в `25%` ядер ЦП (`ProcessorCount / 4`) во всех движках (`StormNczCompressor`, `NszCompressionService`, `Z3dsService`): добавлен динамический расчёт `GetOptimalCompressionThreads()` по бюджету `UsedCores` и активным задачам, а флаг `-L` (`--long`) при запуске `nsz.exe` теперь используется только в режиме Solid NSZ, исключая лишнее выделение 128 МБ на поток при 1 МБ Block NSZ.
  - Реализован режим `FastPassthrough`: при сборке в `NSZ` контейнеры в формате Block NSZ больше не проходят через бесполезный цикл `распаковка -> повторное сжатие` (при `Fast Multi-Content` / одиночном файле / DLC в `MultiContentService`, а также при проверке `InspectPfsNczPassthrough` в `NszCompressionService`, где при смешанном составе сжимаются только несжатые NCA).
- 🌟 **[Управление одновременными скачиваниями и сборками прямо в RuTracker (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`, `SettingsPage.xaml`, `AppSettings.cs`)]**:
  - В заголовочную панель очереди закачек RuTracker добавлены переключатели лимита одновременных скачиваний (`Закачек: 1..10`) и сборок (`Сборок: 1..8`) с моментальным применением и сохранением в `AppSettings`.
  - В общих параметрах приложения максимальный лимит одновременных сборок/задач увеличен с `5` до `8` с двусторонней живой синхронизацией с окном RuTracker.
- 🌟 **[Исправление декомпрессии Block NSZ -> NSP (`Decompressor.py`, `Keys.py`, `StormNczStorage.cs`, `StormNczCompressor.cs`, `NszCompressionService.cs`, `TasksViewModel.cs`)]**:
  - Устранено повреждение итоговых `.nsp` при обратной распаковке блочных `.nsz` (включая игры с DLC `firstSection = 0xC00` и обновлениями BKTR, из-за которого эмулятор Eden отклонял собранный `.nsp`): добавлена единая нормализация секций `_normalize_sections_for_ncz` с чтением `NCZBLOCK.decompressedSize`, исправлен расчёт смещений при `firstSection > 0x4000`, добавлена поддержка `CryptoType == 5`, сортировка секций с покрытием межсекционных и концевых зазоров и расшифровка `TitleKeyBlock` из `ExternalKeySet` / `title.keys` через `TitleKek` (`DecryptTitleKeyWithKeyset`).
  - Устранена двойная XTS-дешифровка заголовка в `StormNczStorage.cs`, исправлена формула смещения бакетов BKTR в `StormNczCompressor.cs`, обновлён `Keys.py` для работы без новейших необязательных ревизий `master_key_XX`, а также добавлена автоматическая проверка целостности распакованного контейнера `ValidateDecompressedContainer` (`PFS0`, отсутствие остаточных `.ncz`, проверка `Nca.Header.NcaSize` с порогом `0xC00` для малых `.cnmt.nca`) и явная ошибка вместо молчаливого копирования `.nsz`.
- 🌟 **[Исправление отображения 4-сегментных версий игр и быстрая загрузка файлов RuTracker (`TitleDbService.cs`, `RuTrackerModService.cs`, `RuTrackerCatalogUpdateService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, Android)]**:
  - Поддержаны 4-компонентные версии до 6 цифр в номере сборки (например, *Dave the Diver* `v1.0.6.1267` -> `1.0.6.1267`), фильтр дат `2015..2099` ограничен 3-сегментными строками, исправлен парсинг `Версия v...` без двоеточия и добавлена синхронизация cookie WebView2 для `.rutracker.org` и `.rutracker.net`.
  - В `LoadSelectedTopicAsync` загрузка списка файлов `.torrent` и фонового P2P `ResolveMagnetMetadataBackgroundAsync` отделена от ожидания HTML/прокси темы, опрос комментариев ограничен последними 2 страницами (`SemaphoreSlim(2)`), а немодовые информационные ссылки `GitHub`/`Telegraph`/`Google Docs` отфильтровываются без ключевых слов модификаций.
- 🌟 **[Синхронизированное обновление Windows и Android 8.1.2]**:
  - Версия обновлена до `8.1.2` (`versionCode = 8102` на Android) для всех сборок Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Models/AppSettings.cs, Views/Dialogs/RuTrackerModDownloaderDialog.xaml(.cs), Views/SettingsPage.xaml(.cs), Services/RuTrackerDownloadQueueService.cs]**: Добавлена настройка `RuTrackerMaxConcurrentDownloads` (1..10), динамический FIFO-шлюз параллельных сборок `AcquireBuildSlotAsync`/`ReleaseBuildSlot` (1..8) вместо жёсткого `SemaphoreSlim(1, 1)`, селекторы одновременных скачиваний и сборок на панели очереди RuTracker, повышение потолка `ConcurrentTasks` до 8 в параметрах и живая синхронизация.
- 🔹 **[Services/NszCompressionService.cs, MultiContentService.cs, Z3dsService.cs, Core/NSZ/StormNczCompressor.cs]**: Добавлен `GetOptimalCompressionThreads()`, удалён жёсткий лимит `ProcessorCount / 4`, флаг `-L` оставлен только для Solid NSZ, добавлены `InspectPfsNczPassthrough` и селективный `FastPassthrough` Block NSZ в `MultiContentService`.
- 🔹 **[tools/nsz/Lib/site-packages/nsz/Decompressor.py, Keys.py]**: Добавлен `_normalize_sections_for_ncz`, расчёт размера по `NCZBLOCK.decompressedSize`, поддержка `cryptoType == 5` и устойчивый `Keys.load`.
- 🔹 **[Core/NSZ/StormNczStorage.cs, StormNczCompressor.cs, Services/NszCompressionService.cs, ViewModels/TasksViewModel.cs]**: Устранена двойная XTS-дешифровка, добавлена сортировка секций с gap-заполнением, поддержка `CryptoType == 5`, фикс формулы бакетов BKTR, `DecryptTitleKeyWithKeyset` и валидация `ValidateDecompressedContainer`.
- 🔹 **[Services/TitleDbService.cs, RuTrackerModService.cs, RuTrackerCatalogUpdateService.cs, Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Поддержка 4-сегментных версий и очистки `v.`, кросс-доменные WebView2-cookie, развязка загрузки файлов от HTML темы, ограничение комментариев до 2 страниц и фильтр немодовых ссылок.
- 🔹 **[android/.../MainActivity.kt, RuTrackerCloudEngine.kt, StormNczEngine.kt, build.gradle.kts]**: Обновлены регулярные выражения 4-сегментных версий и версия приложения до `8.1.2` (`versionCode = 8102`).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Accelerated container builds and compression: replaced the hardcoded 1-build RuTracker gate with a dynamic FIFO gate (up to 8 parallel builds), replaced the 25% CPU cap (`ProcessorCount / 4`) with `GetOptimalCompressionThreads()`, dropped redundant `--long` allocation on 1 MB Block NSZ, and added zero-recompress Block NSZ `FastPassthrough`.
- 🔸 Added live simultaneous download (`1..10`) and build (`1..8`) selectors directly to the RuTracker downloader toolbar, synced two-way with Settings (raised from 5 to 8).
- 🔸 Fixed Block NSZ to NSP decompression corruption (affecting DLC `firstSection = 0xC00` NCAs, BKTR updates, `CryptoType == 5`, gap sections, double-XTS header decryption, and encrypted `TitleKeyBlock` lookup) plus added `ValidateDecompressedContainer`.
- 🔸 Fixed 4-part game version parsing (`v1.0.6.1267` -> `1.0.6.1267`), cross-domain WebView2 cookies, decoupled `.torrent`/magnet loading, comment-page throttling, and generic link filtering.
- 🔸 Updated unified Windows x64 and Android Modern/Legacy releases to version `8.1.2` (`versionCode 8102`).

</details>

## **8.1.1** (23.11.2026)

> 💡 **Релиз 8.1.1** - *Исправление отображения версий игр в загрузчике RuTracker (устранение индикатора `"?"` вместо реальной версии патча, поддержка 3-компонентных названий STORM, автообновление версии при правке названия слота и точный онлайн-резерв через `stormgamesworld.ru`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.1)

- 🌟 **[Исправление отображения версии игр в загрузчике RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена причина отображения `"?"` вместо версии игры при устаревании суточного TTL в локальном кэше `rutracker_topic_parsed_v2.json` и временной недоступности HTML темы RuTracker (например, *Golden Force* `1.1.6`): добавлена мгновенная предгидратация `TryHydrateTopicFromParsedCache`, сохранение кэшированных версий при сбое или пустом парсинге HTML и обязательный пересчёт метаданных после завершения загрузки темы независимо от текущего статуса `"..."` или `"?"`.
  - В `FetchValidatedTopicPageHtmlAsync` увеличено окно быстрой гонки до `5.0` секунд с параллельным опросом основного и резервного зеркал (`rutracker.org` / `rutracker.net`), а в `ExtractGameVersionsFromTopicHtml` расширена граница разделителей для строк вида `1.1.6 - 196608` и `1.1.6-196608`.
  - Исправлены регулярные выражения в `ExtractFilePatchCode` и `ScanVerTextSource` для 3-компонентного формата STORM `(версия - код_патча - TitleID)` с проверкой совпадения кода патча, добавлен автопересчёт версии при редактировании названия слота `GameTitle` и защита `CopySlotVersion_Click` от копирования заглушек `"?"`/`"..."`.
- 🌟 **[Точный онлайн-резерв версий через API `stormgamesworld.ru` (`TitleDbService.cs`)]**:
  - Добавлен метод `TryResolveDisplayVersionOnlineAsync`, который при отсутствии версии в локальных источниках разово за сессию запрашивает `https://stormgamesworld.ru/api/games` и принимает версию строго при точном совпадении одновременно базового `TitleID` (16 hex) и целочисленного кода патча, без эвристических подстановок.
- 🌟 **[Синхронизированная сборка Windows и Android 8.1.1]**:
  - Версия обновлена до `8.1.1` (`versionCode = 8101` на Android) для всех сборок Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлен метод `TryHydrateTopicFromParsedCache(topic)` и его вызов в `PrimeQuickTopicState`; в `FetchTopicDetailsAsync` добавлено сохранение кэшированных версий/постера/хэша/ссылок при просроченном TTL или недоступном HTML темы; расширена регулярка `ExtractGameVersionsFromTopicHtml` на дефис/двоеточие; `FetchValidatedTopicPageHtmlAsync` теперь гоняет оба зеркала (`rutracker.org` и `rutracker.net`) с таймаутом `5.0` сек.
- 🔹 **[Services/TitleDbService.cs]**: Добавлен дедуплицированный онлайн-резерв `TryResolveDisplayVersionOnlineAsync` по API `stormgamesworld.ru/api/games` со строгим совпадением `baseTid` и `patchCode`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Добавлен предпрогрев кэша темы в `LoadSelectedTopicAsync`, синхронизация версий в фоновом обратном вызове `FetchTopicCommentLinksAsync`, обязательный пересчёт в блоке `finally`, триггер `OnGameSlotPropertyChanged` на `GameTitle` с защитой от рекурсии `_isEnrichingSlots`, 3-компонентная поддержка `(dv - iv - tid)`, фоновый онлайн-резерв для слотов со статусом `"?"` и защита `CopySlotVersion_Click`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed the `"?"` game version regression in the RuTracker Downloader where expired topic cache entries (>24h) discarded `ParsedGameVersion` when live RuTracker HTML was unreachable.
- 🔸 Added `TryHydrateTopicFromParsedCache`, mirror-pair parallel HTTP race (`rutracker.org` + `rutracker.net`, 5.0s window), 3-part STORM title `(ver - patchCode - TitleID)` regex parsing, `GameTitle` edit re-enrichment, and a strict exact-`(TitleID, patchCode)` online fallback via `stormgamesworld.ru/api/games`.
- 🔸 Unified build and release update to version `8.1.1` (`versionCode 8101` on Android) across Windows x64 and Android Modern/Legacy.

</details>

## **8.1.0** (23.11.2026)

> 💡 **Релиз 8.1.0** - *Исправление офлайн-индекса и поиска RuTracker #1605 на Android (восстановление всех 7 301 раздачи Switch), устранение задержки 5-8 сек на каждый символ при вводе и единая синхронизированная сборка Windows + Android (`versionCode 8100`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.0)

- 🌟 **[Восстановление поиска игр RuTracker #1605 на Android (`RuTrackerSwitchIndex.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Устранена причина, по которой с версии `8.0.0` на Android не находились игры из локального индекса (например, *Mandragora*): при сборке APK `aapt2` автоматически распаковывает `.gz` и сохраняет ассет как `assets/rutracker_switch_index.json`, а код открывал только `.json.gz` и требовал GZIP-поток. Добавлены приоритетное открытие `ASSET_NAME_PLAIN`, автоопределение GZIP по байтам `0x1F 0x8B` (`maybeGunzip`) и потоковый беззависимостный JSON-парсер `IndexJsonReader` для всех `7 301` раздачи Switch.
  - Исправлен фильтр `isStrictSwitchRelease` (разделение ПК-репаков, проверка границ слов и явный приоритет тегов `[Nintendo Switch]` / `NSP`/`NSZ`/`XCI`/`XCZ`/`NSW`/`[NS]`): разблокирована 51 реальная раздача Switch (*Skyrim Special Edition*, *Overcooked Special Edition*, *Deltarune*, *Persona 3 Portable*, *Rune Factory 3/4/5* и др.), ранее скрывавшаяся подстроками `cia`/`rune`/`portable`/`codex`.
  - В `searchTopics` реальный индекс `#1605` получил приоритет перед живыми дозапросами и синтетическим каталогом `TDB-`, добавлена подстановка живых сидов и названий `tt` в `mapRealIndexTopicToItem`, а также исправлено регулярное выражение `linkRegex` в парсере Atom-ленты.
- 🌟 **[Мгновенный отклик ввода в поиске Android без фризов (`StormNczEngine.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Устранена задержка `5-8` секунд на каждую вводимую букву: в `extractCleanGameTitle` предкомпилированы все 6 `Regex`, в `RuTrackerCloudEngine` добавлены единожды подготавливаемый запрос `PreparedFuzzyQuery` и преднормализованные кэши `curatedSearchKeys`, `realIndexSearchCache` и `titleDbSearchCache` с фоновым прогревом при запуске (`initAndroidContext`).
  - `resolveAuthenticBaseTitleId` переведена с линейного перебора `11 380+` игр TitleDB на UI-потоке на `O(1)`-поиск `StormNczEngine.lookupSwitchTitleIdFast` с кэшированием (`resolvedTitleIdCache`, `60` карточек за `1` мс), а короткие префиксы `1-4` символов (`"m"`, `"ma"`, `"man"`, `"mand"`) теперь сразу находят совпадения без пустых пауз.
  - Все поля поиска (RuTracker, каталог TitleDB и фильтр дерева RomFS) перенесены на фоновый `uiSearchExecutor` с отменой устаревших запросов по счётчикам и debounce (`120` мс локально, `450` мс сетевой дозапрос, `100` мс для дерева RomFS).
- 🌟 **[Единое обновление версии до 8.1.0 для Windows и Android]**:
  - Все сборки для Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`, `versionCode = 8100`) обновлены, пересобраны и подписаны сертификатами `STORM TEAM` / `STORM SERTIFICATE`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerSwitchIndex.kt]**: Добавлена константа `ASSET_NAME_PLAIN = "rutracker_switch_index.json"`, функция `maybeGunzip` с автоопределением `0x1F 0x8B`, потоковый парсер `IndexJsonReader` без `org.json` и помощник `clearLoadedStateForTesting`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/StormNczEngine.kt]**: Предкомпиляция всех 6 регулярных выражений `extractCleanGameTitle`, потокобезопасная загрузка `loadFullTitleDbFromAssets`, общий `parseTitleDbTsvStream` и `O(1)` индекс `buildSwitchTitleIdLookup` / `lookupSwitchTitleIdFast`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt]**: `initAndroidContext` с фоновым прогревом обоих поисковых кэшей, `PreparedFuzzyQuery` + `NormalizedSearchKeys` (`realIndexSearchCache`, `titleDbSearchCache`, `curatedSearchKeys`), поддержка префиксов `1-4` символов в `scorePreparedQuery` и `isWordFuzzyMatch`, `O(1)` `resolveAuthenticBaseTitleId`, точный `isStrictSwitchRelease` и приоритет реального индекса `#1605` в `searchTopics`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/MainActivity.kt]**: Фоновый однопоточный `uiSearchExecutor` (с очисткой в `onDestroy`), предвычисление метаданных карточек вне UI-потока, debounce-поиск для RuTracker (`120`/`450` мс), каталога TitleDB (`120` мс) и фильтра RomFS (`100` мс).
- 🔹 **[android/app/src/test/java/com/storm/switchbox/StormBinaryAndLocTest.kt]**: Добавлены регрессионные и производительные тесты `verifyRuTrackerIndexLoadsPlainAndGzipAndSearchesMandragora` и `verifyAndroidSearchKeystrokeLatencyIsInstant` (`9/9` тестов пройдены, 10 последовательных символов `"mandragora"` суммарно за `439` мс, `60x TitleID` за `1` мс).

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.1.0)

- 🔸 **[Restore RuTracker #1605 Search on Android (`RuTrackerSwitchIndex.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Fixed the Android search regression introduced in `8.0.0` where offline Switch index games (such as *Mandragora*) disappeared because `aapt2` unpacks `rutracker_switch_index.json.gz` into plain `assets/rutracker_switch_index.json` inside APKs. Added plain-first asset opening, `maybeGunzip` magic-byte detection (`0x1F 0x8B`), and a zero-dependency streaming `IndexJsonReader` covering all `7,301` indexed Switch topics.
  - Replaced substring traps in `isStrictSwitchRelease` with word-boundary platform checks and explicit Switch/container priority (`[Nintendo Switch]`, `NSP`, `NSZ`, `XCI`, `XCZ`, `NSW`, `[NS]`), restoring 51 valid `#1605` titles including *Skyrim Special Edition*, *Deltarune*, *Persona 3 Portable*, and *Rune Factory 3/4/5*.
  - Ordered `curatedMatches` and `realIndexMatches` ahead of network scrapes and `TDB-` fallbacks in `searchTopics`, merged live tracker metadata via `mapRealIndexTopicToItem`, and fixed the Atom feed `linkRegex` trailing-space bug.
- 🔸 **[Eliminate 5-8s Per-Keystroke Input Freeze on Android (`StormNczEngine.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Precompiled all 6 regexes in `extractCleanGameTitle`, introduced once-per-search `PreparedFuzzyQuery` with pre-normalized `realIndexSearchCache`, `curatedSearchKeys`, and `titleDbSearchCache` warmed up in the background via `initAndroidContext`.
  - Replaced per-card linear TitleDB scans in `resolveAuthenticBaseTitleId` with `O(1)` `lookupSwitchTitleIdFast` plus `resolvedTitleIdCache` (`60` TitleIDs resolved in `1` ms), enabled smooth `1-4` character prefix matching (`"m"` -> `"ma"` -> `"man"` -> `"mandragora"`), and moved all search filtering onto `uiSearchExecutor` with cancellation sequence guards and debouncing (`120` ms local / `450` ms network / `100` ms RomFS).
- 🔸 **[Unified Windows and Android Release (`8.1.0` / `versionCode 8100`)]**:
  - Bumped version across all Windows and Android targets (`versionCode = 8100`), verified `9/9` Android unit tests and benchmarks, and rebuilt signed Windows Setup/Portable + Android Modern/Legacy packages.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.1.0_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.1.0.zip` / `STORM_SWITCH_BOX_8.1.0_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.1.0.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.1.0_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.1.0_Setup.exe`** (219.75 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.1.0.zip`** / **`_win-x64.zip`** (151.30 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.1.0.apk`** (14.45 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.1.0_LEGACY.apk`** (26.02 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.1.0_Setup.exe** (219.75 МБ)
  - `SHA-256: 24171797C060203219D773EE8E41D2DD6923C6B74E82F6EB9B3AC36654CBB616`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.1.0_win-x64.zip** / **STORM_SWITCH_BOX_8.1.0.zip** (151.30 МБ)
  - `SHA-256: E0F1794A3ADB56756447B775296619E69B1B639224C8BE4D9CD212B8CB13F75E`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.1.0.apk** (14.45 МБ)
  - `SHA-256: 20D2FE874DD9F9D7B6F27B53CD371E71707427A52400BB982B902245FF79B263`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.1.0_LEGACY.apk** (26.02 МБ)
  - `SHA-256: AEC3C996CF7713E634EBAA6A2111BCC0D74CF8739E828FF81DEE3399E8A49F53`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.13** (22.11.2026)

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

---

## **8.0.12** (22.11.2026)

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

---

## **8.0.11** (22.11.2026)

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

---

## **8.0.10** (22.11.2026)

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

---

## **8.0.9** (22.11.2026)

> 💡 **Релиз 8.0.9** - *Точная классификация DLC с обновлениями (`[v65536]` / `[v393216]`), выделенных папок `7 DLC/` и автовыбор всех дополнений по умолчанию*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.9)

- 🌟 **[Точная классификация DLC с ненулевыми тегами версий (`RuTrackerModService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Устранена ошибка, из-за которой DLC-пакеты с тегами версий (`[v65536]`, `[v131072]`, `[v393216]`) и даже явным тегом `[DLC ...]` (например, 5 из 7 дополнений в раздаче `Xenoblade Chronicles 2 + Torna`) ошибочно классифицировались как `ОБНОВЛЕНИЕ`, а затем отключались по умолчанию дедупликатором обновлений как «устаревшие патчи».
  - Добавлена функция `ExtractRawTitleId` (сохраняющая точный суффикс `800`/`000`/`001+` без подмены `800 -> 000` для Switch, Switch 2 `04...` и 3DS `0004008C`/`0004000E`), а также поддержка контейнеров `.3dsx` и Azahar Z3DS (`.zcci`, `.zcxi`, `.zcia`, `.z3dsx`) в классификаторе раздач.
  - Выстроен строгий приоритет `ClassifyTorrentEntryBadge`: `DLC TitleID` (Switch/Switch 2 отличные от `000`/`800`, 3DS `0004008C...`), теги `[DLC ...]` / `[Standalone DLC ...]`, выделенные подкаталоги DLC (`7 DLC/`, `DLCs/`, `Дополнения/`) и имена DLC теперь **всегда проверяются раньше** эвристики версий `[v1-9...]`.
- 🌟 **[Автовыбор 100% оригинальных DLC и симметричное исправление для Android]**:
  - В `PopulateDisplayPathsAndSmartSelection` все уникальные DLC по умолчанию остаются включёнными (`IsSelected = true`); автоотключение дублей применяется строго при совпадении полного 16-символьного `ExtractRawTitleId` (старая и новая версии одного и того же DLC).
  - В движке Android `RuTrackerCloudEngine.torrentFileRole` внедрён аналогичный приоритет: поддержка TitleID `01...`, `04...`, `0004...`, папок `7 DLC/`, явных тегов `[DLC ...]` до проверки `[v1-9...]`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлена функция `ExtractRawTitleId`; обновлены `ClassifyTorrentEntryBadge`, `BinaryContainerExtensions` и `PopulateDisplayPathsAndSmartSelection` для безошибочного распознавания DLC с тегами `[v65536]`+ и дедупликации только полных 16-символьных дубликатов DLC TID.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt]**: Обновлён метод `torrentFileRole` с приоритетной проверкой DLC (теги, подпапки, Switch/Switch 2/3DS TitleID) до эвристики обновлений `[v1-9...]`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.9)

- 🔸 **[Accurate Classification of Updated `[v>0]` DLC Packages (`RuTrackerModService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Fixed a bug where updated DLCs carrying `[v65536]`, `[v131072]`, or `[v393216]` version tags (even with `[DLC ...]` in the filename or placed inside `7 DLC/`, such as in `Xenoblade Chronicles 2 + Torna`) were misclassified as `ОБНОВЛЕНИЕ` and subsequently unchecked as duplicate patches by `PopulateDisplayPathsAndSmartSelection`.
  - Added `ExtractRawTitleId` (preserving Switch `01...`, Switch 2 `04...`, and 3DS `0004...` suffixes without `800 -> 000` rewriting) and Z3DS/3DSX extensions; enforced strict priority checking DLC TitleIDs, `[DLC ...]` / `[Standalone DLC ...]` tags, and dedicated `DLC` folders before version-based `[v1-9...]` update heuristics on both Windows and Android.
- 🔸 **[All Distinct DLCs Enabled by Default (`PopulateDisplayPathsAndSmartSelection`)]**:
  - Every distinct DLC now stays checked (`IsSelected = true`) by default, deduplicating only exact matching 16-character DLC TitleIDs when both older and newer revisions exist.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.9_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.9.zip` / `STORM_SWITCH_BOX_8.0.9_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.9.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.9_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.9_Setup.exe`** (219.68 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.9.zip`** / **`_win-x64.zip`** (151.24 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.9.apk`** (14.40 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.9_LEGACY.apk`** (25.97 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.9_Setup.exe** (219.68 МБ)
  - `SHA-256: 29C7B96561D88E67A632A44BAB21254ADF1614884A64FB3A3B4F4FCE375D8261`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.9_win-x64.zip** / **STORM_SWITCH_BOX_8.0.9.zip** (151.24 МБ)
  - `SHA-256: 7880DE95FA26BC263D180A63761E302F84FC50579B529F7ADE2EABDEEA29AB90`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.9.apk** (14.40 МБ)
  - `SHA-256: 03BE487A008E22BAEEB3084EC76FE3DACF34E12B93E75CABF096FD0BD5D24DCD`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.9_LEGACY.apk** (25.97 МБ)
  - `SHA-256: 9180CBBCEAC7771281615190125A51C2FFFE154E36FEA5E822750D2251AB18E8`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.8** (21.11.2026)

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

---

## **8.0.7** (21.11.2026)

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

---

## **8.0.6** (21.11.2026)

> 💡 **Релиз 8.0.6** - *Умный автовыбор только оригинальных файлов раздачи, структурированная группировка по категориям и чистое копирование названий дополнений (DLC)*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.6)

- 🌟 **[Отключение модов, русификаторов и сэйвов по умолчанию и точная версия патча (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена ошибка автовыбора, из-за которой русификаторы, сторонние модификации, привязанные к ним старые обновления (`ОБНОВЛЕНИЕ (РУС)`), сохранения и текстовые заметки выбирались автоматически и подменяли итоговую версию патча в шапке слота игры (например, в раздаче `Xenoblade Chronicles X Definitive Edition` теперь автоматически выбирается актуальный официальный патч `131072` вместо устаревшего `65536` от русификатора).
  - По умолчанию в списке файлов раздачи автоматически выбираются только оригинальные файлы игры (`ИГРА`), единственное новейшее официальное обновление (`ОБНОВЛЕНИЕ`), оригинальные дополнения (`ДОПОЛНЕНИЯ`) и `UNLOCKER`. Категории `МОДЫ`, `МОД / РУСИФИКАТОР`, `ОБНОВЛЕНИЕ (РУС)`, `СЭЙВЫ`, `ИНФО`, `ФАЙЛ` и устаревшие патчи сняты по умолчанию.
- 🌟 **[Структурированная группировка файлов в раздаче по категориям (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - В блоке «Файлы в раздаче» внутри каждого слота игры файлы сгруппированы в строгом порядке приоритета: `ИГРА` -> `ОБНОВЛЕНИЕ` -> `ДОПОЛНЕНИЯ` -> `UNLOCKER` -> `МОДЫ` -> `ОБНОВЛЕНИЕ (РУС)` -> `СЭЙВЫ` -> `ИНФО`.
  - Для каждой группы добавлены цветной бейдж категории, счётчик выбранных файлов, суммарный объём и кнопка массового переключения галочек категории одним кликом.
- 🌟 **[Чистое копирование названий дополнений (DLC) по стандарту STORM GAMES WORLD (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `CatalogPage.xaml`, `CatalogPage.xaml.cs`)]**:
  - Реализована функция `ExtractCleanDlcName`, очищающая названия дополнений от имени игры, тега `DLC`, `[TitleID]`, версии `[v0]` и расширения файла (например, `Xenoblade Chronicles X Definitive Edition [DLC Early Purchase Bonus Exploration Support Pack] [0100453019AA9001][v0].nsp` -> `Early Purchase Bonus Exploration Support Pack`).
  - Добавлена кнопка копирования названия рядом с каждым DLC и кнопка массового копирования списка «Копировать все DLC» как в загрузчике RuTracker, так и во всплывающем списке дополнений в разделе «Информация».

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `IsSaveEntryPath`, `IsUnlockerEntryPath`, `GetCategoryGroupKey`, `GetCategoryGroupColors`, `RuTrackerFileCategoryGroup` и `ExtractCleanDlcName`; обновлены `ClassifyTorrentEntryBadge`, `GetRoleSortOrder`, `CollapseModFoldersIntoSingleEntries` и `PopulateDisplayPathsAndSmartSelection` с отказом от автовыбора русификаторских патчей и модов; расширен `TorrentFileSelectionItem` полями `IsDlcItem`, `CleanDlcName`, `CopyDlcButtonVisibility` и `CopyDlcTooltip`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml, Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Список файлов переведён на двухуровневую группировку по `CategoryGroups`, добавлены обработчики `ToggleCategoryGroupSelection_Click`, `CopyDlcName_Click`, `CopyAllDlcNamesInGroup_Click`, однострочное уведомление при копировании многострочного списка и фильтрация категорий `МОДЫ` / `МОД / РУСИФИКАТОР`.
- 🔹 **[Views/CatalogPage.xaml, Views/CatalogPage.xaml.cs]**: Во всплывающее окно «Список дополнений» в разделе «Информация» добавлены кнопки копирования чистого названия каждого DLC (`CopyCatalogDlcName_Click`) и пакетного копирования всех DLC (`CopyAllCatalogDlcNames_Click`).
- 🔹 **[Services/LocalizationService.UiTree.cs]**: Добавлены и переведены на 10 языков 5 новых строк интерфейса для группировки категорий и копирования названий дополнений.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.6)

- 🔸 **[Original-Only Default Torrent File Selection and Accurate Selected Patch Version (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Fixed smart default file selection in `Torrent Files`: localizations, mods (`МОДЫ`, `МОД / РУСИФИКАТОР`, `ОБНОВЛЕНИЕ (РУС)`), saves (`СЭЙВЫ`), info files (`ИНФО`), and older patches are now unchecked by default.
  - Only original base game files (`ИГРА`), the single newest official update (`ОБНОВЛЕНИЕ`, such as `v131072` instead of an older rus-paired `v65536` in `Xenoblade Chronicles X Definitive Edition`), original DLCs (`ДОПОЛНЕНИЯ`), and `UNLOCKER` are selected automatically, resolving the mismatched header patch version.
- 🔸 **[Category-Grouped Torrent File Tree (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Files inside every game slot are now organized in strict category order: `ИГРА` -> `ОБНОВЛЕНИЕ` -> `ДОПОЛНЕНИЯ` -> `UNLOCKER` -> `МОДЫ` -> `ОБНОВЛЕНИЕ (РУС)` -> `СЭЙВЫ` -> `ИНФО`, complete with per-category toggle headers, selection counts, and size summaries.
- 🔸 **[Clean DLC Name Copy Matching STORM GAMES WORLD Rules (`RuTrackerModService.cs`, `CatalogPage.xaml`, `CatalogPage.xaml.cs`, `LocalizationService.UiTree.cs`)]**:
  - Added `ExtractCleanDlcName` to strip game prefixes, `[DLC ...]`, TitleID brackets, `[v0]`, and extensions (e.g., yielding `Early Purchase Bonus Exploration Support Pack`) with single-DLC copy and batch Copy All DLC buttons in both the RuTracker downloader and Information catalog DLC flyout, plus 10-language UI translations.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.6_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.6.zip` / `STORM_SWITCH_BOX_8.0.6_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.6.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.6_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.6_Setup.exe`** (219.65 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.6.zip`** / **`_win-x64.zip`** (151.21 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.6.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.6_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.6_Setup.exe** (219.65 МБ)
  - `SHA-256: AB9699D991B03CFA8D1AA6E16E0725614522B6479EA1A7191659A3897DE0F706`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.6_win-x64.zip** / **STORM_SWITCH_BOX_8.0.6.zip** (151.21 МБ)
  - `SHA-256: 5AC6728C33FD9131E1B71AAA2994BE80D6099F0D01B1836C96FA94C698D46B0D`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.6.apk** (14.38 МБ)
  - `SHA-256: 6E870F725A90BAADB3C0BA74C8DF5FFA5C2636A48D6491B6DBE486153C4C7182`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.6_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 766255867D904FEA8D54E738C1E63BA6E03B277C8C79D43873147DD1FE2A755A`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.5** (21.11.2026)

> 💡 **Релиз 8.0.5** - *Точное определение версий игр без ложных подстановок, исключение Standalone-дополнений и полноценный мониторинг обновлений RuTracker в «Информации»*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.5)

- 🌟 **[Точные версии игр и защита базового TitleID в загрузчике RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TitleDbService.cs`, `MultiContentService.cs`)]**:
  - Устранена выдуманная подстановка версий вида `1.{major}.0` (например, `1.3.0` вместо реальной `1.1.6` у `Golden Force` с патчем `v196608`). Теперь семантическая версия извлекается по строгой цепочке: имена файлов -> кэш наблюдённых NACP и TitleDB (`known_display_versions.json`) -> HTML шапка темы RuTracker (с отсечением версий прошивки, перевода, дат и размеров) -> заголовок/слот -> статус `?` без искажения номера при отсутствии точной строки.
  - Реализована фильтрация `IsStandaloneBonusFile` и строгий приоритет выбора базового TitleID: бонусные Standalone-приложения вроде цифровых артбуков и саундтреков (например, `01005F4027E94000` у `Castlevania: Belmont's Curse`) больше не перехватывают базовый TitleID основной игры (`0100BA4026E94000`) и не попадают в счётчик её DLC (`2` настоящих DLC вместо `3`).
- 🌟 **[Динамическое обновление списка игр и отключение рекурсии по умолчанию в «Информации» (`CatalogPage.xaml`, `CatalogPage.xaml.cs`, `CatalogScannerService.cs`, `AppSettings.cs`)]**:
  - После добавления или удаления папок сканирование и обновление карточек в сетке игр происходят мгновенно и динамически, без необходимости переключать вкладки меню (устранена отвязка `ItemsSource` после фильтра/поиска).
  - Рекурсивное сканирование подпапок (`SearchOption.AllDirectories`) отключено по умолчанию. При добавлении папки добавлен чекбокс «Сканировать подпапки (рекурсивно)» (снят по умолчанию) с сохранением настройки для каждой папки (`CatalogRecursiveFolders`).
- 🌟 **[Мониторинг новых версий и дополнений (DLC) по RuTracker в «Информации» (`RuTrackerCatalogUpdateService.cs`, `CatalogItem.cs`, `CatalogPage.xaml`)]**:
  - В «Информацию» добавлена автоматическая и ручная проверка обновлений версий и новых дополнений по базе RuTracker (кнопка «Проверить обновления (RuTracker)», фильтр «Есть обновления (N)», зелёные кликабельные бейджи на карточках и баннер в окне деталей с переходом в 1 клик к раздаче).
  - Сканер каталога `CatalogScannerService` теперь извлекает фактические локальные DLC из заголовков NCA / `.tik` / `.cnmt` (`LocalDlcIds`, `LocalDlcCount`) и сопоставляет их с раздачами RuTracker по базовому TitleID (исключая Standalone-бонусы): например, для локальной `Castlevania: Belmont's Curse` (`1.1.0` + `1` DLC) корректно фиксируется обновление на RuTracker (`1.2.0` + `2` DLC).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `ParsedGameVersion`, `ParsedTitleVersions` (включая `TopicParsedCacheEntry.SchemaVersion = 2`), `ExtractGameVersionsFromTopicHtml`, `IsStandaloneBonusFile`, `IsDlcOfBaseTitle`; улучшен `DisplayVersion`/`ToMetadataEditModel` и группировка слотов без перехвата Standalone-бонусами.
- 🔹 **[Services/TitleDbService.cs]**: Добавлены `IsValidSemanticVersion`, `CompareSemanticVersions` и постоянный кэш наблюдённых NACP-версий `known_display_versions.json` (`RecordObservedDisplayVersion`, `TryResolveDisplayVersion`).
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Проброшена версия из HTML темы, удалена эвристика `1.{major}.0`, исправлен выбор базового TitleID при наличии Standalone-приложений.
- 🔹 **[Services/RuTrackerCatalogUpdateService.cs]**: Создан сервис пакетного сопоставления локальных игр Switch / Switch 2 с раздачами RuTracker по базовому TitleID (обновление индекса, `FetchTopicDetailsAsync`, `InspectTorrentFilesAsync`, сравнение `VersionCode`/семантической версии и не-Standalone DLC).
- 🔹 **[Services/CatalogScannerService.cs, Models/AppSettings.cs, Models/CatalogItem.cs]**: Добавлено нерекурсивное сканирование по умолчанию, извлечение `LocalDlcIds`/`LocalDlcCount`, агрегирование соседних DLC-файлов, сохранение настроек подпапок по пути (`CatalogRecursiveFolders`) и свойства статуса обновлений RuTracker.
- 🔹 **[Views/CatalogPage.xaml, Views/CatalogPage.xaml.cs, Services/LocalizationService.UiTree.cs]**: Добавлены чекбокс «Сканировать подпапки (рекурсивно)», динамическое добавление/удаление папок, панель мониторинга обновлений RuTracker и переводы всех новых строк интерфейса на 10 языков.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.5)

- 🔸 **[Accurate Game Display Versions & Standalone DLC Isolation (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TitleDbService.cs`, `MultiContentService.cs`)]**:
  - Eliminated the synthetic `1.{major}.0` version fallback that fabricated `1.3.0` for games like `Golden Force` (`v196608`, real version `1.1.6`). Display versions now resolve cleanly via filename tags -> persisted NACP/TitleDB cache (`known_display_versions.json`) -> RuTracker topic post HTML header -> topic/slot title -> `?` when genuinely unknown.
  - Added `IsStandaloneBonusFile` filtering and strict Base TitleID prioritization so Standalone bonus apps (soundtracks/artbooks like `01005F4027E94000` in `Castlevania: Belmont's Curse`) never overwrite the main game's Base TitleID (`0100BA4026E94000`) or inflate genuine DLC counts (`2` instead of `3`).
- 🔸 **[Dynamic Catalog Folder Refresh & Non-Recursive Default Scanning (`CatalogPage.xaml`, `CatalogPage.xaml.cs`, `CatalogScannerService.cs`, `AppSettings.cs`)]**:
  - Adding or removing folders in the Information tab now dynamically scans/prunes games immediately without switching tabs or breaking search filtering.
  - Recursive scanning (`SearchOption.AllDirectories`) is disabled by default; an unchecked-by-default `"Scan subfolders (recursively)"` checkbox was added when adding a folder (`CatalogRecursiveFolders`).
- 🔸 **[RuTracker Version & New DLC Update Monitor in Information Tab (`RuTrackerCatalogUpdateService.cs`, `CatalogItem.cs`, `CatalogPage.xaml`)]**:
  - Added automatic and manual RuTracker update & new DLC checking directly inside Information (`Check updates (RuTracker)`, `Updates available (N)` filter chip, clickable green card badges, and detail-overlay banner opening the exact RuTracker topic in 1 click).
  - `CatalogScannerService` extracts local DLC IDs/counts from container NCAs, `.tik`, and `.cnmt` headers and accurately flags both version bumps and new non-Standalone DLCs (e.g., `Castlevania: Belmont's Curse` `1.1.0 + 1 DLC` -> RuTracker `1.2.0 + 2 DLC`).

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.5_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.5.zip` / `STORM_SWITCH_BOX_8.0.5_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.5.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.5_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.5_Setup.exe`** (219.65 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.5.zip`** / **`_win-x64.zip`** (151.20 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.5.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.5_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.5_Setup.exe** (219.65 МБ)
  - `SHA-256: 21B5540180B614C3C427DB558827D8C67BE6CC860830FA284E36D6C59D5FDDB3`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.5_win-x64.zip** / **STORM_SWITCH_BOX_8.0.5.zip** (151.20 МБ)
  - `SHA-256: D229DE288644A018713F2AA1496C63AF554D358D4E89E07696FC2FA4F8DA5B54`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.5.apk** (14.38 МБ)
  - `SHA-256: 1D24A2B75EF7936A2F2445B339CBD0271FE0B3A37D82D517B875F4009859F839`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.5_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 4059A75062F5E38C1859199C0D8456A57EFA8DA9B21901E25817119BC3DF0C56`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.4** (21.11.2026)

> 💡 **Релиз 8.0.4** - *Мгновенное отображение реального списка файлов для всех раздач RuTracker и максимальный разгон нативного движка скачивания C++ libtorrent*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.4)

- 🌟 **[Мгновенное получение списка файлов раздач для всех игр (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена причина срыва загрузки `.torrent` из кэш-зеркал: `itorrents.org` перешёл на `http://itorrents.net` через редирект `HTTP 301` (`HTTPS -> HTTP`), который `.NET SocketsHttpHandler` отклонял по умолчанию за `113 мс`. Основным зеркалом подключён `https://itorrents.net/` (`HTTP 200` за `80-250 мс`) и добавлен обработчик межсхемных редиректов `SendWithCrossSchemeRedirectAsync`.
  - Глобальная 60-секундная блокировка зеркал `_cacheMirrorsCooldownUntilUnix` заменена на точечный кэш промахов по конкретному `InfoHash` (`_missedMirrorHashes`, TTL `45 с`), чтобы редкие/новые раздачи не отключали быстрый кэш для остальных игр.
  - Префетч `PrefetchTopicsMetadata` расширен до 6 раздач (`_prefetchThrottle = 4`), а для тем без готового `.torrent` после быстрого окна запускается неблокирующая фоновая P2P-догрузка списка файлов `ResolveMagnetMetadataBackgroundAsync` на `22 с` без зависания UI на заглушке `MAGNET`.
- 🌟 **[Исправление обрезания главного сида из-за UTF-8 BOM в `peers.txt` (`RuTrackerModService.cs`, `tools/nsz/storm_lt_turbo.py`)]**:
  - В методах `FlushMetaPeers`, `FlushNewPeersToDisk` и `WriteControlCommand` запись через `StreamWriter(..., Encoding.UTF8)` заменена на `Utf8NoBom` без метки BOM (`﻿`), которая портила первый IP-адрес сида в каждой волне. Дополнительно в `storm_lt_turbo.py` включена принудительная очистка `﻿` и валидация IP в `parse_peer_endpoint`.
- 🌟 **[Профиль `MAX TURBO` для C++ `libtorrent 2.1.1` и потоковые UDP-трекеры (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**:
  - `storm_lt_turbo.py` переведён в максимальный скоростной режим: `smooth_connects=False`, `connection_speed=1000`, `connections_limit >= 3000`, `max_peerlist_size=6000`, очереди запросов `4000`, `utp_gain_factor=5000`, `utp_target_delay=200`, `prefer_rc4=True`, `peer_turnover=0`, `piece_extent_affinity=True`, `coalesce_reads=True`, `coalesce_writes=True`, `rate_limit_ip_overhead=False`, `aio_threads=16`, `hashing_threads=8`, `max_queued_disk_bytes=256MB`, `enable_upnp/natpmp=True` при скачивании, ускоренный реконнект пиров каждые `10 с` и периодический ре-анонс трекеров/DHT каждые `45 с`.
  - Список трекеров в `storm_lt_turbo.py` расширен до 20 быстрых публичных UDP/HTTPS трекеров (с лимитом до 24), а `QueryUdpTrackersBep15Async` в C# расширен до 14 UDP-трекеров с мгновенной потоковой передачей каждой пачки пиров через `onBatchPeers`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `Utf8NoBom`, `_missedMirrorHashes` (`45 с` на хэш), `SendWithCrossSchemeRedirectAsync`, публичный метод `ResolveMagnetMetadataBackgroundAsync`; `WarmupFastConnections` и `FetchTorrentFromCacheMirrorsCoreAsync` переведены на `https://itorrents.net/`, `_prefetchThrottle` увеличен до `4`, `PrefetchTopicsMetadata` увеличен до `6`, `fastRaceCts` до `2.6 с`; `QueryUdpTrackersBep15Async` расширен до 14 трекеров с потоковым коллбэком `onBatchPeers`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: В `LoadSelectedTopicAsync` добавлена неблокирующая 22-секундная фоновая P2P-догрузка реального списка файлов, а префетч результатов поиска увеличен до `maxCount: 6`.
- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Расширен `DEFAULT_PUBLIC_TRACKERS` (`20` URL) и `merge_trackers` (`24`), включён профиль `MAX TURBO`, очистка BOM `﻿` в `parse_peer_endpoint` / `pump_peers` / `inject_new_peers`, повтор пиров `BEP-9` на `2.2 с`, реконнект пиров каждые `10 с` и принудительный ре-анонс каждые `45 с`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.4)

- 🔸 **[Fix Missing File Lists Across All RuTracker Topics (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Fixed `itorrents.org` failing in `113 ms` with an unhandled `HTTPS -> HTTP` `301` downgrade to `http://itorrents.net`. Switched primary cache fetches & TLS warmup to `https://itorrents.net/` (`HTTP 200` in `80-250 ms`) and added `SendWithCrossSchemeRedirectAsync`.
  - Replaced the global `60s` mirror cooldown with per-hash negative caching (`_missedMirrorHashes`, `45s` TTL) so uncached releases no longer block `.torrent` cache fetches for other games.
  - Raised top-result prefetching to `6` topics (`_prefetchThrottle = 4`) and added non-blocking `22s` background P2P metadata completion (`ResolveMagnetMetadataBackgroundAsync`) when neither cache mirrors nor the initial fast window return files.
- 🔸 **[Eliminate UTF-8 BOM Corrupting First Seed in `peers.txt` (`RuTrackerModService.cs`, `tools/nsz/storm_lt_turbo.py`)]**: Switched `FlushMetaPeers`, `FlushNewPeersToDisk`, and `WriteControlCommand` from BOM-emitting `Encoding.UTF8` to `Utf8NoBom`, and added defensive `﻿` stripping + IP validation in `storm_lt_turbo.py`.
- 🔸 **[MAX TURBO `libtorrent 2.1.1` Tuning & Streaming 14-Tracker UDP Harvest (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**: Enabled `smooth_connects=False`, `connection_speed=1000`, `connections_limit >= 3000`, `max_peerlist_size=6000`, request queues `4000`, `utp_gain_factor=5000`, `utp_target_delay=200`, `prefer_rc4=True`, `peer_turnover=0`, `piece_extent_affinity=True`, `coalesce_reads/writes=True`, `rate_limit_ip_overhead=False`, `aio_threads=16`, `hashing_threads=8`, `256MB` disk queue, UPnP/NAT-PMP during downloads, `20` public trackers, `10s` peer retries, `45s` tracker/DHT reannounce, and streaming per-tracker `onBatchPeers` callbacks across `14` UDP trackers.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.4_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.4.zip` / `STORM_SWITCH_BOX_8.0.4_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.4.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.4_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.4_Setup.exe`** (219.62 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.4.zip`** / **`_win-x64.zip`** (151.17 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.4.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.4_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.4_Setup.exe** (219.62 МБ)
  - `SHA-256: 2F453F8FA139F2BCA23CE7A8725767BB163150403151768A01239E7D86976706`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.4_win-x64.zip** / **STORM_SWITCH_BOX_8.0.4.zip** (151.17 МБ)
  - `SHA-256: C15090B35D2BE9884FF6B786E84A43ACF9D27C92A8810BCE40051C33C3BD769B`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.4.apk** (14.38 МБ)
  - `SHA-256: CA9DA77E484C247B5C6075DDFF477F80D44715524F4522578AD84A4F134CC69F`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.4_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 226F654BCC5EAF6C1C7EC8C42C03CD2DA984A5D3659E910FC834D47AFB6A93B2`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.3** (21.11.2026)

> 💡 **Релиз 8.0.3** - *Устранение бесконечной загрузки списка файлов раздач RuTracker и восстановление турбо-скорости встроенного движка C++ libtorrent*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.3)

- 🌟 **[Устранение бесконечной загрузки списка файлов и метаданных (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Исправлена критическая ошибка, из-за которой таймаут кэш-зеркал (`itorrents.org` / `torrage.info`) пробрасывал внутренний `OperationCanceledException` и срывал переход к P2P-получению метаданных `BEP-9`, оставляя интерфейс в вечной загрузке. Добавлены 60-секундный cooldown для сбойных зеркал, изолированные фазы загрузки в UI, отказ от 45-секундных блокировок Cloudflare внутри быстрых обработчиков WebView2, обновление `InfoHash` и `MagnetUri` для обновлённых раздач (`[Обновлено]`) через `dl.php?t=...`, а также быстрый шаг `--metadata-only` на базе `libtorrent` с мгновенным завершением процесса (`os._exit(0)` без 5-секундной задержки деструктора). Холодная загрузка 16 файлов `Captain Tsubasa 2` занимает 5.5-6.2 с, а `Animal Well` - около 1.8 с.
- 🌟 **[Восстановление турбо-скорости скачивания C++ `libtorrent-rasterbar` (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**:
  - Устранено скрытое падение `storm_lt_turbo.py` с `ModuleNotFoundError: ipaddress` во встроенном `python.exe` (модуль заменён на встроенный `socket.inet_pton`), из-за которого все загрузки откатывались с нативного C++ `libtorrent 2.1.1` (Tier 2) на более медленный `MonoTorrent` (Tier 3).
  - Включён нативный автотюнинг буферов Windows (`send_socket_buffer_size = 0`, `recv_socket_buffer_size = 0`) и `mixed_mode_algorithm = 1` (`peer_proportional`) с `utp_gain_factor = 3000` и `utp_target_delay = 150` без искусственного троттлинга `uTP`-сидов.
  - Через `handle.replace_trackers` быстрые публичные UDP/HTTPS-трекеры поставлены первыми, а заблокированные DPI адреса `t-ru.org` и `retracker.local` исключены из сессии `libtorrent`; удалено чтение/сохранение `lt_dht_state.dat` (`ses.load_state`), сбивавшее DHT на системах с виртуальными сетевыми адаптерами. На активных раздачах движок разгоняется до 35.5 МБ/с.
- 🌟 **[Параллельная гонка Cloudflare Pipelining и потоковая подача пиров (`RuTrackerModService.cs`)]**: Добавлены 4 быстрых граничных узла Cloudflare (`188.114.96-99.3`), одновременная гонка 2 узлов на каждый трекер (`3.8 с`), ранний выход по завершении приёма блока `5:peers` / `6:peers6` без ожидания `TCP FIN`, а также мгновенная подача кэшированных, UDP и DHT пиров через `onFastWavePeers`.
- 🌟 **[Точные счётчики сидов и пиров (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `storm_lt_turbo.py`)]**: Исправлен подсчёт `Пиры` отдельно от сидов, суммирование сидов и пиров по всем активным закачкам и корректное отображение `Пиры: N` в шапке и карточках очереди.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Удалена зависимость `ipaddress` (`socket.inet_pton`), добавлены режимы `--listen-port`, `--metadata-only`, `--save-torrent`, `--timeout`, настройки `build_turbo_settings` (`mixed_mode_algorithm=1`, `send/recv=0`, `utp_gain_factor=3000`, `utp_target_delay=150`, `enable_upnp=False`, `enable_natpmp=False`, `enable_lsd=False`, `aio_threads=12`, `hashing_threads=4`), очистка трекеров `merge_trackers` + `apply_clean_trackers`, периодический переконнект пиров и немедленный выход `os._exit(0)`.
- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `_cacheMirrorsCooldownUntilUnix`, `_preferredCfEdgeIp`, `FindFreeHighListenPort`, `TryResolveMetadataViaEmbeddedLibtorrentAsync`, `HasCompleteCompactPeersPayload`, `TryFetchTrackerSingleCfEdgeAsync`, параллельная гонка `FetchTrackerViaHttpPipeliningAsync`, поддержка IPv6 `6:peers6` в `ExtractCompactPeersFromBencodedData`, потоковая передача `onFastWavePeers` и обновление `InfoHash` из `dl.php?t=...` для `[Обновлено]`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Изоляция `detailsTask`, `filesTask` и обновления `discoveredNewHash`, снятие 45-секундных ожиданий Cloudflare challenge внутри быстрых обработчиков WebView2, корректное отображение `Пиры: N`.
- 🔹 **[Services/RuTrackerDownloadQueueService.cs]**: Суммирование `totalSeeds` / `totalPeers` по активным элементам очереди и отображение `Пиры: N` в строках прогресса.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.3)

- 🔸 **[Fix Infinite File-List Loading on RuTracker Topics (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Fixed internal `OperationCanceledException` propagation during cache mirror races (`itorrents.org` / `torrage.info`) that aborted `InspectTorrentFilesAsync` before reaching P2P `BEP-9` metadata resolution. Added a `60s` mirror cooldown, fast embedded `libtorrent --metadata-only` (`os._exit(0)` avoiding `~session()` stalls), dynamic `InfoHash`/`MagnetUri` refresh for updated `[Обновлено]` topics via `dl.php?t=...`, removal of `45s` Cloudflare blocks inside fast WebView2 handlers, and isolated UI loading phases with guaranteed fallback (`Captain Tsubasa 2` resolves all 16 files in `5.5-6.2s`, cold `Animal Well` in `~1.8s`).
- 🔸 **[Restore Native C++ `libtorrent` Turbo Download Speeds (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**: Fixed a silent `ModuleNotFoundError: No module named 'ipaddress'` crash in embedded `tools/nsz/python.exe` (replaced with `socket.inet_pton`) that had downgraded all downloads from Tier 2 native C++ `libtorrent 2.1.1` to Tier 3 `MonoTorrent`. Enabled Windows socket autotuning (`send/recv=0`), `mixed_mode_algorithm=1` (`peer_proportional`), `utp_gain_factor=3000`, `utp_target_delay=150`, filtered DPI-reset `t-ru.org` and `retracker.local` trackers via `handle.replace_trackers`, and removed `ses.load_state` (`lt_dht_state.dat`) which degraded DHT across virtual adapters (reaching up to `35.5 MB/s` on active swarms).
- 🔸 **[Parallel Cloudflare Edge Racing & Streaming Peer Injection (`RuTrackerModService.cs`)]**: Added 4 fast Cloudflare edges (`188.114.96-99.3`), 2-edge parallel racing per tracker host (`3.8s` timeout), early socket read exit on complete `5:peers`/`6:peers6` payloads, and immediate `t=0` cached/UDP/DHT peer streaming via `onFastWavePeers`.
- 🔸 **[Accurate Seed & Peer Counters (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `storm_lt_turbo.py`)]**: Reported non-seed peers separately from seeds, summed seeds/peers across active downloads, and rendered `Пиры: N` consistently.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.3_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.3.zip` / `STORM_SWITCH_BOX_8.0.3_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.3.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.3_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.3_Setup.exe`** (219.62 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.3.zip`** / **`_win-x64.zip`** (151.17 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.3.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.3_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.3_Setup.exe** (219.62 МБ)
  - `SHA-256: 9E9B4488B91F61007ECEF6782035C4585F51774EB9C15B21F0BBDE4969938EF8`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.3_win-x64.zip** / **STORM_SWITCH_BOX_8.0.3.zip** (151.17 МБ)
  - `SHA-256: A6B605EB7B799243C0F64312F3E34EAC91624B64255B40FC3EC11D6CF239D5D7`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.3.apk** (14.38 МБ)
  - `SHA-256: A91CDBE77D16E3753B6E187DE73E078D7E14BC648863D3E7C192F909B0356613`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.3_LEGACY.apk** (25.95 МБ)
  - `SHA-256: DCE565C6DA74E6F6155E56344CEBB6952CEEFFB5DBDFF948D3D143B5751F7769`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.2** (20.11.2026)

> 💡 **Релиз 8.0.2** - *Исправление залипания файлов и слотов предыдущей раздачи в загрузчике RuTracker, а также комплексное ускорение загрузки списка файлов, метаданных и поиска раздач*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.2)

- 🌟 **[Исправление обновления списка игр и файлов при выборе раздачи RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Устранена ошибка, при которой после выбора новой раздачи (например, `Animal Well`) в центральной области «Игры и части в раздаче» и «Файлы в раздаче» оставались данные предыдущей игры (`Aggelos`). Теперь при смене раздачи `PrimeQuickTopicState` принудительно очищает `_loadedTorrentBytes`, `_loadedTorrentHash` и `_loadedMagnet`, а `TryGetInstantCachedTorrentFiles` строго сверяет `InfoHash` методом `TryLoadValidatedTorrent`, полностью исключая утечку кэша между темами.
- 🌟 **[Мгновенный сброс UI и надёжное обновление при выборе и клике (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Выделен единый метод `LoadSelectedTopicAsync`, который до любых сетевых запросов мгновенно очищает старые ссылки, файлы и группы и перестраивает слоты под новую тему. Исправлена автовыборка первой раздачи при мгновенном поиске (`autoSelectFirst: true`), добавлен обработчик `TopicsListView_ItemClick` для повторного открытия выделенной строки, а флаги `_isTopicLoading` / `filesApplied` защищают от двойного запуска и гонок обновлений.
- 🌟 **[RAM-кэш, коалесцирование запросов и фоновый префетч (`RuTrackerModService.cs`)]**: Добавлены потокобезопасный оперативный кэш `_memoryTorrentCache`, дедупликация параллельных загрузок `.torrent` через `_inFlightTorrentFetches`, фоновая предзагрузка первых 4 найденных раздач (`PrefetchTopicsMetadata`, до 3 одновременных загрузок через `_prefetchThrottle`) и предварительный прогрев HTTPS-соединения `WarmupFastConnections()` при открытии загрузчика. Повторное переключение между раздачами отдаёт список файлов за миллисекунды.
- 🌟 **[Ускорение поиска, кэш-зеркал и облачных прокси (`RuTrackerModService.cs`)]**: Живой поиск `tracker.php` в `SearchTopicsAsync` ограничен быстрым таймаутом `2.2 с` вместо `30 с`, прямые запросы тем ускорены до `2.0-2.2 с`, из гонки `.torrent`-зеркал исключён устаревший HTML-сервис `btcache.me` (оставлены `itorrents.org` и `torrage.info` с таймаутом `3.0 с` и асинхронной записью на диск), а облачная гонка `FetchTopicHtmlViaCloudRaceAsync` сокращена до `3.2 с` с немедленной отменой проигравших запросов и 60-секундным cooldown для сбойных прокси.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `_loadedTorrentHash`, `ExtractInfoHashHex`, `TryLoadValidatedTorrent` (строгая проверка `InfoHash` SHA-1), `_memoryTorrentCache`, `_inFlightTorrentFetches`, `WarmupFastConnections`, `PrefetchTopicsMetadata` (`maxCount = 4`, `_prefetchThrottle = 3`) и `_cloudProxyCooldownUntilUnix` (`60 с`). Обновлены `PrimeQuickTopicState`, `CreateIsolatedDownloadSession`, `TryGetInstantCachedTorrentFiles`, `TryFetchTorrentFromCacheMirrorsAsync`, `InspectTorrentFilesAsync`, `SearchTopicsAsync` и `FetchTopicHtmlViaCloudRaceAsync`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml]**: На `TopicsListView` включено `IsItemClickEnabled="True"` и подключён обработчик `ItemClick="TopicsListView_ItemClick"`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Вызов `WarmupFastConnections()` в конструкторе, мгновенный выбор первого результата в `SearchOrInspect_Click` без перетирания активного статуса, исправление `ApplyTopicFilterAndSort`, единый асинхронный конвейер `LoadSelectedTopicAsync` с защитой от гонок (`_activeLoadedTopicId`, `_activeTopicHasRealFiles`, `_isTopicLoading`, `filesApplied`) и автоматической догрузкой файлов при обнаружении нового `InfoHash` на странице темы.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.2)

- 🔸 **[Fix Stale Game Slots & File Lists on Topic Switch (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Fixed a bug where selecting a newly searched topic (such as `Animal Well`) kept showing game slots and `.torrent` file lists from the previously viewed topic (`Aggelos`). `PrimeQuickTopicState` now clears `_loadedTorrentBytes`, `_loadedTorrentHash`, and `_loadedMagnet` on topic switches, while `TryGetInstantCachedTorrentFiles` validates the 40-char `InfoHash` via `TryLoadValidatedTorrent` before reusing any bytes.
- 🔸 **[Instant UI Reset & Race-Free Topic Loading (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Consolidated selection handling into `LoadSelectedTopicAsync` to immediately clear previous file/link/slot collections before awaiting network tasks, enabled `autoSelectFirst: true` for instant search results, added `TopicsListView_ItemClick` so re-clicking a highlighted row reloads reliably, and added `_isTopicLoading` / `filesApplied` guards against duplicate loads and stale status overwrites.
- 🔸 **[In-Memory Torrent Cache, Request Coalescing & Prefetch (`RuTrackerModService.cs`)]**: Added thread-safe `_memoryTorrentCache`, in-flight HTTP fetch deduplication (`_inFlightTorrentFetches`), background metadata prefetching for the top 4 search hits (`PrefetchTopicsMetadata` throttled to 3 concurrent transfers), and TLS connection warmup (`WarmupFastConnections`) when opening the downloader dialog.
- 🔸 **[Faster Search, Cache Mirror Race & Cloud Proxy Fallbacks (`RuTrackerModService.cs`)]**: Capped live tracker search HTTP/WebView2 calls at `2.2s`, tightened topic/torrent HTTP timeouts (`2.0-2.2s` direct HTML/viewtorrent, `3.0s` `itorrents.org`/`torrage.info` race after removing HTML-only `btcache.me`, async disk cache write), and reduced `FetchTopicHtmlViaCloudRaceAsync` to `3.2s` with winner-first cancellation and a `60s` failure cooldown per proxy.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.2_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.2.zip` / `STORM_SWITCH_BOX_8.0.2_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.2.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.2_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.2_Setup.exe`** (219.61 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.2.zip`** / **`_win-x64.zip`** (151.16 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.2.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.2_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.2_Setup.exe** (219.61 МБ)
  - `SHA-256: 79E8FDF115D60E56F528F3EF8DAFDF4C80C922A95D7BA8BE6642F393171253A2`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.2_win-x64.zip** / **STORM_SWITCH_BOX_8.0.2.zip** (151.16 МБ)
  - `SHA-256: 6225C516F29B739B8860DEED983BCAAA5673D182EF7F717265F6D2D8B51BF171`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.2.apk** (14.38 МБ)
  - `SHA-256: 416CFB8ACBC6F0A83CEC118CA4F97FF5597C930B95493EC8FA99761B0E42CAD1`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.2_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 78305F15F6E8ABC0C6C24F7A170C6C22775F2AF3ABA3BCF062E3E267F4A64ABD`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.1** (20.11.2026)

> 💡 **Релиз 8.0.1** - *Исправление ошибки блокировки `persisted_tasks.json.tmp` в Задачнике, восстановление оригинального внешнего вида установщика и иконок из линейки 7.X.X, а также комплексная оптимизация скорости интерфейса, GPU-анимаций и плавности работы на Windows и Android*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.1)

- 🌟 **[Исправление ошибки блокировки `persisted_tasks.json.tmp` в Задачнике (`SafeFileOperations.cs`, `TasksViewModel.cs`)]**: Устранён конфликт записи с фиксированным временным файлом (`The process cannot access the file '...persisted_tasks.json.tmp' because it is being used by another process`). Внедрена атомарная запись `WriteAllTextAtomic` / `WriteAllTextAtomicAsync` через уникальные временные файлы (`<name>.<pid>.<guid>.tmp`) с 6-кратным backoff при временной блокировке антивирусом/системой, глобальный семафор процесса и 350-мс коалесценция всплесков сохранений. Аналогичная защита применена для `SettingsService`, `HistoryService` и `RuTrackerDownloadQueueService`.
- 🌟 **[Восстановление оригинального вида установщика и иконок 7.X.X (`StormInstaller`, `SettingsPage.xaml`, `MainWindow.xaml`)]**: Возвращён классический дизайн установщика линейки `7.X.X` (тёмно-фиолетовый заголовок `56x56` с иконкой и мягким неоновым свечением, стандартные чекбоксы и радиокнопки без лишней эмблемы в теле окна), при этом полностью сохранены защита от Zip-Slip, `Mutex` единственного экземпляра, двойная буферизация без мерцания и совместимая загрузка сертификата `STORM_CERTIFICATE.cer`. В приложении восстановлены фирменный логотип `Assets/logo.png` на экране настроек, иконка `storm_switch_box.ico`, `Assets/StoreLogo.png`, цвета уведомления `GlobalAlertInfoBar` и оригинальная `roundIcon` в Android.
- 🌟 **[Аппаратные GPU-анимации и плавность переходов 60-240 Гц (`UiMotionHelper.cs`, `MainWindow.xaml.cs`, `SettingsPage.xaml.cs`, `InstructionPage.xaml.cs`)]**: Внедрён новый движок композитных анимаций `UiMotionHelper` на базе `Windows.UI.Composition` (`ElementCompositionPreview` с кубическим замедлением Безье). Добавлены плавные 170-180 мс GPU-переходы при смене разделов, вкладок настроек, тем справки и всплывающего `GlobalAlertInfoBar`, а переключение режимов внутри `TasksPage` выполняется мгновенно через `SuppressNavigationTransitionInfo` без пустого кадра.
- 🌟 **[Оптимизация скорости UI, видеопамяти GPU и отзывчивости прогресса (`ThemeService.cs`, `LogBatcher.cs`, `BitmapImage`, `MainActivity.kt`)]**: Устранён тройной проход смены `RequestedTheme` с лишним кадром задержки при переключении тем (`ThemeService.RefreshUI`), уменьшено декодирование миниатюр и обложек с помощью `DecodePixelWidth` + `DecodePixelType.Logical` (96-640 px - до 75-85% экономии GPU VRAM и плавная прокрутка), убраны холостые уведомления 500-мс таймера `IsAnyTaskRunning`, добавлена коалесценция `LogBatcher` (120 мс) без заторов UI-потока, а на Android внедрён плавный анимированный `setProgress(..., true)` с ограничением частоты обновлений (`40 мс`).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/SafeFileOperations.cs]**: Добавлены `WriteAllTextAtomic` и `WriteAllTextAtomicAsync` с уникальными `.tmp`-именами (`<pid>.<guid>`), буфером 64 КБ, сбросом `ReadOnly` и повторными попытками переноса с экспоненциальной задержкой (`35-700 мс`).
- 🔹 **[ViewModels/TasksViewModel.cs]**: Единый `static` семафор `_persistSemaphore`, переиспользуемый `JsonSerializerOptions`, 350-мс коалесценция `SavePersistedTasks()` (`RunCoalescedPersistLoopAsync`), устранение лишних `OnPropertyChanged("IsAnyTaskRunning")` каждые 500 мс при простое и защита от зависаний при выходе `SaveTasksOnExit()`.
- 🔹 **[Services/SettingsService.cs, HistoryService.cs, RuTrackerDownloadQueueService.cs]**: Переход на потокобезопасную атомарную запись через `SafeFileOperations` и кэшированные `JsonSerializerOptions`.
- 🔹 **[installer/StormInstaller/Program.cs & StormInstaller.csproj]**: Восстановлен внешний вид установщика версии `7.0.14` (без `header_badge.png` и `VolumetricCheckBox`), добавлены `DoubleBuffered`, Zip-Slip-проверка `GetSafeZipExtractPath`, `Mutex` единственного экземпляра и поддержка обоих имён ресурса сертификата (`STORM_CERTIFICATE.cer` / `STORM_SERTIFICATE.cer`).
- 🔹 **[Views/SettingsPage.xaml, StormSwitchBox.csproj, MainWindow.xaml, AndroidManifest.xml]**: Восстановлены фирменное изображение `Assets/logo.png` вместо `<PathIcon>`, `storm_switch_box.ico` как `<ApplicationIcon>`, `Assets/StoreLogo.png`, цвета `GlobalAlertInfoBar` и `android:roundIcon="@mipmap/ic_launcher"`.
- 🔹 **[Services/UiMotionHelper.cs, MainWindow.xaml.cs, Views/SettingsPage.xaml.cs, Views/InstructionPage.xaml.cs]**: Новые композитные GPU-анимации появления (`AnimateEntrance` / `AnimateSlideFade`), моментальное переключение режимов внутри `TasksPage`, корректный заголовок окна из `App.AppVersionString`.
- 🔹 **[Services/ThemeService.cs & Services/LogBatcher.cs]**: Одиночный проход `RequestedTheme` при смене тем оформления (без тройного мигания) и неблокирующая очередь сброса логов `LogBatcher` (`120 мс`, `_uiFlushPending`).
- 🔹 **[Декодирование обложек и иконок (`BitmapImage`)]**: Установлены `DecodePixelWidth` (96 px для очередей/истории, 256 px для каталога/RuTracker, 440-640 px для деталей и скриншотов) и `DecodePixelType.Logical` в 9 файлах.
- 🔹 **[android/.../MainActivity.kt]**: Добавлен `setProgressSmooth` (нативная аппаратная интерполяция на Android 7.0+) и троттлинг UI-обновлений `40 мс` во время интенсивного сжатия/распаковки.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.1)

- 🔸 **[Task Queue Persistence Lock Fix (`SafeFileOperations.cs`, `TasksViewModel.cs`)]**: Resolved `The process cannot access the file '...persisted_tasks.json.tmp' because it is being used by another process`. Added `WriteAllTextAtomic` / `WriteAllTextAtomicAsync` with unique per-write temp filenames (`<pid>.<guid>.tmp`), 6-step backoff retries against transient AV/system locks, a static process-wide semaphore, and 350 ms write coalescing; rolled out the same atomic writer to `SettingsService`, `HistoryService`, and `RuTrackerDownloadQueueService`.
- 🔸 **[7.X.X Installer UI & App Icons Restored (`StormInstaller`, `SettingsPage.xaml`, `StormSwitchBox.csproj`, `MainWindow.xaml`)]**: Restored the classic `7.0.14` `StormInstaller` layout and visuals while keeping non-visual Zip-Slip protection, single-instance `Mutex`, double buffering, and `STORM_CERTIFICATE.cer` compatibility. Restored the `Assets/logo.png` image in `SettingsPage`, `storm_switch_box.ico` application icon, `Assets/StoreLogo.png`, `GlobalAlertInfoBar` palette, and Android `roundIcon`.
- 🔸 **[GPU Composition Animations & Page/Tab Smoothness (`UiMotionHelper.cs`)]**: Added `Windows.UI.Composition` ease-out GPU entrance animations for page switches, Settings tabs, Instruction topics, and `GlobalAlertInfoBar`, while suppressing blank-frame transitions when switching modes inside `TasksPage`.
- 🔸 **[UI Performance, VRAM & Progress Smoothness (`ThemeService.cs`, `LogBatcher.cs`, `BitmapImage`, `MainActivity.kt`)]**: Eliminated redundant multi-level `RequestedTheme` toggles in `ThemeService.RefreshUI`, added `DecodePixelWidth` + `DecodePixelType.Logical` across thumbnail and cover loaders to cut GPU VRAM and scroll stutter, removed idle `IsAnyTaskRunning` notifications on the 500 ms timer, coalesced `LogBatcher` UI flushes (`120 ms`), and added smooth animated `ProgressBar` updates (`40 ms` throttle) on Android.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.1_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.1.zip` / `STORM_SWITCH_BOX_8.0.1_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.1.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.1_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.1_Setup.exe`** (219.60 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.1.zip`** / **`_win-x64.zip`** (151.16 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.1.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.1_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.1_Setup.exe** (219.60 МБ)
  - `SHA-256: 27AC8A98E7166D3A3D38D7623DE1F3E57CFB1FC484E8185FD9349DF66CA5313C`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.1_win-x64.zip** / **STORM_SWITCH_BOX_8.0.1.zip** (151.16 МБ)
  - `SHA-256: 066AD5ACDA08E5B71A14B98C9436CDC59CC3BD97CA684BAB746176E75E62F75E`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.1.apk** (14.38 МБ)
  - `SHA-256: F5C37F8EDB70E432A21826E959A6045BEED164F44B88E1D06EA0256EDAC24BC6`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.1_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 5D90D4009C194405E8469162E424EC466FE0284455E223168422B569F6F238F3`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---

## **8.0.0** (19.11.2026)

> 💡 **Релиз 8.0.0** - *Крупное мажорное обновление архитектуры, безопасности и совместимости Nintendo Switch 1/2 и Nintendo 3DS: полное устранение 95 выявленных дефектов аудита, 100% соответствие формату NUP v1.0 для эмулятора STORM SWITCH (Windows и Android), исправление заголовков XCI/NSZ/XCZ и 3DS для Citra/Azahar, единый крипто- и сетевой контур, усиленная защита ключей и сертификатов, сокращение размера дистрибутива за счёт Zstd-сжатия `nutdb.json.zst` и сквозная проверка 26/26 сценариев на реальных образах игр*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.0)

- 🌟 **[100% совместимость NUP v1.0 с эмулятором STORM SWITCH (Windows + Android)]**: В `NupContainerService.cs` и `StormNczEngine.kt` исправлены 3 критических дефекта бинарного формата NUP v1.0 - абсолютное смещение `meta_offset` теперь выносится строго после таблицы оглавления `TOC`, записи `children[]` сортируются побайтно по UTF-8 именам, выравнивание `blob_offset`/`extent_offset` кратно 16 байтам, а для файлов нулевой длины пишется `extent_offset = 0`. Подтверждено прохождением эталонного верификатора `nup.py` и прямым запуском игр Nintendo Switch 1 и Switch 2 в `STORM_SWITCH-cli.exe`.
- 🌟 **[Исправление сборки XCI, XCZ, NSZ и NSP (`StormXciBuilder.cs`, `NszCompressionService.cs`)]**: Устранён дефект смещения поля `nameOffset` (`+0x10` вместо `+0x38`) в `WriteHfs0Entry`, восстановлено автоопределение корневого смещения `HFS0` в `CreatePfsFromStorage` вместо жёсткого `0x10000`, внедрены `SwitchContainerHandle` для безопасного освобождения потоков, `TitleIdHelper` для точного разбора идентификаторов и `StormToolResolver` для единого поиска внешних утилит. Все собранные контейнеры проходят `hactoolnet`, `nsz --verify` и запуск в эмуляторе.
- 🌟 **[Nintendo 3DS (3DS / CCI / CIA / CXI) - совместимость с Citra и Azahar]**: Проверены и стабилизированы нативная дешифровка `SeedDB`/`NCCH Crypto`, упаковка и распаковка `3DS`/`CIA`/`CXI`, инъекция `SMDH`-баннеров и `BCWAV`-звука, обрезка картриджей `SmartShrink` и подтверждена валидность собранных образов через `ctrtool --verify` (все 100% разделов `GOOD`) для стабильного запуска в Citra и Azahar.
- 🌟 **[Безопасность, сеть, сертификаты и гигиена репозитория]**: Полностью удалены захардкоженные ключи (`keys.dat`) и приватный `.pfx` из исходников и дистрибутивов, закрыты уязвимости `Command Injection`, `Zip-Slip` / `Path Traversal` (Windows, Android и `StormInstaller`), добавлена проверка `Host`/`Origin` в локальных HTTP-серверах (`DbiInstallService`, `DirectConsoleSenderService`), внедрён единый пул `StormHttpClientFactory` (устранены утечки сокетов и глобальное отключение TLS-проверок) и настроен строгий `network_security_config.xml` на Android.
- 🌟 **[WinUI 3, локализация, оптимизация памяти и дистрибутива]**: Устранены утечки подписок событий (`Loaded`/`LanguageChanged`/`ThemeChanged`), введено кэширование кистей (`UiBrushCache`), исправлены гонки и блокировки UI-потока в `MainWindow` и диалогах, доведена до 0 предупреждений компиляция C# (`0 errors, 0 warnings`), база `nutdb.json` (47.18 МБ) сжата в `nutdb.json.zst` (10.21 МБ), а из дистрибутива удалены неиспользуемые бинарники (`texconv.exe`, `hacbrewpack.exe`, `hac2l.exe`), дубликаты иконок, `kivy` и `__pycache__` (итоговый архив уменьшен на ~12 МБ).
- 🌟 **[Android Engine, StormInstaller и Python Turbo-загрузчик]**: В `StormNczEngine.kt` исправлены смещения `NACP` (`0x30A0`/`0x30B0`) и добавлен набор модульных тестов `StormBinaryAndLocTest.kt`; в `StormInstaller` устранена проблема извлечения и блокировки файлов с проверкой путей, а в `storm_lt_turbo.py` исправлена обработка исключений, тайм-аутов и корректное завершение потоков.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Core/NCA/StormXciBuilder.cs]**: Исправлено смещение поля `nameOffset` (`+0x10`) в `WriteHfs0Entry` для 100% валидности HFS0-разделов в картриджах XCI/XCZ.
- 🔹 **[Services/NupContainerService.cs & android/.../StormNczEngine.kt]**: Полный бинарный паритет NUP v1.0 с `STORM SWITCH` (`nup_container.cpp`) и `nup.py` - размещение TLV-метаданных строго после TOC, сортировка `children[]` по UTF-8, 16-байтное выравнивание экстентов и `extent_offset = 0` для пустых файлов.
- 🔹 **[Services/NszCompressionService.cs]**: Автоопределение смещения корневого HFS0 (`TryDetectXciRootHfs0Offset`) в `CreatePfsFromStorage` вместо жёсткого `0x10000` и корректное освобождение потоков.
- 🔹 **[Services/StormToolResolver.cs, StormHttpClientFactory.cs, TitleIdHelper.cs, SwitchContainerHandle.cs]**: Вынесены общие переиспользуемые сервисы поиска утилит, HTTP-клиентов с безопасным TLS, разбора TitleID и управления дескрипторами контейнеров Switch.
- 🔹 **[Models/UiBrushCache.cs & Views/*]**: Кэширование кистей WinUI 3, устранение утечек подписок на события смены языка/темы, удаление дублирующихся ветвей в `InstructionPage.xaml.cs` и исправление предупреждения `CS8602` в `SwitchFormatService.cs`.
- 🔹 **[Services/TitleDbService.cs & tools/db/nutdb.json.zst]**: Переход на Zstd-сжатую базу `nutdb.json.zst` (10.21 МБ вместо 47.18 МБ) с прозрачной распаковкой в памяти.
- 🔹 **[Services/DbiInstallService.cs, DirectConsoleSenderService.cs, ExternalProcessRunner.cs]**: Защита от Path Traversal, экранирование аргументов процессов (`ArgumentList`), проверка заголовков локальных серверов и безопасный запуск внешних утилит.
- 🔹 **[installer/StormInstaller]**: Исправление обработки путей при распаковке `Payload.zip` (защита от Zip-Slip), корректная установка сертификата и компиляция с `0 errors, 0 warnings`.
- 🔹 **[android/app]**: Добавлены `network_security_config.xml` и unit-тесты `StormBinaryAndLocTest.kt`, исправлены смещения `NACP` (`0x30A0`/`0x30B0`) и очищена упаковка от `keys.dat`.
- 🔹 **[Гигиена репозитория и дистрибутива]**: Удалены неиспользуемые и устаревшие файлы (`GpuAccelerationService.cs`, `VirtualRamDriveService.cs`, `TinfoilDownloaderService.cs`, `StormIvfcBuilder.cs`, `DebugLogger.cs`, `keys.dat`, `STORM_CodeSign.pfx`, старые `.slnx`/`.iss`, лишние бинарники, дубликаты иконок, `kivy` и `__pycache__`).

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.0)

- 🔸 **[100% NUP v1.0 Parity with STORM SWITCH Emulator (Windows + Android)]**: Fixed three critical binary layout issues in `NupContainerService.cs` and `StormNczEngine.kt` - post-TOC absolute `meta_offset`, UTF-8 byte-sorted `children[]` entries, 16-byte aligned `blob_offset`/`extent_offset`, and `extent_offset = 0` for zero-byte files. Verified with `nup.py` and live Switch 1 & Switch 2 boot tests in `STORM_SWITCH-cli.exe`.
- 🔸 **[XCI, XCZ, NSZ & NSP Container Pipeline Fixes (`StormXciBuilder.cs`, `NszCompressionService.cs`)]**: Fixed `WriteHfs0Entry` `nameOffset` write position (`+0x10` instead of `+0x38`), replaced hardcoded `0x10000` XCI HFS0 offset with dynamic `TryDetectXciRootHfs0Offset`, and introduced shared `StormToolResolver`, `TitleIdHelper`, and `SwitchContainerHandle` services. Verified with `hactoolnet` and `nsz --verify`.
- 🔸 **[Nintendo 3DS (3DS / CCI / CIA / CXI) Citra & Azahar Verification]**: Hardened native `SeedDB`/`NCCH Crypto` decryption, `3DS`/`CIA`/`CXI` repacking, `SMDH` banner and `BCWAV` audio injection, and `SmartShrink` trimming; verified 100% valid partitions via `ctrtool --verify` for Citra and Azahar.
- 🔸 **[Security Hardening, Network Pool & Repo Hygiene]**: Removed bundled `keys.dat` and `.pfx` files from source and packages, fixed command injection and Zip-Slip/path traversal vectors across Windows, Android, and `StormInstaller`, introduced `StormHttpClientFactory` with proper TLS validation, and added Android `network_security_config.xml`.
- 🔸 **[WinUI 3 Stability, Zero Warnings & Distribution Size Reduction]**: Fixed event handler leaks, added `UiBrushCache`, achieved `0 errors, 0 warnings` on Windows and `StormInstaller`, compressed `nutdb.json` (47.18 MB) into `nutdb.json.zst` (10.21 MB), and stripped unused binaries (`texconv.exe`, `hacbrewpack.exe`, `hac2l.exe`), duplicate icons, `kivy`, and `__pycache__`.
- 🔸 **[Android Engine, StormInstaller & Python Turbo Downloader]**: Fixed `NACP` offsets (`0x30A0`/`0x30B0`) in `StormNczEngine.kt`, added JUnit tests (`StormBinaryAndLocTest.kt`), hardened `StormInstaller` extraction/cleanup, and stabilized `storm_lt_turbo.py`.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.0_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.0.zip` / `STORM_SWITCH_BOX_8.0.0_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.0.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.0_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.0_Setup.exe`** (218.40 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.0.zip`** / **`_win-x64.zip`** (149.85 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.0.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.0_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.0_Setup.exe** (218.40 МБ)
  - `SHA-256: 0B51B76EDCECE13B389791745FE52C47698E37D55CC1E5084B8E77910314EBDF`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.0_win-x64.zip** / **STORM_SWITCH_BOX_8.0.0.zip** (149.85 МБ)
  - `SHA-256: 8E7A2B0EDBE00EC84CB27C8ABC3336CD4EDA13F5F222BC282A63C2B02F6EB49C`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.0.apk** (14.38 МБ)
  - `SHA-256: 6DDC3DE9FF0DE3C63ECD94F31D5EA8B23D308ED113C9D803F5E387D7AB16CE1D`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.0_LEGACY.apk** (25.95 МБ)
  - `SHA-256: F29E36FA919AF8F7433B310A1FEADA6AA448324D7365E6E79B9A5FD955B40BD5`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

---
## **7.0.14** (17.11.2026)

> 💡 **Релиз 7.0.14** - *Критическое исправление совместимости собранных игр Nintendo Switch 1 со стандартными эмуляторами (Ryujinx, Eden, Citron, Yuzu) и инструментарием (hactoolnet, nsz): устранены сбои заголовков PFS0 и XCI HFS0, искажение смещений NCZ с секциями от 0xC00, нарезки файлов > 32 МБ на Android, принудительного Hard-Patch 1G+1U с конфликтом CNMT, перехвата распакованных папок Switch 1 и перепаковки входных XCI/XCZ*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 7.0.14)

- 🌟 **[Android PFS0 Alignment & ExeFS/CNMT Integrity Fix (`StormNczEngine.kt`)]**: Исправлен расчёт размера таблицы строк `PFS0` (`stringTableSize` теперь включает выравнивание до `alignedHeaderSize` кратно `0x20`) в трёх сборщиках NCA для `compileSwitch2DumpPackage`. Ранее данные начинались после выравнивания, из-за чего `ExeFS` и `.cnmt` внутри собранных NCA читались как нули.
- 🌟 **[Android >32 MB Slice Copy & XCI/XCZ Output Support (`StormNczEngine.kt`)]**: Устранён критический баг, при котором непережатые разделы `> 32 МБ` со смещением `offset > 0` передавали в упаковщик `src.parentFile` целиком с нуля вместо нужного фрагмента NCA. Также `compileSwitch2DumpPackage` теперь корректно собирает `XCI` и `XCZ` при выборе картриджного формата (`packXciOrXczContainer`).
- 🌟 **[Android 1G+1U Multi-Content & Hard-Patch Parity (`StormNczEngine.kt`)]**: Возвращена логика сборки `5.5.7` - обычные `1G+1U` наборы больше не подвергаются принудительному `Hard-Patch` (он запускается только при вшивании модов/русификаторов или обновлении уже монолитной базы), а сам `Hard-Patch` больше не создаёт конфликтующий второй `...800.cnmt.nca` с тем же `progSha256`. Восстановлены запрет сжатия `BKTR`, запрет подстановки `RightsId` вместо реального `titleKey`, проверка ключей в режиме `reader != null`, определение папок Switch 2 строго по `ProgramId` `04...` в `main.npdm` и синхронное обновление `.cnmt.nca` при кастомизации `Control NCA`.
- 🌟 **[Windows NCZ 0xC00 Offset Mapping Fix (`StormNczStorage.cs`)]**: Исправлено отображение смещения внутри Zstd-потока (`streamOffsetInSection = currentOffset - Math.Max(targetSection.Offset, 0x4000)`) для NCZ с началом секции до `0x4000` (например `0xC00` у `PublicData`/DLC). Устранено пропускание первых `0x3400` байт потока и ошибки `Hash error (2002-4604)`.
- 🌟 **[Windows XCI 0x138 Header & XCI/XCZ Unpack/Repack Pipeline (`StormXciBuilder.cs`, `MultiGameCartridgeService.cs`, `MultiContentService.cs`, `NszCompressionService.cs`)]**: Исправлена запись поля `0x138` (`RootHfs0HeaderSize`) в заголовке XCI (размер заголовка HFS0 вместо размера всего раздела - восстановлено прохождение `Partition Header Hash (GOOD)` в `hactoolnet`). Исправлена перепаковка входных `.xci` в `NSZ`/`XCZ`/`NSP`, отключено ошибочное копирование `.xci` байт-в-байт в `.nsp`, а Zero-Disk-IO fallback для `.xcz` теперь читает раздел `/secure` и формирует валидный `.nsp`.
- 🌟 **[Windows CNMT Collision, hacpack ExitCode & Null Progress Guards (`ControlEditorService.cs`, `SwitchFormatService.cs`, `Switch2DumpCompilerService.cs`)]**: В `ControlEditorService` введено точное сравнение 16-символьного `TitleId` без коллизий базы `...000` и патча `...800`. В `RunHacpackAsync` добавлена проверка кода возврата (ошибочные неполные NCA больше не попадают в контейнер), а в `BuildStreamedRomFs` исправлен вылет `NullReferenceException` при отсутствии коллбэка прогресса.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Core/NSZ/StormNczStorage.cs]**: Корректный расчёт `streamOffsetInSection` относительно `Math.Max(targetSection.Offset, ncaHeaderSize)` для секций `< 0x4000` (`0xC00`).
- 🔹 **[Core/NCA/StormXciBuilder.cs & Services/MultiGameCartridgeService.cs]**: Запись точного `hfs0HeaderSize` в смещение XCI `0x138` (`RootPartitionHeaderSize`) для 100% соответствия `SHA-256` в `0x140`.
- 🔹 **[Services/MultiContentService.cs]**: Поддержка входных `.xci`/`.xcz` через `OpenSwitchContainerFileSystem` (`/secure`) при сборке в сжатые и несжатые форматы; сохранение `.xci` при распаковке через `nsz.exe`.
- 🔹 **[Services/NszCompressionService.cs]**: В Zero-Disk-IO декомпрессии `.xcz` открывается раздел `/secure` вместо корневого `HFS0` с сохранением корректного расширения `.nsp`.
- 🔹 **[Services/ControlEditorService.cs]**: Строгое сравнение `TitleId` при сопоставлении `Control` и `Meta` NCA вместо префикса из 13 символов.
- 🔹 **[Services/SwitchFormatService.cs & Services/Switch2DumpCompilerService.cs]**: Явный контроль `ExitCode` в `RunHacpackAsync` и безопасный вызов `onProgress?.Invoke(...)` в `BuildStreamedRomFs`.
- 🔹 **[android/.../StormNczEngine.kt]**: Выровненная запись `stringTableSize` в `PFS0`, потоковая передача `Pfs0Item(name, file, offset, size)` для срезов `> 32 МБ`, отмена авто-HardPatch для обычных `1G+1U` и удаление дублирующего `.800.cnmt.nca`, запрет подмены `RightsId` как ключа, защита `BKTR` от сжатия, изоляция папок Switch 1 по `ACI0 ProgramId`, поддержка `XCI`/`XCZ` в `compileSwitch2DumpPackage` и пересборка `.cnmt.nca` при кастомизации `Control`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 7.0.14)

- 🔸 **[Android PFS0 Alignment & ExeFS/CNMT Integrity Fix (`StormNczEngine.kt`)]**: Fixed unaligned `PFS0` `stringTableSize` header writes across three NCA builders in `compileSwitch2DumpPackage`, preventing zero-filled `ExeFS` and `.cnmt` reads inside generated NCAs.
- 🔸 **[Android >32 MB Slice Streaming & XCI/XCZ Export (`StormNczEngine.kt`)]**: Fixed `> 32 MB` non-zero-offset NCA slices embedding the entire `src.parentFile` instead of copying `(offset, size)` via `Pfs0Item`; enabled `packXciOrXczContainer` output in `compileSwitch2DumpPackage`.
- 🔸 **[Android 1G+1U Multi-Content & Hard-Patch Parity (`StormNczEngine.kt`)]**: Restored 5.5.7 `1G+1U` multi-content packing (no automatic Hard-Patch unless applying mods or updating a monolith), removed conflicting duplicate `...800.cnmt.nca` output, restored `BKTR` uncompressed guard, removed fake `RightsId` title key fallback, restored `reader` key verification, restricted Switch 2 folder detection to `04...` `ACI0` `ProgramId`, and synchronized `.cnmt.nca` hashes when customizing Control NCAs.
- 🔸 **[Windows NCZ 0xC00 Offset Mapping Fix (`StormNczStorage.cs`)]**: Fixed Zstd stream offset calculation (`currentOffset - Math.Max(targetSection.Offset, 0x4000)`) for `0xC00`-offset NCZ sections, eliminating `Hash error (2002-4604)`.
- 🔸 **[Windows XCI Header 0x138 & Input Container Pipeline (`StormXciBuilder.cs`, `MultiGameCartridgeService.cs`, `MultiContentService.cs`, `NszCompressionService.cs`)]**: Fixed XCI `0x138` `RootPartitionHeaderSize` (`hactoolnet` `Partition Header Hash (GOOD)`), fixed `.xci` input handling when targeting compressed formats, prevented raw `.xci` copy into `.nsp`, and fixed `.xcz` Zero-Disk-IO fallback to mount `/secure` as `.nsp`.
- 🔸 **[Windows CNMT Collision, hacpack ExitCode & Null Progress Guards (`ControlEditorService.cs`, `SwitchFormatService.cs`, `Switch2DumpCompilerService.cs`)]**: Enforced exact 16-char `TitleId` matching in `ControlEditorService`, checked `hacpack.exe` exit codes, and guarded null `onProgress` callbacks in `BuildStreamedRomFs`.

</details>

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.14_Setup.exe** (230.52 МБ)
  - `SHA-256: 305032707C350864C6EEC1BBBB00560A001E7A3A0C8CC8050672DF8A2614C470`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.14_Portable.zip** / **STORM_SWITCH_BOX_7.0.14.zip** (162.07 МБ)
  - `SHA-256: 4D6DB93AC0E5502A56F811F92B55221E3DF9C6979A80D0F371392849F2257F69`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.14_Android.apk** / **STORM_SWITCH_BOX_7.0.14.apk** (14.37 МБ)
  - `SHA-256: 196BFC9F87C3D3FC3BEA54C43395FD9474DCFED4E330861C604B4409C941D1D1`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.14_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.14_LEGACY.apk** (25.94 МБ)
  - `SHA-256: FEBEC7555ABBC3B2CEB59B3571949F595B65A9FC85C98043CB5DB04DDC938965`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>


## **7.0.13** (11.11.2026)

> 💡 **Релиз 7.0.13** - *Высокоскоростной нативный загрузчик RuTracker на libtorrent 2.1.1 + Cloudflare Pipelining, мгновенная реакция UI темы (< 5 мс) с параллельной гонкой инспекции первой страницы (~0.35s), хирургическое исправление парсера вложенных скобок [ENG], интерактивные 1-клик плашки копирования TitleID / Версии без "v" / Внутренней версии, ликвидация паразитных окон qbittorrent и дедлоков телеметрии*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker High-Speed Native Download Engine Fix]**: Полностью восстановлена нативная полноскоростная загрузка торрентов через встроенный C++ движок `libtorrent 2.1.1` (`storm_lt_turbo.py`) и прямой комбайн сбора пиров Cloudflare HTTP/1.1 Pipelining (`bt.t-ru.org`..`bt4.t-ru.org`). Реализовано мгновенное разрешение торрент-кэшей и зеркал с нулевым ожиданием метаданных (`0s`), регулярный телеметрический пульс в состоянии паузы для предотвращения дедлока stdout в C#, автоматическое восстановление зомби-задач из статуса "ПАУЗА" при старте и полное устранение нежелательных всплывающих окон внешнего `qbittorrent.exe`.
- 🌟 **[Topic Selection Speed Optimization (< 5 ms Instant UI + Concurrent Page-1 Race)]**: Мгновенный отклик интерфейса при клике на раздачу в каталоге (< 5 мс) за счет локальной гидратации слотов и файлов из индекса с отменой устаревших кликов. Организована параллельная гонка инспекции торрента и первой страницы темы через HTTP/WebView2 (~0.35s), фоновый неблокирующий сбор ссылок на моды из комментариев и замена тяжелого дискового HTML-кэша (29+ МБ) компактным асинхронным хранилищем метаданных.
- 🌟 **[Nested Bracket Title Parser Fix ("ENG]" Bug)]**: Исправлен алгоритм очистки названий игр `ExtractCleanGameTitleFromFilePath` в C# и Android Kotlin. Устранены ошибки обрезки названий при наличии вложенных скобок и составных тегов вроде `+ Wave 4 DLC` и `[NSP][RUS (Mod)/ENG]` (например, для *Xenoblade Chronicles X Definitive edition*, *Xenoblade Chronicles 3*), а также добавлен O(1) поиск по нормализованной базе `TitleDbService` для раздач без явного `[TitleID]`.
- 🌟 **[1-Click Copy Pills for TitleID, Version, and Internal Version]**: Статический бейдж метаданных заменен на 3 независимые интерактивные плашки быстрого копирования в буфер обмена (`TitleID`, `Версия` строго без префикса "v", `Внутренняя версия`) в развернутых и свернутых карточках игр, студии NACP и диалоге Android.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Libtorrent Native Turbo]**: Интегрирован прямой сбор аннонсов и скрейпов через Cloudflare Pipelining, оптимизирован буфер памяти для скоростей 100+ Мбит/с.
- 🔹 **[Deadlock Prevention]**: Потоки stdout и stderr вспомогательного процесса `storm_lt_turbo.py` переведены на неблокирующее асинхронное чтение с тайм-аутом.
- 🔹 **[Stale-Click Cancellation]**: При быстрой навигации по раздачам предыдущие запросы HTTP немедленно отменяются через `CancellationTokenSource`.
- 🔹 **[Compact Metadata Cache]**: Вместо полного дампа HTML-страниц сохраняется только структурированный JSON с распарсенными ссылками и атрибутами.
- 🔹 **[Clean Title Regex]**: Регулярные выражения санитизации заголовков обновлены для корректной обработки многоязычных тегов локализации.
- 🔹 **[Version Formatting Compliance]**: Исключен префикс "v" при копировании и отображении номеров версий в соответствии со стандартами экосистемы STORM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker High-Speed Native Download Engine Fix]**: Restored full-speed torrent downloading via embedded C++ `libtorrent 2.1.1` (`storm_lt_turbo.py`) and Cloudflare HTTP/1.1 Pipelining tracker peer harvester (`bt.t-ru.org`..`bt4.t-ru.org`). Enabled instant `.torrent` cache/mirror resolution with 0s metadata wait, pause-state telemetry heartbeats preventing C# stdout deadlock, automatic zombie "ПАУЗА" queue recovery on launch, and complete elimination of external `qbittorrent.exe` window popups.
- 🔸 **[Topic Selection Speed Optimization (< 5 ms Instant UI + Concurrent Page-1 Race)]**: Instant UI reaction time (< 5 ms) on topic selection via local catalog slot/file hydration with stale-click cancellation. Implemented parallel torrent inspection + Page-1 HTTP/WebView2 race (~0.35s), non-blocking background comment link discovery, and replaced legacy 29+ MB raw HTML disk cache with a compact asynchronous parsed metadata cache.
- 🔸 **[Nested Bracket Title Parser Fix ("ENG]" Bug)]**: Fixed `ExtractCleanGameTitleFromFilePath` across Windows C# and Android Kotlin for nested brackets and compound `+ Wave 4 DLC` / `[NSP][RUS (Mod)/ENG]` tags (e.g. *Xenoblade Chronicles X Definitive edition*, *Xenoblade Chronicles 3*) plus O(1) normalized `TitleDbService` name lookup for topics without explicit `[TitleID]`.
- 🔸 **[1-Click Copy Pills for TitleID, Version, and Internal Version]**: Replaced static metadata badge with 3 independent one-click clipboard copy pills (`TitleID`, `Version` strictly without "v" prefix, `Internal Version`) across expanded/collapsed game cards, NACP Studio overlay, and Android dialog.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.13_Setup.exe** (230.52 МБ)
  - `SHA-256: 33B049C0FE35BA8A6D0CE91CC007559F6D935235EB5BFA7251321B03F323CB9D`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.13_Portable.zip** / **STORM_SWITCH_BOX_7.0.13.zip** (162.07 МБ)
  - `SHA-256: 9A864D7CEA93837F640DCDE9F9D85DF2E0F2FDE8C44AC87A4AF5173D93C2B15C`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.13_Android.apk** / **STORM_SWITCH_BOX_7.0.13.apk** (14.36 МБ)
  - `SHA-256: C35BD320C55B33D444F4EE812CE55B5460647881BACB8701371C873ED1929539`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.13_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.13_LEGACY.apk** (25.93 МБ)
  - `SHA-256: D731D4D268D030D7CF7ABFBE34D311A9CB826FE32DEA78C2D6865B5956C5A90F`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>


## **7.0.12** (11.11.2026)

> 💡 **Релиз 7.0.12** - *Рабочая станция RuTracker AURORA FLUID: ликвидация двойных рамок ContentDialog (+115px полезной высоты), адаптивный экранный лейаут, 4-режимный переключатель рабочего пространства, полновысотный каталог с мгновенным поиском по 7,275+ релизам, клиентская сортировка и фильтрация, складные карточки слотов, расширенные облачные ссылки, складная очередь загрузок, мобильный диспетчер фоновых задач и строгое соблюдение Single Icon Mandate*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker AURORA FLUID Workstation Layout]**: Полностью переработана архитектура окна загрузчика модов и релизов RuTracker. Устранено паразитное двойное обрамление ContentDialog, высвободившее свыше 115px полезного пространства по высоте. Диалог теперь адаптивно заполняет окно на 96% ширины и высоты, предоставляя просторное рабочее пространство на экранах любого разрешения.
- 🌟 **[4-Mode Workspace Switcher]**: Реализован удобный селектор режимов отображения рабочей области [Обзор / Overview, Игры и файлы 50/50, Облако и моды, Загрузки], позволяющий сфокусироваться на нужной задаче в один клик.
- 🌟 **[Мгновенный поиск и клиентская фильтрация каталога]**: Полновысотный список релизов RuTracker оснащен фильтрацией на лету без задержек по базе из 7,275+ Switch-релизов. Добавлены быстрые пресеты, клиентская сортировка [По сидам, По размеру, По названию] и переключатель релизов с русской локализацией.
- 🌟 **[Складные карточки и умное управление файлами]**: Карточки игровых слотов поддерживают компактный 1-строчный вид и детальное раскрытие. Список файлов раздачи получил фильтрацию, переключение групп в один клик и кнопку снятия выделения со всех файлов.
- 🌟 **[Нативные облачные ссылки и складная очередь загрузок]**: Устранена проблема вложенных ScrollViewer в блоке внешних облачных хранилищ - ссылки прокручиваются плавно и нативно. Очередь загрузок Torrent получила виртуализированный компактный вид с разделением телеметрии статуса и очереди.
- 🌟 **[Android Background Task Manager]**: В мобильной версии на Android реализован складной диспетчер фоновых задач с чипами мгновенной сортировки и фильтрации, отображением прогресса и телеметрией.
- 🌟 **[100% Single Icon Mandate Compliance]**: Проведен полный аудит интерфейса загрузчика на ПК и мобильных устройствах. Устранены дублирования иконка + эмодзи и парные иконки, на каждом элементе управления используется строго не более одной векторной иконки.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[AURORA FLUID Chrome Elimination]**: Ликвидированы внешние отступы и заголовочные плашки ContentDialog, интерфейс использует внутреннюю панель инструментов с бесшовной интеграцией в стиль приложения.
- 🔹 **[Zero-Latency Instant Search]**: Поиск по каталогу RuTracker фильтрует коллекцию из 7,275+ релизов за 0 мс при вводе текста без блокировки UI потока.
- 🔹 **[Torrent File Selection Suite]**: Добавлены фильтр списка файлов, кнопка «Снять все» и пакетное переключение чекбоксов файлов торрента.
- 🔹 **[Separated Telemetry HUD]**: Скорость загрузки, сиды, пиры и статус очереди вынесены в компактные специализированные чипы.
- 🔹 **[Android Parity]**: Мобильный клиент MainActivity получил синхронные фильтры, быстрые чипы сортировки и оптимизированный адаптер списка.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker AURORA FLUID Workstation Layout]**: Completely overhauled RuTracker mod and release downloader dialog architecture. Eliminated redundant double ContentDialog chrome, reclaiming over 115px vertical height, expanding the workstation to 96% of window viewport.
- 🔸 **[4-Mode Workspace Switcher]**: Added instant workspace switcher [Overview, Games & Files 50/50, Cloud & Mods, Downloads] for quick navigation and task focus.
- 🔸 **[Zero-Latency Search & Client-Side Sorting]**: Full-height RuTracker releases list features instantaneous 0ms client-side search across 7,275+ Nintendo Switch releases with quick presets and sorting [By seeds, By size, By title].
- 🔸 **[Collapsible Game Cards & File Selection Suite]**: Implemented collapsible 1-row compact and detailed game slot cards, file filter, one-click group toggles, and Deselect All controls.
- 🔸 **[Smooth Cloud Links & Telemetry HUD]**: Resolved nested ScrollViewer traps for external cloud links. Torrent queue features separated telemetry indicators for speeds, seeds, peers, and queue state.
- 🔸 **[Android Collapsible Task Manager]**: Modern Android interface equipped with collapsible background task cards, instant sort chips, and Russian language filter.
- 🔸 **[100% Single Icon Mandate Compliance]**: Enforced strict single icon rule across all desktop and mobile controls with zero dual icon or icon+emoji duplicates.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.12_Setup.exe** (230.49 МБ)
  - `SHA-256: E995139C880B9B83E263AECF1FE5347FBA680B008D1642DC9A1CDBA658974A38`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.12_Portable.zip** / **STORM_SWITCH_BOX_7.0.12.zip** (162.05 МБ)
  - `SHA-256: BB3AA2C97BB6B6D51E1646A44243ACDFAF68D423BC5394E8F9A3A64898FCF035`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.12_Android.apk** / **STORM_SWITCH_BOX_7.0.12.apk** (14.36 МБ)
  - `SHA-256: 1B592EA46F0B042F25A8ACFAC8FA1A81983A2C14893467B5AC5F3A88C9AD53B6`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.12_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.12_LEGACY.apk** (25.93 МБ)
  - `SHA-256: F370A7F99E8013D776D976A05A4B220F016E4D45D82BE45AD28249B64B829AAB`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>

## **7.0.11** (10.11.2026)

> 💡 **Релиз 7.0.11** - *Прямой потоковый компилятор NUP без извлечения на диск NTFS, устранение дискового узкого горлышка (150,000+ файлов RomFS за доли секунды), защита папочного анализа Switch 2 дампов и сквозная интеграция NUP во все режимы на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Direct Zero-NTFS-Extraction NUP Streaming Compiler]**: Реализована прямая потоковая сборка Switch 2 контейнеров (NSP, NSZ, XCI, XCZ) из открытого монолитного архива `.nup` без предварительного извлечения файлов RomFS на диск. Полностью устранено узкое горлышко файловой системы NTFS: сборка масштабных дампов (например, Donkey Kong Bananza на 159,858 файлов) больше не требует 15-30 минут на запись и чтение десятков тысяч мелких файлов, а построение виртуальной иерархии RomFS выполняется в оперативной памяти за доли секунды.
- 🌟 **[In-Memory RomFS Streaming & Zstd Decompression]**: Метод `BuildStreamedRomFs` получил нативную поддержку виртуальных дескрипторов экстентов NUP (`__NUP__|offset|extentSize|codec|size`). Чтение несжатых данных и декомпрессия блоков Zstandard выполняются на лету через `BoundedReadStream` и `ZstdSharp.DecompressionStream` непосредственно в выходной поток NCA-раздела с минимальным расходом оперативной памяти (1 МБ буфер потока).
- 🌟 **[Защита от ошибочного папочного анализа Switch 2]**: Устранена критическая ошибка в `MultiContentService`, при которой задачи с Title ID Switch 2 (`04...`) передавали родительскую директорию контейнера в анализатор распакованных дампов с ошибкой «Не удалось проанализировать дамп Nintendo Switch 2». Добавлены строгие проверки наличия файлов-контейнеров (`.nup`, `.nsp`, `.nsz`, `.xci`, `.xcz`) и приоритетная маршрутизация файлов перед папочным режимом на Windows и Android.
- 🌟 **[Сквозная маршрутизация NUP во всех операциях]**: Поддержка формата `.nup` интегрирована во все сценарии работы приложения: Конвертация (`Convert`), Распаковка (`Unpack`), Проверка и верификация (`Verify`), а также прямая конвертация контейнеров в `NupContainerService`.
- 🌟 **[Безопасное размещение временных данных STORM_TMP]**: Рабочий каталог Switch 2 компилятора (`tempWorkDir`) перенесен из системного диска `C:\` в изолированную папку `STORM_TMP` на целевом накопителе вывода, что предотвращает аварийное переполнение системного диска при сборке крупных Switch 2 образов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Zero-Extraction NUP Compiler]**: Новый специализированный метод `Switch2DumpHelper.CompileNupToSwitch2ContainerAsync` открывает `.nup` в режиме только для чтения, извлекает во временную папку исключительно исполняемые бинарники ExeFS (~6 небольших файлов), а всё дерево RomFS (Base, Update и DLC) индексирует виртуально в памяти за миллисекунды.
- 🔹 **[MultiContentService Priority Routing]**: В `MultiContentService.cs` проверка файлов `.nup` вынесена на первое место перед блоком Switch 2 папок; директория `task.InputFolders` анализируется как распакованный дамп только при отсутствии контейнеров в очереди задач.
- 🔹 **[Target-Drive STORM_TMP]**: Каталог временных файлов компилятора формируется на том же диске, куда сохраняется итоговый образ, исключая нехватку свободного места на системном диске `C:\`.
- 🔹 **[TasksViewModel Routing]**: Режим проверки (`Verify`) отображает структуру пакетов, метаданные и заголовок NUP; режим распаковки (`Unpack`) вызывает нативное извлечение архива; режим конвертации (`Convert`) транслирует `.nup` в выбранный формат Switch 2.
- 🔹 **[Android StormNczEngine Parity]**: В движке `StormNczEngine.kt` добавлены защитные гарды против ошибочного распознавания родительских папок контейнеров и обеспечена синхронная логика обработки NUP.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Direct Zero-NTFS-Extraction NUP Streaming Compiler]**: Implemented direct in-stream compilation of Switch 2 containers (NSP, NSZ, XCI, XCZ) directly from `.nup` archives without unpacking RomFS files to NTFS. Completely eliminates file system thrashing when building massive games (150,000+ files) while indexing the RomFS hierarchy in RAM within milliseconds.
- 🔸 **[In-Memory RomFS Streaming & Zstandard Decompression]**: Enhanced `BuildStreamedRomFs` with virtual NUP extent descriptors (`__NUP__|offset|extentSize|codec|size`). Uncompressed and Zstandard-compressed file extents are streamed directly into output NCA partitions via `BoundedReadStream` and `ZstdSharp.DecompressionStream` using a lightweight 1 MB buffer.
- 🔸 **[Switch 2 Dump Directory Analysis Guard]**: Fixed an issue in `MultiContentService` where Switch 2 tasks with Title ID `04...` caused parent directories containing `.nup` files to be misrouted to raw folder dump compilation. Added strict container file guards across both Windows and Android engines.
- 🔸 **[Full NUP Integration across All Modes]**: Seamlessly routed `.nup` operations through Convert, Unpack, and Verify modes in `TasksViewModel` and `NupContainerService`.
- 🔸 **[Target Drive STORM_TMP Relocation]**: Moved compiler scratch space from `%TEMP%` (`C:\`) to `STORM_TMP` on the target volume, preventing system drive exhaustion during large multi-gigabyte builds.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


---

## **7.0.10** (10.11.2026)

> 💡 **Релиз 7.0.10** - *Каноническая спецификация оглавления NUP v1.0 (TOC 0x30, 64-битные счетчики каталогов и файлов, двойное автоопределение TOC), чтение метаданных TLV по абсолютным смещениям контейнера, сверхбыстрое синхронное извлечение обложек (< 1 мс), устранение коллизий кэша иконок задач и сквозная материализация структуры Control на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Каноническая спецификация NUP v1.0 TOC 0x30 и двойное автоопределение]**: Исправлен размер заголовка TOC контейнеров `.nup` с 0x28 до канонических 0x30 байт (64-битные `dirCount` по смещению `+0x08` и `fileCount` по смещению `+0x10`), что устраняет ошибочное обнуление счетчика файлов. Реализовано автоматическое распознавание как канонических TOC 0x30, так и устаревших TOC 0x28, а упаковщик `PackSwitchDumpToNupAsync` переведен на формирование канонического заголовка 0x30.
- 🌟 **[Чтение метаданных TLV по абсолютным смещениям файла]**: Исправлено считывание блоков метаданных пакетов: смещение `metaOffset` (`pkg + 0x20`) теперь корректно читается как абсолютное файловое смещение в конце архива NUP, обеспечивая 100% извлечение официальных JPEG-обложек 256×256 (тэг 3), названий игр (тэги 5 и 8), актуальных версий патчей (тэг 4), издателя и бинарников NPDM/NACP.
- 🌟 **[Сверхбыстрое синхронное извлечение обложек и паспорта (< 1 мс)]**: Метод `TryExtractIconAndMetadata` оптимизирован для прямого чтения суперблока (`0x80` байт), заголовка оглавления (`0x30` байт) и точечного чтения TLV блоков пакетов без выделения и сканирования многомегабайтного буфера TOC, гарантируя мгновенную отзывчивость интерфейса при drag-and-drop.
- 🌟 **[Устранение коллизий кэша иконок и поддержка TitleId Switch 2]**: В `HistoryService` исключено кэширование и загрузка по неустойчивым номерам задач (`T001.png`), вызывавшим наложение обложек между разными играми. Внедрено детерминированное хэширование путей к источникам (`src_<hash>.png`), полная нормализация TitleId Switch 2 (`0400...`) для базовых игр, патчей и DLC, а также мгновенная привязка `GameIcon` в UI-потоке.
- 🌟 **[Сквозная распаковка и материализация Control на Windows и Android]**: Реализована автоматическая материализация структуры Control RomFS при распаковке NUP на Windows (`NupContainerService`) и Android (`StormNczEngine`): создание `base/control/icon_AmericanEnglish.dat`, синтез полноразмерного `control.nacp` (0x4000 байт) на 16 языках при его отсутствии и выгрузка `main.npdm`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[NUP v1.0 TOC 0x30 Specification Parity]**: Полноценная поддержка 48-байтного заголовка `TOC\x01` с 64-битными полями `dirCount` и `fileCount`, смещением таблицы пакетов с `0x30` и расчетом записей таблицы `children`.
- 🔹 **[Dual-TOC Backward Compatibility]**: Прозрачная поддержка ранее созданных архивов с устаревшим заголовком 0x28 через верификацию соотношения смещений таблицы строк.
- 🔹 **[Absolute metaOffset TLV Parser]**: Прямое чтение блоков TLV со смещений `metaOffset` в потоке файла и поддержка тегов `0x0001` (NPDM), `0x0002` (NACP), `0x0003` (Icon JPEG), `0x0004` (DisplayVersion), `0x0005`/`0x0008` (TitleName), `0x0006` (Publisher), `0x0007` (BuildId).
- 🔹 **[Version & Title Prioritization]**: Приоритет отображения наивысшей версии патча (`DisplayVersion = 3.2.0`) и базового названия игры в метаданных `NupInfo` и сводке состава пакетов.
- 🔹 **[Sub-Millisecond Icon Probing]**: Синхронное извлечение паспорта игры и обложки без вызова асинхронных блокировок и без аллокации буфера TOC.
- 🔹 **[Deterministic Icon Caching]**: Защита от коллизий общих идентификаторов задач `T001..T999` с сохранением по TitleId, BaseTitleId, GroupId и хэшу исходного файла `src_<12-hex>.png`.
- 🔹 **[Switch 2 TitleId Normalization]**: Нормализация шестнадцатеричных идентификаторов `0400...` для патчей (`0x800`) и дополнений DLC (`(tid - 0x1000) & ~0xFFF`).
- 🔹 **[Full Control Materialization]**: Запись извлеченной иконки в `base/control/icon_AmericanEnglish.dat` и синтез валидного `control.nacp` для бесшовной последующей перепаковки в NSP/NSZ через `Switch2DumpCompilerService`.
- 🔹 **[Android Engine Full Parity]**: Обновление алгоритмов `inspectNupFile` и `extractNupToTempNspOrDir` в Kotlin (`StormNczEngine`) с полной декомпрессией Zstd extents и созданием контрольных структур.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Canonical NUP v1.0 TOC 0x30 Specification & Dual Auto-Detection]**: Corrected NUP container TOC header size from 0x28 to canonical 0x30 bytes (64-bit `dirCount` at `+0x08` and `fileCount` at `+0x10`), resolving zero file count reports. Added automatic dual-detection for legacy 0x28 TOCs and updated `PackSwitchDumpToNupAsync` to emit canonical 0x30 headers.
- 🔸 **[Absolute File Offset TLV Metadata Reading]**: Fixed package metadata parsing: `metaOffset` (`pkg + 0x20`) is now treated as an absolute file offset near the end of the NUP container, ensuring 100% extraction of official 256x256 JPEG cover icons (tag 3), game titles (tags 5 and 8), update versions (tag 4), publisher, and NPDM/NACP binaries.
- 🔸 **[Sub-Millisecond Synchronous Cover & Metadata Extraction (< 1 ms)]**: Optimized `TryExtractIconAndMetadata` to read only the superblock (`0x80` bytes), TOC header (`0x30` bytes), and package TLV blocks directly, avoiding allocation and scanning of multi-megabyte TOC buffers for instant drag-and-drop UI responsiveness.
- 🔸 **[Eliminated Task Icon Cache Collisions & Switch 2 TitleId Support]**: In `HistoryService`, removed caching by generic task IDs (`T001.png`) that caused icon cross-contamination. Added deterministic source hashing (`src_<hash>.png`), Switch 2 `0400...` TitleId normalization across base, update, and DLC packages, and immediate UI thread `GameIcon` binding.
- 🔸 **[End-to-End Unpacking & Control Materialization on Windows & Android]**: Implemented automatic Control RomFS materialization when extracting NUPs on Windows (`NupContainerService`) and Android (`StormNczEngine`), writing `base/control/icon_AmericanEnglish.dat`, synthesizing full 0x4000-byte 16-language `control.nacp` when missing, and outputting `main.npdm`.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>

## **7.0.9** (10.11.2026)

> 💡 **Релиз 7.0.9** - *Полный паритет Nintendo Switch 2 (эталонный хэш RomFS, розничный header_key, нативный RSA-3072 ACID, автоопределение версий NACP/CNMT и упаковка DLC), поддержка контейнеров .nup, блочное сжатие NoCrypto NCA3 и упреждающий контроль дискового пространства*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Switch 2 и RomFS Hash Parity (CalcRomFsPathHash)]**: Исправлен битовый сдвиг в хэш-таблице RomFS на эталонный по спецификации Nintendo `(hash >> 5) | (hash << 27)`, что полностью устраняет ошибку `LoadResult = 5` в `nn::fs::RomFsFileSystem` и обеспечивает нативный запуск файловых систем Switch 2 в эмуляторах.
- 🌟 **[Розничный header_key NCA3 и обратная совместимость]**: Константа `StandardNcaHeaderKey` приведена к розничному 32-байтовому `header_key` (`AEAAB1CA...`, 100% паритет Windows и Android с `prod.keys`), с сохранением `LegacyStormNcaHeaderKey` в цепочке дешифровки заголовков для прозрачного чтения ранее собранных контейнеров.
- 🌟 **[Нативный Switch 2 ACID (0x300 байт RSA-3072) в main.npdm]**: Отключена принудительная нормализация под Switch 1 (`normalizeAcidForLegacySwitch1 = false`), благодаря чему эмуляторы корректно активируют режим `is_switch2 = true` и профиль памяти 12 GiB Ampere T239.
- 🌟 **[Точная метаинформация NACP и CNMT (DetectDisplayVersion)]**: Внедрено автоопределение реальной версии обновления из `control.nacp (+0x3060)` и тегов `Product.<XYZ>.` в `romfs/System/AddressTable` и `GameData`, автоматическая запись `AddOnContentBaseId (base_tid + 0x1000)` по смещению `+0x3070` и `SdkAddonVersion (0x00140000)`.
- 🌟 **[Полноценная упаковка DLC и потоковая сборка на Android]**: Реализована упаковка всех каталогов `dlc/<tid>` (включая `<dlc>/0/romfs`) в `PublicData NCA (ContentType = 5)` и `AddOnContent CNMT (TitleType = 0x82)`, упреждающая проверка свободного места (`DriveInfo` на Windows, `StatFs` на Android) и полная потоковая сборка на Android без заглушек `0x4000`.
- 🌟 **[Сжатие NCZSECTN + NCZBLOCK v2 для Switch 2 NoCrypto NCA3]**: Прямое чтение таблицы секций из заголовка NCA (`+0x240 + i * 16`) без отката к несжатому копированию и блочное Zstd-сжатие (1 МБ, до 25% потоков ЦП) разделов `Program (0)`, `Data (4)` и `PublicData (5)`.
- 🌟 **[Поддержка контейнеров .nup и мгновенное извлечение обложек Switch 2 и NUP]**: Нативное распознавание контейнеров `.nup` в файловых диалогах, drag-and-drop и операциях конвертации/мульти-контента; извлечение иконки 256x256 и метаданных из Switch 2 `NoCrypto` Control NCA и NUP v1.0 TOC/extents за < 5 мс, интеллектуальная очистка названий для поиска в TitleDB.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[RomFS Path Hash Correction]**: Коррекция формулы хэширования путей `CalcRomFsPathHash` по стандарту Nintendo `(hash >> 5) | (hash << 27)` взамен ошибочного сдвига влево.
- 🔹 **[Standard & Legacy HeaderKey Fallback]**: Двухуровневая дешифровка заголовков NCA3 (0x000..0xBFF) через розничный `header_key` и `LegacyStormNcaHeaderKey` в C# (`StormNczStorage`, `StormNczCompressor`, `NszCompressionService`) и Kotlin (`StormNczEngine`).
- 🔹 **[Native RSA-3072 ACID in npdm]**: Сохранение аутентичной 768-байтной секции ACID Switch 2 по смещению `+0x300` в `main.npdm` без урезания до 512 байт.
- 🔹 **[NACP & CNMT Version Harmonization]**: Точное извлечение строковых версий отображения из внутренних структур и синхронизация полей `AddOnContentBaseId` и `SdkAddonVersion`.
- 🔹 **[Multi-DLC Packaging Pipeline]**: Упаковка дополнений из подкаталогов `dlc/` в валидные контейнеры `PublicData NCA` и генерация индивидуальных мета-NCA для каждого DLC.
- 🔹 **[Pre-Flight Space Guard]**: Предварительный расчёт требуемого дискового пространства и проверка через `DriveInfo.AvailableFreeSpace` (Windows) и `android.os.StatFs` (Android) с буфером 256 МБ.
- 🔹 **[NoCrypto NCA3 Compression Parity]**: Полноценное блочное сжатие несжатых разделов Switch 2 в `NCZBLOCK v2` (1024 КБ) с ограничением параллелизма до 25% ядер ЦП.
- 🔹 **[Full .nup Container Extension Support]**: Распознавание `.nup` файлов в проводнике, очередях задач, конвертере и сборщике мульти-контента.
- 🔹 **[Sub-5ms Cover Extraction for Switch 2 & NUP]**: Мгновенное считывание обложек и паспорта из заголовков Switch 2 `NoCrypto` Control NCA и TOC/extents контейнеров `.nup`.
- 🔹 **[Intelligent TitleDB Cleanup]**: Нормализация названий игр с удалением суффиксов «- Update ...» и «+ DLC» для надёжного сопоставления в базе данных.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Switch 2 and RomFS Hash Parity (CalcRomFsPathHash)]**: Corrected RomFS path hash shift to Nintendo reference specification `(hash >> 5) | (hash << 27)`, resolving `LoadResult = 5` in `nn::fs::RomFsFileSystem` and ensuring native RomFS mounting in emulators.
- 🔸 **[Retail header_key NCA3 and Backward Compatibility]**: Updated `StandardNcaHeaderKey` to retail 32-byte `header_key` (`AEAAB1CA...`, 100% Windows and Android parity with `prod.keys`), preserving `LegacyStormNcaHeaderKey` in fallback chain for transparent reading of previously built archives.
- 🔸 **[Native Switch 2 ACID (0x300 bytes RSA-3072) in main.npdm]**: Disabled legacy Switch 1 normalization (`normalizeAcidForLegacySwitch1 = false`), allowing emulators to properly detect `is_switch2 = true` and activate the 12 GiB Ampere T239 memory profile.
- 🔸 **[Accurate NACP and CNMT Versioning (DetectDisplayVersion)]**: Automated detection of display versions from `control.nacp (+0x3060)` and `Product.<XYZ>.` tags in `romfs/System/AddressTable` and `GameData`, properly setting `AddOnContentBaseId (base_tid + 0x1000)` at `+0x3070` and `SdkAddonVersion (0x00140000)`.
- 🔸 **[Complete DLC Packaging and Android Streaming Build]**: Full packaging of all `dlc/<tid>` folders (including `<dlc>/0/romfs`) into `PublicData NCA (ContentType = 5)` and `AddOnContent CNMT (TitleType = 0x82)`, with pre-flight storage validation (`DriveInfo` on Windows, `StatFs` on Android) and clean streaming builds on Android without 0x4000 stubs.
- 🔸 **[NCZSECTN + NCZBLOCK v2 Compression for Switch 2 NoCrypto NCA3]**: Direct section table extraction from NCA header (`+0x240 + i * 16`) without falling back to uncompressed copies, enabling block-based Zstd compression (1 MB blocks, <= 25% CPU threads) for `Program (0)`, `Data (4)`, and `PublicData (5)` partitions.
- 🔸 **[Full .nup Container Support and Sub-5ms Switch 2 / NUP Cover Extraction]**: Added native `.nup` container recognition across file pickers, drag-and-drop, conversion, and multi-content workflows; sub-5ms extraction of 256x256 game covers and metadata from Switch 2 `NoCrypto` Control NCAs and NUP v1.0 TOC/extents, plus intelligent game name sanitization for TitleDB lookups.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


## **7.0.8** (08.11.2026)

> 💡 **Релиз 7.0.8** - *Защита от непрерывного сжатия Zero-Solid NCZ Guard (1 МБ NCZBLOCK), модульная распаковка NSP/NSZ/XCI/XCZ со слиянием 1G+1U BkTR, бескэшевый стриминг BlockNczByteReader на Android без дублирования памяти и персистентное сохранение обложек, задач и метрик сжатия*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Zero-Solid NCZ Guard (NCZSECTN -> 1 МБ NCZBLOCK)]**: Автоматическое выявление и динамическое пережатие устаревших непрерывных Solid NCZ (`NCZSECTN`) в стандартный блочный формат `NCZBLOCK` с шагом 1 МБ при сборке мульти-контента, сжатии и конвертации. Это обеспечивает мгновенный запуск и чтение игр в эмуляторах без необходимости предварительной распаковки гигабайтного кэша во временные директории.
- 🌟 **[Windows: Полное обновление движка распаковки (NSP / NSZ / XCI / XCZ, 1G+1U BkTR, Multi-DLC)]**: Устранены сбои при обработке малых `.ncz` файлов (< 1024 байт), добавлена поддержка чтения разделов `HFS0 / Secure` для картриджей `XCI / XCZ`, внедрено автообнаружение `prod.keys` в путях установленных эмуляторов, реализовано автоматическое слияние базы и патча обновления (`BkTR`) при распаковке `1G+1U` контейнеров, изолированы выходные папки `<Имя>_Unpacked` и обеспечено безопасное размещение временных потоков `STORM_TMP` на накопителе с максимальным объёмом свободного места.
- 🌟 **[Android: Прямая потоковая распаковка без дублирования памяти (Zero-Bloat и BlockNczByteReader)]**: Внедрён движок `BlockNczByteReader` с 4 МБ LRU-кэшем в оперативной памяти и прямой сквозной стриминг `PFS0`. Полностью исключено создание промежуточных 14+ ГБ копий `temp_program.nca`, дублирование в `atmosphere/contents` и авто-дамп `pfs0/` (расход дискового пространства при распаковке 14 ГБ игры снижен с 30-56 ГБ до ~14 ГБ). Добавлена предварительная проверка доступного пространства `StatFs` и перехват системной ошибки `ENOSPC` с автоочисткой неполных файлов и информативным диалогом.
- 🌟 **[Сохранение состояния задач, обложек и метрик сжатия при перезапуске]**: Реализовано многоуровневое кэширование и восстановление постеров (`IconCache` / `TitleDB` / `RuTracker`), персистентное сохранение списка входных каталогов и корректный перерасчёт параметров «Нач. размер», «Кон. размер», «Разница» и коэффициента сжатия после паузы или перезапуска приложения.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Zero-Solid NCZ Guard]**: Перехват и переупаковка `NCZSECTN` в `NCZBLOCK` (1024 КБ) в реальном времени при сжатии и сборке.
- 🔹 **[Advanced Unpack Pipeline]**: Обработка `NSP`, `NSZ`, `XCI`, `XCZ` с извлечением `HFS0/Secure`, расшифровкой и применением патчей обновления `BkTR` на лету.
- 🔹 **[Smart Keys Auto-Discovery]**: Автоматический поиск и загрузка ключей `prod.keys` и `title.keys` из системных каталогов Yuzu, Ryujinx, Citron, Eden и Suyu.
- 🔹 **[BlockNczByteReader LRU Cache]**: Прямой поблочный доступ к сжатым секциям NCZ через кольцевой 4 МБ LRU-буфер на Android без создания временных файлов на диске.
- 🔹 **[Zero-Bloat Memory Footprint]**: Снижение пикового расхода памяти на Android до фактического размера распакованных данных (~14 ГБ вместо 30-56 ГБ).
- 🔹 **[StatFs & ENOSPC Guard]**: Упреждающий контроль свободного места на накопителе устройства с защитой от повреждения файловой системы при исчерпании квоты.
- 🔹 **[Persistent Task State]**: Бесшовное сохранение очереди, статусов прогресса, списка каталогов и вычисленных объёмов экономии диска между сессиями.
- 🔹 **[Multi-Tier Icon Cache]**: Кэширование игровых обложек в локальном хранилище с запасными каналами TitleDB и сетевых метаданных.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Zero-Solid NCZ Guard (NCZSECTN -> 1 MB NCZBLOCK)]**: Automatically detects and recompresses legacy streaming solid NCZ files into 1 MB block-based `NCZBLOCK` format during multi-content packaging, compression, and conversion, enabling instantaneous loading in emulators without multi-gigabyte cache decompression.
- 🔸 **[Windows: Unpack Engine Overhaul (NSP / NSZ / XCI / XCZ, 1G+1U BkTR, Multi-DLC)]**: Fixed small `.ncz` (< 1024 bytes) decompression edge cases, added `HFS0 / Secure` partition extraction for `XCI / XCZ`, integrated automatic `prod.keys` discovery from installed emulators, implemented automated base and update patch (`BkTR`) merging for `1G+1U` containers, safely routed `STORM_TMP` streams to the drive with maximum free space, and isolated output folders.
- 🔸 **[Android: Direct Zero-Bloat Stream Decompression (BlockNczByteReader)]**: Implemented `BlockNczByteReader` with 4 MB in-memory LRU cache and direct `PFS0` streaming, eliminating 14+ GB temporary `temp_program.nca` copies, `atmosphere/contents` duplicates, and auto-dump `pfs0/` folders (reducing storage overhead from 30-56 GB down to ~14 GB for a 14 GB game). Added pre-flight `StatFs` storage checks and robust `ENOSPC` handling with clean file rollbacks.
- 🔸 **[Session Persistence: Task State, Game Covers, and Compression Metrics]**: Multi-tiered cover caching and recovery (`IconCache` / `TitleDB` / `RuTracker`), persistent input folder history, and accurate recalculation of initial size, final size, space saved, and compression ratio across application restarts.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


## **7.0.7** (07.11.2026)

> 💡 **Релиз 7.0.7** - *Динамическая синхронизация игр и слотов в раздачах RuTracker, интеллектуальная 5+ потоковая очередь с обходом медленных сидов (< 200 КБ/с) и автоматическая раскладка антологий*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Динамическая синхронизация игр и слотов в раздачах]**: При ручном распределении файлов или облачных ссылок по слотам («Игра #1», «Игра #2» и далее) блок игр в раздаче автоматически добавляет недостающие карточки, извлекая `TitleID`, `Base TitleID` (вычисление базы для DLC по смещению `Base + 0x1000`), официальные обложки и названия из Control NCA/SMDH.
- 🌟 **[RuTracker: Восстановление бейджа «ИГРА» и точная сортировка антологий]**: При разъединении слотов или удалении файлов базовый бейдж `ИГРА` возвращается на место. Реализована точная раскладка многоигровых сборников и антологий по индивидуальным подпапкам (`1..N`) с сохранением структуры контента.
- 🌟 **[RuTracker: Умная очередь скачивания на 5+ потоков]**: Лимит параллельных быстрых сетевых закачек увеличен до 5 одновременных потоков. Внедрён адаптивный механизм освобождения квоты: раздачи со скоростью загрузки ниже **200 КБ/с** переводятся в фоновый низкоприоритетный режим и **не занимают** основные 5 слотов, мгновенно пропуская вперёд следующие готовые игры из очереди.
- 🌟 **[StormInstaller: Стабильность и безопасная локализация]**: Интеграция 24 системных строк на всех 11 языках с контролем границ через `T(index)` для предотвращения исключений выхода за пределы диапазона.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Dynamic Slot Expansion]**: Автоматическое создание слотов `EnsureGameSlotExists` при назначении файлов на новые игры в раздаче.
- 🔹 **[Smart TitleID DLC Matcher]**: Автоопределение принадлежности DLC к базовой игре по маске `0x1000` и привязка к соответствующему слоту пакета.
- 🔹 **[Bandwidth-Aware Concurrency Guard]**: Задачи со скоростью `< 200 КБ/с` исключаются из подсчёта активных сетевых слотов `activeFastDownloads`, ускоряя обработку всей очереди.
- 🔹 **[Anthology Directory Isolation]**: Физическая изоляция сгенерированных образов в персональные директории по именам слотов игр.
- 🔹 **[UI Status Dispatcher & IsBuildCompletedOnDisk]**: Потокобезопасная маршрутизация статусов сборки и защита от ложных сбоев.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Dynamic Game Slot Synchronization]**: Automatically adds and configures slots in multi-game torrent distributions when assigning files or cloud links, extracting TitleID, base game references for DLC (`Base + 0x1000`), and metadata.
- 🔸 **[Multi-Game Anthology Layout]**: Clean sorting of anthology games into individual output folders (`1..N`) with proper badge recovery upon unlinking.
- 🔸 **[Smart 5+ Stream Download Queue]**: Concurrent download limit raised to 5+ active streams. Torrents under 200 KB/s run in background without consuming active concurrency slots, allowing queued downloads to proceed immediately.
- 🔸 **[StormInstaller Localization Safeguard]**: Full 24-key localization matrix across all 11 languages with bounds-safe accessor `T(index)`.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


## **7.0.6** (07.11.2026)

> 💡 **Релиз 7.0.6** - *Хотфикс автономного установщика StormInstaller (устранение IndexOutOfRangeException при установке на всех 11 языках и безопасный доступ к локализации T(index)), стабилизация статуса очереди RuTracker и полная мультиязычность*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[StormInstaller: Исправление критического исключения IndexOutOfRangeException]**: Устранена аварийная остановка инсталлятора `StormInstaller` на этапе `BtnInstall_Click`, вызванная отсутствием строк статусов распаковки и финализации (индексы `15..23`) в словарях переводов. Матрица локализации расширена до полных 24 строк для всех 11 языков экосистемы (`RU, EN, DE, FR, ZH, KO, JA, ES, IT, PT, HI`).
- 🌟 **[StormInstaller: Безопасный акцессор локализации T(index)]**: Внедрён защитный метод `T(index)` с автоматическим контролем границ массивов строк, гарантирующий fallback на английскую и русскую локализацию без возможности генерации исключений выхода за пределы диапазона.
- 🌟 **[RuTracker: Устранение ложного статуса «ОШИБКА» после успешной сборки]**: Межпоточная синхронизация `PropertyChanged` (`COMException 0x8001010E RPC_E_WRONG_THREAD`) при очистке временной папки раздачи после создания мульти-контентных контейнеров (`.nsz`/`.nsp`/`.xci`/`.xcz`), барьер ожидания UI-потока `StatusSyncBarrier` и защита `IsBuildCompletedOnDisk`.
- 🌟 **[RuTracker: Автоисцеление очереди и семантическая индикация SpeedBrush]**: Автоматическое восстановление сохранённых задач с готовым образом на диске в статус `ГОТОВО` (100%) при старте приложения, а также адаптивная цветовая дифференциация скорости и статуса задач под все темы оформления.
- 🌟 **[Локализация: 100% покрытие 11 языков и автоопределение языка ОС]**: Полная динамическая мультиязычность интерфейса Windows (WinUI 3), установщика `StormInstaller` и Android-клиента с мгновенным переключением на лету и автоопределением системного языка ОС.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[StormInstaller Full 24-Key Translation Matrix]**: Добавлены все недостающие системные строки стадий установки (остановка процессов, регистрация сертификата, распаковка, создание ярлыков, брандмауэр, успешное завершение и ошибки) для 11 языков.
- 🔹 **[Safe Localization Bounds Fallback]**: Метод `T(index)` защищает процесс установки от любых сбоев локализации с плавным переходом на дефолтные формулировки.
- 🔹 **[Thread-Safe Status Dispatcher]**: Маршрутизация всех уведомлений `PropertyChanged` модели задачи через `App.MainDispatcher.TryEnqueue` с защитой от вызова из пула рабочих потоков.
- 🔹 **[IsBuildCompletedOnDisk Guard]**: Проверка существования и ненулевого размера сгенерированного файла перед генерацией любых исключений в блоке пост-процессинга раздачи.
- 🔹 **[Watchdog Auto-Healing]**: Фоновый таймер очереди верифицирует целостность выходных контейнеров и переводит успешно завершённые задачи в `ГОТОВО`.
- 🔹 **[SpeedBrush Adaptive Binding]**: Привязка цвета текста в DataGrid к статусу задачи с гармоничной палитрой для всех 8 тем STORM SOFT.
- 🔹 **[Android StormLocalization]**: Полнофункциональный локализационный модуль `StormLocalization.kt` для Android с поддержкой 11 языков и динамическим обновлением UI.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[StormInstaller Hotfix: IndexOutOfRangeException Resolved]**: Fixed crash during `BtnInstall_Click` in `StormInstaller` by providing complete 24-string localization arrays across all 11 supported languages (RU, EN, DE, FR, ZH, KO, JA, ES, IT, PT, HI).
- 🔸 **[Safe Localization Accessor T(index)]**: Added bounds-safe string accessor with fallback to English/Russian strings, eliminating out-of-range indexing hazards.
- 🔸 **[RuTracker Build False-Error Elimination]**: Synchronized `PropertyChanged` notifications via `App.MainDispatcher` during torrent folder post-processing, preventing COMException 0x8001010E.
- 🔸 **[Status Sync Barrier & IsBuildCompletedOnDisk]**: Added UI status propagation barrier before disk cleanup and `IsBuildCompletedOnDisk` safeguard preventing post-cleanup error overrides.
- 🔸 **[11 Languages Ecosystem Localization]**: Full runtime localization across WinUI 3 desktop client, StormInstaller, and Android client with OS language auto-detection.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


## **7.0.5** (07.11.2026)

> 💡 **Релиз 7.0.5** - *Устранение ложного статуса «ОШИБКА» после успешной сборки в очереди RuTracker, барьер синхронизации UI-потока, 100% мультиязычность на 11 языках и автоопределение системного языка ОС*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Устранение ложного статуса «ОШИБКА» после успешной сборки]**: Исправлена межпоточная синхронизация `PropertyChanged` (`COMException 0x8001010E RPC_E_WRONG_THREAD`) при финализации и очистке временной папки раздачи после успешного создания мульти-контентного контейнера (`.nsz`/`.nsp`/`.xci`/`.xcz`). Устранены ложные падения задач при корректно собранном образе.
- 🌟 **[RuTracker: Барьер синхронизации UI-потока и защита IsBuildCompletedOnDisk]**: Добавлен барьер ожидания применения статуса в UI-потоке (`StatusSyncBarrier`) перед очисткой временных файлов. Внедрена проверка `IsBuildCompletedOnDisk`, предотвращающая перезапись успешного статуса сборки кодами ошибок пост-очистки.
- 🌟 **[RuTracker: Автоисцеление сохранённой очереди]**: При загрузке и валидации очереди задач из постоянного хранилища (`LoadPersistedState`) задачи с готовым выходным образом на диске автоматически валидируются и восстанавливают статус `ГОТОВО` (100%), ликвидируя залипание в «ОШИБКА».
- 🌟 **[RuTracker: Семантическая цветовая индикация статуса и скорости]**: Реализовано динамическое свойство `SpeedBrush` для столбца скорости и состояния - адаптивная цветовая дифференциация активной сборки, паузы, ошибок и завершённых задач в тёмных и светлых темах оформления.
- 🌟 **[Локализация: 100% покрытие 11 языков экосистемы STORM SOFT]**: Полная динамическая мультиязычность всех экранов, таблиц, диалоговых окон, контекстных меню, всплывающих подсказок (ToolTips), справочника и установщика `StormInstaller` на 11 языках: русский (RU), английский (EN), немецкий (DE), французский (FR), китайский (ZH), корейский (KO), японский (JA), испанский (ES), итальянский (IT), португальский (PT) и хинди (HI).
- 🌟 **[Локализация: Автоопределение системного языка ОС и переключение на лету]**: Интеллектуальный выбор языка операционной системы при первом запуске приложения и мгновенное реактивное переключение языка через настройки без необходимости перезапуска программы как на Windows (WinUI 3), так и на Android.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Thread-Safe Status Dispatcher]**: Маршрутизация всех уведомлений `PropertyChanged` модели задачи через `App.MainDispatcher.TryEnqueue` с защитой от вызова из пула рабочих потоков.
- 🔹 **[IsBuildCompletedOnDisk Guard]**: Проверка существования и ненулевого размера сгенерированного файла перед генерацией любых исключений в блоке пост-процессинга раздачи.
- 🔹 **[Watchdog Auto-Healing]**: Фоновый таймер очереди верифицирует целостность выходных контейнеров и переводит успешно завершённые задачи в `ГОТОВО`.
- 🔹 **[SpeedBrush Adaptive Binding]**: Привязка цвета текста в DataGrid к статусу задачи с гармоничной палитрой для всех 8 тем STORM SOFT.
- 🔹 **[11-Language UI Tree Walker]**: Рекурсивный обход визуального дерева WinUI 3 с автоматическим переводом строк XAML, заголовков столбцов DataGrid и элементов управления.
- 🔹 **[StormInstaller 11 Languages]**: Внедрение полной матрицы локализации в автономный инсталлятор `StormInstaller` с автоопределением языка Windows (`CultureInfo.CurrentUICulture`).
- 🔹 **[Android StormLocalization]**: Полнофункциональный локализационный модуль `StormLocalization.kt` для Android с поддержкой 11 языков и динамическим обновлением UI.
- 🔹 **[Settings & Quick Actions Localization]**: Локализация селекторов тем, путей к ключам и эмуляторам, диалогов инспектора RomFS и карточек модификаций.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker: Build False-Error Elimination]**: Fixed cross-thread `PropertyChanged` synchronization (`COMException 0x8001010E`) during torrent folder cleanup after successful multi-content container creation (`.nsz`/`.nsp`/`.xci`/`.xcz`).
- 🔸 **[UI Status Barrier & IsBuildCompletedOnDisk]**: Added UI status propagation barrier before disk cleanup and `IsBuildCompletedOnDisk` safeguard preventing post-cleanup error overrides.
- 🔸 **[Queue Auto-Healing]**: Persisted state validator automatically identifies existing target images on disk and heals task states to completed (100%).
- 🔸 **[Semantic SpeedBrush]**: Introduced adaptive dynamic coloring for task status and transfer speeds across all 8 STORM SOFT visual themes.
- 🔸 **[11 Languages Ecosystem Localization]**: Complete coverage of 11 ecosystem languages across all UI screens, tables, dialogs, tooltips, reference manuals, StormInstaller, and Android client (RU, EN, DE, FR, ZH, KO, JA, ES, IT, PT, HI).
- 🔸 **[OS Language Auto-Detection & Dynamic Switch]**: Automatic OS language detection on first boot with seamless runtime switching without app restart.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


# **7.0.3** (07.11.2026)

## **7.0.4**

> 💡 **Релиз 7.0.4** - *Устранение зависания статуса «СБОРКА», мгновенное освобождение сетевых слотов очереди, фильтрация строго актуальных версий модов и многоуровневый обход проверок 4PDA/форумов*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Устранение блокировки очереди и зависания статуса «СБОРКА»]**: Ликвидировано падение фонового конвейера по `COMException (0x8001010E RPC_E_WRONG_THREAD)` при обращении к `App.MainWindow` из рабочих потоков. Внедрен потокобезопасный `App.MainDispatcher` с прямым доступом к `EffectiveStage`.
- 🌟 **[RuTracker: Мгновенное освобождение слотов загрузки при старте сборки]**: Задачи в фазе упаковки мульти-контента (`СБОРКА`) больше не расходуют сетевую квоту `MaxConcurrentDownloads = 2`. Следующая игра из статуса «В ОЧЕРЕДИ» начинает загрузку по сети параллельно с локальной сборкой на диске.
- 🌟 **[RuTracker: Watchdog-самоисцеление и восстановление сохраненного состояния]**: Сторожевой таймер очереди и десериализатор `LoadPersistedState` автоматически переводят завершенные задачи с созданным выходным контейнером в статус `ГОТОВО` (100%), предотвращая застревание пакетов при перезапуске.
- 🌟 **[Моды: Фильтрация строго максимальной версии (Latest-Version-Only)]**: Реализован семантический парсер версий `TryExtractModVersionTuple` с анализом контекстных префиксов строк (напр. `v0.9.1: MailCloud`). Устаревшие ревизии (как `v0.9` при наличии `v0.9.1`) и старые спойлеры архивов автоматически исключаются как в темах форумов, так и внутри публичных папок Облака Mail.ru и Яндекс.Диска.
- 🌟 **[Моды: Приоритет открытых облачных зеркал и прямая интеграция Mail.ru]**: Обновлена шкала надежности провайдеров (`cloud.mail.ru` поднят до 94 баллов перед авторизованными вложениями 4PDA). Реализована поддержка API диспетчера Mail.ru (`api=2`) с отдачей прямого бинарного потока `PK\x03\x04` на максимальной скорости.
- 🌟 **[Моды: Многоуровневый обход проверок 4PDA и защита от HTML-заглушек]**: Четырехступенчатый каскад (прямой запрос Chrome 131 + зеркала 4pda.ws + прокси codetabs/r.jina.ai + бесшумный и интерактивный WebView2) с валидацией целостности архивов `IsHtmlOrErrorStubFile`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Dispatcher Thread Isolation]**: Полный отказ от вызовов `App.MainWindow` из рабочих потоков `Task.Run` в пользу изолированного `App.MainDispatcher.TryEnqueue`.
- 🔹 **[EffectiveStage Immediate Propagation]**: Внедрено вычисляемое свойство `EffectiveStage`, мгновенно передающее статус выполнения планировщику до тика UI-диспетчера.
- 🔹 **[Non-Blocking Network Slots]**: Исключение стадии «СБОРКА» из пула сетевых кандидатов `shouldBeActive` с защитой от преждевременного вытеснения.
- 🔹 **[Post-Build Cleanup & Status Transition]**: Гарантированный перевод в `ГОТОВО` (100% Завершено) по завершении сборки мульти-контента и в блоке `finally`.
- 🔹 **[Context-Aware Mod Versioning]**: Извлечение семантических версий модов из имен файлов, анкоров ссылок и предваряющего текста строки.
- 🔹 **[Mail.ru Dispatcher API 2]**: Корректная склейка базового URL диспетчера Mail.ru с относительным путем файла и автоматическим определением имени архива.
- 🔹 **[HTML Stub Rejection]**: Детектирование и удаление веб-страниц ошибок, страниц логина 4PDA и заглушек Cloudflare при сохранении архивов модов.
- 🔹 **[WebView2 Persistent Forum Profile]**: Сохранение сессий и cookies форумов в `%LOCALAPPDATA%\StormSwitchBox\WebView2ForumProfile`.
- 🔹 **[Selective Companion Spidering]**: Пропуск паука тем-спутников (Zone of Games) при наличии готовых открытых зеркал в первичном посте для исключения задержек DPI.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Queue Concurrency & Build Fix]**: Resolved stuck "СБОРКА" state and COMException RPC_E_WRONG_THREAD via isolated `App.MainDispatcher` and immediate `EffectiveStage` evaluation.
- 🔸 **[Non-Blocking Download Slots]**: Items in local packaging stage no longer consume concurrent network download slots, allowing subsequent queued downloads to proceed immediately.
- 🔸 **[Latest-Version-Only Mod Selection]**: Integrated semantic version parser with context prefix support, filtering out legacy revisions (e.g. dropping v0.9 when v0.9.1 is present).
- 🔸 **[Mail.ru Cloud Direct Stream]**: Full integration with Mail.ru Dispatcher API 2 resolving high-speed binary zip streams without 4PDA authentication barriers.
- 🔸 **[Multi-Layer Anti-Bot Bypass]**: Chrome 131 fingerprint headers, mirror swapping, headless renderer fallback, and WebView2 bridge with HTML error stub rejection.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>


---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: 3-уровневая архитектура максимальной скорости (Tier 1 + Tier 2 + Tier 3)]**: Ликвидирована регрессия скорости загрузки (0.45 МБ/с, 1 сид) - восстановлен полноценный разгон до 15-25+ МБ/с и поддержка сотен сидов в рое. Внедрена отказоустойчивая трёхуровневая архитектура: **Tier 1** (внешний qBittorrent WebUI) -> **Tier 2** (встроенный C++ `libtorrent-rasterbar 2.1.1` с uTP BEP-29 и Holepunch BEP-55) -> **Tier 3** (высокоскоростной MonoTorrent).
- 🌟 **[RuTracker: Устранение преждевременного выхода qBittorrent и авто-старт в фоне]**: Исправлена ошибка, из-за которой отсутствие запущенного процесса qbittorrent приводило к моментальному откату на резервный движок. Добавлен потокобезопасный бутстрап `EnsureLocalQBitReadyAsync` с авто-запуском `qbittorrent.exe --no-splash`, валидацией WebUI и авто-обходом конфликтов портов на 18080.
- 🌟 **[RuTracker: Потокобезопасный сбор пиров с изоляцией по InfoHash]**: Устранено состояние гонки при конкурентном парсинге bencoded-ответов трекеров `bt..bt4.t-ru.org` и UDP-анонсов. Пул пиров теперь строго изолирован по хэшам раздач, исключая взаимное загрязнение роя между задачами очереди.
- 🌟 **[RuTracker: Встроенный нативный движок libtorrent-rasterbar (Tier 2)]**: Интегрирован скрипт `storm_lt_turbo.py` на базе `libtorrent-rasterbar` внутри встроенного Python-окружения (`tools\nsz`). Позволяет скачивать раздачи на полной скорости канала без необходимости установки сторонних торрент-клиентов на ПК пользователя.
- 🌟 **[RuTracker: Точный селективный прогресс и мгновенные приоритеты файлов]**: Приоритеты файлов применяются немедленно после добавления торрента с 100% сопоставлением сгруппированных путей модов. Прогресс вычисляется через `amount_left`, устраняя зависание на 99.9%.

---

## **Релиз 7.0.2**

- **RuTracker: Мгновенный старт очереди закачек**: Устранена асинхронная гонка `DispatcherQueue.TryEnqueue`, внедрён потокобезопасный список `_backingItems`, метод `RunOnUiSyncOrAsync` и 250 мс самовосстанавливающийся сторожевой цикл в `OnUiTimerTick()`.
- **RuTracker: Высокоскоростная загрузка BitTorrent**: Отключено блокирующее автоопределение UPnP (`AllowPortForwarding = false`), оптимизированы параметры роя (`MaximumConnections = 400`, `MaximumHalfOpenConnections = 80`, `DiskCacheBytes = 64 MB`), мгновенная детекция отсутствия qBittorrent.
- **RuTracker: Парсинг всех страниц обсуждения темы**: Параллельный сбор страниц темы `&start=30, 60...` через `FetchValidatedTopicPageHtmlAsync` с пулом `SemaphoreSlim(4)` и устойчивым обходом блокировок через DoH и Cloud Race.
- **RuTracker: Извлечение ссылок из спойлеров и plain-text**: Полнотекстовый парсер спойлеров `[скрытый текст]`, тегов `<var>` и прямых URL-адресов с отсечением блоков подписей пользователей (`sig-body`).
- **RuTracker: Интеграция 4PDA и автоскачивание модов**: Распознавание ссылок `4pda.to/forum/`, извлечение заголовка и автора перевода из контекста сообщения, декодер постов `ResolveForumPostEmbeddedCloudUrlsAsync` (Windows-1251, `4pda.to/pages/go/?u=`) и прямое автоскачивание облачных архивов.
- **RuTracker: Персистентность очереди и восстановление**: Сохранение полезной нагрузки задач `RuTrackerPersistedTaskPayload` в `rutracker_download_queue.json`, возобновление закачек при перезапуске приложения и безопасное отсечение пустых записей.

## **Релиз 7.0.1**

- **RuTracker: Глобальный менеджер очереди (`RuTrackerDownloadQueueService`)**: Сохранение и отображение списка всех закачек при закрытии/открытии диалога, планировщик с лимитом `MaxConcurrentDownloads = 2`, сохранение состояния очереди в `%LOCALAPPDATA%\StormSwitchBox\rutracker_download_queue.json`.
- **RuTracker: Честный динамический расчёт прогресса 0..100%**: Исправлено зависание `manager.Progress` при частичной загрузке торрента; расчёт ведётся по выбранным файлам `(downloadedWantedBytes * 100.0) / totalWantedBytes`.
- **RuTracker: Управление очередью закачек**: Кнопки «Пауза/Продолжить» (`manager.PauseAsync`/`StartAsync`), «Вверх» и «Вниз» с динамическим вытеснением (`PauseForPreemption`), «Отмена/Удаление», кнопки шапки «Пауза все», «Продолжить все», «Очистить готовые» (Single Icon Mandate).
- **RuTracker: Отображение этапа «СБОРКА»**: Фиксация статуса сборки контейнера прямо в строке загрузки после завершения докачки.
- **RuTracker: Целостность GroupedRelativePaths**: Полное копирование структуры модов и TitleID при клонировании файлов в `ExecuteDownloadPipelineAsync`.
- **RuTracker: Устранение таймаутов**: Замена фатального `TimeoutException` на авто-реанонс трекеров и инжект DHT пиров.
- **Локализация**: 100% перевод всех новых элементов интерфейса на 6 языков в `LocalizationService.UiTree.cs`.

# CHANGELOG - STORM SWITCH BOX

## [7.0.0] - 2026-11-06

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: нормализация TitleID Switch и связывание DLC]**: Добавлен метод `NormalizeSwitchBaseApplicationTitleId`, который математически вычисляет базовый 16-символьный TitleID для любых обновлений (`...800`) и DLC (`(val & mask) - 0x1000`), гарантируя, что базовые игры, патчи и любые дополнения (например, *Assassin's Creed III Remastered* и DLC *Liberation*) безошибочно привязываются к одному слоту.
- 🌟 **[RuTracker: авто-детекция и защита от фантомных игр]**: Метод `DiscoverGameTitlesFromFiles` теперь группирует слоты только по настоящим базовым играм (`CategoryBadge == "ИГРА"`), не создавая ложные слоты для DLC; текстовая эвристика игнорирует слэши внутри скобок `(...)` и не дробит названия по ` + `, если правая часть является дополнением (*Liberation*, *Rogue*, *DLC*, *Season Pass* и др.).
- 🌟 **[RuTracker: ручное объединение слотов игр на лету]**: На бейдже каждого слота в списке «Игры / Части в раздаче» внедрён стилизованный переключатель (ComboBox). При выборе другого слота (например, `ИГРА #2 -> ИГРА #1`) происходит мгновенное бесшовное слияние: все файлы и ссылки переносятся в целевой слот, синтетические записи конвертируются в `DLC`, а нумерация оставшихся слотов автоматически пересчитывается без пробелов.
- 🌟 **[RuTracker: автоматическое сворачивание папок распакованных модов]**: Папки с распакованными файлами модов/русификаторов (`atmosphere/contents/...`, `romfs/`, `exefs/`, например в раздаче *Cadence of Hyrule*) теперь автоматически объединяются в одну аккуратную строку `МОД / РУСИФИКАТОР` с подсчётом суммарного размера, извлечением `DetectedTitleId` и полным скачиванием всех вложенных файлов при отмеченной галочке через `GetEffectiveRelativePaths()`.
- 🌟 **[RuTracker UI: расширение списка файлов раздачи на всю ширину]**: Блок «Файлы в раздаче» вынесен на нижний ярус с `Grid.ColumnSpan="2"` под список найденных тем, получив комфортную ширину более 1120 пикселей для свободного чтения длинных путей и бейджей без обрезки текста.
- 🌟 **[RuTracker UI: устранение моргания при параллельных закачках]**: Реализован чистый список добавленных закачек (`ActiveDownloadsItemsControl`) с отдельной строкой на каждую игру, собственным бейджем этапа, мини-прогрессбаром, скоростью, объёмом и кнопкой отмены, а также вынесен суммарный индикатор общей скорости и пиров без взаимного перетирания текста.
- 🌟 **[RuTracker UI: очистка ссылок при переключении тем]**: При переходе между темами список внешних ссылок и обсуждений мгновенно очищается и сбрасывается в статус «Поиск внешних ссылок...», предотвращая отображение ссылок от предыдущей раздачи.
- 🌟 **[Синхронизация слотов и 100% локализация на 6 языков]**: Метод `SyncGameSlotsWithDiscoveredFiles` безопасно схлопывает слоты при уменьшении числа игр с сохранением форматов, а все новые элементы интерфейса переведены на 6 языков.

## [6.9.9] - 2026-11-06

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: строгая передача только выбранных файлов и очистка граничных кусков]**: Исправлена фильтрация контейнеров и архивов модов строго по отмеченным чекбоксам (`IsSelected == true`) во всех режимах (1 слот и мульти-слот), мгновенное применение `Priority.DoNotDownload` в момент `MetadataReceived`, автоматическое удаление частичных граничных файлов BitTorrent и живой пересчёт выбранной версии и размера в карточке игры.
- 🌟 **[RuTracker: неблокирующий пакетный запуск нескольких загрузок подряд]**: Добавлены изолированные сессии `CreateIsolatedDownloadSession()` с динамическим портом `0` и отдельными подпапками пакетов - теперь сразу после старта скачивания можно выбрать другой набор файлов или следующую раздачу и запустить новую загрузку без отмены текущей и без переоткрытия окна.
- 🌟 **[Windows: полное восстановление Распаковки, Упаковки, Обновления, Конвертации и Инспектора RomFS/ExeFS]**: Устранено отравление `ExternalKeySet` через `RegisterRightsIdFallback`, исправлена расшифровка `TitleKey` через `TitleKek` в `StormNczStorage` и устранено двойное шифрование билетов `.tik`, возвращена прямая маршрутизация режимов `Convert`, `Pack` и `Update` в `TasksViewModel` без перехвата в `Multi`, устранены дедлоки пайпов и поддержана обратная сборка распакованных папок `exefs/` + `romfs/` в загружаемые `NSP`/`NSZ`/`XCI`.
- 🌟 **[Android: полноценная криптография `.tik` (`TitleKey`), потоковая Распаковка 13+ ГБ, сборка `NCA3` (`IVFC Lv1-Lv6`) и Инспектор RomFS]**: Внедрён сбор и расшифровка билетов `.tik` (`titlekek_XX` AES-128-ECB) перед распаковкой и Zstd-сжатием `NCZ`, прямое чтение слайсов `RomFS`/`ExeFS` без переполнения памяти, замена заглушек упаковки и виртуального проводника на полноценную компиляцию `Program/Control/Meta NCA3` и чтение реального дерева `IVFC Level 6`.

### 📋 Полный список изменений
- 🔹 **RuTrackerModService.cs**: Добавлена очистка граничных файлов BitTorrent (piece boundary leftovers), селективное применение `Priority.DoNotDownload` по маске отмеченных элементов и динамическое выделение изолированных торрент-сессий `CreateIsolatedDownloadSession()` с отдельными рабочими директориями.
- 🔹 **RuTrackerModDownloaderDialog.xaml.cs**: Обеспечен неблокирующий запуск загрузок с сохранением открытого окна и возможностью параллельного скачивания других раздач, реализован реактивный пересчёт выбранного объёма данных и версии игры при переключении чекбоксов.
- 🔹 **KeysService.cs & TicketHarvesterService.cs**: Защита `ExternalKeySet` от фиктивных RightsID через метод `RegisterRightsIdFallback`, корректное дешифрование зашифрованного TitleKeyBlock через соответствующий master/title kek и предотвращение записи нулевых ключей.
- 🔹 **StormNczStorage.cs & SwitchFormatService.cs**: Восстановление корректной последовательности расшифровки заголовков NCA и сжатия блоков NCZ с сохранением валидной структуры билетов `.tik` без повторного шифрования.
- 🔹 **TasksViewModel.cs & MultiContentService.cs**: Исправлена маршрутизация задач в очереди: режимы `Convert`, `Pack` и `Update` теперь исполняются через соответствующие целевые конвейеры без принудительного перехвата в multi-merge.
- 🔹 **VirtualRomFsInspectorService.cs**: Полная ликвидация блокировок анонимных каналов при чтении списков файлов RomFS/ExeFS, корректная распаковка выбранных поддиректорий и сборка готовых контейнеров из папок.
- 🔹 **Android StormNczEngine.kt & MainActivity.kt**: Реализация аппаратной расшифровки билетов `.tik` по таблице `titlekek_XX`, потоковое чтение больших контейнеров (13+ ГБ) с нулевым оверхедом по памяти, компилятор `NCA3` с генерацией структур `IVFC` уровней 1-6 и интеграция инспектора RomFS в мобильный интерфейс.


## [6.9.8] - 2026-11-06

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Полное раскрытие дерева файлов RuTracker]**: Прямое чтение структуры раздачи через `viewtorrent.php` (WebView2 Same-Origin + CSRF `form_token`) и живое P2P BEP-9 (`ut_metadata`) предразрешение списка файлов из роя сидов, отображающее все реальные компоненты (`ИГРА`, `ОБНОВЛЕНИЕ`, `ОБНОВЛЕНИЕ (РУС)`, `DLC`, `МОД / РУСИФИКАТОР`, `ИНФО`) с точными байтовыми размерами вместо заглушки `MAGNET`.
- 🌟 **[Защита от Cloudflare & автозагрузка постеров]**: Фильтрация и очистка отравленных `Just a moment...` страниц в кэше тем через валидатор `IsValidRuTrackerTopicHtml`, а также извлечение и кэширование обложек раздач для новых игр, отсутствующих в локальной TitleDB.
- 🌟 **[Интеллектуальная классификация и авто-комплектация]**: Приоритетная типизация файлов `.nsp`/`.nsz`/`.xci` по расширению, TitleID и версии `[vXXX]`, авто-выбор совместимой пары обновления и перевода (например, `v131072.nsp` + `atmosphere.7z`) и исключение текстовых `ReadMe.txt`.
- 🌟 **[Автораспаковка архивов модов внутри раздач]**: Встроенная распаковка `atmosphere.7z`, `.zip` и `.rar` архивов модификаций с прямой привязкой извлеченных каталогов `romfs` / `exefs` к задаче сборки монолитного контейнера.
- 🌟 **[6-языковая локализация бейджей и статусов]**: Полная локализация новых бейджей файлов (`ОБНОВЛЕНИЕ (РУС)`, `МОД / РУСИФИКАТОР`, `ИНФО`) и индикаторов парсинга на всех 6 языках интерфейса.


## [6.9.7] - 2026-11-05

### 🚀 Ключевые изменения и улучшения
- 🌟 **[100% 6-языковая локализация интерфейса]**: Полный перевод всех страниц, модальных окон, подсказок ToolTip и контекстных меню на 6 основных языков мира: 🇷🇺 Русский, 🇬🇧 Английский, 🇩🇪 Немецкий, 🇫🇷 Французский, 🇨🇳 Упрощенный китайский и 🇯🇵 Японский.
- 🌟 **[Рекурсивный движок `TranslateRoot`]**: Реализован интеллектуальный обход визуального и логического дерева XAML (`ConditionalWeakTable`), автоматически локализующий даже свернутые панели настроек, заголовки вкладок `TabViewItem`, строки `CommunityToolkit DataGrid`, выпадающие меню `Flyout` и `MenuFlyout`, вложенные блоки `Run` и подсказки `ToolTipService`.
- 🌟 **[Single Icon Mandate & Чистый UI]**: Устранено дублирование эмодзи и векторных иконок на кнопках фильтров платформ (`Nintendo Switch`, `Nintendo 3DS`), таблицах и тултипах в строгом соответствии с дизайн-системой STORM SOFT.
- 🌟 **[Французский раздел интерактивной инструкции]**: Добавлена полная поддержка французского языка (`GetTopicsFr`) для всех 22 тем и симулятора параметров `InstructionPage`.
- 🌟 **[Динамическое переключение на лету]**: Мгновенная реакция всех компонентов (`TasksPage`, `SettingsPage`, `CatalogPage`, `GameLibraryPage`, `HistoryPage`, `InstructionPage` и всех 6 диалоговых окон) на смену языка без необходимости перезапуска программы.

## [6.9.6] - 2026-11-05

### 🚀 Ключевые изменения и улучшения
- 🌟 **[TicketHarvester & title.keys] Сырой TitleKeyBlock для криптографического конвейера**: в `title.keys` и `ExternalKeySet` внедрена запись сырого `TitleKeyBlock` из бинарных билетов `.tik` (зашифрованный `TitleKek`), что полностью восстановило совместимость с `LibHac`, `hactoolnet` и `yanu-cli`, выполняющими штатную дешифровку ключей прав через TitleKek.
- 🌟 **[Ликвидация дедлоков stdout/stderr в hactoolnet и hacpack] Асинхронное дренирование каналов ввода-вывода**: во всех системных вызовах `hactoolnet` и `hacpack` при распаковке и компиляции RomFS, ExeFS и Control NCA реализовано асинхронное считывание (`BeginOutputReadLine` / `BeginErrorReadLine` с подпиской на события), что полностью устраняет 120-секундные дедлоки и таймауты из-за переполнения 4-килобайтного буфера анонимных каналов Windows.
- 🌟 **[Контроль полноты извлечения Base RomFS и 65% порог валидации]**: добавлена проверка завершённости и полноты дерева файлов RomFS при частичном выходе `hactoolnet`, а также внедрён адаптивный порог валидации размера выходного файла (не менее 65% от размера базы для полноразмерных игр), гарантирующий отсутствие битых или пустых образов.
- 🌟 **[Исключение дублирующего Patch CNMT 0x81 из монолитных NSP]**: в монолитные пакеты NSP теперь внедряется строго единый Program NCA (`...000`) и корневой Application CNMT (`...000.cnmt.nca`) с обновлённой версией приложения, исключая конфликтный мета-раздел Patch CNMT (`...800.cnmt.nca`), вызывавший ошибки монтирования в эмуляторе Eden.
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.6**: обновлены версия и код сборки (`versionCode = 696`, `versionName = "6.9.6"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) редакций.

### 📋 Полный список изменений
- 🔹 **[TicketHarvesterService]**: метод сбора ключей теперь извлекает сырой зашифрованный TitleKeyBlock из `.tik` и добавляет его в `ExternalKeySet` и `title.keys`.
- 🔹 **[HardPatchEngine Async Drain]**: внедрены асинхронные обработчики `OutputDataReceived`/`ErrorDataReceived` для всех процессов `hactoolnet` (ExeFS, RomFS base, RomFS update, Control) и `hacpack`.
- 🔹 **[RomFS Extraction Guard]**: очистка и повторная попытка распаковки RomFS при аварийном завершении процесса без зависания конвейера.
- 🔹 **[Monolithic NSP CNMT Layout]**: удалено встраивание дублирующего файла `extraCnmtRebuiltPath` (`.cnmt.nca` с контентом Patch), предотвращая попытки эмуляторов обращаться к несуществующим секциям патча.
- 🔹 **[Adaptive Size Threshold]**: порог валидации размера Program NCA установлен в 65% от исходной базы при полной пересборке и 30% при фильтрации языковых дорожек.
- 🔹 **[Инсталлятор StormInstaller]**: сборка инсталлятора и метаданные обновлены до версии 6.9.6 с сохранением Authenticode-подписи STORM TEAM.
