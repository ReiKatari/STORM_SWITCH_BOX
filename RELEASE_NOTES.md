> 💡 **Релиз 6.8.7** — *Высокоточный поисковый движок RuTracker с контролем опечаток (перестановка букв и 1 неверная буква без ложных совпадений), исправление инициализации WebView2 и Same-Origin CDP в WinUI 3, автоматическая сборка задач по умолчанию и обновление индекса раздач*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Поисковый движок RuTracker] Точный поиск с умным допуском 1 опечатки или перестановки букв**: полностью переработан алгоритм ранжирования раздач в Windows (`RuTrackerModService.cs`) и Android (`RuTrackerCloudEngine.kt`). Удалён шумный фонетический скелет (`PhoneticSkeleton`), вызывавший ложные срабатывания на посторонние игры (например, выдача `Angels of Death` и `Gears of War 2` при поиске `Aggelos`). Теперь поиск работает строго по введённому запросу с допуском ровно одной перестановки соседних букв (`IsSingleLetterSwap`) или одной неправильной/пропущенной буквы (`DamerauLevenshtein <= 1`), а словарь синонимов сопоставляется строго по границам целых слов.
- 🌟 **[WebView2 и Same-Origin CDP] Устранение ошибок NullReferenceException при фоновом обходе**: исправлена ранняя инициализация `CloudflareWebView` до включения `ContentDialog` в визуальное дерево WinUI 3 (`RuTrackerModDownloaderDialog.xaml.cs`). Добавлен потокобезопасный менеджер `EnsureWebViewInitializedAsync` с выделенным пользовательским каталогом `%LOCALAPPDATA%\STORM SWITCH BOX\WebView2`, синхронизацией через события `Opened` / `Loaded` и защитой вызовов `CallDevToolsProtocolMethodAsync`.
- 🌟 **[Диспетчер задач и Автосборка] Полный паритет конвейера задач Windows и Android**: опция автоматической сборки после скачивания раздачи и модов (`AutoBuild`) теперь включена по умолчанию на Windows аналогично Android. Все обновления свойств задачи и привязка скачанных файлов (`NSP`, `NSZ`, `XCI`, `XCZ`, `NUP`, `NRO`, `3DS`, `CIA`) выполняются потокобезопасно в потоке UI, а при ручном отключении автосборки задача корректно переводится в статус `Ожидание` для запуска из основной очереди.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Точный поиск и фильтрация тегов]**: из поискового индекса слов исключены технические теги релиза (`NSP`, `NSZ`, `XCI`, `RUS`, `Multi`, `Update`, `DLC`), сохранены альтернативные и русские названия игр в скобках (`CleanTitleKeepAltNames`).
- 🔹 **[Контроль опечаток]**: внедрён детектор перестановки двух букв (`IsSingleLetterSwap`) и жёсткий порог расстояния Дамерау-Левенштейна `<= 1` с проверкой первой буквы слова.
- 🔹 **[Жизненный цикл WebView2 в WinUI 3]**: исключён вызов `EnsureCoreWebView2Async()` в конструкторе диалога до открытия окна; добавлена инициализация `CoreWebView2Environment` с правами записи при запуске от имени администратора.
- 🔹 **[Синхронизация API RuTracker #1605]**: исправлен парсинг JSON-структуры `result` из `api.rutracker.cc/v1/static/pvc/f/1605` и очистка `CDATA`-заголовков Atom-ленты; локальный индекс обогащён новыми релизами Nintendo Switch и Switch 2 (`7 285` тем).
- 🔹 **[Потокобезопасность очереди задач]**: устранены кросс-поточные обращения к `ObservableCollection` и `SolidColorBrush` (`RPC_E_WRONG_THREAD`) в фоновом конвейере `ExecuteRuTrackerPipelineAsync`.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Precision RuTracker Search with Single-Typo and Transposition Tolerance**: overhauled the search scoring engine across Windows (`RuTrackerModService.cs`) and Android (`RuTrackerCloudEngine.kt`). Removed noisy consonant skeleton matching and restricted fuzzy matching to exact words, valid prefixes, single letter swaps (`IsSingleLetterSwap`), and a strict Damerau-Levenshtein distance of `<= 1`.
- 🔸 **WinUI 3 WebView2 and Same-Origin CDP NullReferenceException Fix**: deferred `CloudflareWebView` initialization until `ContentDialog.Opened` / `Loaded` in the visual tree with a dedicated `%LOCALAPPDATA%\STORM SWITCH BOX\WebView2` environment and full null-safety guards before CDP `Runtime.evaluate` calls.
- 🔸 **Windows and Android Task Pipeline Parity**: enabled automatic multi-content build (`AutoBuild`) by default on Windows to match Android, added UI-thread marshaling for all task updates, and ensured tasks with `AutoBuild` disabled transition cleanly to the `Pending` queue state.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
