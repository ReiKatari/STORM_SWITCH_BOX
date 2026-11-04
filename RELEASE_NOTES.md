> 💡 **Релиз 6.9.0** — *Поддержка мульти-игровых раздач и слот-менеджер RuTracker, расширенное кэширование обложек с управлением автозагрузкой, гибридный распаковщик RomFS/ExeFS для нестандартных обновлений и синхронизация Android-клиента*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker Слот-Менеджер Мульти-Раздач] Поддержка комплектов игр и сборников в одном торренте**: модуль RuTracker теперь полноценно поддерживает раздачи, содержащие несколько независимых игр (например, антологии, бандлы или сплит-версии). В интерфейс добавлен динамический менеджер игровых слотов (`RuTrackerGameSlotItem`, бейджи `ИГРА #1`, `ИГРА #2`), позволяющий индивидуально настраивать заголовок мода, ручные ссылки и фильтровать файлы торрента для каждой игры в отдельную сборочную задачу.
- 🌟 **[Кэш Обложек и Настройки] Умный сервис кэширования обложек (`CoverCacheService`)**: внедрено централизованное кэширование постеров и обложек игр с многоуровневым поиском (папки лаунчера, системные директории, эмуляторы). Добавлена пользовательская настройка `AutoDownloadCovers` («Автоматически загружать обложки игр») на вкладке «Параметры», исключающая нежелательный сетевой трафик и фоновые дисковые операции при отключении.
- 🌟 **[Hard Patch Engine] Гибридный LibHac/Hactoolnet распаковщик для Non-BKTR обновлений**: реализован отказоустойчивый нативный распаковщик RomFS и ExeFS (`ExtractMergedRomFsNativeFallbackAsync`), перехватывающий ошибки `yanu-cli unpack` в случаях, когда обновление игры не содержит дельта-патча BKTR (например, ExeFS-only или чистые накопительные апдейты). Добавлена автоматическая перепаковка `Control NCA` через `hacpack` в чистый `StandardCrypto` с базовым TitleID.
- 🌟 **[Android Паритет и Синхронизация] Релиз 6.9.0 для Android**: обновлены версия и код сборки (`versionCode = 690`, `versionName = "6.9.0"`), обеспечена 100% совместимость с API RuTracker и синхронизация обеих редакций пакетов (`Modern` 64-bit и `Legacy` 32-bit).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Слот-менеджер RuTracker]**: реализованы классы `RuTrackerGameSlotItem` с привязкой к визуальному дереву диалога, поддержкой раздельной маски файлов и индивидуальных ссылок модификаций.
- 🔹 **[Опция AutoDownloadCovers]**: в `AppSettings.cs` и `SettingsPage.xaml` добавлен тумблер управления фоновой загрузкой постеров с сохранением в конфигурационный JSON.
- 🔹 **[Отказоустойчивость HardPatchEngine]**: устранены сбои слияния RomFS/ExeFS для игр со сложной структурой разделов, поддержана регенерация `control.nacp` и иконок для базового `TitleID`.
- 🔹 **[Синхронизация Android]**: актуализированы строковые ресурсы, заголовки лога и карточка обновления `RuTrackerCloudEngine`.
- 🔹 **[Контроль целостности инсталлятора]**: обновлена конфигурация `StormInstaller` и манифесты сборщика.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker Multi-Game Slot Manager**: added support for multi-game torrent bundles with per-game slot configuration (`RuTrackerGameSlotItem`, badges `GAME #1`, `GAME #2`), allowing independent mod title customization, manual link routing, and selective file assignment per task.
- 🔸 **Advanced Cover Cache Service with User Toggle**: integrated central cover cache engine (`CoverCacheService.cs`) with multi-tier folder resolution and added the `AutoDownloadCovers` setting in Settings to avoid unwanted background web downloads.
- 🔸 **Hard Patch Engine Native RomFS/ExeFS Hybrid Fallback**: added automatic fallback unpacking via LibHac and hactoolnet for updates lacking BKTR patch NCAs, with automatic `Control NCA` standard crypto repackaging.
- 🔸 **Android Parity 6.9.0**: bumped version to 6.9.0 (`versionCode 690`) across Modern and Legacy editions.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
