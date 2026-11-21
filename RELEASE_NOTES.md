> 💡 **Релиз 8.0.6** - *Умный автовыбор только оригинальных файлов раздачи, структурированная группировка по категориям и чистое копирование названий дополнений (DLC)*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.6)

- 🌟 **[Отключение модов, русификаторов и сэйвов по умолчанию и точная версия патча (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена ошибка автовыбора, из-за которой русификаторы, сторонние модификации, привязанные к ним старые обновления (`ОБНОВЛЕНИЕ (РУС)`), сохранения и текстовые заметки выбирались автоматически и подменяли итоговую версию патча в шапке слота игры (например, в раздаче `Xenoblade Chronicles X Definitive Edition` теперь автоматически выбирается актуальный официальный патч `131072` вместо устаревшего `65536` от русификатора).
  - По умолчанию в списке файлов раздачи автоматически выбираются только оригинальные файлы игры (`ИГРА`), единственное новейшее официальное обновление (`ОБНОВЛЕНИЕ`), оригинальные дополнения (`ДОПОЛНЕНИЯ`) и `UNLOCKER`. Категории `МОДЫ`, `МОД / РУСИФИКАТОР`, `ОБНОВЛЕНИЕ (РУС)`, `СЭЙВЫ`, `ИНФО`, `ФАЙЛ` и устаревшие патчи сняты по умолчанию.
- 🌟 **[Структурированная группировка файлов в раздаче по категориям (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - В блоке «Файлы в раздаче» внутри каждого слота игры файлы сгруппированы в строгом порядке приоритета: `ИГРА` -> `ОБНОВЛЕНИЕ` -> `ДОПОЛНЕНИЯ` -> `UNLOCKER` -> `МОДЫ` -> `ОБНОВЛЕНИЕ (РУС)` -> `СЭЙВЫ` -> `ИНФО`.
  - Для каждой группы добавлены цветной бейдж категории, счётчик выбранных файлов, суммарный объём и кнопка массового переключения галочек категории одним кликом.
- 🌟 **[Чистое копирование названий дополнений (DLC) по стандарту STORM GAMES WORLD (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `CatalogPage.xaml`, `CatalogPage.xaml.cs`)]**:
  - Реализована функция `ExtractCleanDlcName`, очищающая названия дополнений от имени игры, тега `DLC`, `[TitleID]`, версии `[v0]` и расширения файла (например, `Xenoblade Chronicles X Definitive Edition [DLC Early Purchase Bonus Exploration Support Pack] [0100453019AA9001][v0].nsp` -> `Early Purchase Bonus Exploration Support Pack`).
  - Добавлена кнопка копирования названия рядом с каждым DLC и кнопка массового копирования списка «Копировать все DLC» как в загрузчике RuTracker, так и во всплывающем списке дополнений в разделе «Информация».

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `IsSaveEntryPath`, `IsUnlockerEntryPath`, `GetCategoryGroupKey`, `GetCategoryGroupColors`, `RuTrackerFileCategoryGroup` и `ExtractCleanDlcName`; обновлены `ClassifyTorrentEntryBadge`, `GetRoleSortOrder`, `CollapseModFoldersIntoSingleEntries` и `PopulateDisplayPathsAndSmartSelection` с отказом от автовыбора русификаторских патчей и модов; расширен `TorrentFileSelectionItem` полями `IsDlcItem`, `CleanDlcName`, `CopyDlcButtonVisibility` и `CopyDlcTooltip`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml, Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Список файлов переведён на двухуровневую группировку по `CategoryGroups`, добавлены обработчики `ToggleCategoryGroupSelection_Click`, `CopyDlcName_Click`, `CopyAllDlcNamesInGroup_Click`, однострочное уведомление при копировании многострочного списка и фильтрация категорий `МОДЫ` / `МОД / РУСИФИКАТОР`.
- 🔹 **[Views/CatalogPage.xaml, Views/CatalogPage.xaml.cs]**: Во всплывающее окно «Список дополнений» в разделе «Информация» добавлены кнопки копирования чистого названия каждого DLC (`CopyCatalogDlcName_Click`) и пакетного копирования всех DLC (`CopyAllCatalogDlcNames_Click`).
- 🔹 **[Services/LocalizationService.UiTree.cs]**: Добавлены и переведены на 10 языков 5 новых строк интерфейса для группировки категорий и копирования названий дополнений.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.6)

- 🔸 **[Original-Only Default Torrent File Selection and Accurate Selected Patch Version (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Fixed smart default file selection in `Torrent Files`: localizations, mods (`МОДЫ`, `МОД / РУСИФИКАТОР`, `ОБНОВЛЕНИЕ (РУС)`), saves (`СЭЙВЫ`), info files (`ИНФО`), and older patches are now unchecked by default.
  - Only original base game files (`ИГРА`), the single newest official update (`ОБНОВЛЕНИЕ`, such as `v131072` instead of an older rus-paired `v65536` in `Xenoblade Chronicles X Definitive Edition`), original DLCs (`ДОПОЛНЕНИЯ`), and `UNLOCKER` are selected automatically, resolving the mismatched header patch version.
- 🔸 **[Category-Grouped Torrent File Tree (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Files inside every game slot are now organized in strict category order: `ИГРА` -> `ОБНОВЛЕНИЕ` -> `ДОПОЛНЕНИЯ` -> `UNLOCKER` -> `МОДЫ` -> `ОБНОВЛЕНИЕ (РУС)` -> `СЭЙВЫ` -> `ИНФО`, complete with per-category toggle headers, selection counts, and size summaries.
- 🔸 **[Clean DLC Name Copy Matching STORM GAMES WORLD Rules (`RuTrackerModService.cs`, `CatalogPage.xaml`, `CatalogPage.xaml.cs`, `LocalizationService.UiTree.cs`)]**:
  - Added `ExtractCleanDlcName` to strip game prefixes, `[DLC ...]`, TitleID brackets, `[v0]`, and extensions (e.g., yielding `Early Purchase Bonus Exploration Support Pack`) with single-DLC copy and batch Copy All DLC buttons in both the RuTracker downloader and Information catalog DLC flyout, plus 10-language UI translations.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.6_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.6.zip` / `STORM_SWITCH_BOX_8.0.6_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.6.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.6_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.6_Setup.exe`** (219.65 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.6.zip`** / **`_win-x64.zip`** (151.21 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.6.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.6_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.6_Setup.exe** (219.65 МБ)
  - `SHA-256: AB9699D991B03CFA8D1AA6E16E0725614522B6479EA1A7191659A3897DE0F706`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.6_win-x64.zip** / **STORM_SWITCH_BOX_8.0.6.zip** (151.21 МБ)
  - `SHA-256: 5AC6728C33FD9131E1B71AAA2994BE80D6099F0D01B1836C96FA94C698D46B0D`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.6.apk** (14.38 МБ)
  - `SHA-256: 6E870F725A90BAADB3C0BA74C8DF5FFA5C2636A48D6491B6DBE486153C4C7182`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.6_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 766255867D904FEA8D54E738C1E63BA6E03B277C8C79D43873147DD1FE2A755A`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
