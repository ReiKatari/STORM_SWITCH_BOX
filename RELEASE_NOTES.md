> 💡 **Релиз 6.7.9** — *Фоновая работа при сворачивании и сохранение задач на паузе при закрытии, прямой сбор сидов через Direct Tracker Peer Harvester и 100% автоматический обход проверок Cloudflare Turnstile*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Менеджер задач] Фоновая работа при сворачивании и сохранение задач на паузе при закрытии (Persistent Background Task Engine)**: при сворачивании приложения активные задачи продолжают выполнение и скачивание в фоне благодаря системным блокировкам `WakeLock` и `WifiLock` на Android и фоновой службе системного трея на Windows. При закрытии программы все текущие задачи автоматически переводятся в статус «⏸ Сохранено на паузе» с сохранением состояния на диск (`bg_tasks_state.json` / `persisted_tasks.json`). При следующем запуске задачи восстанавливаются в списке и продолжают скачивание с сохранённой позиции по кнопке «▶️ Продолжить».
- 🌟 **[P2P / Сеть] Максимальный сбор сидов Direct Tracker Peer Harvester и Multi-Wave Swarm Boost**: внедрён прямой параллельный сбор компактных списков пиров (`5:peers` и `7:peers6`) с официальных открытых magnet-анонсеров RuTracker (`bt.t-ru.org/ann?magnet`, `bt2`, `bt3`, `bt4`, `retracker.local`) напрямую и через обходные прокси-шлюзы, а также с 32 верифицированных открытых трекеров. Цикличное пополнение роя каждые 20 секунд (HTTP Harvester + 160-bit Big-Endian Kademlia DHT) и поддержка `UPnP / NAT-PMP` с расширением пула до 500 соединений (`connection_speed = 250` попыток/сек, `unchoke_slots_limit = 100`) гарантируют максимальную скорость отдачи.
- 🌟 **[Облачные моды] 100% автоматический обход Cloudflare Turnstile (Zero-Click Auto-Solver)**: многоканальная гонка облачных рендер-прокси (`Jina Headless Chrome`, `CodeTabs`, `AllOrigins`, `CorsProxy`), дисковый кэш ссылок (`rutracker_topic_links_cache.json`) и полностью автоматический кликер Turnstile в скрытом браузерном движке (`WebView` / `WebView2` с маскировкой `navigator.webdriver` и аппаратной эмуляцией клика по координатам) обеспечивают мгновенное извлечение ссылок без ручного участия пользователя.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Task State Persistence]**: сохранение и восстановление метаданных незавершённых задач при перезапуске приложения (`persisted_tasks.json` на Windows и `bg_tasks_state.json` на Android).
- 🔹 **[Swarm Harvester]**: фоновый поллер опрашивает анонсеры раздачи по протоколу BEP-15 / BEP-23 с компактным бинарным декодированием IPv4 и IPv6 адресов.
- 🔹 **[Turnstile Stealth Clicker]**: инъекция скрипта симуляции тача и клика по iframe Cloudflare с рандомизацией таймингов и маскировкой автоматизации.
- 🔹 **[Android Locks]**: удержание процессора и радиомодуля Wi-Fi (`PARTIAL_WAKE_LOCK`, `WIFI_MODE_FULL_HIGH_PERF`) при активных сетевых загрузках в свернутом состоянии.
- 🔹 **[MonoTorrent Boost]**: увеличение лимита полуоткрытых соединений и слотов раздачи для быстрого выхода на гигабитные скорости.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Persistent Background Task Engine**: ongoing tasks continue uninterrupted in background upon minimizing (`WakeLock` + `WifiLock` on Android, system tray on Windows). On application exit, running tasks auto-save in paused state (`persisted_tasks.json` / `bg_tasks_state.json`) and instantly resume from the exact byte offset upon clicking "Resume".
- 🔸 **Direct Tracker Peer Harvester & Multi-Wave Swarm Boost**: direct parallel extraction of compact peer lists (`5:peers` and `7:peers6`) from RuTracker magnet announcers (`bt.t-ru.org/ann?magnet`, `bt2-bt4`, `retracker.local`) and 32 verified public trackers with 20-second cyclical replenishment, UPnP/NAT-PMP, and 500 peer capacity (`connection_speed = 250`, `unchoke_slots = 100`).
- 🔸 **Zero-Click Cloudflare Turnstile Auto-Solver**: multi-channel cloud render proxy race (`Jina Headless Chrome`, `CodeTabs`, `AllOrigins`, `CorsProxy`), disk link cache, and fully automated stealth Turnstile clicker in headless `WebView`/`WebView2` with `navigator.webdriver` masking and simulated touch dispatch.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
