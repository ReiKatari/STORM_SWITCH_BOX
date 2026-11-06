> 💡 **Релиз 7.0.1** — *Комплексная оболочка, ROM-комбайн, пакетный конвертер и студия модификаций для Nintendo Switch и Nintendo 3DS.*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Глобальный менеджер очереди и планировщик (до 2 параллельных закачек)]**: Внедрён долговечный синглтон-сервис `RuTrackerDownloadQueueService`, сохраняющий и отображающий список всех закачек даже при закрытии и повторном открытии диалога. Планировщик автоматически поддерживает до 2 одновременных закачек на полной скорости канала без перегрузки сокетов TCP/NAT роутера, плавно запуская следующие задачи из очереди.
- 🌟 **[RuTracker: Динамический расчёт прогресса 0..100% при выборочной загрузке]**: Исправлена ошибка зависания прогресса при выборе части файлов торрента (напр. 60.1% в *Cadence of Hyrule*). Процент вычисляется строго по выбранным файлам, динамически доходя до 100.0% с автоматическим переходом на этапы загрузки модов, сборки и успешного завершения.
- 🌟 **[RuTracker: Полное интерактивное управление очередью (Пауза, Вверх, Вниз, Отмена)]**: На каждой строке добавлены 4 компактные векторные кнопки управления (Single Icon Mandate): мгновенная пауза/возобновление (`TorrentManager.PauseAsync`), перемещение вверх и вниз по приоритету с мягким вытеснением (`PauseForPreemption`), отмена и очистка. В шапку добавлены кнопки пакетного управления «Пауза все», «Продолжить все», «Очистить готовые».
- 🌟 **[RuTracker: Отображение статуса «СБОРКА» и сквозная телеметрия]**: Статус сборки контейнера отображается прямо в строке загрузки (`СБОРКА`), гарантируя прозрачность для пользователя после завершения скачивания. Глобальный UI-таймер (250 мс) непрерывно синхронизирует прогресс и скорость строк и задач в основном планировщике независимо от открытия диалога.
- 🌟 **[RuTracker: Сохранение путей группировки модов и устойчивость к просадкам]**: При клонировании файлов торрента полностью сохраняются `GroupedRelativePaths`, `DetectedTitleId` и `DisplayPath`. Устранены фатальные `TimeoutException` — при просадке скорости сервис автоматически запрашивает анонсы трекеров и собирает пиры через DHT.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[RuTrackerDownloadQueueService]**: Полнофункциональный менеджер очереди закачек с персистентным сохранением состояния в `rutracker_download_queue.json`.
- 🔹 **[Smart Queue Concurrency]**: Лимит `MaxConcurrentDownloads = 2` с автоматической активацией задач со статусом `В ОЧЕРЕДИ`.
- 🔹 **[MonoTorrent Dynamic Progress & True Pause]**: Вычисление `PartialProgress` и точных байт выбранных файлов, аппаратная пауза `manager.PauseAsync()` и возобновление `manager.StartAsync()`.
- 🔹 **[Queue Preemption on Reorder]**: Динамический пересчёт приоритета очереди при нажатии кнопок «Вверх» / «Вниз» с вытеснением нижних задач в очередь.
- 🔹 **[GroupedRelativePaths & TitleId Preservation]**: Полное копирование структуры файлов модов и TitleID в `ExecuteDownloadPipelineAsync`.
- 🔹 **[UI Grid.Row 4 Overhaul]**: Просторная панель закачек (высота строк 30px, `ScrollViewer MinHeight="96" MaxHeight="156"`), сводка очередей и индикатор суммарного роя.
- 🔹 **[Single Icon Mandate Controls]**: Строго по одной векторной иконке (`FontIcon`) на кнопках управления закачками без текстовых эмодзи.
- 🔹 **[6-Language UI Localization]**: Добавлены переводы всех новых кнопок и подсказок в `LocalizationService.UiTree.cs`.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker Global Queue Service & Smart Scheduler]**: Introduced `RuTrackerDownloadQueueService` singleton which manages all downloads throughout the application lifecycle. Limits active downloads to 2 concurrent torrents, preventing TCP/NAT overload and automatically dispatching queued items as slots become available.
- 🔸 **[Accurate 0..100% Selective Progress Calculation]**: Fixed progress getting stuck when selecting a subset of torrent files (e.g. 60.1% in *Cadence of Hyrule*). Progress is calculated strictly against selected bytes and advances smoothly all the way to 100.0%.
- 🔸 **[Comprehensive Queue Controls (Pause, Move Up, Move Down, Cancel)]**: Added 4 vector icon buttons per download item adhering to Single Icon Mandate: pause/resume, priority reordering with preemption (`PauseForPreemption`), and cancellation. Added "Pause all", "Resume all", and "Clear finished" batch buttons in the panel header.
- 🔸 **[Multi-Content Build Stage Display & Telemetry]**: The downloads list reflects the post-download multi-content building stage (`СБОРКА`) with dedicated telemetry. A 250 ms UI timer keeps tasks in `TasksViewModel` and download items synchronized even when the RuTracker dialog is closed.
- 🔸 **[GroupedRelativePaths Preservation & Resilient Swarm Harvesting]**: Fixed loss of grouped paths and TitleIDs during file selection cloning. Replaced fatal timeout exceptions with automatic tracker re-announcements and DHT peer reinjection.
- 🔸 **[Full 6-Language Localization]**: Complete localization across Russian, English, German, French, Chinese, and Japanese.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
