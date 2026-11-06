> 💡 **Релиз 7.0.0** — *Комплексная оболочка, ROM-комбайн, пакетный конвертер и студия модификаций для Nintendo Switch и Nintendo 3DS.*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: нормализация TitleID Switch и связывание DLC]**: Добавлен метод `NormalizeSwitchBaseApplicationTitleId`, математически вычисляющий базовый TitleID для обновлений (`...800`) и DLC (`(val & mask) - 0x1000`), объединяя базу, патчи и дополнения (*Assassin's Creed III Remastered* и DLC *Liberation*) в один слот.
- 🌟 **[RuTracker: авто-детекция и защита от фантомных игр]**: `DiscoverGameTitlesFromFiles` группирует слоты только по базовым играм (`CategoryBadge == "ИГРА"`), не создавая ложные слоты для DLC; текстовая эвристика игнорирует слэши внутри скобок и дополнения после ` + `.
- 🌟 **[RuTracker: ручное объединение слотов игр на лету]**: На каждом слоте внедрён выпадающий список (ComboBox): выбор другого номера мгновенно объединяет все файлы, ссылки и метаданные в целевой слот без дыр в нумерации.
- 🌟 **[RuTracker: сворачивание папок распакованных модов в одну запись]**: Распакованные папки русификаторов (`atmosphere/contents/...`, `romfs/`, `exefs/`, как в *Cadence of Hyrule*) сворачиваются в одну строку `МОД / РУСИФИКАТОР` с суммарным размером и сохранением привязки `DetectedTitleId`.
- 🌟 **[RuTracker UI: расширение списка файлов раздачи]**: Блок «Файлы в раздаче» расширен влево под список раздач (`Grid.ColumnSpan="2"`), предоставляя более 1120 пикселей ширины для полного отображения путей и бейджей.
- 🌟 **[RuTracker UI: устранение моргания при параллельных закачках]**: Внедрён список активных загрузок с отдельной строкой на каждую игру, независимыми прогрессбарами, кнопками отмены и общим ровным индикатором скорости.
- 🌟 **[RuTracker UI: гарантированная очистка ссылок]**: При смене темы список внешних ссылок мгновенно очищается, исключая отображение ссылок от предыдущей раздачи.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[NormalizeSwitchBaseApplicationTitleId]**: Алгоритмический расчёт базового Application TitleID по маске `0xFFFFFFFFFFFFF000 - 0x1000` для DLC и `...000` для обновлений.
- 🔹 **[DiscoverGameTitlesFromFiles]**: Фильтрация слотов исключительно по настоящим базовым играм, исключая дублирование DLC слотов.
- 🔹 **[GameSlotNumberCombo_SelectionChanged & MergeGameSlotIntoTarget]**: Интерактивное слияние слотов игр прямо в интерфейсе раздачи.
- 🔹 **[CollapseModFoldersIntoSingleEntries]**: Умное объединение сотен файлов распакованных модов в единый элемент с сохранением всех путей через `GetEffectiveRelativePaths()`.
- 🔹 **[Layout Grid.Row 3 & 4]**: 2-строчная сетка рабочих карточек с широким списком файлов и панелью загрузок без мерцания.
- 🔹 **[RuTrackerActiveDownloadItem & UiAggregationTimer]**: Реактивная потокобезопасная агрегация телеметрии P2P и облака каждые 300 мс.
- 🔹 **[LocalizationService.UiTree]**: 100% перевод всех новых элементов интерфейса на 6 поддерживаемых языков.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker Switch TitleID Normalization & DLC Linking]**: Added `NormalizeSwitchBaseApplicationTitleId` which computes the base 16-character TitleID for updates and DLCs, ensuring base games, patches, and add-ons are unified under the same game slot.
- 🔸 **[RuTracker Phantom Slots Prevention]**: `DiscoverGameTitlesFromFiles` now groups only by real base game files (`CategoryBadge == "ИГРА"`), preventing DLCs from creating phantom second game slots. Text heuristic ignores slashes inside brackets and add-on names after ` + `.
- 🔸 **[Manual Game Slot Merging UI]**: Added a styled dropdown on each game slot badge allowing users to merge slots (e.g. `GAME #2 -> GAME #1`) on the fly with automatic file, link, and metadata migration.
- 🔸 **[Collapse Mod Folders into Single Entries]**: Folders with unpacked mod trees (`atmosphere/contents/...`) are automatically collapsed into a single `MOD / TRANSLATION` entry with total size calculation and unified multi-file download.
- 🔸 **[Extended Torrent Files Panel]**: The files panel now spans columns 0 and 1 (`Grid.ColumnSpan="2"`) under the topic list (~1120+ px wide) for complete readability of long paths.
- 🔸 **[Flicker-Free Multi-Download Manager]**: Clean items list with dedicated progress bars, speed metrics, cancel buttons per game, and an aggregated header speedometer.
- 🔸 **[Instant Link Clearance on Topic Switch]**: Prevents stale links from previous releases from lingering when switching topics.
- 🔸 **[Full 6-Language Localization]**: Complete coverage for all new UI text across English, German, French, Chinese, and Japanese.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
