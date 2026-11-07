> 💡 **Релиз 7.0.5** — *Устранение ложного статуса «ОШИБКА» после успешной сборки в очереди RuTracker, барьер синхронизации UI-потока, 100% мультиязычность на 11 языках и автоопределение системного языка ОС*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: Устранение ложного статуса «ОШИБКА» после успешной сборки]**: Исправлена межпоточная синхронизация `PropertyChanged` (`COMException 0x8001010E RPC_E_WRONG_THREAD`) при финализации и очистке временной папки раздачи после успешного создания мульти-контентного контейнера (`.nsz`/`.nsp`/`.xci`/`.xcz`). Устранены ложные падения задач при корректно собранном образе.
- 🌟 **[RuTracker: Барьер синхронизации UI-потока и защита IsBuildCompletedOnDisk]**: Добавлен барьер ожидания применения статуса в UI-потоке (`StatusSyncBarrier`) перед очисткой временных файлов. Внедрена проверка `IsBuildCompletedOnDisk`, предотвращающая перезапись успешного статуса сборки кодами ошибок пост-очистки.
- 🌟 **[RuTracker: Автоисцеление сохранённой очереди]**: При загрузке и валидации очереди задач из постоянного хранилища (`LoadPersistedState`) задачи с готовым выходным образом на диске автоматически валидируются и восстанавливают статус `ГОТОВО` (100%), ликвидируя залипание в «ОШИБКА».
- 🌟 **[RuTracker: Семантическая цветовая индикация статуса и скорости]**: Реализовано динамическое свойство `SpeedBrush` для столбца скорости и состояния — адаптивная цветовая дифференциация активной сборки, паузы, ошибок и завершённых задач в тёмных и светлых темах оформления.
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
