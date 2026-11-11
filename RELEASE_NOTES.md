> 💡 **Релиз 7.0.13** - *Высокоскоростной нативный загрузчик RuTracker на libtorrent 2.1.1 + Cloudflare Pipelining, мгновенная реакция UI темы (< 5 мс) с параллельной гонкой инспекции первой страницы (~0.35s), хирургическое исправление парсера вложенных скобок [ENG], интерактивные 1-клик плашки копирования TitleID / Версии без "v" / Внутренней версии, ликвидация паразитных окон qbittorrent и дедлоков телеметрии*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker High-Speed Native Download Engine Fix]**: Полностью восстановлена нативная полноскоростная загрузка торрентов через встроенный C++ движок `libtorrent 2.1.1` (`storm_lt_turbo.py`) и прямой комбайн сбора пиров Cloudflare HTTP/1.1 Pipelining (`bt.t-ru.org`..`bt4.t-ru.org`). Реализовано мгновенное разрешение торрент-кэшей и зеркал с нулевым ожиданием метаданных (`0s`), регулярный телеметрический пульс в состоянии паузы для предотвращения дедлока stdout в C#, автоматическое восстановление зомби-задач из статуса "ПАУЗА" при старте и полное устранение нежелательных всплывающих окон внешнего `qbittorrent.exe`.
- 🌟 **[Topic Selection Speed Optimization (< 5 ms Instant UI + Concurrent Page-1 Race)]**: Мгновенный отклик интерфейса при клике на раздачу в каталоге (< 5 мс) за счет локальной гидратации слотов и файлов из индекса с отменой устаревших кликов. Организована параллельная гонка инспекции торрента и первой страницы темы через HTTP/WebView2 (~0.35s), фоновый неблокирующий сбор ссылок на моды из комментариев и замена тяжелого дискового HTML-кэша (29+ МБ) компактным асинхронным хранилищем метаданных.
- 🌟 **[Nested Bracket Title Parser Fix ("ENG]" Bug)]**: Исправлен алгоритм очистки названий игр `ExtractCleanGameTitleFromFilePath` в C# и Android Kotlin. Устранены ошибки обрезки названий при наличии вложенных скобок и составных тегов вроде `+ Wave 4 DLC` и `[NSP][RUS (Mod)/ENG]` (например, для *Xenoblade Chronicles X Definitive edition*, *Xenoblade Chronicles 3*), а также добавлен O(1) поиск по нормализованной базе `TitleDbService` для раздач без явного `[TitleID]`.
- 🌟 **[1-Click Copy Pills for TitleID, Version, and Internal Version]**: Статический бейдж метаданных заменен на 3 независимые интерактивные плашки быстрого копирования в буфер обмена (`TitleID`, `Версия` строго без префикса "v", `Внутренняя версия`) в развернутых и свернутых карточках игр, студии NACP и диалоге Android.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Libtorrent Native Turbo]**: Интегрирован прямой сбор аннонсов и скрейпов через Cloudflare Pipelining, оптимизирован буфер памяти для скоростей 100+ Мбит/с.
- 🔹 **[Deadlock Prevention]**: Потоки stdout и stderr вспомогательного процесса `storm_lt_turbo.py` переведены на неблокирующее асинхронное чтение с тайм-аутом.
- 🔹 **[Stale-Click Cancellation]**: При быстрой навигации по раздачам предыдущие запросы HTTP немедленно отменяются через `CancellationTokenSource`.
- 🔹 **[Compact Metadata Cache]**: Вместо полного дампа HTML-страниц сохраняется только структурированный JSON с распарсенными ссылками и атрибутами.
- 🔹 **[Clean Title Regex]**: Регулярные выражения санитизации заголовков обновлены для корректной обработки многоязычных тегов локализации.
- 🔹 **[Version Formatting Compliance]**: Исключен префикс "v" при копировании и отображении номеров версий в соответствии со стандартами экосистемы STORM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker High-Speed Native Download Engine Fix]**: Restored full-speed torrent downloading via embedded C++ `libtorrent 2.1.1` (`storm_lt_turbo.py`) and Cloudflare HTTP/1.1 Pipelining tracker peer harvester (`bt.t-ru.org`..`bt4.t-ru.org`). Enabled instant `.torrent` cache/mirror resolution with 0s metadata wait, pause-state telemetry heartbeats preventing C# stdout deadlock, automatic zombie "ПАУЗА" queue recovery on launch, and complete elimination of external `qbittorrent.exe` window popups.
- 🔸 **[Topic Selection Speed Optimization (< 5 ms Instant UI + Concurrent Page-1 Race)]**: Instant UI reaction time (< 5 ms) on topic selection via local catalog slot/file hydration with stale-click cancellation. Implemented parallel torrent inspection + Page-1 HTTP/WebView2 race (~0.35s), non-blocking background comment link discovery, and replaced legacy 29+ MB raw HTML disk cache with a compact asynchronous parsed metadata cache.
- 🔸 **[Nested Bracket Title Parser Fix ("ENG]" Bug)]**: Fixed `ExtractCleanGameTitleFromFilePath` across Windows C# and Android Kotlin for nested brackets and compound `+ Wave 4 DLC` / `[NSP][RUS (Mod)/ENG]` tags (e.g. *Xenoblade Chronicles X Definitive edition*, *Xenoblade Chronicles 3*) plus O(1) normalized `TitleDbService` name lookup for topics without explicit `[TitleID]`.
- 🔸 **[1-Click Copy Pills for TitleID, Version, and Internal Version]**: Replaced static metadata badge with 3 independent one-click clipboard copy pills (`TitleID`, `Version` strictly without "v" prefix, `Internal Version`) across expanded/collapsed game cards, NACP Studio overlay, and Android dialog.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.13_Setup.exe** (230.52 МБ)
  - `SHA-256: 33B049C0FE35BA8A6D0CE91CC007559F6D935235EB5BFA7251321B03F323CB9D`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.13_Portable.zip** / **STORM_SWITCH_BOX_7.0.13.zip** (162.07 МБ)
  - `SHA-256: 9A864D7CEA93837F640DCDE9F9D85DF2E0F2FDE8C44AC87A4AF5173D93C2B15C`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.13_Android.apk** / **STORM_SWITCH_BOX_7.0.13.apk** (14.36 МБ)
  - `SHA-256: C35BD320C55B33D444F4EE812CE55B5460647881BACB8701371C873ED1929539`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.13_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.13_LEGACY.apk** (25.93 МБ)
  - `SHA-256: D731D4D268D030D7CF7ABFBE34D311A9CB826FE32DEA78C2D6865B5956C5A90F`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
