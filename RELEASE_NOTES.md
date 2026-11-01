> 💡 **Релиз 6.7.7** — *Фоновый менеджер многозадачности с поддержкой мгновенной приостановки и возобновления задач на Windows и Android, системный Foreground Service со статусом очереди в уведомлении и глубокая интеграция с торрент-движком MonoTorrent*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Менеджер задач] Приостановка и возобновление задач (Pause and Resume)**: в структуру задач `ProcessingTask` и модель очереди добавлена полноценная поддержка переключения состояний выполнения. Пользователи могут в один клик приостановить длительную операцию (сборку многогигабайтных контейнеров, компрессию NCZ или загрузку модификаций) и продолжить её в удобное время без потери прогресса.
- 🌟 **[P2P / MonoTorrent] Потокобезопасная пауза торрент-сессий**: для сетевых задач RuTracker внедрена бесшовная интеграция с `ActiveTorrentManager.PauseAsync()` и `StartAsync()`, совмещённая с механизмом синхронизации `ManualResetEventSlim.Reset()` / `Set()`. Пауза мгновенно освобождает дисковый ввод-вывод и сетевой канал, сохраняя подключённые пиры в разогретом состоянии.
- 🌟 **[Android] Полноценный Foreground Service**: на мобильной платформе загрузка и сборка переведены на службу переднего плана `FOREGROUND_SERVICE_DATA_SYNC` с интерактивным системным уведомлением. Прогресс, скорость и кнопки управления отображаются прямо в шторке Android, предотвращая завершение процесса системой при блокировке экрана.
- 🌟 **[Интерфейс WinUI 3] Интерактивные элементы управления очередью**: на вкладке «Задачи» добавлены динамические кнопки и пункты контекстного меню «⏸ Приостановить выполнение задачи» и «▶️ Возобновить выполнение задачи» с автоматическим переключением видимости в зависимости от текущего состояния процесса.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[ProcessingTask Engine]**: свойства `IsPaused`, `PauseButtonVisibility`, `ResumeButtonVisibility` интегрированы в систему реактивных уведомлений CommunityToolkit.Mvvm с поддержкой команд `Pause()` и `Resume()`.
- 🔹 **[I/O & Worker Throttling]**: фоновые воркеры сжатия и конвертации проверяют сигнальное состояние `PauseEvent.Wait()` на границах итераций чтения блоков, обеспечивая мгновенную реакцию на команду паузы.
- 🔹 **[RuTracker Dialog UI]**: диалог загрузчика модификаций расширен поддержкой фоновой отправки задач в общую очередь без блокировки графического интерфейса.
- 🔹 **[Android Notification Actions]**: добавлены интерактивные кнопки быстрого действия в системную шторку Android, позволяющие ставить загрузку на паузу и возобновлять её без открытия главного экрана приложения.
- 🔹 **[Theme & Icons Harmonization]**: векторные глифы паузы (`\uE769`) и возобновления (`\uE768`) стилизованы в единой цветовой палитре STORM SOFT с контрастными маркерами состояния.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Background Multi-Task Manager with Pause and Resume**: introduced full task pause and resume capabilities for multi-content building, NSZ compression, and mod downloading across Windows and Android.
- 🔸 **Thread-Safe MonoTorrent Session Pausing**: seamless integration with `PauseAsync()` and `StartAsync()` backed by `ManualResetEventSlim`, instantly releasing disk I/O and bandwidth while preserving peer connections.
- 🔸 **Android Foreground Service Support**: download and processing tasks now operate under `FOREGROUND_SERVICE_DATA_SYNC` with an interactive notification shade supporting instant pause, resume, and cancel actions.
- 🔸 **WinUI 3 Tasks Page Controls**: dynamic contextual buttons and menu flyout items for pausing and resuming active operations with reactive state transitions.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
