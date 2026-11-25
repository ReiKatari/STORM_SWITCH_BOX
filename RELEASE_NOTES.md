> 💡 **Релиз 8.2.6** - *Устранение перезаписи кэшированных `.torrent`-файлов заглушкой `MAGNET` и мгновенное отображение реального списка файлов во всех раздачах RuTracker*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Исправление вечного статуса «Magnet-разрешение активно» (`RuTrackerModDownloaderDialog.xaml.cs`)]**: Исправлена ошибка инициализации `filesApplied` в `LoadSelectedTopicAsync`, из-за которой мгновенно загруженные из кэша реальные `.torrent`-файлы (например, `Mario Party Superstars`, `t=6126947`) тут же затирались фейковой строкой `MAGNET` и зависали в ожидании P2P. Теперь все кэшированные и свежие раздачи сохраняют реальный список файлов и не могут быть перезаписаны заглушкой (`_activeTopicHasRealFiles`).
- 🌟 **[Обходы RuTracker и FastResume скачивания (из 8.2.5)]**: Сохранены все обходы `8.2.5` - анти-503 троттлинг `itorrents.net`, прямой обход через Cloudflare Edge TLS ClientHello Split (`TlsSplitStream`), быстрая гонка BEP-9 и FastResume `--resume-file` с честным статусом `ПРОВЕРКА` в `storm_lt_turbo.py`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Views/Dialogs/RuTrackerModDownloaderDialog.xaml.cs]**: `filesApplied = instantCachedFiles != null && instantCachedFiles.Count > 0`, защита `_activeTopicHasRealFiles && !hasReal` в `ApplyResolvedFilesOnUi`, проверка `Any(f => f.IsRealTorrentFile)` в `StartMagnetMetadataBackground` и `!filesApplied && !_activeTopicHasRealFiles` в фолбэке.
- 🔹 **[Релиз 8.2.6]**: Синхронизированы версии Windows (`8.2.6`) и Android (`versionCode = 8206`, `versionName = "8.2.6"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `LoadSelectedTopicAsync` leaving `filesApplied = false` when `instantCachedFiles` resolved real `.torrent` entries (which previously caused `if (!filesApplied)` to overwrite real files with a `MAGNET` placeholder forever), added `_activeTopicHasRealFiles` protection across `ApplyResolvedFilesOnUi` and `StartMagnetMetadataBackground`, retained all `8.2.5` Cloudflare TLS-split/BEP-9/FastResume improvements, and bumped version to `8.2.6` (`versionCode = 8206`).

</details>

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.6_Setup.exe`** (`219.85 МБ`, SHA-256: `DF2CAF22617EF953C43A10CD98AEA6819F732F1646DE48ED93D9C7E196CD0031`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.6_win-x64.zip` / `STORM_SWITCH_BOX_8.2.6.zip`** (`151.40 МБ`, SHA-256: `5C8B7245EE767414DBAEE9ED35A1D585718D8A4440C29145B67C6082F757D517`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.6.apk`** (`14.47 МБ`, SHA-256: `BA1CBA63D5668ED07B5A58DC6DAC4BC89551E6DE0B49F48AA29810B0975C66B7`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
