> 💡 **Релиз 8.0.4** - *Мгновенное отображение реального списка файлов для всех раздач RuTracker и максимальный разгон нативного движка скачивания C++ libtorrent*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.4)

- 🌟 **[Мгновенное получение списка файлов раздач для всех игр (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Устранена причина срыва загрузки `.torrent` из кэш-зеркал: `itorrents.org` перешёл на `http://itorrents.net` через редирект `HTTP 301` (`HTTPS -> HTTP`), который `.NET SocketsHttpHandler` отклонял по умолчанию за `113 мс`. Основным зеркалом подключён `https://itorrents.net/` (`HTTP 200` за `80-250 мс`) и добавлен обработчик межсхемных редиректов `SendWithCrossSchemeRedirectAsync`.
  - Глобальная 60-секундная блокировка зеркал `_cacheMirrorsCooldownUntilUnix` заменена на точечный кэш промахов по конкретному `InfoHash` (`_missedMirrorHashes`, TTL `45 с`), чтобы редкие/новые раздачи не отключали быстрый кэш для остальных игр.
  - Префетч `PrefetchTopicsMetadata` расширен до 6 раздач (`_prefetchThrottle = 4`), а для тем без готового `.torrent` после быстрого окна запускается неблокирующая фоновая P2P-догрузка списка файлов `ResolveMagnetMetadataBackgroundAsync` на `22 с` без зависания UI на заглушке `MAGNET`.
- 🌟 **[Исправление обрезания главного сида из-за UTF-8 BOM в `peers.txt` (`RuTrackerModService.cs`, `tools/nsz/storm_lt_turbo.py`)]**:
  - В методах `FlushMetaPeers`, `FlushNewPeersToDisk` и `WriteControlCommand` запись через `StreamWriter(..., Encoding.UTF8)` заменена на `Utf8NoBom` без метки BOM (`﻿`), которая портила первый IP-адрес сида в каждой волне. Дополнительно в `storm_lt_turbo.py` включена принудительная очистка `﻿` и валидация IP в `parse_peer_endpoint`.
- 🌟 **[Профиль `MAX TURBO` для C++ `libtorrent 2.1.1` и потоковые UDP-трекеры (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**:
  - `storm_lt_turbo.py` переведён в максимальный скоростной режим: `smooth_connects=False`, `connection_speed=1000`, `connections_limit >= 3000`, `max_peerlist_size=6000`, очереди запросов `4000`, `utp_gain_factor=5000`, `utp_target_delay=200`, `prefer_rc4=True`, `peer_turnover=0`, `piece_extent_affinity=True`, `coalesce_reads=True`, `coalesce_writes=True`, `rate_limit_ip_overhead=False`, `aio_threads=16`, `hashing_threads=8`, `max_queued_disk_bytes=256MB`, `enable_upnp/natpmp=True` при скачивании, ускоренный реконнект пиров каждые `10 с` и периодический ре-анонс трекеров/DHT каждые `45 с`.
  - Список трекеров в `storm_lt_turbo.py` расширен до 20 быстрых публичных UDP/HTTPS трекеров (с лимитом до 24), а `QueryUdpTrackersBep15Async` в C# расширен до 14 UDP-трекеров с мгновенной потоковой передачей каждой пачки пиров через `onBatchPeers`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/RuTrackerModService.cs]**: Добавлены `Utf8NoBom`, `_missedMirrorHashes` (`45 с` на хэш), `SendWithCrossSchemeRedirectAsync`, публичный метод `ResolveMagnetMetadataBackgroundAsync`; `WarmupFastConnections` и `FetchTorrentFromCacheMirrorsCoreAsync` переведены на `https://itorrents.net/`, `_prefetchThrottle` увеличен до `4`, `PrefetchTopicsMetadata` увеличен до `6`, `fastRaceCts` до `2.6 с`; `QueryUdpTrackersBep15Async` расширен до 14 трекеров с потоковым коллбэком `onBatchPeers`.
- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: В `LoadSelectedTopicAsync` добавлена неблокирующая 22-секундная фоновая P2P-догрузка реального списка файлов, а префетч результатов поиска увеличен до `maxCount: 6`.
- 🔹 **[tools/nsz/storm_lt_turbo.py]**: Расширен `DEFAULT_PUBLIC_TRACKERS` (`20` URL) и `merge_trackers` (`24`), включён профиль `MAX TURBO`, очистка BOM `﻿` в `parse_peer_endpoint` / `pump_peers` / `inject_new_peers`, повтор пиров `BEP-9` на `2.2 с`, реконнект пиров каждые `10 с` и принудительный ре-анонс каждые `45 с`.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.4)

- 🔸 **[Fix Missing File Lists Across All RuTracker Topics (`RuTrackerModService.cs`, `RuTrackerModDownloaderDialog.xaml.cs`)]**:
  - Fixed `itorrents.org` failing in `113 ms` with an unhandled `HTTPS -> HTTP` `301` downgrade to `http://itorrents.net`. Switched primary cache fetches & TLS warmup to `https://itorrents.net/` (`HTTP 200` in `80-250 ms`) and added `SendWithCrossSchemeRedirectAsync`.
  - Replaced the global `60s` mirror cooldown with per-hash negative caching (`_missedMirrorHashes`, `45s` TTL) so uncached releases no longer block `.torrent` cache fetches for other games.
  - Raised top-result prefetching to `6` topics (`_prefetchThrottle = 4`) and added non-blocking `22s` background P2P metadata completion (`ResolveMagnetMetadataBackgroundAsync`) when neither cache mirrors nor the initial fast window return files.
- 🔸 **[Eliminate UTF-8 BOM Corrupting First Seed in `peers.txt` (`RuTrackerModService.cs`, `tools/nsz/storm_lt_turbo.py`)]**: Switched `FlushMetaPeers`, `FlushNewPeersToDisk`, and `WriteControlCommand` from BOM-emitting `Encoding.UTF8` to `Utf8NoBom`, and added defensive `﻿` stripping + IP validation in `storm_lt_turbo.py`.
- 🔸 **[MAX TURBO `libtorrent 2.1.1` Tuning & Streaming 14-Tracker UDP Harvest (`tools/nsz/storm_lt_turbo.py`, `RuTrackerModService.cs`)]**: Enabled `smooth_connects=False`, `connection_speed=1000`, `connections_limit >= 3000`, `max_peerlist_size=6000`, request queues `4000`, `utp_gain_factor=5000`, `utp_target_delay=200`, `prefer_rc4=True`, `peer_turnover=0`, `piece_extent_affinity=True`, `coalesce_reads/writes=True`, `rate_limit_ip_overhead=False`, `aio_threads=16`, `hashing_threads=8`, `256MB` disk queue, UPnP/NAT-PMP during downloads, `20` public trackers, `10s` peer retries, `45s` tracker/DHT reannounce, and streaming per-tracker `onBatchPeers` callbacks across `14` UDP trackers.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.4_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.4.zip` / `STORM_SWITCH_BOX_8.0.4_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.4.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.4_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.4_Setup.exe`** (219.62 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.4.zip`** / **`_win-x64.zip`** (151.17 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.4.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.4_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.4_Setup.exe** (219.62 МБ)
  - `SHA-256: 2F453F8FA139F2BCA23CE7A8725767BB163150403151768A01239E7D86976706`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.4_win-x64.zip** / **STORM_SWITCH_BOX_8.0.4.zip** (151.17 МБ)
  - `SHA-256: C15090B35D2BE9884FF6B786E84A43ACF9D27C92A8810BCE40051C33C3BD769B`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.4.apk** (14.38 МБ)
  - `SHA-256: CA9DA77E484C247B5C6075DDFF477F80D44715524F4522578AD84A4F134CC69F`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.4_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 226F654BCC5EAF6C1C7EC8C42C03CD2DA984A5D3659E910FC834D47AFB6A93B2`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
