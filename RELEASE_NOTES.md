> 💡 **Релиз 6.9.3** — *Прямая интеграция с qBittorrent WebUI для загрузки на полной скорости канала, селектор целевого формата конвертации для всех слотов раздачи и оптимизация ядра HardPatch*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[qBittorrent WebUI & Line-Speed Torrent Engine] Прямая интеграция с локальным клиентом**: внедрено бесшовное подключение к работающему локальному клиенту qBittorrent через WebUI API (`127.0.0.1:8080`, автоопределение порта и учетных данных из `qBittorrent.ini`). Загрузка файлов раздачи происходит на полной скорости канала (6–50+ МБ/с) с обходом анти-личерской фильтрации сидов (qBittorrent Enhanced / BEP-29 uTP / BEP-55 holepunch) и фоновой инъекцией обнаруженных через HTTP/1.1 Pipelining пиров.
- 🌟 **[Выбор Целевого Формата Конвертации] TargetFormat Selector для всех слотов раздачи**: в интерфейсе диалога RuTracker (`RuTrackerModDownloaderDialog`) добавлен выбор целевого формата (`NSP`, `NSZ`, `XCI`, `XCZ`, `NSP + NSZ`, `XCI + XCZ`) как индивидуально для каждого игрового слота, так и пакетно для всех слотов сразу, с автоматической передачей формата в очередь задач.
- 🌟 **[HardPatch & Multi-Content Engine] Оптимизация сборки многофайлового контента**: усовершенствованы алгоритмы прямого слияния патчей и модификаций RomFS/ExeFS (`HardPatchEngine` и `MultiContentService`) с повышенной надежностью валидации NCA-экстентов и очистки временных дескрипторов.
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.3**: обновлены версия и код сборки (`versionCode = 693`, `versionName = "6.9.3"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) с поддержкой новых форматов и синхронизированным сетевым ядром.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Интеграция qBittorrent WebUI]**: реализован метод `TryDownloadViaLocalQBitAsync` с авторизацией, контролем паузы/отмены, выборочным скачиванием файлов и авто-очисткой торрент-задачи при завершении.
- 🔹 **[Сбор пиров и инъекция через HTTP Pipelining]**: разработан харвестер `HarvestRawPeerStringsFromTrackersAsync` для передачи пиров напрямую в локальный клиент.
- 🔹 **[Селектор целевого формата в UI]**: добавлено свойство `TargetFormat` в модель слота `RuTrackerGameSlot` и глобальный комбобокс выбора формата в шапке загрузчика.
- 🔹 **[Настройки движка MonoTorrent]**: расширен дисковый кэш до 128 МБ (`DiskCacheBytes`), оптимизировано максимальное количество полуоткрытых соединений (`MaximumHalfOpenConnections = 40`) и зафиксирован выделенный порт DHT.
- 🔹 **[Инсталлятор StormInstaller]**: конфигурация сборки и метаданные развёртывания обновлены до версии 6.9.3 с сохранением цифровой подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **qBittorrent WebUI High-Speed Integration**: seamless direct pipeline to local qBittorrent instance via WebUI API (`127.0.0.1:8080`, automatic credential/port detection from `qBittorrent.ini`), unlocking full line speed (6-50+ MB/s), bypassing qBittorrent Enhanced anti-leech peer bans, with automated peer harvesting from HTTP/1.1 Pipelining trackers.
- 🔸 **Per-Game & Global Target Format Selector**: added flexible output format selection (`NSP`, `NSZ`, `XCI`, `XCZ`, `NSP + NSZ`, `XCI + XCZ`) across all RuTracker multi-game download slots.
- 🔸 **HardPatch & MultiContent Core Enhancements**: improved RomFS/ExeFS direct patching reliability, NCA extent validation, and task queuing pipeline.
- 🔸 **Android Parity 6.9.3**: synchronized Modern and Legacy Android editions to version 6.9.3 (`versionCode 693`).

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
