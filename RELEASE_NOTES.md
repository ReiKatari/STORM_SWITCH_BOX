> 💡 **Релиз 8.0.0** - *Крупное мажорное обновление архитектуры, безопасности и совместимости Nintendo Switch 1/2 и Nintendo 3DS: полное устранение 95 выявленных дефектов аудита, 100% соответствие формату NUP v1.0 для эмулятора STORM SWITCH (Windows и Android), исправление заголовков XCI/NSZ/XCZ и 3DS для Citra/Azahar, единый крипто- и сетевой контур, усиленная защита ключей и сертификатов, сокращение размера дистрибутива за счёт Zstd-сжатия `nutdb.json.zst` и сквозная проверка 26/26 сценариев на реальных образах игр*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.0)

- 🌟 **[100% совместимость NUP v1.0 с эмулятором STORM SWITCH (Windows + Android)]**: В `NupContainerService.cs` и `StormNczEngine.kt` исправлены 3 критических дефекта бинарного формата NUP v1.0 - абсолютное смещение `meta_offset` теперь выносится строго после таблицы оглавления `TOC`, записи `children[]` сортируются побайтно по UTF-8 именам, выравнивание `blob_offset`/`extent_offset` кратно 16 байтам, а для файлов нулевой длины пишется `extent_offset = 0`. Подтверждено прохождением эталонного верификатора `nup.py` и прямым запуском игр Nintendo Switch 1 и Switch 2 в `STORM_SWITCH-cli.exe`.
- 🌟 **[Исправление сборки XCI, XCZ, NSZ и NSP (`StormXciBuilder.cs`, `NszCompressionService.cs`)]**: Устранён дефект смещения поля `nameOffset` (`+0x10` вместо `+0x38`) в `WriteHfs0Entry`, восстановлено автоопределение корневого смещения `HFS0` в `CreatePfsFromStorage` вместо жёсткого `0x10000`, внедрены `SwitchContainerHandle` для безопасного освобождения потоков, `TitleIdHelper` для точного разбора идентификаторов и `StormToolResolver` для единого поиска внешних утилит. Все собранные контейнеры проходят `hactoolnet`, `nsz --verify` и запуск в эмуляторе.
- 🌟 **[Nintendo 3DS (3DS / CCI / CIA / CXI) - совместимость с Citra и Azahar]**: Проверены и стабилизированы нативная дешифровка `SeedDB`/`NCCH Crypto`, упаковка и распаковка `3DS`/`CIA`/`CXI`, инъекция `SMDH`-баннеров и `BCWAV`-звука, обрезка картриджей `SmartShrink` и подтверждена валидность собранных образов через `ctrtool --verify` (все 100% разделов `GOOD`) для стабильного запуска в Citra и Azahar.
- 🌟 **[Безопасность, сеть, сертификаты и гигиена репозитория]**: Полностью удалены захардкоженные ключи (`keys.dat`) и приватный `.pfx` из исходников и дистрибутивов, закрыты уязвимости `Command Injection`, `Zip-Slip` / `Path Traversal` (Windows, Android и `StormInstaller`), добавлена проверка `Host`/`Origin` в локальных HTTP-серверах (`DbiInstallService`, `DirectConsoleSenderService`), внедрён единый пул `StormHttpClientFactory` (устранены утечки сокетов и глобальное отключение TLS-проверок) и настроен строгий `network_security_config.xml` на Android.
- 🌟 **[WinUI 3, локализация, оптимизация памяти и дистрибутива]**: Устранены утечки подписок событий (`Loaded`/`LanguageChanged`/`ThemeChanged`), введено кэширование кистей (`UiBrushCache`), исправлены гонки и блокировки UI-потока в `MainWindow` и диалогах, доведена до 0 предупреждений компиляция C# (`0 errors, 0 warnings`), база `nutdb.json` (47.18 МБ) сжата в `nutdb.json.zst` (10.21 МБ), а из дистрибутива удалены неиспользуемые бинарники (`texconv.exe`, `hacbrewpack.exe`, `hac2l.exe`), дубликаты иконок, `kivy` и `__pycache__` (итоговый архив уменьшен на ~12 МБ).
- 🌟 **[Android Engine, StormInstaller и Python Turbo-загрузчик]**: В `StormNczEngine.kt` исправлены смещения `NACP` (`0x30A0`/`0x30B0`) и добавлен набор модульных тестов `StormBinaryAndLocTest.kt`; в `StormInstaller` устранена проблема извлечения и блокировки файлов с проверкой путей, а в `storm_lt_turbo.py` исправлена обработка исключений, тайм-аутов и корректное завершение потоков.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Core/NCA/StormXciBuilder.cs]**: Исправлено смещение поля `nameOffset` (`+0x10`) в `WriteHfs0Entry` для 100% валидности HFS0-разделов в картриджах XCI/XCZ.
- 🔹 **[Services/NupContainerService.cs & android/.../StormNczEngine.kt]**: Полный бинарный паритет NUP v1.0 с `STORM SWITCH` (`nup_container.cpp`) и `nup.py` - размещение TLV-метаданных строго после TOC, сортировка `children[]` по UTF-8, 16-байтное выравнивание экстентов и `extent_offset = 0` для пустых файлов.
- 🔹 **[Services/NszCompressionService.cs]**: Автоопределение смещения корневого HFS0 (`TryDetectXciRootHfs0Offset`) в `CreatePfsFromStorage` вместо жёсткого `0x10000` и корректное освобождение потоков.
- 🔹 **[Services/StormToolResolver.cs, StormHttpClientFactory.cs, TitleIdHelper.cs, SwitchContainerHandle.cs]**: Вынесены общие переиспользуемые сервисы поиска утилит, HTTP-клиентов с безопасным TLS, разбора TitleID и управления дескрипторами контейнеров Switch.
- 🔹 **[Models/UiBrushCache.cs & Views/*]**: Кэширование кистей WinUI 3, устранение утечек подписок на события смены языка/темы, удаление дублирующихся ветвей в `InstructionPage.xaml.cs` и исправление предупреждения `CS8602` в `SwitchFormatService.cs`.
- 🔹 **[Services/TitleDbService.cs & tools/db/nutdb.json.zst]**: Переход на Zstd-сжатую базу `nutdb.json.zst` (10.21 МБ вместо 47.18 МБ) с прозрачной распаковкой в памяти.
- 🔹 **[Services/DbiInstallService.cs, DirectConsoleSenderService.cs, ExternalProcessRunner.cs]**: Защита от Path Traversal, экранирование аргументов процессов (`ArgumentList`), проверка заголовков локальных серверов и безопасный запуск внешних утилит.
- 🔹 **[installer/StormInstaller]**: Исправление обработки путей при распаковке `Payload.zip` (защита от Zip-Slip), корректная установка сертификата и компиляция с `0 errors, 0 warnings`.
- 🔹 **[android/app]**: Добавлены `network_security_config.xml` и unit-тесты `StormBinaryAndLocTest.kt`, исправлены смещения `NACP` (`0x30A0`/`0x30B0`) и очищена упаковка от `keys.dat`.
- 🔹 **[Гигиена репозитория и дистрибутива]**: Удалены неиспользуемые и устаревшие файлы (`GpuAccelerationService.cs`, `VirtualRamDriveService.cs`, `TinfoilDownloaderService.cs`, `StormIvfcBuilder.cs`, `DebugLogger.cs`, `keys.dat`, `STORM_CodeSign.pfx`, старые `.slnx`/`.iss`, лишние бинарники, дубликаты иконок, `kivy` и `__pycache__`).

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.0)

- 🔸 **[100% NUP v1.0 Parity with STORM SWITCH Emulator (Windows + Android)]**: Fixed three critical binary layout issues in `NupContainerService.cs` and `StormNczEngine.kt` - post-TOC absolute `meta_offset`, UTF-8 byte-sorted `children[]` entries, 16-byte aligned `blob_offset`/`extent_offset`, and `extent_offset = 0` for zero-byte files. Verified with `nup.py` and live Switch 1 & Switch 2 boot tests in `STORM_SWITCH-cli.exe`.
- 🔸 **[XCI, XCZ, NSZ & NSP Container Pipeline Fixes (`StormXciBuilder.cs`, `NszCompressionService.cs`)]**: Fixed `WriteHfs0Entry` `nameOffset` write position (`+0x10` instead of `+0x38`), replaced hardcoded `0x10000` XCI HFS0 offset with dynamic `TryDetectXciRootHfs0Offset`, and introduced shared `StormToolResolver`, `TitleIdHelper`, and `SwitchContainerHandle` services. Verified with `hactoolnet` and `nsz --verify`.
- 🔸 **[Nintendo 3DS (3DS / CCI / CIA / CXI) Citra & Azahar Verification]**: Hardened native `SeedDB`/`NCCH Crypto` decryption, `3DS`/`CIA`/`CXI` repacking, `SMDH` banner and `BCWAV` audio injection, and `SmartShrink` trimming; verified 100% valid partitions via `ctrtool --verify` for Citra and Azahar.
- 🔸 **[Security Hardening, Network Pool & Repo Hygiene]**: Removed bundled `keys.dat` and `.pfx` files from source and packages, fixed command injection and Zip-Slip/path traversal vectors across Windows, Android, and `StormInstaller`, introduced `StormHttpClientFactory` with proper TLS validation, and added Android `network_security_config.xml`.
- 🔸 **[WinUI 3 Stability, Zero Warnings & Distribution Size Reduction]**: Fixed event handler leaks, added `UiBrushCache`, achieved `0 errors, 0 warnings` on Windows and `StormInstaller`, compressed `nutdb.json` (47.18 MB) into `nutdb.json.zst` (10.21 MB), and stripped unused binaries (`texconv.exe`, `hacbrewpack.exe`, `hac2l.exe`), duplicate icons, `kivy`, and `__pycache__`.
- 🔸 **[Android Engine, StormInstaller & Python Turbo Downloader]**: Fixed `NACP` offsets (`0x30A0`/`0x30B0`) in `StormNczEngine.kt`, added JUnit tests (`StormBinaryAndLocTest.kt`), hardened `StormInstaller` extraction/cleanup, and stabilized `storm_lt_turbo.py`.

</details>

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.0_Setup.exe** (218.40 МБ)
  - `SHA-256: 0B51B76EDCECE13B389791745FE52C47698E37D55CC1E5084B8E77910314EBDF`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_8.0.0_win-x64.zip** / **STORM_SWITCH_BOX_8.0.0.zip** (149.85 МБ)
  - `SHA-256: 8E7A2B0EDBE00EC84CB27C8ABC3336CD4EDA13F5F222BC282A63C2B02F6EB49C`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_8.0.0.apk** (14.38 МБ)
  - `SHA-256: 6DDC3DE9FF0DE3C63ECD94F31D5EA8B23D308ED113C9D803F5E387D7AB16CE1D`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_8.0.0_LEGACY.apk** (25.95 МБ)
  - `SHA-256: F29E36FA919AF8F7433B310A1FEADA6AA448324D7365E6E79B9A5FD955B40BD5`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
