> 💡 **Релиз 8.2.4** - *Мгновенное определение `Title ID`, метаданных и списка `.torrent`-файлов (`Mario + Rabbids: Sparks of Hope`), всегда видимый `Title ID` слева от расширенной версии и ускорение загрузки всех тем RuTracker*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Мгновенное заполнение `Title ID`, метаданных и списка `.torrent`-файлов (`t=6271728` и все темы RuTracker)]**: Исключён перехват названия синтетической Magnet-заглушкой в `EnrichGameSlotsFromFilesAndTitleDb`, добавлен фолбэк поиска TitleDB по имени слота и заголовку, а фоновое получение `.torrent` (параллельная гонка кэш-зеркал `itorrents` и P2P `ut_metadata`) стартует сразу без ожидания HTML/ТСПУ-таймаутов.
- 🌟 **[Обновлённая 3-колоночная строка слота и всегда видимый `Title ID`]**: Поле «Доп. облачные ссылки только для этой части» сужено, блок чипов сдвинут левее (`0.22*` правый отступ), `Title ID` (`TitleIdChipDisplay`) всегда виден слева от версии и зафиксирован по размеру вместе с внутренней версией, а поле версии расширено (`MinWidth="124"`).
- 🌟 **[Глобальное ускорение загрузки тем и торрентов]**: Увеличены таймауты и снята блокировка по временным таймаутам зеркал, предзагрузка топ-8 тем теперь заранее сохраняет дерево файлов, HTML-парсер тем использует 5-минутный ТСПУ-кулдаун и параллельную гонку облачных рендеров, а `StormHttpClientFactory` расширен до 64 соединений.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Загрузчик RuTracker и метаданные]**: Фильтр `IsRealTorrentFile` + фолбэк `slot.GameTitle` в `EnrichGameSlotsFromFilesAndTitleDb`, запуск `StartMagnetMetadataBackground` по `!_activeTopicHasRealFiles`, `TitleIdChipDisplay`, параллельный `ResolveMagnetMetadataBackgroundAsync`, удаление нерабочего `torrage.info`, 5-минутный ТСПУ-кулдаун в `FetchValidatedTopicPageHtmlAsync` и пул `MaxConnectionsPerServer = 64`.
- 🔹 **[Интерфейс слота игры]**: 3-колоночная сетка в `RuTrackerModDownloaderDialog.xaml` с суженным полем доп. ссылок, всегда видимым `Title ID`, расширенной кнопкой версии и фиксированными `Title ID` / внутренней версией.
- 🔹 **[Релиз 8.2.4]**: Версии синхронизированы до `8.2.4` (`versionCode = 8204`), в релизе публикуются установщик Windows, портативный архив и актуальная сборка Android APK без Legacy.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `EnrichGameSlotsFromFilesAndTitleDb` ignoring Magnet placeholder strings with `slot.GameTitle` TitleDB fallback and launched parallel `.torrent`/BEP-9 resolution immediately in `LoadSelectedTopicAsync` without waiting behind RuTracker HTML/TSPU timeouts.
- 🔸 Added always-visible `TitleIdChipDisplay` and converted the GameSlot extra-links row to a 3-column grid (`1*`, `Auto`, `0.22*`), narrowing the extra-links box, shifting `Title ID`, `Version`, and `Internal Version` leftward, widening `Version`, and keeping `Title ID` and `Internal Version` fixed.
- 🔸 Accelerated all RuTracker loads (12s mirror race without timeout poisoning, top-8 filetree prefetching, 5-minute TSPU direct-mirror cooldown with parallel cloud race, `StormHttpClientFactory` 64 connections) and bumped version to `8.2.4` (`versionCode = 8204`).

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.4_Setup.exe`** (`219.84 МБ`, SHA-256: `4081CDE1B6553E37530A9F6F614DEC74CFA69C7337DBC8F1E5B3619982BA8141`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.4_win-x64.zip` / `STORM_SWITCH_BOX_8.2.4.zip`** (`151.39 МБ`, SHA-256: `AD8EF894338374AA598B93119071D83BD57A06476EC13C5E0C20F2E1BF15E6AA`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.4.apk`** (`14.47 МБ`, SHA-256: `871FE2B666454FCD8F11582D0A553757A3797BCAC2C6BCFAACE2F645C628DE67`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
