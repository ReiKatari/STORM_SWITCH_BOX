## Русский

> 💡 **Релиз 8.2.7** - *Устранение OOM-краша и вечного зависания `nsz.exe` на уровне 22 (в 37 раз меньше памяти) и умное планирование процессов сжатия*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Исправление OOM и зависания в `BlockCompressor.py`]**: Добавлена передача `source_size=blockSize` (1 МБ) и кеширование `ZstdCompressor` в каждом воркере `nsz.exe`, что снижает потребление памяти Zstd на 22-м уровне с `641.3 МБ` до `17.3 МБ` на процесс (в 37 раз меньше без потери сжатия), а также добавлена мгновенная детекция падения воркеров (`_wait_until_workers_ready`, `_put_work_checked`) и очистка процессов в `finally` без вечного deadlock.
- 🌟 **[Ограничение процессов `nsz.exe` по ОЗУ и `ConcurrentTasks` (`NszCompressionService.cs`)]**: Добавлен метод `GetOptimalExternalNszThreads` с проверкой свободной памяти через `GlobalMemoryStatusEx`, чтобы параллельные задачи сжатия не запускали десятки тяжёлых Python-процессов одновременно.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[tools/nsz/Lib/site-packages/nsz/BlockCompressor.py]**: Кеширование `ZstdCompressor` на процесс, `source_size=blockSize`, ограничение `TasksPerChunk` до `max(threads * 4, 32)`, `_wait_until_workers_ready`, `_put_work_checked` и `try...finally` очистка пула и `Manager`.
- 🔹 **[Services/NszCompressionService.cs]**: Добавлены `GetAvailableMemoryMb()` (`GlobalMemoryStatusEx`) и `GetOptimalExternalNszThreads(isSolidMode, level)`.
- 🔹 **[Релиз 8.2.7]**: Обновлены сборки Windows (`8.2.7`) и Android (`versionCode = 8207`, `versionName = "8.2.7"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed `nsz.exe` level-22 out-of-memory crashes and infinite deadlocks in `BlockCompressor.py` by passing `source_size=blockSize` (reducing Zstd level-22 worker RAM from `641.3 MB` to `17.3 MB`), caching `ZstdCompressor` per worker, capping `TasksPerChunk`, and adding dead-worker guards (`_wait_until_workers_ready` and `_put_work_checked`).
- 🔸 Added RAM- and concurrency-aware `GetOptimalExternalNszThreads` in `Services/NszCompressionService.cs` and bumped version to `8.2.7` (`versionCode = 8207`).

</details>

## English

- Fixed `nsz.exe` level-22 block compression out-of-memory crashes and infinite deadlocks in `BlockCompressor.py` (`source_size=blockSize` cuts per-worker Zstd context RAM from `641.3 MB` to `17.3 MB`, 37x lower; per-worker compressor caching; dead-worker guards and cleanup).
- Added RAM-aware external worker budgeting (`GetOptimalExternalNszThreads` + `GlobalMemoryStatusEx`) in `NszCompressionService.cs`.
- Updated Windows (`8.2.7`) and Android (`versionCode = 8207`, `versionName = "8.2.7"`).

## Deutsch

- Behebung von OOM-Abstuerzen und Endlos-Deadlocks in `nsz.exe` (`BlockCompressor.py`) bei Zstd-Stufe 22 (`source_size=blockSize` reduziert RAM pro Worker von `641.3 MB` auf `17.3 MB`, plus Dead-Worker-Erkennung und Bereinigung).
- Arbeitsspeicher- und parallelitaetsbewusste Prozessbudgetierung (`GetOptimalExternalNszThreads`) in `NszCompressionService.cs` fuer Version `8.2.7`.

## Español

- Corregidos los cierres por falta de memoria y bloqueos infinitos en `nsz.exe` (`BlockCompressor.py`) en nivel 22 (`source_size=blockSize` reduce la RAM por proceso de `641.3 MB` a `17.3 MB`, con deteccion de procesos caidos y limpieza).
- Presupuesto de procesos segun RAM disponible y `ConcurrentTasks` en `NszCompressionService.cs` para la version `8.2.7`.

## Français

- Correction des plantages OOM et blocages infinis dans `nsz.exe` (`BlockCompressor.py`) au niveau 22 (`source_size=blockSize` reduit la RAM par worker de `641.3 Mo` a `17.3 Mo`, avec detection de crash worker et nettoyage).
- Gestion des processus externes selon la RAM disponible et `ConcurrentTasks` dans `NszCompressionService.cs` pour la version `8.2.7`.

## Bahasa Indonesia

- Memperbaiki crash kehabisan memori (OOM) dan deadlock permanen pada `nsz.exe` (`BlockCompressor.py`) di level 22 (`source_size=blockSize` menurunkan RAM per worker dari `641.3 MB` ke `17.3 MB`, ditambah deteksi worker mati dan pembersihan).
- Penjadwalan proses pekerja berbasis RAM dan `ConcurrentTasks` di `NszCompressionService.cs` untuk versi `8.2.7`.

## Italiano

- Risolti i crash OOM e i blocchi infiniti in `nsz.exe` (`BlockCompressor.py`) al livello 22 (`source_size=blockSize` riduce la RAM per worker da `641.3 MB` a `17.3 MB`, con rilevamento worker terminati e pulizia).
- Pianificazione dei processi esterni in base alla RAM disponibile e a `ConcurrentTasks` in `NszCompressionService.cs` per la versione `8.2.7`.

## 日本語

- `nsz.exe` (`BlockCompressor.py`) のレベル22圧縮時のメモリ不足クラッシュと無限デッドロックを修正 (`source_size=blockSize` によりワーカーあたりRAMを `641.3 MB` から `17.3 MB` へ削減、ワーカー異常終了検知・後処理を追加)。
- `NszCompressionService.cs` に空きメモリと `ConcurrentTasks` に応じたプロセス数制限を追加し、バージョン `8.2.7` に更新しました。

## Português

- Corrigidas falhas por falta de memoria (OOM) e travamentos infinitos no `nsz.exe` (`BlockCompressor.py`) no nivel 22 (`source_size=blockSize` reduz RAM por worker de `641.3 MB` para `17.3 MB`, com deteccao de falha e limpeza).
- Alocacao inteligente de processos baseada em RAM livre e `ConcurrentTasks` em `NszCompressionService.cs` para a versao `8.2.7`.

## 简体中文

- 修复 `nsz.exe` (`BlockCompressor.py`) 在 22 级分块压缩时的内存溢出崩溃与无限死锁问题 (`source_size=blockSize` 将单进程 Zstd 内存从 `641.3 MB` 降至 `17.3 MB`，并新增工作进程异常退出检测与清理)。
- 在 `NszCompressionService.cs` 中新增基于可用内存与 `ConcurrentTasks` 的进程数调度，并更新版本至 `8.2.7`。

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.7_Setup.exe`** (`219.85 МБ`, SHA-256: `DC25458E7019739DA98DCB646E61BB33D22EDE62F2301F74C0CCD2ECC02E209C`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.7_win-x64.zip` / `STORM_SWITCH_BOX_8.2.7.zip`** (`151.40 МБ`, SHA-256: `64DF8962E829DA03110ACEBA0B622BB100B61722F5049583DC6077E5F1CB3C99`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.7.apk`** (`14.47 МБ`, SHA-256: `D589AFB9FA3F5FCE209B6EF9BF39BD30F3F0DB4129AE6B57603318162D087D41`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
