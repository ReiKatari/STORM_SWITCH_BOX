> 💡 **Релиз 8.2.1** - *Внутренние русские и английские названия игр из NACP/SMDH во вкладке «Информация», адаптивная панель поиска без наложения и широкий стилизованный справочник системных кодов ошибок*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Внутренние названия игр в «Информации» (NACP / SMDH)]**: Во вкладке «Информация» теперь выводятся именно те названия, которые вшиты внутрь самой игры (`control.nacp` для Switch и `SMDH` для Nintendo 3DS). Добавлено полное потоковое чтение всех `0x4000` байт NACP, корректная обработка слота `11` (русский) и слота `0`/`1` (английский), защита от перезаписи базой TitleDB, показ второго вшитого названия в подсказке и заголовке деталей, а также поиск сразу по обоим внутренним названиям.
- 🌟 **[Адаптивная шапка вкладки «Информация» без наложения элементов]**: Переработана верхняя панель `CatalogPage`: кнопки сканирования, прокручиваемый фильтр платформ и статус TitleDB вынесены в верхнюю строку, а гибкое поле поиска (`Width="*"`) и кнопка **«Коды ошибок (2xxx-xxxx)»** размещены отдельно с чётким отступом и больше никогда не перекрывают друг друга.
- 🌟 **[Широкий стилизованный справочник кодов ошибок Switch и Atmosphère]**: Справочник перенесён в отдельное широкое окно `ErrorCodesDialog` (до `1120x760`), полностью адаптируемое под все 8 тем оформления **STORM SOFT**, с категориями фильтрации, поиском по кодам и симптомам, 10 подробными карточками решений, кнопкой копирования в буфер обмена и поддержкой 11 языков.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Windows: Models, Services, Views]**: Обновлены `CatalogItem.cs`, `Nintendo3dsInfo.cs`, `CatalogScannerService.cs` (`ReadStreamFull`, слоты Base/Update NACP `11` и `0`/`1`, `IntegrityCheckLevel.None` для Control NCA, приоритет `icon_Russian.dat`), `Nintendo3dsService.cs` (отдельное чтение слотов `11` и `1` SMDH), `TitleDbService.cs` (защита `HasEmbeddedInternalTitle`), `CatalogPage.xaml(.cs)`, `ErrorCodesDialog.xaml(.cs)` и `LocalizationService.UiTree.cs`.
- 🔹 **[Android: StormNczEngine.kt]**: Добавлена функция `extractSmdhMeta` с приоритетным чтением русского и английского слотов SMDH (`11 -> 1 -> 0`) вместо смещённого слота `0`.
- 🔹 **[Версия 8.2.1]**: Синхронизированы версии Windows (`8.2.1`) и Android (`versionCode = 8201`, `versionName = "8.2.1"`), обновлены `README.md`, `CHANGELOG.md` и `RELEASE_NOTES.md`.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed the Info view (`CatalogScannerService`, `Nintendo3dsService`, `TitleDbService`) to display embedded game titles directly from Switch `control.nacp` (full `0x4000`-byte read, Russian slot `11` + English slots `0`/`1`) and 3DS `SMDH` (slots `11` and `1`) instead of letting TitleDB overwrite custom or localized internal titles, with dual-language search and secondary title display.
- 🔸 Fixed the Info view header layout (`CatalogPage.xaml`) into two responsive rows so the search box never overlaps the `Error codes (2xxx-xxxx)` button.
- 🔸 Added a wide, theme-synchronized `ErrorCodesDialog` (`ErrorCodesDialog.xaml(.cs)`) with category filter pills, live search across 10 detailed Switch/Atmosphère error entries, one-click clipboard copy, and 11-language support.

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.1_Setup.exe`** (`219.84 МБ`, SHA-256: `F79C7E4F1ACE86E55C9C5F95BA45EE08BEFDC3B848A462646D3508739518D9AA`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.1_win-x64.zip` / `STORM_SWITCH_BOX_8.2.1.zip`** (`151.39 МБ`, SHA-256: `B716DBFA8EC376F61261264777C0AC54E903CC7D747121F7B7AB66AF72614143`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.1.apk`** (`14.47 МБ`, SHA-256: `614A7C6517D376FD11E36ED91E4E95F394BC4DF36A916F81620DC43D549226A6`) - **Modern** сборка Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 📱 **`STORM_SWITCH_BOX_8.2.1_LEGACY.apk`** (`26.04 МБ`, SHA-256: `2B00F12CEEBBFEC1E92BA9D5B43954C73272C24EFF4CF21D0D3A3E767897293A`) - **Legacy** универсальная сборка Android 5.0+ (`minSdk 21`, `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
