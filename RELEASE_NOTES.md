> 💡 **Релиз 8.1.2** - *Ускорение сборки и многопоточного Zstd-сжатия (`FastPassthrough` без пересжатия Block NSZ и снятие лимита CPU/4), выбор лимита одновременных сборок и скачиваний в интерфейсе RuTracker, исправление обратной декомпрессии Block NSZ -> NSP (совместимость с Eden) и поддержка 4-сегментных версий игр (`1.0.6.1267`)*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.2)

- 🌟 **[Многократное ускорение сборки и сжатия NSZ/Z3DS (`MultiContentService.cs`, `NszCompressionService.cs`, `StormNczCompressor.cs`, `Z3dsService.cs`, `RuTrackerDownloadQueueService.cs`)]**:
  - Снято жёсткое ограничение очереди RuTracker в `1` одновременную сборку (`SemaphoreSlim(1, 1)`): внедрён потокобезопасный динамический планировщик `AcquireBuildSlotAsync` / `ReleaseBuildSlot` с поддержкой до `8` параллельных сборок и мгновенным пробуждением задач из очереди при смене лимита.
  - Снято жёсткое ограничение потоков сжатия в `25%` ядер ЦП (`ProcessorCount / 4`) во всех движках (`StormNczCompressor`, `NszCompressionService`, `Z3dsService`): добавлен динамический расчёт `GetOptimalCompressionThreads()` по бюджету `UsedCores` и активным задачам, а флаг `-L` (`--long`) при запуске `nsz.exe` теперь используется только в режиме Solid NSZ, исключая лишнее выделение 128 МБ на поток при 1 МБ Block NSZ.
  - Реализован режим `FastPassthrough`: при сборке в `NSZ` контейнеры в формате Block NSZ больше не проходят через бесполезный цикл `распаковка -> повторное сжатие` (при `Fast Multi-Content` / одиночном файле / DLC в `MultiContentService`, а также при проверке `InspectPfsNczPassthrough` в `NszCompressionService`, где при смешанном составе сжимаются только несжатые NCA).
- 🌟 **[Управление одновременными скачиваниями и сборками прямо в RuTracker (`RuTrackerModDownloaderDialog.xaml`, `RuTrackerModDownloaderDialog.xaml.cs`, `SettingsPage.xaml`, `AppSettings.cs`)]**:
  - В заголовочную панель очереди закачек RuTracker добавлены переключатели лимита одновременных скачиваний (`Закачек: 1..10`) и сборок (`Сборок: 1..8`) с моментальным применением и сохранением в `AppSettings`.
  - В общих параметрах приложения максимальный лимит одновременных сборок/задач увеличен с `5` до `8` с двусторонней живой синхронизацией с окном RuTracker.
- 🌟 **[Исправление декомпрессии Block NSZ -> NSP (`Decompressor.py`, `Keys.py`, `StormNczStorage.cs`, `StormNczCompressor.cs`, `NszCompressionService.cs`, `TasksViewModel.cs`)]**:
  - Устранено повреждение итоговых `.nsp` при обратной распаковке блочных `.nsz` (включая игры с DLC `firstSection = 0xC00` и обновлениями BKTR, из-за которого эмулятор Eden отклонял собранный `.nsp`): добавлена единая нормализация секций `_normalize_sections_for_ncz` с чтением `NCZBLOCK.decompressedSize`, исправлен расчёт смещений при `firstSection > 0x4000`, добавлена поддержка `CryptoType == 5`, сортировка секций с покрытием межсекционных и концевых зазоров и расшифровка `TitleKeyBlock` из `ExternalKeySet` / `title.keys` через `TitleKek` (`DecryptTitleKeyWithKeyset`).
  - Устранена двойная XTS-дешифровка заголовка в `StormNczStorage.cs`, исправлена формула смещения бакетов BKTR в `StormNczCompressor.cs`, обновлён `Keys.py` для работы без новейших необязательных ревизий `master_key_XX`, а также добавлена автоматическая проверка целостности распакованного контейнера `ValidateDecompressedContainer` (`PFS0`, отсутствие остаточных `.ncz`, проверка `Nca.Header.NcaSize` с порогом `0xC00` для малых `.cnmt.nca`) и явная ошибка вместо молчаливого копирования `.nsz`.
- 🌟 **[Исправление отображения 4-сегментных версий игр и быстрая загрузка файлов RuTracker (`TitleDbService.cs`, `RuTrackerModService.cs`, `RuTrackerCatalogUpdateService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, Android)]**:
  - Поддержаны 4-компонентные версии до 6 цифр в номере сборки (например, *Dave the Diver* `v1.0.6.1267` -> `1.0.6.1267`), фильтр дат `2015..2099` ограничен 3-сегментными строками, исправлен парсинг `Версия v...` без двоеточия и добавлена синхронизация cookie WebView2 для `.rutracker.org` и `.rutracker.net`.
  - В `LoadSelectedTopicAsync` загрузка списка файлов `.torrent` и фонового P2P `ResolveMagnetMetadataBackgroundAsync` отделена от ожидания HTML/прокси темы, опрос комментариев ограничен последними 2 страницами (`SemaphoreSlim(2)`), а немодовые информационные ссылки `GitHub`/`Telegraph`/`Google Docs` отфильтровываются без ключевых слов модификаций.
- 🌟 **[Синхронизированное обновление Windows и Android 8.1.2]**:
  - Версия обновлена до `8.1.2` (`versionCode = 8102` на Android) для всех сборок Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Models/AppSettings.cs, Views/Dialogs/RuTrackerModDownloaderDialog.xaml(.cs), Views/SettingsPage.xaml(.cs), Services/RuTrackerDownloadQueueService.cs]**: Добавлена настройка `RuTrackerMaxConcurrentDownloads` (1..10), динамический FIFO-шлюз параллельных сборок `AcquireBuildSlotAsync`/`ReleaseBuildSlot` (1..8) вместо жёсткого `SemaphoreSlim(1, 1)`, селекторы одновременных скачиваний и сборок на панели очереди RuTracker, повышение потолка `ConcurrentTasks` до 8 в параметрах и живая синхронизация.
- 🔹 **[Services/NszCompressionService.cs, MultiContentService.cs, Z3dsService.cs, Core/NSZ/StormNczCompressor.cs]**: Добавлен `GetOptimalCompressionThreads()`, удалён жёсткий лимит `ProcessorCount / 4`, флаг `-L` оставлен только для Solid NSZ, добавлены `InspectPfsNczPassthrough` и селективный `FastPassthrough` Block NSZ в `MultiContentService`.
- 🔹 **[tools/nsz/Lib/site-packages/nsz/Decompressor.py, Keys.py]**: Добавлен `_normalize_sections_for_ncz`, расчёт размера по `NCZBLOCK.decompressedSize`, поддержка `cryptoType == 5` и устойчивый `Keys.load`.
- 🔹 **[Core/NSZ/StormNczStorage.cs, StormNczCompressor.cs, Services/NszCompressionService.cs, ViewModels/TasksViewModel.cs]**: Устранена двойная XTS-дешифровка, добавлена сортировка секций с gap-заполнением, поддержка `CryptoType == 5`, фикс формулы бакетов BKTR, `DecryptTitleKeyWithKeyset` и валидация `ValidateDecompressedContainer`.
- 🔹 **[Services/TitleDbService.cs, RuTrackerModService.cs, RuTrackerCatalogUpdateService.cs, Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Поддержка 4-сегментных версий и очистки `v.`, кросс-доменные WebView2-cookie, развязка загрузки файлов от HTML темы, ограничение комментариев до 2 страниц и фильтр немодовых ссылок.
- 🔹 **[android/.../MainActivity.kt, RuTrackerCloudEngine.kt, StormNczEngine.kt, build.gradle.kts]**: Обновлены регулярные выражения 4-сегментных версий и версия приложения до `8.1.2` (`versionCode = 8102`).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Accelerated container builds and compression: replaced the hardcoded 1-build RuTracker gate with a dynamic FIFO gate (up to 8 parallel builds), replaced the 25% CPU cap (`ProcessorCount / 4`) with `GetOptimalCompressionThreads()`, dropped redundant `--long` allocation on 1 MB Block NSZ, and added zero-recompress Block NSZ `FastPassthrough`.
- 🔸 Added live simultaneous download (`1..10`) and build (`1..8`) selectors directly to the RuTracker downloader toolbar, synced two-way with Settings (raised from 5 to 8).
- 🔸 Fixed Block NSZ to NSP decompression corruption (affecting DLC `firstSection = 0xC00` NCAs, BKTR updates, `CryptoType == 5`, gap sections, double-XTS header decryption, and encrypted `TitleKeyBlock` lookup) plus added `ValidateDecompressedContainer`.
- 🔸 Fixed 4-part game version parsing (`v1.0.6.1267` -> `1.0.6.1267`), cross-domain WebView2 cookies, decoupled `.torrent`/magnet loading, comment-page throttling, and generic link filtering.
- 🔸 Updated unified Windows x64 and Android Modern/Legacy releases to version `8.1.2` (`versionCode 8102`).

</details>
---

### 📦 Выбор сборки и архитектура

| Файл | Платформа | Архитектура | Назначение и отличие |
| :--- | :--- | :--- | :--- |
| **`STORM_SWITCH_BOX_8.1.2_Setup.exe`** (219.76 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Рекомендуемый установщик для ПК**: автоматически устанавливает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.1.2.zip`** / **`_win-x64.zip`** (151.32 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.1.2.apk`** (14.45 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.1.2_LEGACY.apk`** (26.02 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.1.2_Setup.exe** (219.76 МБ)
  - `SHA-256: C72F7C44A0888CA3DBD968024C291F72A60A2EF8AB3E89173AFEE6DB993542E0`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.1.2_win-x64.zip** / **STORM_SWITCH_BOX_8.1.2.zip** (151.32 МБ)
  - `SHA-256: B66DFEC6A8C23B597582AD0B5AE2478B65839EF2299F5D9106121BF997A71F6A`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.1.2.apk** (14.45 МБ)
  - `SHA-256: F2FA3A112236D0BFA885798F4A3A6F89B302D2AE48A3630B7AA30533998D9D0D`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.1.2_LEGACY.apk** (26.02 МБ)
  - `SHA-256: E94D2F2EE94111E9C58E8EAB98D8B4C5F05F0BD5C638A48FCA6A67279FC59FE9`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
