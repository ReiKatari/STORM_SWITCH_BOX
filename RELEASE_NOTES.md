> 💡 **Релиз 7.0.2** — *Комплексная оболочка, ROM-комбайн, пакетный конвертер и студия модификаций для Nintendo Switch и Nintendo 3DS.*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Мгновенный старт очереди закачек и устранение гонки UI-диспетчера]**: Устранена асинхронная гонка `App.RunOnUI` (`DispatcherQueue.TryEnqueue`) при добавлении в очередь `EnqueueBatch`: авторитетный синхронизированный список `_backingItems` обновляется под блокировкой немедленно с синхронным выполнением `RunOnUiSyncOrAsync`. Добавлен 250 мс самовосстанавливающийся сторожевой цикл в `OnUiTimerTick()`, автоматически запускающий ожидающие загрузки, если количество активных слотов меньше лимита.
- 🌟 **[RuTracker: Высокоскоростная загрузка BitTorrent и оптимизация swarm-движка]**: Отключено блокирующее автоопределение маршрутизатора UPnP (`AllowPortForwarding = false`) в MonoTorrent, устраняющее 5–15-секундную задержку при старте. Увеличены лимиты соединений (`MaximumConnections = 400`, `MaximumHalfOpenConnections = 80`, `DiskCacheBytes = 64 МБ`), обеспечен мгновенный возврат `null` из `TryDownloadViaLocalQBittorrentAsync`, если процесс qBittorrent не запущен.
- 🌟 **[RuTracker: Парсинг всех страниц обсуждения и сквозной сбор внешних ссылок]**: Реализован параллельный сбор всех страниц темы форума (`&start=30, 60, ...`) через `FetchValidatedTopicPageHtmlAsync` с пулом `SemaphoreSlim(4)` и устойчивым обходом блокировок через DoH и Cloud Race. Извлечение ссылок теперь анализирует спойлеры `[скрытый текст]`, теги `<var>` и plain-text URL-адреса с отсечением подписей пользователей (`sig-body`).
- 🌟 **[RuTracker: Интеграция постов 4PDA и автоматическое извлечение вложенных модов]**: Добавлена классификация ссылок `4pda.to/forum/` как `"4PDA"`, извлечение названия мода и переводчика из контекста сообщения (например, *«Скачать машинный перевод от Trevor795»* в *Ys X: Nordics*). Разработан декодер постов форума `ResolveForumPostEmbeddedCloudUrlsAsync` с поддержкой кодировки Windows-1251, обходом редиректов `4pda.to/pages/go/?u=` и прямым скачиванием облачных архивов русификаторов (Cloud Mail.ru, Google Drive, прямые вложения `.zip`).
- 🌟 **[RuTracker: Долговечность очереди и восстановление незавершённых загрузок]**: Полная сериализация полезной нагрузки задач `RuTrackerPersistedTaskPayload` в `rutracker_download_queue.json`, восстановление настоящих пайплайнов загрузки при повторном открытии приложения и безопасная нейтрализация устаревших пустых записей без блокировки очереди.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Authoritative Backing Queue]**: Введён потокобезопасный список `_backingItems` в `RuTrackerDownloadQueueService`, устраняющий рассинхронизацию между асинхронным UI-потоком и планировщиком задач.
- 🔹 **[RunOnUiSyncOrAsync Dispatch]**: Мгновенное выполнение действий очереди на UI-потоке без задержек планирования `TryEnqueue`.
- 🔹 **[250ms Watchdog Scheduler]**: Автоматический подхват задач со статусом «В ОЧЕРЕДИ» при освобождении лимита активных загрузок.
- 🔹 **[Zero-Delay MonoTorrent Startup]**: Отключение UPnP port mapping (`AllowPortForwarding = false`) для мгновенного подключения к пирамиде без зависания на маршрутизаторах.
- 🔹 **[Swarm Throughput Scaling]**: Увеличение лимитов соединений до 400 сокетов и кэша дискового ввода-вывода до 64 МБ для стабильных 10–25+ МБ/с.
- 🔹 **[Instant qBittorrent Check]**: Экспресс-проверка наличия процесса `qbittorrent` без сетевых таймаутов.
- 🔹 **[Multi-Page Thread Harvester]**: Сбор страниц темы форума через `FetchValidatedTopicPageHtmlAsync` с лимитом параллелизма `SemaphoreSlim(4)`.
- 🔹 **[Full-Text Spoiler & Var Link Extractor]**: Сканирование текста внутри спойлеров, тегов `<var>` и открытых ссылок с фильтрацией подписей пользователей.
- 🔹 **[4PDA Post Resolver]**: Парсинг конкретных сообщений 4PDA по ID с декодированием Windows-1251 и снятием редиректов `4pda.to/pages/go/?u=`.
- 🔹 **[Deep Cloud Auto-Extractor]**: Автоматическое скачивание архивов русификаторов из постов 4PDA через Cloud Mail.ru и прямые форумные вложения.
- 🔹 **[Queue Payload Persistence]**: Сериализация `RuTrackerPersistedTaskPayload` для сохранения параметров сборки и возобновления закачек после перезапуска.
- 🔹 **[Ghost Record Defense]**: Автоматическое маркирование повреждённых или пустых записей как «ОТМЕНЕНО», исключающее зависание очереди.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker: Immediate Queue Dispatch & UI Dispatcher Race Fix]**: Fixed asynchronous race condition in `App.RunOnUI` (`DispatcherQueue.TryEnqueue`) where priority evaluation was executed before item was appended. Introduced authoritative thread-safe backing list `_backingItems` and `RunOnUiSyncOrAsync` along with 250 ms self-healing watchdog scheduler.
- 🔸 **[RuTracker: High-Speed Swarm Optimization & Instant Startup]**: Disabled blocking UPnP router probing (`AllowPortForwarding = false`) to eliminate 5–15s initial stall. Upgraded swarm throughput (`MaximumConnections = 400`, `MaximumHalfOpenConnections = 80`, `DiskCacheBytes = 64 MB`) and added instantaneous non-blocking qBittorrent detection.
- 🔸 **[RuTracker: Multi-Page Forum Link Harvester & Plaintext URL Extraction]**: Added concurrent multi-page thread crawler (`FetchValidatedTopicPageHtmlAsync`) via `SemaphoreSlim(4)` with DoH & Cloud Race resilience. Extracts links across all thread pages including spoilers, `<var>` tags, and plain-text URLs while cleanly stripping user signature blocks.
- 🔸 **[RuTracker: 4PDA Forum Post Link Resolution & Cloud Mod Auto-Download]**: Added 4PDA post detection and context-aware metadata extraction (e.g. *Ys X: Nordics* post by `@omg_gods` with translation by Trevor795). Implemented `ResolveForumPostEmbeddedCloudUrlsAsync` supporting Windows-1251 decoding, `4pda.to/pages/go/?u=` redirect bypassing, and direct auto-downloading from embedded cloud/attachment links.
- 🔸 **[RuTracker: Persistent Queue State & Resilient Session Recovery]**: Added `RuTrackerPersistedTaskPayload` schema into `rutracker_download_queue.json`, restoring active pipelines across restarts and safely discarding orphaned legacy records without blocking concurrency slots.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
