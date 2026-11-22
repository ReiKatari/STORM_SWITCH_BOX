> 💡 **Релиз 8.0.9** - *Точная классификация DLC с обновлениями (`[v65536]` / `[v393216]`), выделенных папок `7 DLC/` и автовыбор всех дополнений по умолчанию*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.9)

- 🌟 **[Точная классификация DLC с ненулевыми тегами версий (`RuTrackerModService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Устранена ошибка, из-за которой DLC-пакеты с тегами версий (`[v65536]`, `[v131072]`, `[v393216]`) и даже явным тегом `[DLC ...]` (например, 5 из 7 дополнений в раздаче `Xenoblade Chronicles 2 + Torna`) ошибочно классифицировались как `ОБНОВЛЕНИЕ`, а затем отключались по умолчанию дедупликатором обновлений как «устаревшие патчи».
  - Добавлена функция `ExtractRawTitleId` (сохраняющая точный суффикс `800`/`000`/`001+` без подмены `800 -> 000` для Switch, Switch 2 `04...` и 3DS `0004008C`/`0004000E`), а также поддержка контейнеров `.3dsx` и Azahar Z3DS (`.zcci`, `.zcxi`, `.zcia`, `.z3dsx`) в классификаторе раздач.
  - Выстроен строгий приоритет `ClassifyTorrentEntryBadge`: `DLC TitleID` (Switch/Switch 2 отличные от `000`/`800`, 3DS `0004008C...`), теги `[DLC ...]` / `[Standalone DLC ...]`, выделенные подкаталоги DLC (`7 DLC/`, `DLCs/`, `Дополнения/`) и имена DLC теперь **всегда проверяются раньше** эвристики версий `[v1-9...]`.
- 🌟 **[Автовыбор 100% оригинальных DLC и симметричное исправление для Android]**:
  - В `PopulateDisplayPathsAndSmartSelection` все уникальные DLC по умолчанию остаются включёнными (`IsSelected = true`); автоотключение дублей применяется строго при совпадении полного 16-символьного `ExtractRawTitleId` (старая и новая версии одного и того же DLC).
  - В движке Android `RuTrackerCloudEngine.torrentFileRole` внедрён аналогичный приоритет: поддержка TitleID `01...`, `04...`, `0004...`, папок `7 DLC/`, явных тегов `[DLC ...]` до проверки `[v1-9...]`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлена функция `ExtractRawTitleId`; обновлены `ClassifyTorrentEntryBadge`, `BinaryContainerExtensions` и `PopulateDisplayPathsAndSmartSelection` для безошибочного распознавания DLC с тегами `[v65536]`+ и дедупликации только полных 16-символьных дубликатов DLC TID.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt]**: Обновлён метод `torrentFileRole` с приоритетной проверкой DLC (теги, подпапки, Switch/Switch 2/3DS TitleID) до эвристики обновлений `[v1-9...]`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.0.9)

- 🔸 **[Accurate Classification of Updated `[v>0]` DLC Packages (`RuTrackerModService.cs`, `RuTrackerCloudEngine.kt`)]**:
  - Fixed a bug where updated DLCs carrying `[v65536]`, `[v131072]`, or `[v393216]` version tags (even with `[DLC ...]` in the filename or placed inside `7 DLC/`, such as in `Xenoblade Chronicles 2 + Torna`) were misclassified as `ОБНОВЛЕНИЕ` and subsequently unchecked as duplicate patches by `PopulateDisplayPathsAndSmartSelection`.
  - Added `ExtractRawTitleId` (preserving Switch `01...`, Switch 2 `04...`, and 3DS `0004...` suffixes without `800 -> 000` rewriting) and Z3DS/3DSX extensions; enforced strict priority checking DLC TitleIDs, `[DLC ...]` / `[Standalone DLC ...]` tags, and dedicated `DLC` folders before version-based `[v1-9...]` update heuristics on both Windows and Android.
- 🔸 **[All Distinct DLCs Enabled by Default (`PopulateDisplayPathsAndSmartSelection`)]**:
  - Every distinct DLC now stays checked (`IsSelected = true`) by default, deduplicating only exact matching 16-character DLC TitleIDs when both older and newer revisions exist.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.9_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.9.zip` / `STORM_SWITCH_BOX_8.0.9_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.9.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.9_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.9_Setup.exe`** (219.68 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.9.zip`** / **`_win-x64.zip`** (151.24 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.9.apk`** (14.40 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.9_LEGACY.apk`** (25.97 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.9_Setup.exe** (219.68 МБ)
  - `SHA-256: 29C7B96561D88E67A632A44BAB21254ADF1614884A64FB3A3B4F4FCE375D8261`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.9_win-x64.zip** / **STORM_SWITCH_BOX_8.0.9.zip** (151.24 МБ)
  - `SHA-256: 7880DE95FA26BC263D180A63761E302F84FC50579B529F7ADE2EABDEEA29AB90`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.9.apk** (14.40 МБ)
  - `SHA-256: 03BE487A008E22BAEEB3084EC76FE3DACF34E12B93E75CABF096FD0BD5D24DCD`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.9_LEGACY.apk** (25.97 МБ)
  - `SHA-256: 9180CBBCEAC7771281615190125A51C2FFFE154E36FEA5E822750D2251AB18E8`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
