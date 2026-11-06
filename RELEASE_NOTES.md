> 💡 **Релиз 6.9.8** — *Полное раскрытие файлов раздач RuTracker через viewtorrent.php и P2P BEP-9 ut_metadata, защита кэша от Cloudflare, авто-комплектация переводов и автораспаковка модов atmosphere.7z*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Полное раскрытие файлов RuTracker без входа и для новинок]**: Реализовано прямое чтение дерева файлов через `viewtorrent.php` в контексте WebView2 с передачей CSRF `form_token`, а также механизм живого разрешения торрент-метаданных P2P BEP-9 (`ut_metadata`) из роя сидов. Пользователь видит реальный список файлов (`ИГРА`, `ОБНОВЛЕНИЕ`, `ОБНОВЛЕНИЕ (РУС)`, `DLC`, `МОД / РУСИФИКАТОР`, `ИНФО`) с точными размерами вместо одиночной заглушки `MAGNET`.
- 🌟 **[Защита кэша тем от Cloudflare и постеры свежих релизов]**: Валидатор `IsValidRuTrackerTopicHtml` отсекает отравленные страницы-заглушки Cloudflare (`Just a moment...`), очищает повреждённый кэш и извлекает прямые ссылки на обложки раздач для игр, ещё не добавленных в оффлайн-базу TitleDB.
- 🌟 **[Умная типизация и авто-комплектация файлов перевода]**: Приоритетное распознавание контейнеров `.nsp`/`.nsz`/`.xci` по расширениям, сопоставление TitleID и тегов версий `[vXXX]`, автоматический выбор корректной пары базового обновления и русификатора в подпапке перевода (например, `v131072.nsp` + `atmosphere.7z`) и авто-снятие выделения с сопутствующих текстовых файлов (`ReadMe.txt`).
- 🌟 **[Автораспаковка архивов модов (7z / zip / rar)]**: Встроенный модуль автоматической распаковки вложенных архивов модификаций `atmosphere.7z` с сопоставлением извлечённых папок `romfs` и `exefs` и прямой привязкой к конвейеру сборки монолитного пакета.
- 🌟 **[100% 6-языковая локализация новых компонентов]**: Полный перевод всех бейджей типов файлов, статусов предразрешения роя торрента и элементов управления на 6 языков интерфейса.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **RuTrackerModService.cs**: Добавлен метод `FetchTorrentFileListViaViewTorrentAsync` с извлечением `form_token` из HTML темы и разбором таблицы файлов `viewtorrent.php`.
- 🔹 **P2P BEP-9 ut_metadata**: Реализован фоновый коннектор `ResolveMetadataViaP2PAsync`, запрашивающий мета-инфо торрента напрямую из роя пиров по info-hash, если страница темы защищена или недоступна.
- 🔹 **Cloudflare Protection**: Функция `IsValidRuTrackerTopicHtml` проверяет минимальный размер (>= 2048 байт) и сигнатуры Cloudflare, предотвращая засорение локального кэша.
- 🔹 **Direct Poster Fetching**: Добавлено извлечение тегов `<var class="postImg" title="...">` и `postImgAligned` с автозагрузкой и сохранением в локальный кэш обложек.
- 🔹 **Mod Archive Auto-Extraction**: Реализована фоновая распаковка архивов `atmosphere.7z` через встроенный `7za` с детекцией путей `contents/<TitleID>/romfs` и автоматическим выставлением папки мода в `ProcessingTask`.
- 🔹 **UI & DataGrid Enhancements**: Обновлены стили строк файлов, бейджи типов контента и селекторы в `RuTrackerModDownloaderDialog.xaml`.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker File Tree Expansion**: Full file tree retrieval via `viewtorrent.php` (WebView2 Same-Origin session + CSRF `form_token`) and live P2P BEP-9 (`ut_metadata`) swarm pre-resolution replacing fallback `MAGNET` stubs with individual files (`BASE`, `UPDATE`, `UPDATE (RUS)`, `DLC`, `MOD / TRANSLATION`, `INFO`).
- 🔸 **Cloudflare Cache Guard & Dynamic Poster Extraction**: `IsValidRuTrackerTopicHtml` prevents cached `Just a moment...` challenge pages and dynamically caches topic cover artwork for recent titles missing from offline TitleDB.
- 🔸 **Intelligent Classification & Translation Pairing**: Advanced regex classification of `.nsp`/`.nsz`/`.xci` containers by TitleID and `[vXXX]`, automatic selection of matching update and translation mod (e.g., `v131072.nsp` + `atmosphere.7z`), and deselecting text info files.
- 🔸 **Automatic Mod Archive Extraction**: Built-in decompression of nested `atmosphere.7z`, `.zip`, and `.rar` archives with automatic mapping of extracted `romfs` and `exefs` directories directly into the monolithic build task.
- 🔸 **6-Language Badge Localization**: Fully localized badges and status notifications across Russian, English, German, French, Chinese, and Japanese.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
