> 💡 **Релиз 7.0.14** - *Критическое исправление совместимости собранных игр Nintendo Switch 1 со стандартными эмуляторами (Ryujinx, Eden, Citron, Yuzu) и инструментарием (hactoolnet, nsz): устранены сбои заголовков PFS0 и XCI HFS0, искажение смещений NCZ с секциями от 0xC00, нарезки файлов > 32 МБ на Android, принудительного Hard-Patch 1G+1U с конфликтом CNMT, перехвата распакованных папок Switch 1 и перепаковки входных XCI/XCZ*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 7.0.14)

- 🌟 **[Android PFS0 Alignment & ExeFS/CNMT Integrity Fix (`StormNczEngine.kt`)]**: Исправлен расчёт размера таблицы строк `PFS0` (`stringTableSize` теперь включает выравнивание до `alignedHeaderSize` кратно `0x20`) в трёх сборщиках NCA для `compileSwitch2DumpPackage`. Ранее данные начинались после выравнивания, из-за чего `ExeFS` и `.cnmt` внутри собранных NCA читались как нули.
- 🌟 **[Android >32 MB Slice Copy & XCI/XCZ Output Support (`StormNczEngine.kt`)]**: Устранён критический баг, при котором непережатые разделы `> 32 МБ` со смещением `offset > 0` передавали в упаковщик `src.parentFile` целиком с нуля вместо нужного фрагмента NCA. Также `compileSwitch2DumpPackage` теперь корректно собирает `XCI` и `XCZ` при выборе картриджного формата (`packXciOrXczContainer`).
- 🌟 **[Android 1G+1U Multi-Content & Hard-Patch Parity (`StormNczEngine.kt`)]**: Возвращена логика сборки `5.5.7` - обычные `1G+1U` наборы больше не подвергаются принудительному `Hard-Patch` (он запускается только при вшивании модов/русификаторов или обновлении уже монолитной базы), а сам `Hard-Patch` больше не создаёт конфликтующий второй `...800.cnmt.nca` с тем же `progSha256`. Восстановлены запрет сжатия `BKTR`, запрет подстановки `RightsId` вместо реального `titleKey`, проверка ключей в режиме `reader != null`, определение папок Switch 2 строго по `ProgramId` `04...` в `main.npdm` и синхронное обновление `.cnmt.nca` при кастомизации `Control NCA`.
- 🌟 **[Windows NCZ 0xC00 Offset Mapping Fix (`StormNczStorage.cs`)]**: Исправлено отображение смещения внутри Zstd-потока (`streamOffsetInSection = currentOffset - Math.Max(targetSection.Offset, 0x4000)`) для NCZ с началом секции до `0x4000` (например `0xC00` у `PublicData`/DLC). Устранено пропускание первых `0x3400` байт потока и ошибки `Hash error (2002-4604)`.
- 🌟 **[Windows XCI 0x138 Header & XCI/XCZ Unpack/Repack Pipeline (`StormXciBuilder.cs`, `MultiGameCartridgeService.cs`, `MultiContentService.cs`, `NszCompressionService.cs`)]**: Исправлена запись поля `0x138` (`RootHfs0HeaderSize`) в заголовке XCI (размер заголовка HFS0 вместо размера всего раздела - восстановлено прохождение `Partition Header Hash (GOOD)` в `hactoolnet`). Исправлена перепаковка входных `.xci` в `NSZ`/`XCZ`/`NSP`, отключено ошибочное копирование `.xci` байт-в-байт в `.nsp`, а Zero-Disk-IO fallback для `.xcz` теперь читает раздел `/secure` и формирует валидный `.nsp`.
- 🌟 **[Windows CNMT Collision, hacpack ExitCode & Null Progress Guards (`ControlEditorService.cs`, `SwitchFormatService.cs`, `Switch2DumpCompilerService.cs`)]**: В `ControlEditorService` введено точное сравнение 16-символьного `TitleId` без коллизий базы `...000` и патча `...800`. В `RunHacpackAsync` добавлена проверка кода возврата (ошибочные неполные NCA больше не попадают в контейнер), а в `BuildStreamedRomFs` исправлен вылет `NullReferenceException` при отсутствии коллбэка прогресса.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Core/NSZ/StormNczStorage.cs]**: Корректный расчёт `streamOffsetInSection` относительно `Math.Max(targetSection.Offset, ncaHeaderSize)` для секций `< 0x4000` (`0xC00`).
- 🔹 **[Core/NCA/StormXciBuilder.cs & Services/MultiGameCartridgeService.cs]**: Запись точного `hfs0HeaderSize` в смещение XCI `0x138` (`RootPartitionHeaderSize`) для 100% соответствия `SHA-256` в `0x140`.
- 🔹 **[Services/MultiContentService.cs]**: Поддержка входных `.xci`/`.xcz` через `OpenSwitchContainerFileSystem` (`/secure`) при сборке в сжатые и несжатые форматы; сохранение `.xci` при распаковке через `nsz.exe`.
- 🔹 **[Services/NszCompressionService.cs]**: В Zero-Disk-IO декомпрессии `.xcz` открывается раздел `/secure` вместо корневого `HFS0` с сохранением корректного расширения `.nsp`.
- 🔹 **[Services/ControlEditorService.cs]**: Строгое сравнение `TitleId` при сопоставлении `Control` и `Meta` NCA вместо префикса из 13 символов.
- 🔹 **[Services/SwitchFormatService.cs & Services/Switch2DumpCompilerService.cs]**: Явный контроль `ExitCode` в `RunHacpackAsync` и безопасный вызов `onProgress?.Invoke(...)` в `BuildStreamedRomFs`.
- 🔹 **[android/.../StormNczEngine.kt]**: Выровненная запись `stringTableSize` в `PFS0`, потоковая передача `Pfs0Item(name, file, offset, size)` для срезов `> 32 МБ`, отмена авто-HardPatch для обычных `1G+1U` и удаление дублирующего `.800.cnmt.nca`, запрет подмены `RightsId` как ключа, защита `BKTR` от сжатия, изоляция папок Switch 1 по `ACI0 ProgramId`, поддержка `XCI`/`XCZ` в `compileSwitch2DumpPackage` и пересборка `.cnmt.nca` при кастомизации `Control`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 7.0.14)

- 🔸 **[Android PFS0 Alignment & ExeFS/CNMT Integrity Fix (`StormNczEngine.kt`)]**: Fixed unaligned `PFS0` `stringTableSize` header writes across three NCA builders in `compileSwitch2DumpPackage`, preventing zero-filled `ExeFS` and `.cnmt` reads inside generated NCAs.
- 🔸 **[Android >32 MB Slice Streaming & XCI/XCZ Export (`StormNczEngine.kt`)]**: Fixed `> 32 MB` non-zero-offset NCA slices embedding the entire `src.parentFile` instead of copying `(offset, size)` via `Pfs0Item`; enabled `packXciOrXczContainer` output in `compileSwitch2DumpPackage`.
- 🔸 **[Android 1G+1U Multi-Content & Hard-Patch Parity (`StormNczEngine.kt`)]**: Restored 5.5.7 `1G+1U` multi-content packing (no automatic Hard-Patch unless applying mods or updating a monolith), removed conflicting duplicate `...800.cnmt.nca` output, restored `BKTR` uncompressed guard, removed fake `RightsId` title key fallback, restored `reader` key verification, restricted Switch 2 folder detection to `04...` `ACI0` `ProgramId`, and synchronized `.cnmt.nca` hashes when customizing Control NCAs.
- 🔸 **[Windows NCZ 0xC00 Offset Mapping Fix (`StormNczStorage.cs`)]**: Fixed Zstd stream offset calculation (`currentOffset - Math.Max(targetSection.Offset, 0x4000)`) for `0xC00`-offset NCZ sections, eliminating `Hash error (2002-4604)`.
- 🔸 **[Windows XCI Header 0x138 & Input Container Pipeline (`StormXciBuilder.cs`, `MultiGameCartridgeService.cs`, `MultiContentService.cs`, `NszCompressionService.cs`)]**: Fixed XCI `0x138` `RootPartitionHeaderSize` (`hactoolnet` `Partition Header Hash (GOOD)`), fixed `.xci` input handling when targeting compressed formats, prevented raw `.xci` copy into `.nsp`, and fixed `.xcz` Zero-Disk-IO fallback to mount `/secure` as `.nsp`.
- 🔸 **[Windows CNMT Collision, hacpack ExitCode & Null Progress Guards (`ControlEditorService.cs`, `SwitchFormatService.cs`, `Switch2DumpCompilerService.cs`)]**: Enforced exact 16-char `TitleId` matching in `ControlEditorService`, checked `hacpack.exe` exit codes, and guarded null `onProgress` callbacks in `BuildStreamedRomFs`.

</details>

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.14_Setup.exe** (230.52 МБ)
  - `SHA-256: 305032707C350864C6EEC1BBBB00560A001E7A3A0C8CC8050672DF8A2614C470`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.14_Portable.zip** / **STORM_SWITCH_BOX_7.0.14.zip** (162.07 МБ)
  - `SHA-256: 4D6DB93AC0E5502A56F811F92B55221E3DF9C6979A80D0F371392849F2257F69`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.14_Android.apk** / **STORM_SWITCH_BOX_7.0.14.apk** (14.37 МБ)
  - `SHA-256: 196BFC9F87C3D3FC3BEA54C43395FD9474DCFED4E330861C604B4409C941D1D1`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.14_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.14_LEGACY.apk** (25.94 МБ)
  - `SHA-256: FEBEC7555ABBC3B2CEB59B3571949F595B65A9FC85C98043CB5DB04DDC938965`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
