> 💡 **Релиз 7.0.7** — *Динамическая синхронизация игр и слотов в раздачах RuTracker, интеллектуальная 5+ потоковая очередь с обходом медленных сидов (< 200 КБ/с) и автоматическая раскладка антологий*

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
