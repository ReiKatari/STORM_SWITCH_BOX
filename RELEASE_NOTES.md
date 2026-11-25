> 💡 **Релиз 8.2.5** - *Мгновенное получение списка файлов раздач RuTracker, обход HTTP 503 и ТСПУ (TLS Split + Cloudflare Edge), FastResume скачивания и устранение простоев `0,00 МБ/с`*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Мгновенное получение списка файлов раздач и анти-503 обходы (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Устранены ошибки HTTP 503 на `itorrents.net` за счёт приоритетного троттлинга фоновой предзагрузки, добавлен прямой обход через граничные IP Cloudflare с фрагментацией TLS ClientHello (`TlsSplitStream`), в `InspectTorrentFilesAsync` добавлена быстрая гонка P2P BEP-9, а также устранена гонка сброса кеша `.torrent` при переключении тем.
- 🌟 **[Мгновенное возобновление скачивания FastResume и встроенный обход трекеров (`storm_lt_turbo.py`, `RuTrackerDownloadQueueService.cs`)]**: Добавлено сохранение и чтение данных быстрого возобновления (`--resume-file`), чтобы закачки больше не застревали на повторном перехэшировании после перезапуска, внедрён честный статус дисковой проверки (`ПРОВЕРКА`), залповый реконнект пиров сразу после проверки и автономный обход ТСПУ трекеров `bt..bt4.t-ru.org` методом Cloudflare HTTP/1.1 Pipelining прямо внутри движка.
- 🌟 **[Синхронизация зеркал Android (`RuTrackerCloudEngine.kt`)]**: Мобильный клиент переведён на `itorrents.net` -> `itorrents.org` с удалением нерабочего `torrage.info`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Список файлов и обходы RuTracker]**: Приоритетное ограничение предзагрузки (до 4 тем, пауза 250 мс и бэкофф 10 с на 429/503), `FetchItorrentsViaCloudflareEdgeTlsSplitAsync` (`TlsSplitStream`), потокобезопасный `TryGetInstantCachedTorrentFiles`, гонка BEP-9 через 1.0 с в `InspectTorrentFilesAsync`, распределение `bt..bt4.t-ru.org` по разным Edge IP, удаление гонки `filesTask.ContinueWith` в диалоге.
- 🔹 **[Быстрая загрузка торрентов и FastResume]**: Поддержка `--resume-file` в `storm_lt_turbo.py` (сохранение каждые 25 с и на паузе/выходе, автоочистка по завершении), снижение `hashing_threads` до 2 на HDD, честный статус `ПРОВЕРКА` в `RuTrackerDownloadQueueService`, залповый `connect_peer` после проверки и автономный `harvest_cf_bt_peers`.
- 🔹 **[Android и релиз 8.2.5]**: Обновлены зеркала в `RuTrackerCloudEngine.kt`, синхронизированы версии Windows (`8.2.5`) и Android (`versionCode = 8205`, `versionName = "8.2.5"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed RuTracker file-list stalls caused by `itorrents.net` HTTP 503 self-throttling, added Cloudflare Edge TLS ClientHello split bypass (`TlsSplitStream`), added 1.0s-staggered BEP-9 `ut_metadata` race in `InspectTorrentFilesAsync`, and eliminated UI/cache races in `TryGetInstantCachedTorrentFiles` and `LoadSelectedTopicAsync`.
- 🔸 Added FastResume (`--resume-file`) for torrent downloads in `storm_lt_turbo.py` so restarts resume immediately without re-hashing tens of GB on HDD, reported disk verification cleanly as `checking` (`ПРОВЕРКА`), added post-check burst peer connect and built-in Cloudflare HTTP/1.1 Pipelining tracker harvesting (`bt..bt4.t-ru.org`).
- 🔸 Updated Android torrent mirrors in `RuTrackerCloudEngine.kt` (`itorrents.net` -> `itorrents.org`), and bumped version to `8.2.5` (`versionCode = 8205`).

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.5_Setup.exe`** (`219.85 МБ`, SHA-256: `038A8D1D7AAC83C69941FA732C79C2AC6915A8928017D5E7AD7CD7200E5565CC`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.5_win-x64.zip` / `STORM_SWITCH_BOX_8.2.5.zip`** (`151.40 МБ`, SHA-256: `EC47EE5E44C3137FAEC2A4D7DA50414C2CC4404C6343465958C3998625212CCB`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.5.apk`** (`14.47 МБ`, SHA-256: `1773BD6829D6E1C9C08E8BE837BC9376D0CE64C4099060F42FC97756F8B32B30`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
