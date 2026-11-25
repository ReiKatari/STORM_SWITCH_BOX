> 💡 **Релиз 8.2.2** - *Мгновенное и точное определение версий игр в RuTracker (включая `3.0.5b`), внутренние названия NACP/SMDH в «Информации», адаптивная шапка и новый стилизованный справочник кодов ошибок*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Мгновенное определение версий игр в RuTracker (включая буквенные `3.0.5b`)]**: Устранено обрезание версий вроде `3.0.5b` до `3.0` во всех регулярных выражениях загрузчика, базы и сканера (`ExtractGameVersionsFromTopicHtml`, `ScanVerTextSource`, `IsValidSemanticVersion`, `CompareSemanticVersions`, `StormTitleTripleRegex`, `CatalogScannerService`). Отображаемая версия теперь подтягивается сразу после выбора раздачи и чтения списка файлов `.torrent` (включая параллельный поиск по точному `TitleID` на `stormgamesworld.ru`), без необходимости вводить её вручную в поле выходного имени, а кэш тем `SchemaVersion = 3` автоматически обновляет старые записи.
- 🌟 **[Внутренние русские и английские названия игр в «Информации» (NACP / SMDH)]**: Во вкладке «Информация» выводятся именно те названия, которые вшиты внутрь самой игры (`control.nacp` для Switch с полным чтением `0x4000` байт, слот `11` русский и слоты `0`/`1` английский, а также слоты `11` и `1` в `SMDH` для 3DS), с защитой от перезаписи базой TitleDB, показом второго внутреннего названия в подсказке и деталях, и поиском сразу по обоим языкам.
- 🌟 **[Адаптивная шапка «Информации» и широкий стилизованный справочник кодов ошибок]**: Поле поиска и кнопка **«Коды ошибок (2xxx-xxxx)»** разнесены в адаптивные строки шапки и больше не накладываются друг на друга. Справочник кодов ошибок Switch и Atmosphère перенесён в широкое окно `ErrorCodesDialog` (до `1120x760`) под все 8 тем оформления **STORM SOFT**, с категориями, живым поиском по 10 подробным карточкам и кнопками копирования.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Загрузчик RuTracker и версии]**: Исправлен бэктрекинг регулярок `3.0.5b -> 3.0`, добавлена поддержка суффиксов `[a-zA-Z]{1,2}` / `-hf...`, ускорен параллельный поиск версии по `TitleID` без вечной блокировки ошибок и повышена `SchemaVersion` кэша тем RuTracker до `3`.
- 🔹 **[Вкладка «Информация» и метаданные]**: Обновлены `CatalogItem.cs`, `Nintendo3dsInfo.cs`, `CatalogScannerService.cs`, `Nintendo3dsService.cs`, `TitleDbService.cs`, `CatalogPage.xaml(.cs)` и Android `StormNczEngine.kt` (`extractSmdhMeta`).
- 🔹 **[Справочник ошибок и вёрстка]**: Создан широкий стилизованный `Views/Dialogs/ErrorCodesDialog.xaml(.cs)` с переводами на 11 языков (`LocalizationService.UiTree.cs`) и исправлена двухстрочная шапка `CatalogPage.xaml`.
- 🔹 **[Релиз 8.2.2]**: Синхронизированы версии Windows (`8.2.2`) и Android (`versionCode = 8202`, `versionName = "8.2.2"`); публикация сфокусирована на трёх основных артефактах без устаревшей сборки Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed immediate display version detection in the RuTracker downloader and semantic parsers (`TitleDbService`, `RuTrackerModService`, `RuTrackerModDownloaderDialog`, `CatalogScannerService`) so alphanumeric versions such as `3.0.5b` never truncate to `3.0`, triggering TitleID online resolution immediately when torrent files load.
- 🔸 Updated the Info view (`CatalogScannerService`, `Nintendo3dsService`, `TitleDbService`, `CatalogItem`) to prioritize embedded Switch `control.nacp` and 3DS `SMDH` Russian/English titles, added a non-overlapping two-row responsive header in `CatalogPage.xaml`, and introduced a wide theme-aware `ErrorCodesDialog`.
- 🔸 Bumped version to `8.2.2` (`versionCode = 8202`) and streamlined distribution to Windows Setup, Windows Portable ZIP, and Modern Android APK.

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.2_Setup.exe`** (`219.84 МБ`, SHA-256: `3076B1F97E9855652D7A84C94BA82B9DB008BB135A6FFE8B6357A7F8FE3F1FD1`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.2_win-x64.zip` / `STORM_SWITCH_BOX_8.2.2.zip`** (`151.39 МБ`, SHA-256: `C88BA4377312ABD7E782D4D1F2E3CF10072E139A37CB782FC79C4858A03390F1`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.2.apk`** (`14.47 МБ`, SHA-256: `F8C52999F7EF71DA484C425EC240A0B0325A058E63F8A8113A54641AB0A0C005`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
