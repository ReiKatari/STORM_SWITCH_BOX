> 💡 **Релиз 6.9.4** — *Полное исправление структуры заголовков NCA3 и RomFS при компиляции дампов Nintendo Switch 2, иерархические таблицы директорий и штатное AES-128-XTS шифрование заголовков*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Switch 2 Dump Compiler (`Switch2DumpCompilerService`)] Полное исправление структуры заголовков NCA3 и RomFS**: при сборке дампов игр Nintendo Switch 2 (`0400...`) в `.nsp` / `.nsz` / `.xci` / `.xcz` обеспечена 100% совместимость со спецификацией Horizon OS и эмуляторами Switch 2.
- 🌟 **[Meta NCA & SdkAddonVersion] Устранение сбоя Bad Header**: внедрена запись версии SDK (`SdkAddonVersion = 0x000E0000`) по смещению `0x21C` в `Meta NCA (.cnmt.nca)`, полностью устраняющая критическую ошибку эмуляторов `(0007-000C): The NCA file has a bad header`.
- 🌟 **[RomFS Partition & IVFC Layout] Коррекция типов файловой системы и метаданных целостности**: исправлен код типа файловой системы `NcaFsHeader::FsType` для разделов `RomFS` (`0` вместо `2`) в `Control NCA` и `Program NCA`, внедрена разметка структуры `IntegrityMetaInfo (IVFC)` с полем `master_hash_size = 0x20` по смещению `0x10`, `max_layers = 7` по смещению `0x14` и выравниванием дескрипторов 6 уровней хешей по смещениям `0x18..0xA7`.
- 🌟 **[Иерархические таблицы RomFS и AES-128-XTS шифрование]**: реализован полноценный иерархический генератор таблиц директорий и файлов Nintendo RomFS (`Directory Meta Table` + `File Meta Table` + хеш-бакеты с простыми числами) вместо плоского списка путей, а также штатное шифрование заголовков `0x000..0xBFF` ключом `header_key` (`AES-128-XTS`) для прозрачной совместимости с эмуляторами и блочным сжатием `NSZ / NCZ`.
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.4**: обновлены версия и код сборки (`versionCode = 694`, `versionName = "6.9.4"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) с синхронизированным ядром компиляции дампов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[SdkAddonVersion в Meta NCA]**: добавлена запись версии SDK `0x000E0000` в `BuildCnmtNca` по смещению `0x21C`.
- 🔹 **[Коррекция RomFS FsType]**: исправлена константа типа файловой системы `FsType::RomFs = 0` для всех создаваемых разделов NCA.
- 🔹 **[Выравнивание IVFC дескрипторов]**: выровнены дескрипторы уровней IVFC хеш-дерева со смещения `0x18` с установкой размера мастер-хеша `0x20` и `max_layers = 7`.
- 🔹 **[Иерархический генератор RomFS]**: разработаны структуры `Directory Meta Table` и `File Meta Table` с расчетом хеш-бакетов по простым числам.
- 🔹 **[Штатное шифрование AES-128-XTS]**: внедрен метод `FinalizeAndEncryptNcaHeaderInPlace` с шифрованием 6 секторов заголовка NCA ключом `header_key`.
- 🔹 **[Парсинг версий в CatalogScannerService]**: улучшено автоопределение версий и парсинг парных версий в имени файлов.
- 🔹 **[Инсталлятор StormInstaller]**: конфигурация сборки и метаданные развёртывания обновлены до версии 6.9.4 с сохранением цифровой подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Switch 2 Dump Compiler (`Switch2DumpCompilerService`)**: complete overhaul of NCA3 and RomFS header structures when assembling Nintendo Switch 2 game dumps (`0400...`) into `.nsp` / `.nsz` / `.xci` / `.xcz`.
- 🔸 **SDK Addon Version Fix**: written SDK version (`SdkAddonVersion = 0x000E0000`) at offset `0x21C` in Meta NCA (`.cnmt.nca`), resolving emulator error `(0007-000C): The NCA file has a bad header`.
- 🔸 **RomFS FsType & IVFC Integrity Alignment**: corrected `NcaFsHeader::FsType` for RomFS partitions (`0` instead of `2`) in Control and Program NCAs; aligned `IntegrityMetaInfo (IVFC)` with `master_hash_size = 0x20` at `0x10`, `max_layers = 7` at `0x14`, and 6-level hash descriptors from `0x18..0xA7`.
- 🔸 **Hierarchical RomFS & AES-128-XTS Header Encryption**: added hierarchical directory/file meta tables with prime-number hash buckets and standard AES-128-XTS header encryption (`0x000..0xBFF`) for emulator and NSZ/NCZ compatibility.
- 🔸 **Android Parity 6.9.4**: synchronized Modern and Legacy Android packages to version 6.9.4 (`versionCode 694`).

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
