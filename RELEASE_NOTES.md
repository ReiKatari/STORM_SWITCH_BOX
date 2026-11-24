> 💡 **Релиз 8.1.3** - *Надёжная прямая конвертация крупных образов 3DS/CCI (>= 2 ГБ и 4 ГБ) в CIA/ZCIA через нарезку игровых разделов NCSD, защита от сбоев по длине пути `MAX_PATH` во всех операциях 3DS и синхронизированное обновление версий*

---

### 🚀 Ключевые изменения и улучшения (STORM SWITCH BOX 8.1.3)

- 🌟 **[Надёжная конвертация крупных образов 3DS/CCI -> CIA/ZCIA (`Nintendo3dsService.cs`)]**:
  - Устранено падение `makerom.exe -ccitocia` (`Access Violation 0xC0000005`) на картриджных дампах Nintendo 3DS / New Nintendo 3DS объёмом `>= 1.875 ГБ` (включая 4 ГБ образы уровня *Xenoblade Chronicles 3D*), а также отказ `[NCCH ERROR] AccessDesc Sigcheck Failed` на расшифрованных No-Intro дампах без флага `-ignoresign`.
  - Внедрён новый движок `ConvertCciToCiaReliableAsync`: для образов `< 1.875 ГБ` с коротким путём сначала пробуется быстрый `-ignoresign -ccitocia`, а для крупных образов (`>= 1.875 ГБ`) и при любом отказе/краше выполняется прямое чтение заголовка NCSD (`0x120`) и потоковое извлечение только игровых разделов `0..2` (`p0.cxi`, `p1.cfa`, `p2.cfa` без разделов обновления прошивки `6..7`) с прямой сборкой через `makerom -f cia -ignoresign -target p` (с резервом `-target t`).
  - В `ConvertCiaToCciReliableAsync` добавлена многоступенчатая конвертация CIA -> CCI/3DS с приоритетом флага `-ignoresign`, а в `Build3dsFromExtractedDirAsync` флаг `-ignoresign` и резерв через `ConvertCciToCiaReliableAsync` защищают двойную сборку `3DS + CIA`.
- 🌟 **[Короткие временные пути 3DS и защита от `MAX_PATH` (`Nintendo3dsService.cs`, `TempCleanupService.cs`)]**:
  - Добавлены хелперы `CreateShortTempDirectory` и `CleanupShortTempDirectory`, создающие компактные рабочие каталоги формата `<диск>:\STORM_TMP\<tag>_<6hex>` на быстром фиксированном диске с достаточным свободным местом (с безопасным резервом `%TEMP%\S3D_<tag>_<6hex>` и автоудалением пустого `STORM_TMP` и паттерна `S3D_*` в `TempCleanupService`).
  - Короткие пути и сокращённые имена подпапок (`b`, `u1`, `d1`, `c_...`) внедрены в `Build3dsMultiContentAsync`, `Extract3dsContainerToDirAsync`, `Convert3dsContainerAsync`, `Unpack3dsContainerAsync` и `Pack3dsContainerAsync`, исключая ошибку `ctrtool.exe` `[tc::io::VirtualFileSystem::openFile() ERROR] File does not exist.` на глубоких деревьях RomFS.
- 🌟 **[Синхронизированное обновление Windows и Android 8.1.3]**:
  - Версия обновлена до `8.1.3` (`versionCode = 8103` на Android) для всех сборок Windows x64 (`Setup.exe` и Portable ZIP) и Android (`Modern` и `Legacy`).

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/Nintendo3dsService.cs]**: Добавлены `HasValidOutputFile`, `TryDeleteFileSilent`, `CreateShortTempDirectory`, `CleanupShortTempDirectory`, `ConvertCciToCiaReliableAsync` и `ConvertCiaToCciReliableAsync`; все прямые вызовы `-ccitocia` и `-ciatocci` в `Convert3dsContainerAsync` и `Build3dsFromExtractedDirAsync` переведены на надёжную логику с разбором NCSD (`0..2`) и `-ignoresign`; временные каталоги `Build3dsMultiContentAsync`, `Convert3dsContainerAsync`, `Unpack3dsContainerAsync` и `Pack3dsContainerAsync` переведены на короткие пути `STORM_TMP`.
- 🔹 **[Services/TempCleanupService.cs]**: В `tempDirPatterns` добавлен шаблон `"S3D_*"` для автоматической очистки резервных временных папок 3DS.
- 🔹 **[StormSwitchBox.csproj, App.xaml.cs, Build_Release.ps1, Services/Z3dsService.cs, app.manifest, installer/StormInstaller/*, android/app/build.gradle.kts, StormNczEngine.kt, README.md]**: Версия продукта обновлена до `8.1.3` (`versionCode = 8103`).

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `makerom -ccitocia` crashes (`0xC0000005` on `>= 1.875 GB` / 4 GB NCSD images such as *Xenoblade Chronicles 3D*) and `AccessDesc Sigcheck Failed` errors on decrypted No-Intro dumps by introducing `ConvertCciToCiaReliableAsync`: extracts game NCSD partitions `0..2` (`NCCH`) directly from `0x120` (skipping firmware update partitions `6..7`) and packages via `makerom -f cia -ignoresign -target p`.
- 🔸 Added `ConvertCiaToCciReliableAsync` and `-ignoresign` handling for `CIA -> CCI/3DS` and dual `3DS + CIA` builds in `Build3dsFromExtractedDirAsync`.
- 🔸 Added `CreateShortTempDirectory` (`<drive>:\STORM_TMP\<tag>_<6hex>` with `%TEMP%\S3D_*` fallback) and shortened 3DS extraction folder names across all 3DS multi-content, convert, unpack, and pack pipelines to eliminate Windows `MAX_PATH` failures in `ctrtool.exe`.
- 🔸 Updated unified Windows x64 and Android Modern/Legacy releases to version `8.1.3` (`versionCode 8103`).

</details>
---

### 📦 Выбор сборки и архитектура

| Файл | Платформа | Архитектура | Назначение и отличие |
| :--- | :--- | :--- | :--- |
| **`STORM_SWITCH_BOX_8.1.3_Setup.exe`** (219.77 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Рекомендуемый установщик для ПК**: автоматически устанавливает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.1.3.zip`** / **`_win-x64.zip`** (151.32 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.1.3.apk`** (14.45 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.1.3_LEGACY.apk`** (26.02 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS/Z3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.1.3_Setup.exe** (219.77 МБ)
  - `SHA-256: C5F7C97027F4CFAE665C615C8ABE0231CA0AAC044DA22B2B90D50A8F6698425E`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.1.3_win-x64.zip** / **STORM_SWITCH_BOX_8.1.3.zip** (151.32 МБ)
  - `SHA-256: D97E56AF77A6B2EF33E6A90B7A4EB58893160257A7F166623DD2B93ABFB6D6D7`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.1.3.apk** (14.45 МБ)
  - `SHA-256: 6951570BF63F6B27E0BDE3C07B3AC2CB34C0373DA79DB77EAA73E1022D6F2250`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.1.3_LEGACY.apk** (26.02 МБ)
  - `SHA-256: 64A5E99A2446EB2AA3685541F303FDA7F5BEAB10120C73095372947331B2044F`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>