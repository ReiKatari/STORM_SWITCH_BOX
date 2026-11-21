> 💡 **Релиз 8.0.3** - *Устранение бесконечной загрузки списка файлов раздач RuTracker и восстановление турбо-скорости встроенного движка C++ libtorrent*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.3)

- 🌟 **[Устранение бесконечной загрузки списка файлов и метаданных (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Исправлена критическая ошибка, из-за которой таймаут кэш-зеркал (`itorrents.org` / `torrage.info`) пробрасывал внутренний `OperationCanceledException` и срывал переход к P2P-получению метаданных `BEP-9`, оставляя интерфейс в вечной загрузке. Добавлены 60-секундный cooldown для сбойных зеркал, изолированные фазы загрузки в UI, отказ от 45-секундных блокировок Cloudflare внутри быстрых обработчиков WebView2, обновление `InfoHash` и `MagnetUri` для обновлённых раздач (`[Обновлено]`) через `dl.php?t=...`, а также быстрый шаг `--metadata-only` на базе `libtorrent` с мгновенным завершением процесса (`os._exit(0)` без 5-секундной задержки деструктора). Холодная загрузка 16 файлов `Captain Tsubasa 2` занимает 5.5-6.2 с, а `Animal Well` - около 1.8 с.
- 🌟 **[Восстановление турбо-скорости скачивания C++ `libtorrent-rasterbar` (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**:
  - Устранено скрытое падение `storm_lt_turbo.py` с `ModuleNotFoundError: ipaddress` во встроенном `python.exe` (модуль заменён на встроенный `socket.inet_pton`), из-за которого все загрузки откатывались с нативного C++ `libtorrent 2.1.1` (Tier 2) на более медленный `MonoTorrent` (Tier 3).
  - Включён нативный автотюнинг буферов Windows (`send_socket_buffer_size = 0`, `recv_socket_buffer_size = 0`) и `mixed_mode_algorithm = 1` (`peer_proportional`) с `utp_gain_factor = 3000` и `utp_target_delay = 150` без искусственного троттлинга `uTP`-сидов.
  - Через `handle.replace_trackers` быстрые публичные UDP/HTTPS-трекеры поставлены первыми, а заблокированные DPI адреса `t-ru.org` и `retracker.local` исключены из сессии `libtorrent`; удалено чтение/сохранение `lt_dht_state.dat` (`ses.load_state`), сбивавшее DHT на системах с виртуальными сетевыми адаптерами. На активных раздачах движок разгоняется до 35.5 МБ/с.
- 🌟 **[Параллельная гонка Cloudflare Pipelining и потоковая подача пиров (`RuTrackerModService.cs`)]**: Добавлены 4 быстрых граничных узла Cloudflare (`188.114.96-99.3`), одновременная гонка 2 узлов на каждый трекер (`3.8 с`), ранний выход по завершении приёма блока `5:peers` / `6:peers6` без ожидания `TCP FIN`, а также мгновенная подача кэшированных, UDP и DHT пиров через `onFastWavePeers`.
- 🌟 **[Точные счётчики сидов и пиров (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `storm_lt_turbo.py`)]**: Исправлен подсчёт `Пиры` отдельно от сидов, суммирование сидов и пиров по всем активным закачкам и корректное отображение `Пиры: N` в шапке и карточках очереди.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Удалена зависимость `ipaddress` (`socket.inet_pton`), добавлены режимы `--listen-port`, `--metadata-only`, `--save-torrent`, `--timeout`, настройки `build_turbo_settings` (`mixed_mode_algorithm=1`, `send/recv=0`, `utp_gain_factor=3000`, `utp_target_delay=150`, `enable_upnp=False`, `enable_natpmp=False`, `enable_lsd=False`, `aio_threads=12`, `hashing_threads=4`), очистка трекеров `merge_trackers` + `apply_clean_trackers`, периодический переконнект пиров и немедленный выход `os._exit(0)`.
- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `_cacheMirrorsCooldownUntilUnix`, `_preferredCfEdgeIp`, `FindFreeHighListenPort`, `TryResolveMetadataViaEmbeddedLibtorrentAsync`, `HasCompleteCompactPeersPayload`, `TryFetchTrackerSingleCfEdgeAsync`, параллельная гонка `FetchTrackerViaHttpPipeliningAsync`, поддержка IPv6 `6:peers6` в `ExtractCompactPeersFromBencodedData`, потоковая передача `onFastWavePeers` и обновление `InfoHash` из `dl.php?t=...` для `[Обновлено]`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: Изоляция `detailsTask`, `filesTask` и обновления `discoveredNewHash`, снятие 45-секундных ожиданий Cloudflare challenge внутри быстрых обработчиков WebView2, корректное отображение `Пиры: N`.
- 🔹 **[Services/RuTrackerDownloadQueueService.cs]**: Суммирование `totalSeeds` / `totalPeers` по активным элементам очереди и отображение `Пиры: N` в строках прогресса.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.3)

- 🔸 **[Fix Infinite File-List Loading on RuTracker Topics (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**: Fixed internal `OperationCanceledException` propagation during cache mirror races (`itorrents.org` / `torrage.info`) that aborted `InspectTorrentFilesAsync` before reaching P2P `BEP-9` metadata resolution. Added a `60s` mirror cooldown, fast embedded `libtorrent --metadata-only` (`os._exit(0)` avoiding `~session()` stalls), dynamic `InfoHash`/`MagnetUri` refresh for updated `[Обновлено]` topics via `dl.php?t=...`, removal of `45s` Cloudflare blocks inside fast WebView2 handlers, and isolated UI loading phases with guaranteed fallback (`Captain Tsubasa 2` resolves all 16 files in `5.5-6.2s`, cold `Animal Well` in `~1.8s`).
- 🔸 **[Restore Native C++ `libtorrent` Turbo Download Speeds (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**: Fixed a silent `ModuleNotFoundError: No module named 'ipaddress'` crash in embedded `tools/nsz/python.exe` (replaced with `socket.inet_pton`) that had downgraded all downloads from Tier 2 native C++ `libtorrent 2.1.1` to Tier 3 `MonoTorrent`. Enabled Windows socket autotuning (`send/recv=0`), `mixed_mode_algorithm=1` (`peer_proportional`), `utp_gain_factor=3000`, `utp_target_delay=150`, filtered DPI-reset `t-ru.org` and `retracker.local` trackers via `handle.replace_trackers`, and removed `ses.load_state` (`lt_dht_state.dat`) which degraded DHT across virtual adapters (reaching up to `35.5 MB/s` on active swarms).
- 🔸 **[Parallel Cloudflare Edge Racing & Streaming Peer Injection (`RuTrackerModService.cs`)]**: Added 4 fast Cloudflare edges (`188.114.96-99.3`), 2-edge parallel racing per tracker host (`3.8s` timeout), early socket read exit on complete `5:peers`/`6:peers6` payloads, and immediate `t=0` cached/UDP/DHT peer streaming via `onFastWavePeers`.
- 🔸 **[Accurate Seed & Peer Counters (`RuTrackerDownloadQueueService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`, `storm_lt_turbo.py`)]**: Reported non-seed peers separately from seeds, summed seeds/peers across active downloads, and rendered `Пиры: N` consistently.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.3_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.3.zip` / `STORM_SWITCH_BOX_8.0.3_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.3.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.3_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.3_Setup.exe`** (219.62 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.3.zip`** / **`_win-x64.zip`** (151.17 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.3.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.3_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.3_Setup.exe** (219.62 МБ)
  - `SHA-256: 9E9B4488B91F61007ECEF6782035C4585F51774EB9C15B21F0BBDE4969938EF8`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.3_win-x64.zip** / **STORM_SWITCH_BOX_8.0.3.zip** (151.17 МБ)
  - `SHA-256: A6B605EB7B799243C0F64312F3E34EAC91624B64255B40FC3EC11D6CF239D5D7`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.3.apk** (14.38 МБ)
  - `SHA-256: A91CDBE77D16E3753B6E187DE73E078D7E14BC648863D3E7C192F909B0356613`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.3_LEGACY.apk** (25.95 МБ)
  - `SHA-256: DCE565C6DA74E6F6155E56344CEBB6952CEEFFB5DBDFF948D3D143B5751F7769`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
