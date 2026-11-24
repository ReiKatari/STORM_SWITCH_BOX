> 💡 **Релиз 8.2.0** - *Полная поддержка всех 17+1 нативных форматов Nintendo 3DS (включая единые бандлы без установки `.bcci`, `.bcxi` и `.bcia`), справочник и умные рекомендации форматов на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Бандлы без установки `.bcci`, `.bcxi` и `.bcia` («Игра + Патч + DLC + Мод» в 1 файле)]**: Внедрён бинарно-точный контейнер `microtar` + `Z3DSMetadata` для **STORM 3DS** и **Azahar** на Windows и Android. **`.bcci` (рекомендуется №1)** упаковывает сжатую игру `.zcci` + обновления `.zcia` + все DLC `.zcia` без установки в `sdmc` и без 2x расхода памяти, а **`.bcxi`** даёт абсолютный минимум размера (только игровой Partition 0 NCCH `.zcxi` + `.zcia`). Сборка без модов проходит без тяжёлой распаковки RomFS.
- 🌟 **[Полный охват 17+1 форматов 3DS]**: Добавлена поддержка **`.bcci`**, **`.bcxi`**, **`.bcia`**, **`.zcci`**, **`.zcxi`**, **`.zcia`**, **`.z3dsx`**, **`.z3ds`**, **`.cci`**, **`.3ds`**, **`.cxi`**, **`.cia`**, **`.3dsx`**, **`.app`**, **`.cfa`**, **`.elf`**, **`.axf`** и **`.artic`**, включая быструю вырезку Partition 0 NCCH (`3DS/CIA -> CXI`), обёртку `CXI/APP -> CCI`, виртуальный инспектор `/bundle/...`, Аудитор, Умные папки и верификатор целостности.
- 🌟 **[Умные рекомендации и обновлённая Инструкция]**: Для каждого формата 3DS на Windows и Android добавлены подробные описания, живые рекомендации с выделением **`.bcci` / `.bcxi`** и обновлённая мультиязычная Инструкция (11 языков на ПК, 10 языков на Android).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Windows: ThreeDsBundleService.cs и Nintendo3dsService.cs]**: Новый модуль `ThreeDsFormatCatalog` и потоковый движок `microtar` (`CreateBundleAsync`, `ExtractBundleAsync`, `VerifyBundleIntegrity`, `TryOpenMainEntryTransparentStream`), быстрая вырезка Partition 0 (`ExtractNcsdPartition0ToCxiAsync` / `ExtractCiaContent0ToCxiAsync`), `WrapCxiToCciReliableAsync`, `Z3dsService.VerifyZ3dsIntegrity(Stream)` и полная поддержка 18 расширений во всех сервисах (WatchFolder, Auditor, Inspector, History, DirectConsoleSender, RuTrackerModService).
- 🔹 **[Windows: Интерфейс, Инструкция и Локализация]**: Значение по умолчанию 3DS переведено на `BCCI` (при сохранении совместимости с сохранённым выбором пользователя); в `TasksPage` и `SettingsPage` добавлены все форматы, кнопка-справочник и карточка рекомендаций; обновлена `InstructionPage` и локализация на 11 языках.
- 🔹 **[Android: StormNczEngine.kt и MainActivity.kt]**: Реализован чистый Kotlin-движок TAR-бандлов `microtar` + `Z3DSMetadata` (`build3dsBundlePackage`, режимы `13=BCCI ★`, `14=BCXI ★`, `15=BCIA`, `16=CXI`, `17=3DSX`), исправлена группировка 3DS TitleID (`00040000`/`0004000E`/`0004008C`), добавлены панель рекомендаций, диалог-справочник всех 17+1 форматов, тема справки №9 и юнит-тесты `ThreeDsBundleAndFormatsTest.kt`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Implemented bit-exact `microtar` + `Z3DSMetadata` zero-install bundle formats (`.bcci`, `.bcxi`, `.bcia`) for `STORM 3DS` and `Azahar` on Windows (C#) and Android (Kotlin), combining compressed base games (`.zcci` / `.zcxi`), updates (`.zcia`), DLCs (`.zcia`), and baked mods in a single file with instant zero-unpack build support when no RomFS/ExeFS mods are applied.
- 🔸 Added all remaining Nintendo 3DS native formats (`17+1`: `.bcci`, `.bcxi`, `.bcia`, `.zcci`, `.zcxi`, `.zcia`, `.z3dsx`, `.z3ds`, `.cci`, `.3ds`, `.cxi`, `.cia`, `.3dsx`, `.app`, `.cfa`, `.elf`, `.axf`, `.artic`), fast Partition 0 `NCSD/CIA -> CXI` extraction, and `CXI/APP -> CCI` wrapping.
- 🔸 Added per-format descriptions, live recommendation banners highlighting `.bcci` and `.bcxi`, and updated interactive user guides on Windows and Android.

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.0_Setup.exe`** (`219.80 МБ`, SHA-256: `2405EFFEDAB6F791B0793D09D1BAF871C8F93AF1846ED4BF39888FEC54982117`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.0_win-x64.zip` / `STORM_SWITCH_BOX_8.2.0.zip`** (`151.36 МБ`, SHA-256: `ED26302D95C6774BF03698249F93E6EBD3FCF7A83B6AEE6B4D0B0BCFBF5531CA`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.0.apk`** (`14.47 МБ`, SHA-256: `3CE59BA6832FA14F2AB3ACCF459A74CA36466DA43E7C5C11D0BEDDA126012106`) - **Modern** сборка Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 📱 **`STORM_SWITCH_BOX_8.2.0_LEGACY.apk`** (`26.04 МБ`, SHA-256: `4E998711420B858F97EA52EE8A9BCF156B57D64566F4117E93A891859D5B4102`) - **Legacy** универсальная сборка Android 5.0+ (`minSdk 21`, `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
