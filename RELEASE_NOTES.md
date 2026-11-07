> 💡 **Релиз 7.0.4** — *Устранение зависания статуса «СБОРКА», мгновенное освобождение сетевых слотов очереди, фильтрация строго актуальных версий модов и многоуровневый обход проверок 4PDA/форумов*

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
