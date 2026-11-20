> 💡 **Релиз 8.0.2** - *Исправление залипания файлов и слотов предыдущей раздачи в загрузчике RuTracker, а также комплексное ускорение загрузки списка файлов, метаданных и поиска раздач*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.2)

- 🌟 **[Исправление обновления списка игр и файлов при выборе раздачи RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Устранена ошибка, при которой после выбора новой раздачи (например, `Animal Well`) в центральной области «Игры и части в раздаче» и «Файлы в раздаче» оставались данные предыдущей игры (`Aggelos`). Теперь при смене раздачи `PrimeQuickTopicState` принудительно очищает `_loadedTorrentBytes`, `_loadedTorrentHash` и `_loadedMagnet`, а `TryGetInstantCachedTorrentFiles` строго сверяет `InfoHash` методом `TryLoadValidatedTorrent`, полностью исключая утечку кэша между темами.
- 🌟 **[Мгновенный сброс UI и надёжное обновление при выборе и клике (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Выделен единый метод `LoadSelectedTopicAsync`, который до любых сетевых запросов мгновенно очищает старые ссылки, файлы и группы и перестраивает слоты под новую тему. Исправлена автовыборка первой раздачи при мгновенном поиске (`autoSelectFirst: true`), добавлен обработчик `TopicsListView_ItemClick` для повторного открытия выделенной строки, а флаги `_isTopicLoading` / `filesApplied` защищают от двойного запуска и гонок обновлений.
- 🌟 **[RAM-кэш, коалесцирование запросов и фоновый префетч (`RuTrackerModService.cs`)]**: Добавлены потокобезопасный оперативный кэш `_memoryTorrentCache`, дедупликация параллельных загрузок `.torrent` через `_inFlightTorrentFetches`, фоновая предзагрузка первых 4 найденных раздач (`PrefetchTopicsMetadata`, до 3 одновременных загрузок через `_prefetchThrottle`) и предварительный прогрев HTTPS-соединения `WarmupFastConnections()` при открытии загрузчика. Повторное переключение между раздачами отдаёт список файлов за миллисекунды.
- 🌟 **[Ускорение поиска, кэш-зеркал и облачных прокси (`RuTrackerModService.cs`)]**: Живой поиск `tracker.php` в `SearchTopicsAsync` ограничен быстрым таймаутом `2.2 с` вместо `30 с`, прямые запросы тем ускорены до `2.0-2.2 с`, из гонки `.torrent`-зеркал исключён устаревший HTML-сервис `btcache.me` (оставлены `itorrents.org` и `torrage.info` с таймаутом `3.0 с` и асинхронной записью на диск), а облачная гонка `FetchTopicHtmlViaCloudRaceAsync` сокращена до `3.2 с` с немедленной отменой проигравших запросов и 60-секундным cooldown для сбойных прокси.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `_loadedTorrentHash`, `ExtractInfoHashHex`, `TryLoadValidatedTorrent` (строгая проверка `InfoHash` SHA-1), `_memoryTorrentCache`, `_inFlightTorrentFetches`, `WarmupFastConnections`, `PrefetchTopicsMetadata` (`maxCount = 4`, `_prefetchThrottle = 3`) и `_cloudProxyCooldownUntilUnix` (`60 с`). Обновлены `PrimeQuickTopicState`, `CreateIsolatedDownloadSession`, `TryGetInstantCachedTorrentFiles`, `TryFetchTorrentFromCacheMirrorsAsync`, `InspectTorrentFilesAsync`, `SearchTopicsAsync` и `FetchTopicHtmlViaCloudRaceAsync`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml]**: На `TopicsListView` включено `IsItemClickEnabled="True"` и подключён обработчик `ItemClick="TopicsListView_ItemClick"`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Вызов `WarmupFastConnections()` в конструкторе, мгновенный выбор первого результата в `SearchOrInspect_Click` без перетирания активного статуса, исправление `ApplyTopicFilterAndSort`, единый асинхронный конвейер `LoadSelectedTopicAsync` с защитой от гонок (`_activeLoadedTopicId`, `_activeTopicHasRealFiles`, `_isTopicLoading`, `filesApplied`) и автоматической догрузкой файлов при обнаружении нового `InfoHash` на странице темы.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.2)

- 🔸 **[Fix Stale Game Slots & File Lists on Topic Switch (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Fixed a bug where selecting a newly searched topic (such as `Animal Well`) kept showing game slots and `.torrent` file lists from the previously viewed topic (`Aggelos`). `PrimeQuickTopicState` now clears `_loadedTorrentBytes`, `_loadedTorrentHash`, and `_loadedMagnet` on topic switches, while `TryGetInstantCachedTorrentFiles` validates the 40-char `InfoHash` via `TryLoadValidatedTorrent` before reusing any bytes.
- 🔸 **[Instant UI Reset & Race-Free Topic Loading (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Consolidated selection handling into `LoadSelectedTopicAsync` to immediately clear previous file/link/slot collections before awaiting network tasks, enabled `autoSelectFirst: true` for instant search results, added `TopicsListView_ItemClick` so re-clicking a highlighted row reloads reliably, and added `_isTopicLoading` / `filesApplied` guards against duplicate loads and stale status overwrites.
- 🔸 **[In-Memory Torrent Cache, Request Coalescing & Prefetch (`RuTrackerModService.cs`)]**: Added thread-safe `_memoryTorrentCache`, in-flight HTTP fetch deduplication (`_inFlightTorrentFetches`), background metadata prefetching for the top 4 search hits (`PrefetchTopicsMetadata` throttled to 3 concurrent transfers), and TLS connection warmup (`WarmupFastConnections`) when opening the downloader dialog.
- 🔸 **[Faster Search, Cache Mirror Race & Cloud Proxy Fallbacks (`RuTrackerModService.cs`)]**: Capped live tracker search HTTP/WebView2 calls at `2.2s`, tightened topic/torrent HTTP timeouts (`2.0-2.2s` direct HTML/viewtorrent, `3.0s` `itorrents.org`/`torrage.info` race after removing HTML-only `btcache.me`, async disk cache write), and reduced `FetchTopicHtmlViaCloudRaceAsync` to `3.2s` with winner-first cancellation and a `60s` failure cooldown per proxy.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.2_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.2.zip` / `STORM_SWITCH_BOX_8.0.2_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.2.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.2_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.2_Setup.exe`** (219.61 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.2.zip`** / **`_win-x64.zip`** (151.16 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.2.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.2_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.2_Setup.exe** (219.61 МБ)
  - `SHA-256: 79E8FDF115D60E56F528F3EF8DAFDF4C80C922A95D7BA8BE6642F393171253A2`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.2_win-x64.zip** / **STORM_SWITCH_BOX_8.0.2.zip** (151.16 МБ)
  - `SHA-256: 6225C516F29B739B8860DEED983BCAAA5673D182EF7F717265F6D2D8B51BF171`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.2.apk** (14.38 МБ)
  - `SHA-256: 416CFB8ACBC6F0A83CEC118CA4F97FF5597C930B95493EC8FA99761B0E42CAD1`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.2_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 78305F15F6E8ABC0C6C24F7A170C6C22775F2AF3ABA3BCF062E3E267F4A64ABD`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
