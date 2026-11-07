> 💡 **Релиз 7.0.8** — *Защита от непрерывного сжатия Zero-Solid NCZ Guard (1 МБ NCZBLOCK), модульная распаковка NSP/NSZ/XCI/XCZ со слиянием 1G+1U BkTR, бескэшевый стриминг BlockNczByteReader на Android без дублирования памяти и персистентное сохранение обложек, задач и метрик сжатия*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Zero-Solid NCZ Guard (NCZSECTN -> 1 МБ NCZBLOCK)]**: Автоматическое выявление и динамическое пережатие устаревших непрерывных Solid NCZ (`NCZSECTN`) в стандартный блочный формат `NCZBLOCK` с шагом 1 МБ при сборке мульти-контента, сжатии и конвертации. Это обеспечивает мгновенный запуск и чтение игр в эмуляторах без необходимости предварительной распаковки гигабайтного кэша во временные директории.
- 🌟 **[Windows: Полное обновление движка распаковки (NSP / NSZ / XCI / XCZ, 1G+1U BkTR, Multi-DLC)]**: Устранены сбои при обработке малых `.ncz` файлов (< 1024 байт), добавлена поддержка чтения разделов `HFS0 / Secure` для картриджей `XCI / XCZ`, внедрено автообнаружение `prod.keys` в путях установленных эмуляторов, реализовано автоматическое слияние базы и патча обновления (`BkTR`) при распаковке `1G+1U` контейнеров, изолированы выходные папки `<Имя>_Unpacked` и обеспечено безопасное размещение временных потоков `STORM_TMP` на накопителе с максимальным объёмом свободного места.
- 🌟 **[Android: Прямая потоковая распаковка без дублирования памяти (Zero-Bloat и BlockNczByteReader)]**: Внедрён движок `BlockNczByteReader` с 4 МБ LRU-кэшем в оперативной памяти и прямой сквозной стриминг `PFS0`. Полностью исключено создание промежуточных 14+ ГБ копий `temp_program.nca`, дублирование в `atmosphere/contents` и авто-дамп `pfs0/` (расход дискового пространства при распаковке 14 ГБ игры снижен с 30–56 ГБ до ~14 ГБ). Добавлена предварительная проверка доступного пространства `StatFs` и перехват системной ошибки `ENOSPC` с автоочисткой неполных файлов и информативным диалогом.
- 🌟 **[Сохранение состояния задач, обложек и метрик сжатия при перезапуске]**: Реализовано многоуровневое кэширование и восстановление постеров (`IconCache` / `TitleDB` / `RuTracker`), персистентное сохранение списка входных каталогов и корректный перерасчёт параметров «Нач. размер», «Кон. размер», «Разница» и коэффициента сжатия после паузы или перезапуска приложения.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Zero-Solid NCZ Guard]**: Перехват и переупаковка `NCZSECTN` в `NCZBLOCK` (1024 КБ) в реальном времени при сжатии и сборке.
- 🔹 **[Advanced Unpack Pipeline]**: Обработка `NSP`, `NSZ`, `XCI`, `XCZ` с извлечением `HFS0/Secure`, расшифровкой и применением патчей обновления `BkTR` на лету.
- 🔹 **[Smart Keys Auto-Discovery]**: Автоматический поиск и загрузка ключей `prod.keys` и `title.keys` из системных каталогов Yuzu, Ryujinx, Citron, Eden и Suyu.
- 🔹 **[BlockNczByteReader LRU Cache]**: Прямой поблочный доступ к сжатым секциям NCZ через кольцевой 4 МБ LRU-буфер на Android без создания временных файлов на диске.
- 🔹 **[Zero-Bloat Memory Footprint]**: Снижение пикового расхода памяти на Android до фактического размера распакованных данных (~14 ГБ вместо 30–56 ГБ).
- 🔹 **[StatFs & ENOSPC Guard]**: Упреждающий контроль свободного места на накопителе устройства с защитой от повреждения файловой системы при исчерпании квоты.
- 🔹 **[Persistent Task State]**: Бесшовное сохранение очереди, статусов прогресса, списка каталогов и вычисленных объёмов экономии диска между сессиями.
- 🔹 **[Multi-Tier Icon Cache]**: Кэширование игровых обложек в локальном хранилище с запасными каналами TitleDB и сетевых метаданных.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Zero-Solid NCZ Guard (NCZSECTN -> 1 MB NCZBLOCK)]**: Automatically detects and recompresses legacy streaming solid NCZ files into 1 MB block-based `NCZBLOCK` format during multi-content packaging, compression, and conversion, enabling instantaneous loading in emulators without multi-gigabyte cache decompression.
- 🔸 **[Windows: Unpack Engine Overhaul (NSP / NSZ / XCI / XCZ, 1G+1U BkTR, Multi-DLC)]**: Fixed small `.ncz` (< 1024 bytes) decompression edge cases, added `HFS0 / Secure` partition extraction for `XCI / XCZ`, integrated automatic `prod.keys` discovery from installed emulators, implemented automated base and update patch (`BkTR`) merging for `1G+1U` containers, safely routed `STORM_TMP` streams to the drive with maximum free space, and isolated output folders.
- 🔸 **[Android: Direct Zero-Bloat Stream Decompression (BlockNczByteReader)]**: Implemented `BlockNczByteReader` with 4 MB in-memory LRU cache and direct `PFS0` streaming, eliminating 14+ GB temporary `temp_program.nca` copies, `atmosphere/contents` duplicates, and auto-dump `pfs0/` folders (reducing storage overhead from 30–56 GB down to ~14 GB for a 14 GB game). Added pre-flight `StatFs` storage checks and robust `ENOSPC` handling with clean file rollbacks.
- 🔸 **[Session Persistence: Task State, Game Covers, and Compression Metrics]**: Multi-tiered cover caching and recovery (`IconCache` / `TitleDB` / `RuTracker`), persistent input folder history, and accurate recalculation of initial size, final size, space saved, and compression ratio across application restarts.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
