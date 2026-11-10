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

