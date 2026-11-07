> 💡 **Релиз 7.0.3** — *Высокоскоростная 3-уровневая архитектура загрузки RuTracker, встроенный C++ движок libtorrent-rasterbar, авто-старт qBittorrent WebUI и устранение просадки скорости*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: 3-уровневая архитектура максимальной скорости (Tier 1 + Tier 2 + Tier 3)]**: Полное устранение просадки скорости до 0.45 МБ/с — восстановлен разгон до 15–25+ МБ/с. Автоматический каскад: внешний qBittorrent WebUI -> встроенный C++ libtorrent-rasterbar -> высокоскоростной MonoTorrent.
- 🌟 **[RuTracker: Авто-старт qBittorrent в фоне и обход конфликтов портов]**: Добавлен надёжный бутстрап `EnsureLocalQBitReadyAsync`, запускающий `qbittorrent.exe --no-splash` в свернутом виде и гарантирующий активность WebUI с переключением на порт 18080 при конфликтах с Docker/WSL.
- 🌟 **[RuTracker: Потокобезопасный сборщик пиров с изоляцией по InfoHash]**: Ликвидирована гонка потоков при одновременных ответах `bt..bt4.t-ru.org` и UDP BEP-15. Пулы пиров изолированы по хэшам раздач с многоволновой инжекцией живых сидов на 0с, 2.5с, 6с и каждые 12с.
- 🌟 **[RuTracker: Встроенный нативный C++ движок libtorrent-rasterbar (Tier 2)]**: Разработан модуль `storm_lt_turbo.py` на базе `libtorrent 2.1.1` (uTP BEP-29 + BEP-55 Holepunch) для ПК без установленного qBittorrent.
- 🌟 **[RuTracker: Точный селективный прогресс 0%..100%]**: Расчет через `amount_left` и мгновенное выставление приоритетов файлов с поддержкой объединенных каталогов модов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[TryDownloadViaLocalQBitAsync Bootstrap]**: Устранен преждевременный выход из метода при незапущенном qBittorrent, внедрен синхронизированный запуск через `_qbitBootstrapLock`.
- 🔹 **[Port Conflict Auto-Detection]**: Автоматическое переключение порта WebUI на 18080 в `qBittorrent.ini`, если стандартный порт 8080 занят Docker или системными службами.
- 🔹 **[Thread-Safe Peer Parsing]**: Блокировка списков пиров при десериализации компактных bencoded-списков и UDP-ответов, предотвращающая падение по исключению `List.Add`.
- 🔹 **[Per-InfoHash Scoped Swarms]**: Разделение коллекций обнаруженных HTTP и DHT пиров по хэшу раздачи во избежание перекрестного загрязнения очередей.
- 🔹 **[Multi-Wave Peer Feeder]**: Каскадная инжекция найденных сидов в торрент-клиент на T=0с, T=2.5с, T=6с и затем каждые 12 секунд.
- 🔹 **[Passkey Tracker Injection]**: Автоматическое извлечение персональных passkey-трекеров `http://bt*.t-ru.org/ann?pk=...` из .torrent файла и добавление в сессию загрузки.
- 🔹 **[Embedded libtorrent Engine]**: Полная интеграция C++ движка `libtorrent-rasterbar 2.1.1` через Python-рантайм `tools\nsz\python.exe` с поддержкой uTP и PEX.
- 🔹 **[Clean Cancellation]**: Корректное удаление недокачанного торрента из сессии qBittorrent и завершение дочерних процессов при отмене задачи пользователем.
- 🔹 **[MonoTorrent Tier-3 Restoration]**: Восстановление параметров соединений (800 коннектов, 150 half-open, 256 МБ кэш) в резервном движке.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[3-Tier BitTorrent Architecture]**: Resolved download speed drop to 0.45 MB/s — restored 15–25+ MB/s channel saturation via Tier 1 (qBittorrent WebUI) -> Tier 2 (embedded C++ libtorrent-rasterbar) -> Tier 3 (high-throughput MonoTorrent).
- 🔸 **[qBittorrent Background Auto-Launch]**: Added thread-safe `EnsureLocalQBitReadyAsync` to bootstrap minimized qBittorrent and automatically bypass port 8080 conflicts.
- 🔸 **[Thread-Safe Peer Harvest]**: Fixed race conditions during simultaneous tracker announce processing, scoped peer caches per infohash.
- 🔸 **[Embedded libtorrent Tier 2]**: Integrated `storm_lt_turbo.py` with libtorrent 2.1.1 for users without external torrent clients.
- 🔸 **[Selective Download Progress]**: Accurate progress via `amount_left` and immediate file priorities matching merged mod folders.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
