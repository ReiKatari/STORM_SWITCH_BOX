## Русский

> 💡 **Релиз 8.2.8** - *Исправление распознавания папок `base` + `update` для Nintendo Switch 2 (NS2) и корректного вывода формата `.nup` вместо `.nsp` на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Распознавание `base` + `update` для Switch 2 (`Switch2DumpCompilerService.cs`, `NupContainerService.cs`, `TasksViewModel.cs`, `StormNczEngine.kt`)]**: Добавлен резолвер компонентов `ResolveDumpComponents(...)`, исключено ложное определение подпапки `update` как отдельного корня дампа, объединена группировка `base`/`update`/`dlc` и исправлена маршрутизация нескольких файлов в ветке «Конвертация».
- 🌟 **[Корректная сборка в `.nup` вместо `.nsp` (`MultiContentService.cs`, `Switch2DumpCompilerService.cs`, `TasksViewModel.cs`, `StormNczEngine.kt`)]**: При выборе целевого формата `NUP` во всех режимах («Конвертация», «Упаковка», «Мульти-контент», дамп Switch 2 и `targetFormatMode == 7` на Android) теперь формируется именно файл `.nup` без оставления `.nsp`.

<details>

<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/Switch2DumpCompilerService.cs]**: `ResolveDumpComponents(...)`, защита от выбора `update/` как корня дампа, ранняя сборка `NUP` до `.nsp` и сохранение `.nup` при `NUP -> NUP`.
- 🔹 **[Services/NupContainerService.cs]**: `PackSwitchDumpToNupAsync` с поддержкой `base`/`basedata`, `update`/`updates`/`patch` и `dlc`/`dlcdata`, обновление метрик в `ConvertContainerToNupAsync`.
- 🔹 **[Services/MultiContentService.cs]**: Поддержка `isTargetNup` (`ConvertIntermediateToNupAsync`) на всех выходах `BuildMultiContentAsync`.
- 🔹 **[ViewModels/TasksViewModel.cs]**: Объединение `base`/`update`/`dlc` при сканировании папок, порядок `Base` -> `Update`, `Pack` в `.nup` и мульти-файл в `Convert`.
- 🔹 **[Android (`StormNczEngine.kt`, `MainActivity.kt`)]**: Поддержка `base` + `update`/`updates` без захвата подпапки `update` и прямой вывод `.nup` при `targetFormatMode == 7`.
- 🔹 **[Релиз 8.2.8]**: Синхронизированы версии Windows (`8.2.8`) и Android (`versionCode = 8208`, `versionName = "8.2.8"`), без Legacy APK.

</details>

<details>

<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 Fixed Switch 2 (`NS2`) folder detection with `base` + `update` subfolders so both base and update are included instead of only `update`, and fixed multi-file `Convert` routing in `TasksViewModel.cs`.
- 🔸 Fixed selecting `NUP` target output producing `.nsp` instead of `.nup` across Windows and Android, and bumped version to `8.2.8` (`versionCode = 8208`).

</details>

## English

- Fixed Switch 2 (`NS2`) folder scanning when `base` and `update` coexist (prevents `update/` from being picked alone as root, supports both `update/` and `updates/<ver>/`, and groups folder components properly).
- Fixed `NUP` target output producing `.nsp` across Convert, Pack, Multi-Content, Switch 2 dump packing, and Android (`targetFormatMode == 7`).
- Updated Windows (`8.2.8`) and Android (`versionCode = 8208`, `versionName = "8.2.8"`).

## Deutsch

- Fehler bei der Erkennung von Switch-2-Ordnern (`NS2`) mit `base` + `update` behoben, sodass Basisspiel und Update gemeinsam erkannt werden.
- Ausgabeformat `NUP` erzeugt nun korrekt `.nup` statt `.nsp` unter Windows und Android (`8.2.8`).

## Español

- Corregida la deteccion de carpetas Switch 2 (`NS2`) con `base` + `update` para incluir ambos componentes.
- Corregida la salida del formato `NUP` para generar `.nup` en lugar de `.nsp` en Windows y Android (`8.2.8`).

## Français

- Correction de la detection des dossiers Switch 2 (`NS2`) avec `base` + `update` afin d'inclure les deux composants.
- Correction de la sortie `NUP` pour generer `.nup` au lieu de `.nsp` sur Windows et Android (`8.2.8`).

## Bahasa Indonesia

- Memperbaiki deteksi folder Switch 2 (`NS2`) yang berisi `base` + `update` agar kedua komponen terbaca dengan benar.
- Memperbaiki format tujuan `NUP` agar menghasilkan file `.nup` alih-alih `.nsp` di Windows dan Android (`8.2.8`).

## Italiano

- Risolto il rilevamento delle cartelle Switch 2 (`NS2`) con `base` + `update` per includere entrambi i componenti.
- Risolto l'output `NUP` per generare `.nup` invece di `.nsp` su Windows e Android (`8.2.8`).

## 日本語

- Switch 2 (`NS2`) の `base` と `update` フォルダ認識を修正し、ベースとアップデートの両方を正しく取り込むようにしました。
- 出力形式に `NUP` を指定した際に Windows / Android の両方で `.nsp` ではなく `.nup` が出力されるよう修正しました (`8.2.8`)。

## Português

- Corrigida a deteccao de pastas Switch 2 (`NS2`) com `base` + `update` para incluir ambos os componentes.
- Corrigida a saida do formato `NUP` para gerar `.nup` em vez de `.nsp` no Windows e Android (`8.2.8`).

## 简体中文

- 修复 Switch 2 (`NS2`) 文件夹同时包含 `base` 与 `update` 时只识别更新的问题，确保完整包含基础版与更新。
- 修复选择目标格式 `NUP` 时在 Windows 与 Android 仍输出 `.nsp` 的问题，版本号更新至 `8.2.8`。

<details>

<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 🖥️ **`STORM_SWITCH_BOX_8.2.8_Setup.exe`** (`219.86 МБ`, SHA-256: `0CC04DCD88E4B0319102577A93038AEDF242585A7CCE01CD73DCE08E2B4E5ADB`) - официальный установщик для Windows 10/11 x64 с цифровой подписью `CN=STORM TEAM` (включает `.NET` runtime, сертификат `STORM_Certificate.cer` и все инструменты).
- 📦 **`STORM_SWITCH_BOX_8.2.8_win-x64.zip` / `STORM_SWITCH_BOX_8.2.8.zip`** (`151.41 МБ`, SHA-256: `03A48DA207DAAE6D55BB81D3358D62BEAEF6DBB169241315F22EC5AA4A2AF32E`) - портативная автономная сборка Windows x64 без установки.
- 📱 **`STORM_SWITCH_BOX_8.2.8.apk`** (`14.48 МБ`, SHA-256: `C7CDAE4CB6A499E3DE38BE70B8AB70754C62A237C91451A0CB0719F054CB7E9D`) - сборка для Android 8.0+ (`minSdk 26`, `arm64-v8a` и `x86_64`, подпись `CN=STORM SERTIFICATE`).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода, подписаны сертификатами проекта и проверены перед публикацией.

</details>
