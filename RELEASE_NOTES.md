> 💡 **Релиз 8.1.0** - *Исправление офлайн-индекса и поиска RuTracker #1605 на Android (восстановление всех 7 301 раздачи Switch), устранение задержки 5-8 сек на каждый символ при вводе и единая синхронизированная сборка Windows + Android (`versionCode 8100`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.0)

- 🌟 **[Восстановление поиска игр RuTracker #1605 на Android (`RuTrackerSwitchIndex.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Устранена причина, по которой с версии `8.0.0` на Android не находились игры из локального индекса (например, *Mandragora*): при сборке APK `aapt2` автоматически распаковывает `.gz` и сохраняет ассет как `assets/rutracker_switch_index.json`, а код открывал только `.json.gz` и требовал GZIP-поток. Добавлены приоритетное открытие `ASSET_NAME_PLAIN`, автоопределение GZIP по байтам `0x1F 0x8B` (`maybeGunzip`) и потоковый беззависимостный JSON-парсер `IndexJsonReader` для всех `7 301` раздачи Switch.
  - Исправлен фильтр `isStrictSwitchRelease` (разделение ПК-репаков, проверка границ слов и явный приоритет тегов `[Nintendo Switch]` / `NSP`/`NSZ`/`XCI`/`XCZ`/`NSW`/`[NS]`): разблокирована 51 реальная раздача Switch (*Skyrim Special Edition*, *Overcooked Special Edition*, *Deltarune*, *Persona 3 Portable*, *Rune Factory 3/4/5* и др.), ранее скрывавшаяся подстроками `cia`/`rune`/`portable`/`codex`.
  - В `searchTopics` реальный индекс `#1605` получил приоритет перед живыми дозапросами и синтетическим каталогом `TDB-`, добавлена подстановка живых сидов и названий `tt` в `mapRealIndexTopicToItem`, а также исправлено регулярное выражение `linkRegex` в парсере Atom-ленты.
- 🌟 **[Мгновенный отклик ввода в поиске Android без фризов (`StormNczEngine.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Устранена задержка `5-8` секунд на каждую вводимую букву: в `extractCleanGameTitle` предкомпилированы все 6 `Regex`, в `RuTrackerCloudEngine` добавлены единожды подготавливаемый запрос `PreparedFuzzyQuery` и преднормализованные кэши `curatedSearchKeys`, `realIndexSearchCache` и `titleDbSearchCache` с фоновым прогревом при запуске (`initAndroidContext`).
  - `resolveAuthenticBaseTitleId` переведена с линейного перебора `11 380+` игр TitleDB на UI-потоке на `O(1)`-поиск `StormNczEngine.lookupSwitchTitleIdFast` с кэшированием (`resolvedTitleIdCache`, `60` карточек за `1` мс), а короткие префиксы `1-4` символов (`"m"`, `"ma"`, `"man"`, `"mand"`) теперь сразу находят совпадения без пустых пауз.
  - Все поля поиска (RuTracker, каталог TitleDB и фильтр дерева RomFS) перенесены на фоновый `uiSearchExecutor` с отменой устаревших запросов по счётчикам и debounce (`120` мс локально, `450` мс сетевой дозапрос, `100` мс для дерева RomFS).
- 🌟 **[Единое обновление версии до 8.1.0 для Windows и Android]**:
  - Все сборки для Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`, `versionCode = 8100`) обновлены, пересобраны и подписаны сертификатами `STORM TEAM` / `STORM SERTIFICATE`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerSwitchIndex.kt]**: Добавлена константа `ASSET_NAME_PLAIN = "rutracker_switch_index.json"`, функция `maybeGunzip` с автоопределением `0x1F 0x8B`, потоковый парсер `IndexJsonReader` без `org.json` и помощник `clearLoadedStateForTesting`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/StormNczEngine.kt]**: Предкомпиляция всех 6 регулярных выражений `extractCleanGameTitle`, потокобезопасная загрузка `loadFullTitleDbFromAssets`, общий `parseTitleDbTsvStream` и `O(1)` индекс `buildSwitchTitleIdLookup` / `lookupSwitchTitleIdFast`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/RuTrackerCloudEngine.kt]**: `initAndroidContext` с фоновым прогревом обоих поисковых кэшей, `PreparedFuzzyQuery` + `NormalizedSearchKeys` (`realIndexSearchCache`, `titleDbSearchCache`, `curatedSearchKeys`), поддержка префиксов `1-4` символов в `scorePreparedQuery` и `isWordFuzzyMatch`, `O(1)` `resolveAuthenticBaseTitleId`, точный `isStrictSwitchRelease` и приоритет реального индекса `#1605` в `searchTopics`.
- 🔹 **[android/app/src/main/java/com/storm/switchbox/MainActivity.kt]**: Фоновый однопоточный `uiSearchExecutor` (с очисткой в `onDestroy`), предвычисление метаданных карточек вне UI-потока, debounce-поиск для RuTracker (`120`/`450` мс), каталога TitleDB (`120` мс) и фильтра RomFS (`100` мс).
- 🔹 **[android/app/src/test/java/com/storm/switchbox/StormBinaryAndLocTest.kt]**: Добавлены регрессионные и производительные тесты `verifyRuTrackerIndexLoadsPlainAndGzipAndSearchesMandragora` и `verifyAndroidSearchKeystrokeLatencyIsInstant` (`9/9` тестов пройдены, 10 последовательных символов `"mandragora"` суммарно за `439` мс, `60x TitleID` за `1` мс).

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements and Fixes (STORM SWITCH BOX 8.1.0)

- 🔸 **[Restore RuTracker #1605 Search on Android (`RuTrackerSwitchIndex.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Fixed the Android search regression introduced in `8.0.0` where offline Switch index games (such as *Mandragora*) disappeared because `aapt2` unpacks `rutracker_switch_index.json.gz` into plain `assets/rutracker_switch_index.json` inside APKs. Added plain-first asset opening, `maybeGunzip` magic-byte detection (`0x1F 0x8B`), and a zero-dependency streaming `IndexJsonReader` covering all `7,301` indexed Switch topics.
  - Replaced substring traps in `isStrictSwitchRelease` with word-boundary platform checks and explicit Switch/container priority (`[Nintendo Switch]`, `NSP`, `NSZ`, `XCI`, `XCZ`, `NSW`, `[NS]`), restoring 51 valid `#1605` titles including *Skyrim Special Edition*, *Deltarune*, *Persona 3 Portable*, and *Rune Factory 3/4/5*.
  - Ordered `curatedMatches` and `realIndexMatches` ahead of network scrapes and `TDB-` fallbacks in `searchTopics`, merged live tracker metadata via `mapRealIndexTopicToItem`, and fixed the Atom feed `linkRegex` trailing-space bug.
- 🔸 **[Eliminate 5-8s Per-Keystroke Input Freeze on Android (`StormNczEngine.kt`, `RuTrackerCloudEngine.kt`, `MainActivity.kt`)]**:
  - Precompiled all 6 regexes in `extractCleanGameTitle`, introduced once-per-search `PreparedFuzzyQuery` with pre-normalized `realIndexSearchCache`, `curatedSearchKeys`, and `titleDbSearchCache` warmed up in the background via `initAndroidContext`.
  - Replaced per-card linear TitleDB scans in `resolveAuthenticBaseTitleId` with `O(1)` `lookupSwitchTitleIdFast` plus `resolvedTitleIdCache` (`60` TitleIDs resolved in `1` ms), enabled smooth `1-4` character prefix matching (`"m"` -> `"ma"` -> `"man"` -> `"mandragora"`), and moved all search filtering onto `uiSearchExecutor` with cancellation sequence guards and debouncing (`120` ms local / `450` ms network / `100` ms RomFS).
- 🔸 **[Unified Windows and Android Release (`8.1.0` / `versionCode 8100`)]**:
  - Bumped version across all Windows and Android targets (`versionCode = 8100`), verified `9/9` Android unit tests and benchmarks, and rebuilt signed Windows Setup/Portable + Android Modern/Legacy packages.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.1.0_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps and Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.1.0.zip` / `STORM_SWITCH_BOX_8.1.0_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.1.0.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.1.0_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.1.0_Setup.exe`** (219.75 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.1.0.zip`** / **`_win-x64.zip`** (151.30 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.1.0.apk`** (14.45 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.1.0_LEGACY.apk`** (26.02 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.1.0_Setup.exe** (219.75 МБ)
  - `SHA-256: 24171797C060203219D773EE8E41D2DD6923C6B74E82F6EB9B3AC36654CBB616`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.1.0_win-x64.zip** / **STORM_SWITCH_BOX_8.1.0.zip** (151.30 МБ)
  - `SHA-256: E0F1794A3ADB56756447B775296619E69B1B639224C8BE4D9CD212B8CB13F75E`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.1.0.apk** (14.45 МБ)
  - `SHA-256: 20D2FE874DD9F9D7B6F27B53CD371E71707427A52400BB982B902245FF79B263`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.1.0_LEGACY.apk** (26.02 МБ)
  - `SHA-256: AEC3C996CF7713E634EBAA6A2111BCC0D74CF8739E828FF81DEE3399E8A49F53`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
