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

- 💻 **STORM_SWITCH_BOX_7.0.9_Setup.exe** (230.47 МБ)
  - `SHA-256: E230A9EF2A90404FFBD2CF2FE2FDE3644DFBA2D391ECEF9950C70B9EF0CC0187`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.9_Portable.zip** / **STORM_SWITCH_BOX_7.0.9.zip** (162.02 МБ)
  - `SHA-256: 1DC688A06EB9D17D908C054605BF8B18D092F9BD05FE43F20B59899B830F4C46`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.9.apk** (14.36 МБ)
  - `SHA-256: 9D29592E5A06683D17CC7E913C727B98FF4FE09B2B7F3759F8C3E1A4DF8F66C7`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.9_LEGACY.apk** (25.93 МБ)
  - `SHA-256: 93A86C62AC1A884CF20C9B049B8935DD7D4BB96CCBB79226C14ECE47259A32AB`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
