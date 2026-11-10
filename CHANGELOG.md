## **7.0.10** (10.11.2026)

> 💡 **Релиз 7.0.10** — *Каноническая спецификация оглавления NUP v1.0 (TOC 0x30, 64-битные счетчики каталогов и файлов, двойное автоопределение TOC), чтение метаданных TLV по абсолютным смещениям контейнера, сверхбыстрое синхронное извлечение обложек (< 1 мс), устранение коллизий кэша иконок задач и сквозная материализация структуры Control на Windows и Android*

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

> 💡 **Релиз 7.0.9** — *Полный паритет Nintendo Switch 2 (эталонный хэш RomFS, розничный header_key, нативный RSA-3072 ACID, автоопределение версий NACP/CNMT и упаковка DLC), поддержка контейнеров .nup, блочное сжатие NoCrypto NCA3 и упреждающий контроль дискового пространства*

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
- 🔹 **[Intelligent TitleDB Cleanup]**: Нормализация названий игр с удалением суффиксов «– Update ...» и «+ DLC» для надёжного сопоставления в базе данных.

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


## **7.0.7** (07.11.2026)

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


## **7.0.6** (07.11.2026)

> 💡 **Релиз 7.0.6** — *Хотфикс автономного установщика StormInstaller (устранение IndexOutOfRangeException при установке на всех 11 языках и безопасный доступ к локализации T(index)), стабилизация статуса очереди RuTracker и полная мультиязычность*

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


# **7.0.3** (07.11.2026)

## **7.0.4**

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


---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: 3-уровневая архитектура максимальной скорости (Tier 1 + Tier 2 + Tier 3)]**: Ликвидирована регрессия скорости загрузки (0.45 МБ/с, 1 сид) — восстановлен полноценный разгон до 15–25+ МБ/с и поддержка сотен сидов в рое. Внедрена отказоустойчивая трёхуровневая архитектура: **Tier 1** (внешний qBittorrent WebUI) -> **Tier 2** (встроенный C++ `libtorrent-rasterbar 2.1.1` с uTP BEP-29 и Holepunch BEP-55) -> **Tier 3** (высокоскоростной MonoTorrent).
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
- 🌟 **[RuTracker: неблокирующий пакетный запуск нескольких загрузок подряд]**: Добавлены изолированные сессии `CreateIsolatedDownloadSession()` с динамическим портом `0` и отдельными подпапками пакетов — теперь сразу после старта скачивания можно выбрать другой набор файлов или следующую раздачу и запустить новую загрузку без отмены текущей и без переоткрытия окна.
- 🌟 **[Windows: полное восстановление Распаковки, Упаковки, Обновления, Конвертации и Инспектора RomFS/ExeFS]**: Устранено отравление `ExternalKeySet` через `RegisterRightsIdFallback`, исправлена расшифровка `TitleKey` через `TitleKek` в `StormNczStorage` и устранено двойное шифрование билетов `.tik`, возвращена прямая маршрутизация режимов `Convert`, `Pack` и `Update` в `TasksViewModel` без перехвата в `Multi`, устранены дедлоки пайпов и поддержана обратная сборка распакованных папок `exefs/` + `romfs/` в загружаемые `NSP`/`NSZ`/`XCI`.
- 🌟 **[Android: полноценная криптография `.tik` (`TitleKey`), потоковая Распаковка 13+ ГБ, сборка `NCA3` (`IVFC Lv1–Lv6`) и Инспектор RomFS]**: Внедрён сбор и расшифровка билетов `.tik` (`titlekek_XX` AES-128-ECB) перед распаковкой и Zstd-сжатием `NCZ`, прямое чтение слайсов `RomFS`/`ExeFS` без переполнения памяти, замена заглушек упаковки и виртуального проводника на полноценную компиляцию `Program/Control/Meta NCA3` и чтение реального дерева `IVFC Level 6`.

### 📋 Полный список изменений
- 🔹 **RuTrackerModService.cs**: Добавлена очистка граничных файлов BitTorrent (piece boundary leftovers), селективное применение `Priority.DoNotDownload` по маске отмеченных элементов и динамическое выделение изолированных торрент-сессий `CreateIsolatedDownloadSession()` с отдельными рабочими директориями.
- 🔹 **RuTrackerModDownloaderDialog.xaml.cs**: Обеспечен неблокирующий запуск загрузок с сохранением открытого окна и возможностью параллельного скачивания других раздач, реализован реактивный пересчёт выбранного объёма данных и версии игры при переключении чекбоксов.
- 🔹 **KeysService.cs & TicketHarvesterService.cs**: Защита `ExternalKeySet` от фиктивных RightsID через метод `RegisterRightsIdFallback`, корректное дешифрование зашифрованного TitleKeyBlock через соответствующий master/title kek и предотвращение записи нулевых ключей.
- 🔹 **StormNczStorage.cs & SwitchFormatService.cs**: Восстановление корректной последовательности расшифровки заголовков NCA и сжатия блоков NCZ с сохранением валидной структуры билетов `.tik` без повторного шифрования.
- 🔹 **TasksViewModel.cs & MultiContentService.cs**: Исправлена маршрутизация задач в очереди: режимы `Convert`, `Pack` и `Update` теперь исполняются через соответствующие целевые конвейеры без принудительного перехвата в multi-merge.
- 🔹 **VirtualRomFsInspectorService.cs**: Полная ликвидация блокировок анонимных каналов при чтении списков файлов RomFS/ExeFS, корректная распаковка выбранных поддиректорий и сборка готовых контейнеров из папок.
- 🔹 **Android StormNczEngine.kt & MainActivity.kt**: Реализация аппаратной расшифровки билетов `.tik` по таблице `titlekek_XX`, потоковое чтение больших контейнеров (13+ ГБ) с нулевым оверхедом по памяти, компилятор `NCA3` с генерацией структур `IVFC` уровней 1–6 и интеграция инспектора RomFS в мобильный интерфейс.


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
