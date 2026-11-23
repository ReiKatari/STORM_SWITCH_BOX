> 💡 **Релиз 8.1.1** - *Исправление отображения версий игр в загрузчике RuTracker (устранение индикатора `"?"` вместо реальной версии патча, поддержка 3-компонентных названий STORM, автообновление версии при правке названия слота и точный онлайн-резерв через `stormgamesworld.ru`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.1)

- 🌟 **[Исправление отображения версии игр в загрузчике RuTracker (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена причина отображения `"?"` вместо версии игры при истечении суточного TTL в локальном кэше `rutracker_topic_parsed_v2.json` и временной недоступности HTML темы RuTracker (например, *Golden Force* `1.1.6`): добавлена мгновенная предгидратация `TryHydrateTopicFromParsedCache`, сохранение кэшированных версий при сбое или пустом парсинге HTML и обязательный пересчёт метаданных после завершения загрузки темы независимо от статуса `"..."` или `"?"`.
  - В `FetchValidatedTopicPageHtmlAsync` увеличено окно быстрой гонки до `5.0` секунд с параллельным опросом основного и резервного зеркал (`rutracker.org` / `rutracker.net`), а в `ExtractGameVersionsFromTopicHtml` расширена граница разделителей для строк вида `1.1.6 - 196608` и `1.1.6-196608`.
  - Исправлены регулярные выражения в `ExtractFilePatchCode` и `ScanVerTextSource` для 3-компонентного формата STORM `(версия - код_патча - TitleID)` с проверкой совпадения кода патча, добавлен автопересчёт версии при редактировании названия слота `GameTitle` и защита `CopySlotVersion_Click` от копирования заглушек `"?"` / `"..."`.
- 🌟 **[Точный онлайн-резерв версий через API `stormgamesworld.ru` (`TitleDbService.cs`)]**:
  - Добавлен метод `TryResolveDisplayVersionOnlineAsync`, который при отсутствии версии в локальных источниках разово за сессию запрашивает `https://stormgamesworld.ru/api/games` и принимает версию строго при точном совпадении одновременно базового `TitleID` (16 hex) и целочисленного кода патча, без эвристических подстановок.
- 🌟 **[Синхронизированная сборка Windows и Android 8.1.1]**:
  - Сборки Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`, `versionCode = 8101`) обновлены, пересобраны и подписаны сертификатами `STORM TEAM` / `STORM SERTIFICATE`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлен метод `TryHydrateTopicFromParsedCache(topic)` и его вызов в `PrimeQuickTopicState`; в `FetchTopicDetailsAsync` добавлено сохранение кэшированных версий, постера, хэша и ссылок при истёкшем TTL или недоступном HTML темы; расширена регулярка `ExtractGameVersionsFromTopicHtml` на дефис/двоеточие; `FetchValidatedTopicPageHtmlAsync` теперь опрашивает оба зеркала (`rutracker.org` и `rutracker.net`) с таймаутом `5.0` сек.
- 🔹 **[Services/TitleDbService.cs]**: Добавлен дедуплицированный онлайн-резерв `TryResolveDisplayVersionOnlineAsync` по API `stormgamesworld.ru/api/games` со строгим совпадением `baseTid` и `patchCode`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Добавлен предпрогрев кэша темы в `LoadSelectedTopicAsync`, синхронизация версий в фоновом обратном вызове `FetchTopicCommentLinksAsync`, обязательный пересчёт в блоке `finally`, триггер `OnGameSlotPropertyChanged` на `GameTitle` с защитой от рекурсии `_isEnrichingSlots`, 3-компонентная поддержка `(dv - iv - tid)`, фоновый онлайн-резерв для слотов со статусом `"?"` и защита `CopySlotVersion_Click`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed the `"?"` game version regression in the RuTracker Downloader where expired topic cache entries (>24h) discarded `ParsedGameVersion` when live RuTracker HTML was unreachable.
- 🔸 Added `TryHydrateTopicFromParsedCache`, mirror-pair parallel HTTP race (`rutracker.org` + `rutracker.net`, 5.0s window), 3-part STORM title `(ver - patchCode - TitleID)` regex parsing, `GameTitle` edit re-enrichment, and a strict exact-`(TitleID, patchCode)` online fallback via `stormgamesworld.ru/api/games`.
- 🔸 Unified build and release update to version `8.1.1` (`versionCode 8101` on Android) across Windows x64 and Android Modern/Legacy.

</details>

---

### 📦 Выбор сборки и архитектура

| Файл | Платформа | Архитектура | Назначение и отличие |
| :--- | :--- | :--- | :--- |
| **`STORM_SWITCH_BOX_8.1.1_Setup.exe`** (219.75 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Рекомендуемый установщик для ПК**: автоматически устанавливает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.1.1.zip`** / **`_win-x64.zip`** (151.31 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.1.1.apk`** (14.45 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.1.1_LEGACY.apk`** (26.02 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.1.1_Setup.exe** (219.75 МБ)
  - `SHA-256: F7F2DB1AC2F871744E449341095BE4E31E9A4EEDBED5FF152F215FB719CA8824`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.1.1_win-x64.zip** / **STORM_SWITCH_BOX_8.1.1.zip** (151.31 МБ)
  - `SHA-256: 0088325E2BED5DDDBD829DFE06870568947BE688EA478596C036B515928EE542`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.1.1.apk** (14.45 МБ)
  - `SHA-256: 59D657F095A69355DE9899676C847836EC06823AC14D5B82CDFD5EA5E1F2CDF4`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.1.1_LEGACY.apk** (26.02 МБ)
  - `SHA-256: 6FDE6C4749F7DAB56DA1ACABE7E26F78BA8ED9FFE1346BB3CA853C68E561ED69`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
