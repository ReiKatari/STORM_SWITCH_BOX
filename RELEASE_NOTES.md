> 💡 **Релиз 8.0.5** - *Точное определение версий игр без ложных подстановок, исключение Standalone-дополнений и полноценный мониторинг обновлений RuTracker в «Информации»*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.5)

- 🌟 **[Точные версии игр и защита базового TitleID в загрузчике RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TitleDbService.cs`, `MultiContentService.cs`)]**:
  - Устранена выдуманная подстановка версий вида `1.{major}.0` (например, `1.3.0` вместо реальной `1.1.6` у `Golden Force` с патчем `v196608`). Теперь семантическая версия извлекается по строгой цепочке: имена файлов -> кэш наблюдённых NACP и TitleDB (`known_display_versions.json`) -> HTML шапка темы RuTracker (с отсечением версий прошивки, перевода, дат и размеров) -> заголовок/слот -> статус `?` без искажения номера при отсутствии точной строки.
  - Реализована фильтрация `IsStandaloneBonusFile` и строгий приоритет выбора базового TitleID: бонусные Standalone-приложения вроде цифровых артбуков и саундтреков (например, `01005F4027E94000` у `Castlevania: Belmont's Curse`) больше не перехватывают базовый TitleID основной игры (`0100BA4026E94000`) и не попадают в счётчик её DLC (`2` настоящих DLC вместо `3`).
- 🌟 **[Динамическое обновление списка игр и отключение рекурсии по умолчанию в «Информации» (`CatalogPage.xaml`, `CatalogPage.xaml.cs`, `CatalogScannerService.cs`, `AppSettings.cs`)]**:
  - После добавления или удаления папок сканирование и обновление карточек в сетке игр происходят мгновенно и динамически, без необходимости переключать вкладки меню (устранена отвязка `ItemsSource` после фильтра/поиска).
  - Рекурсивное сканирование подпапок (`SearchOption.AllDirectories`) отключено по умолчанию. При добавлении папки добавлен чекбокс «Сканировать подпапки (рекурсивно)» (снят по умолчанию) с сохранением настройки для каждой папки (`CatalogRecursiveFolders`).
- 🌟 **[Мониторинг новых версий и дополнений (DLC) по RuTracker в «Информации» (`RuTrackerCatalogUpdateService.cs`, `CatalogItem.cs`, `CatalogPage.xaml`)]**:
  - В «Информацию» добавлена автоматическая и ручная проверка обновлений версий и новых дополнений по базе RuTracker (кнопка «Проверить обновления (RuTracker)», фильтр «Есть обновления (N)», зелёные кликабельные бейджи на карточках и баннер в окне деталей с переходом в 1 клик к раздаче).
  - Сканер каталога `CatalogScannerService` теперь извлекает фактические локальные DLC из заголовков NCA / `.tik` / `.cnmt` (`LocalDlcIds`, `LocalDlcCount`) и сопоставляет их с раздачами RuTracker по базовому TitleID (исключая Standalone-бонусы): например, для локальной `Castlevania: Belmont's Curse` (`1.1.0` + `1` DLC) корректно фиксируется обновление на RuTracker (`1.2.0` + `2` DLC).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `ParsedGameVersion`, `ParsedTitleVersions` (включая `TopicParsedCacheEntry.SchemaVersion = 2`), `ExtractGameVersionsFromTopicHtml`, `IsStandaloneBonusFile`, `IsDlcOfBaseTitle`; улучшен `DisplayVersion`/`ToMetadataEditModel` и группировка слотов без перехвата Standalone-бонусами.
- 🔹 **[Services/TitleDbService.cs]**: Добавлены `IsValidSemanticVersion`, `CompareSemanticVersions` и постоянный кэш наблюдённых NACP-версий `known_display_versions.json` (`RecordObservedDisplayVersion`, `TryResolveDisplayVersion`).
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Проброшена версия из HTML темы, удалена эвристика `1.{major}.0`, исправлен выбор базового TitleID при наличии Standalone-приложений.
- 🔹 **[Services/RuTrackerCatalogUpdateService.cs]**: Создан сервис пакетного сопоставления локальных игр Switch / Switch 2 с раздачами RuTracker по базовому TitleID (обновление индекса, `FetchTopicDetailsAsync`, `InspectTorrentFilesAsync`, сравнение `VersionCode`/семантической версии и не-Standalone DLC).
- 🔹 **[Services/CatalogScannerService.cs, Models/AppSettings.cs, Models/CatalogItem.cs]**: Добавлено нерекурсивное сканирование по умолчанию, извлечение `LocalDlcIds`/`LocalDlcCount`, агрегирование соседних DLC-файлов, сохранение настроек подпапок по пути (`CatalogRecursiveFolders`) и свойства статуса обновлений RuTracker.
- 🔹 **[Views/CatalogPage.xaml, Views/CatalogPage.xaml.cs, Services/LocalizationService.UiTree.cs]**: Добавлены чекбокс «Сканировать подпапки (рекурсивно)», динамическое добавление/удаление папок, панель мониторинга обновлений RuTracker и переводы всех новых строк интерфейса на 10 языков.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.5)

- 🔸 **[Accurate Game Display Versions & Standalone DLC Isolation (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `TitleDbService.cs`, `MultiContentService.cs`)]**:
  - Eliminated the synthetic `1.{major}.0` version fallback that fabricated `1.3.0` for games like `Golden Force` (`v196608`, real version `1.1.6`). Display versions now resolve cleanly via filename tags -> persisted NACP/TitleDB cache (`known_display_versions.json`) -> RuTracker topic post HTML header -> topic/slot title -> `?` when genuinely unknown.
  - Added `IsStandaloneBonusFile` filtering and strict Base TitleID prioritization so Standalone bonus apps (soundtracks/artbooks like `01005F4027E94000` in `Castlevania: Belmont's Curse`) never overwrite the main game's Base TitleID (`0100BA4026E94000`) or inflate genuine DLC counts (`2` instead of `3`).
- 🔸 **[Dynamic Catalog Folder Refresh & Non-Recursive Default Scanning (`CatalogPage.xaml`, `CatalogPage.xaml.cs`, `CatalogScannerService.cs`, `AppSettings.cs`)]**:
  - Adding or removing folders in the Information tab now dynamically scans/prunes games immediately without switching tabs or breaking search filtering.
  - Recursive scanning (`SearchOption.AllDirectories`) is disabled by default; an unchecked-by-default `"Scan subfolders (recursively)"` checkbox was added when adding a folder (`CatalogRecursiveFolders`).
- 🔸 **[RuTracker Version & New DLC Update Monitor in Information Tab (`RuTrackerCatalogUpdateService.cs`, `CatalogItem.cs`, `CatalogPage.xaml`)]**:
  - Added automatic and manual RuTracker update & new DLC checking directly inside Information (`Check updates (RuTracker)`, `Updates available (N)` filter chip, clickable green card badges, and detail-overlay banner opening the exact RuTracker topic in 1 click).
  - `CatalogScannerService` extracts local DLC IDs/counts from container NCAs, `.tik`, and `.cnmt` headers and accurately flags both version bumps and new non-Standalone DLCs (e.g., `Castlevania: Belmont's Curse` `1.1.0 + 1 DLC` -> RuTracker `1.2.0 + 2 DLC`).

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.5_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.5.zip` / `STORM_SWITCH_BOX_8.0.5_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.5.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.5_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.5_Setup.exe`** (219.65 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.5.zip`** / **`_win-x64.zip`** (151.20 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.5.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.5_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.5_Setup.exe** (219.65 МБ)
  - `SHA-256: 21B5540180B614C3C427DB558827D8C67BE6CC860830FA284E36D6C59D5FDDB3`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.5_win-x64.zip** / **STORM_SWITCH_BOX_8.0.5.zip** (151.20 МБ)
  - `SHA-256: D229DE288644A018713F2AA1496C63AF554D358D4E89E07696FC2FA4F8DA5B54`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.5.apk** (14.38 МБ)
  - `SHA-256: 1D24A2B75EF7936A2F2445B339CBD0271FE0B3A37D82D517B875F4009859F839`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.5_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 4059A75062F5E38C1859199C0D8456A57EFA8DA9B21901E25817119BC3DF0C56`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
