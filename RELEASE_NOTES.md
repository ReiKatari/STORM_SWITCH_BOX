> 💡 **Релиз 8.2.3** - *Исправление разбиения двуязычных раздач и скачивания через Magnet-заглушки (`Mario + Rabbids: Sparks of Hope`), расширенное поле версии в слоте и оптимизация плавности работы*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Исправление двуязычных раздач RuTracker (`Mario + Rabbids - Sparks of Hope`, `t=6271728`)]**: Темы формата `English Title / Русское название + N DLC` больше не разбиваются ошибочно на 2 отдельные игры, сохраняя корректное разбиение настоящих мульти-сборников (`Bayonetta / Bayonetta 2`, `South Park 1 + 2`), а поиск по локальной базе TitleDB дополнительно распознаёт слово `plus` (`0100317013770000`).
- 🌟 **[Исправление запуска скачивания при Magnet-заглушках и ускорение получения `.torrent`]**: `ComputeSkippedTorrentEntries` теперь фильтрует только реальные файлы торрента (`IsRealTorrentFile`), исключая пропуск всех файлов при старте с заглушки; добавлено зеркало `torrage.info`, увеличены таймауты гонки и усилена гидратация `InfoHash` для живого поиска.
- 🌟 **[Удобное выравнивание полей в карточке игры RuTracker]**: Поле «Доп. облачные ссылки только для этой части» стало компактнее по ширине, бейджи `Title ID`, версии и внутренней версии сдвинуты левее, поле версии расширено, а чипы `Title ID` и внутренней версии сохранили фиксированный размер.
- 🌟 **[Оптимизация скорости отклика и плавности интерфейса]**: Устранены лишние перерисовки `OnPropertyChanged` в очереди закачек, добавлен пропуск фонового тика при простое, кеширование текстов шапки очереди, пакетная замена `ItemsSource` в «Информации» и «Библиотеке игр», фоновый резолв URL обложек и `DecodePixelWidth` в диалогах.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Загрузчик RuTracker и базы]**: `IsBilingualSingleGameSlashPair` в `DetectMultiGameTitlesFromRelease`, защита `IsRealTorrentFile` в `ComputeSkippedTorrentEntries`, `torrage.info`, фолбэк `InfoHash` из индекса, `storm_lt_turbo.py` и `plus`-нормализация в `TitleDbService`.
- 🔹 **[Интерфейс слотов и плавность UI]**: Обновлён `RuTrackerModDownloaderDialog.xaml(.cs)`, оптимизированы `RuTrackerDownloadQueueService.cs`, `CatalogPage.xaml.cs`, `GameLibraryPage.xaml.cs`, `ControlEditorDialog.xaml.cs`, `DirectConsoleSenderDialog.xaml.cs` и `RomFsInspectorDialog.xaml.cs`.
- 🔹 **[Релиз 8.2.3]**: Обновлены версии Windows (`8.2.3`) и Android (`versionCode = 8203`, `versionName = "8.2.3"`), публикуются установщик Windows, портативный архив и сборка Android без устаревшей версии Legacy.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed bilingual `English / Russian` RuTracker topic splitting (`t=6271728` `Mario plus Rabbids Sparks of Hope / Марио и Кролики: Искры Надежды + 7 DLC` now resolves as 1 single game) and added `plus` fallback matching in `TitleDbService`.
- 🔸 Fixed Magnet/DHT placeholder rows causing all real `.torrent` files to be skipped at download start (`ComputeSkippedTorrentEntries` + `storm_lt_turbo.py`), added `torrage.info` cache mirror, and improved live search hash hydration.
- 🔸 Adjusted the GameSlot extra-links row layout (narrower extra-links field, wider version button, fixed Title ID and internal version shifted left).
- 🔸 Improved UI smoothness and responsiveness (`RuTrackerDownloadQueueService`, `RuTrackerModDownloaderDialog`, `CatalogPage`, `GameLibraryPage`, and `DecodePixelWidth` in preview dialogs) and bumped version to `8.2.3` (`versionCode = 8203`).

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.3_Setup.exe`** (`219.84 МБ`, SHA-256: `5BF29B87FA577F1D82566A684B766F2E3C5AFF7FD63363A3196638B366996458`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.3_win-x64.zip` / `STORM_SWITCH_BOX_8.2.3.zip`** (`151.39 МБ`, SHA-256: `67FA940F7FDB1F1AB8EF172EB4FBB96E8B5313B2FC0AAB196EE11A9A74E48559`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.3.apk`** (`14.47 МБ`, SHA-256: `A5564CE47194BA8D24250495FD33CB09D4467A04A23F80741C0B9E6D77CC6320`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
