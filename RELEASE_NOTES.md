> 💡 **Релиз 8.0.1** - *Исправление ошибки блокировки `persisted_tasks.json.tmp` в Задачнике, восстановление оригинального внешнего вида установщика и иконок из линейки 7.X.X, а также комплексная оптимизация скорости интерфейса, GPU-анимаций и плавности работы на Windows и Android*

---

### 🚀 Ключевые исправления и улучшения (STORM SWITCH BOX 8.0.1)

- 🌟 **[Исправление ошибки блокировки `persisted_tasks.json.tmp` в Задачнике (`SafeFileOperations.cs`, `TasksViewModel.cs`)]**: Устранён конфликт записи с фиксированным временным файлом (`The process cannot access the file '...persisted_tasks.json.tmp' because it is being used by another process`). Внедрена атомарная запись `WriteAllTextAtomic` / `WriteAllTextAtomicAsync` через уникальные временные файлы (`<name>.<pid>.<guid>.tmp`) с 6-кратным backoff при временной блокировке антивирусом/системой, глобальный семафор процесса и 350-мс коалесценция всплесков сохранений. Аналогичная защита применена для `SettingsService`, `HistoryService` и `RuTrackerDownloadQueueService`.
- 🌟 **[Восстановление оригинального вида установщика и иконок 7.X.X (`StormInstaller`, `SettingsPage.xaml`, `MainWindow.xaml`)]**: Возвращён классический дизайн установщика линейки `7.X.X` (тёмно-фиолетовый заголовок `56x56` с иконкой и мягким неоновым свечением, стандартные чекбоксы и радиокнопки без лишней эмблемы в теле окна), при этом полностью сохранены защита от Zip-Slip, `Mutex` единственного экземпляра, двойная буферизация без мерцания и совместимая загрузка сертификата `STORM_CERTIFICATE.cer`. В приложении восстановлены фирменный логотип `Assets/logo.png` на экране настроек, иконка `storm_switch_box.ico`, `Assets/StoreLogo.png`, цвета уведомления `GlobalAlertInfoBar` и оригинальная `roundIcon` в Android.
- 🌟 **[Аппаратные GPU-анимации и плавность переходов 60-240 Гц (`UiMotionHelper.cs`, `MainWindow.xaml.cs`, `SettingsPage.xaml.cs`, `InstructionPage.xaml.cs`)]**: Внедрён новый движок композитных анимаций `UiMotionHelper` на базе `Windows.UI.Composition` (`ElementCompositionPreview` с кубическим замедлением Безье). Добавлены плавные 170-180 мс GPU-переходы при смене разделов, вкладок настроек, тем справки и всплывающего `GlobalAlertInfoBar`, а переключение режимов внутри `TasksPage` выполняется мгновенно через `SuppressNavigationTransitionInfo` без пустого кадра.
- 🌟 **[Оптимизация скорости UI, видеопамяти GPU и отзывчивости прогресса (`ThemeService.cs`, `LogBatcher.cs`, `BitmapImage`, `MainActivity.kt`)]**: Устранён тройной проход смены `RequestedTheme` с лишним кадром задержки при переключении тем (`ThemeService.RefreshUI`), уменьшено декодирование миниатюр и обложек с помощью `DecodePixelWidth` + `DecodePixelType.Logical` (96-640 px - до 75-85% экономии GPU VRAM и плавная прокрутка), убраны холостые уведомления 500-мс таймера `IsAnyTaskRunning`, добавлена коалесценция `LogBatcher` (120 мс) без заторов UI-потока, а на Android внедрён плавный анимированный `setProgress(..., true)` с ограничением частоты обновлений (`40 мс`).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Services/SafeFileOperations.cs]**: Добавлены `WriteAllTextAtomic` и `WriteAllTextAtomicAsync` с уникальными `.tmp`-именами (`<pid>.<guid>`), буфером 64 КБ, сбросом `ReadOnly` и повторными попытками переноса с экспоненциальной задержкой (`35-700 мс`).
- 🔹 **[ViewModels/TasksViewModel.cs]**: Единый `static` семафор `_persistSemaphore`, переиспользуемый `JsonSerializerOptions`, 350-мс коалесценция `SavePersistedTasks()` (`RunCoalescedPersistLoopAsync`), устранение лишних `OnPropertyChanged("IsAnyTaskRunning")` каждые 500 мс при простое и защита от зависаний при выходе `SaveTasksOnExit()`.
- 🔹 **[Services/SettingsService.cs, HistoryService.cs, RuTrackerDownloadQueueService.cs]**: Переход на потокобезопасную атомарную запись через `SafeFileOperations` и кэшированные `JsonSerializerOptions`.
- 🔹 **[installer/StormInstaller/Program.cs & StormInstaller.csproj]**: Восстановлен внешний вид установщика версии `7.0.14` (без `header_badge.png` и `VolumetricCheckBox`), добавлены `DoubleBuffered`, Zip-Slip-проверка `GetSafeZipExtractPath`, `Mutex` единственного экземпляра и поддержка обоих имён ресурса сертификата (`STORM_CERTIFICATE.cer` / `STORM_SERTIFICATE.cer`).
- 🔹 **[Views/SettingsPage.xaml, StormSwitchBox.csproj, MainWindow.xaml, AndroidManifest.xml]**: Восстановлены фирменное изображение `Assets/logo.png` вместо `<PathIcon>`, `storm_switch_box.ico` как `<ApplicationIcon>`, `Assets/StoreLogo.png`, цвета `GlobalAlertInfoBar` и `android:roundIcon="@mipmap/ic_launcher"`.
- 🔹 **[Services/UiMotionHelper.cs, MainWindow.xaml.cs, Views/SettingsPage.xaml.cs, Views/InstructionPage.xaml.cs]**: Новые композитные GPU-анимации появления (`AnimateEntrance` / `AnimateSlideFade`), моментальное переключение режимов внутри `TasksPage`, корректный заголовок окна из `App.AppVersionString`.
- 🔹 **[Services/ThemeService.cs & Services/LogBatcher.cs]**: Одиночный проход `RequestedTheme` при смене тем оформления (без тройного мигания) и неблокирующая очередь сброса логов `LogBatcher` (`120 мс`, `_uiFlushPending`).
- 🔹 **[Декодирование обложек и иконок (`BitmapImage`)]**: Установлены `DecodePixelWidth` (96 px для очередей/истории, 256 px для каталога/RuTracker, 440-640 px для деталей и скриншотов) и `DecodePixelType.Logical` в 9 файлах.
- 🔹 **[android/.../MainActivity.kt]**: Добавлен `setProgressSmooth` (нативная аппаратная интерполяция на Android 7.0+) и троттлинг UI-обновлений `40 мс` во время интенсивного сжатия/распаковки.

</details>

---

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

### 🚀 Key Improvements & Fixes (STORM SWITCH BOX 8.0.1)

- 🔸 **[Task Queue Persistence Lock Fix (`SafeFileOperations.cs`, `TasksViewModel.cs`)]**: Resolved `The process cannot access the file '...persisted_tasks.json.tmp' because it is being used by another process`. Added `WriteAllTextAtomic` / `WriteAllTextAtomicAsync` with unique per-write temp filenames (`<pid>.<guid>.tmp`), 6-step backoff retries against transient AV/system locks, a static process-wide semaphore, and 350 ms write coalescing; rolled out the same atomic writer to `SettingsService`, `HistoryService`, and `RuTrackerDownloadQueueService`.
- 🔸 **[7.X.X Installer UI & App Icons Restored (`StormInstaller`, `SettingsPage.xaml`, `StormSwitchBox.csproj`, `MainWindow.xaml`)]**: Restored the classic `7.0.14` `StormInstaller` layout and visuals while keeping non-visual Zip-Slip protection, single-instance `Mutex`, double buffering, and `STORM_CERTIFICATE.cer` compatibility. Restored the `Assets/logo.png` image in `SettingsPage`, `storm_switch_box.ico` application icon, `Assets/StoreLogo.png`, `GlobalAlertInfoBar` palette, and Android `roundIcon`.
- 🔸 **[GPU Composition Animations & Page/Tab Smoothness (`UiMotionHelper.cs`)]**: Added `Windows.UI.Composition` ease-out GPU entrance animations for page switches, Settings tabs, Instruction topics, and `GlobalAlertInfoBar`, while suppressing blank-frame transitions when switching modes inside `TasksPage`.
- 🔸 **[UI Performance, VRAM & Progress Smoothness (`ThemeService.cs`, `LogBatcher.cs`, `BitmapImage`, `MainActivity.kt`)]**: Eliminated redundant multi-level `RequestedTheme` toggles in `ThemeService.RefreshUI`, added `DecodePixelWidth` + `DecodePixelType.Logical` across thumbnail and cover loaders to cut GPU VRAM and scroll stutter, removed idle `IsAnyTaskRunning` notifications on the 500 ms timer, coalesced `LogBatcher` UI flushes (`120 ms`), and added smooth animated `ProgressBar` updates (`40 ms` throttle) on Android.

### 📦 Edition Comparison (Which File Should You Download?)

- 🔸 **`STORM_SWITCH_BOX_8.0.1_Setup.exe` (Windows Installer)**: Full standalone installer for Windows 10/11 x64 (`StormInstaller`) with embedded archive, Start Menu/Desktop shortcuts, Windows Apps & Features registration/uninstaller, and `STORM_Certificate.cer` integration.
- 🔸 **`STORM_SWITCH_BOX_8.0.1.zip` / `STORM_SWITCH_BOX_8.0.1_win-x64.zip` (Windows Portable)**: Identical Windows x64 application and toolset (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`) with zero installation or registry changes - unpack anywhere and launch `StormSwitchBox.exe`.
- 🔸 **`STORM_SWITCH_BOX_8.0.1.apk` (Android Modern - Recommended)**: For modern 64-bit Android 8.0+ devices (`minSdk 26`, `arm64-v8a` + `x86_64`, APK Signature Scheme v2/v3). Nearly half the size because 32-bit native libraries are omitted.
- 🔸 **`STORM_SWITCH_BOX_8.0.1_LEGACY.apk` (Android Legacy / Universal)**: Same features and app code as Modern, built for older Android 5.0-7.1 or 32-bit devices (`minSdk 21`, all 4 ABIs: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, APK Signature Scheme v1/v2/v3).

</details>

---

### 🧩 Какую версию скачать? (Отличия всех сборок)

| Версия / Файл | Платформа и ОС | Архитектуры (ABI) | Главные отличия и кому подходит |
|---|---|---|---|
| **`STORM_SWITCH_BOX_8.0.1_Setup.exe`** (219.60 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Установщик для ПК (`StormInstaller`)**: сам распаковывает программу, создаёт ярлыки в меню «Пуск» и на рабочем столе, регистрирует деинсталлятор в Windows и устанавливает сертификат доверия `STORM_Certificate.cer`. Больше по размеру, т.к. содержит встроенный автономный установщик (.NET 8 self-contained). |
| **`STORM_SWITCH_BOX_8.0.1.zip`** / **`_win-x64.zip`** (151.16 МБ) | Windows 10 / 11 (x64) | `win-x64` | **Портативная версия для ПК**: по возможностям и составу утилит (`hactoolnet`, `hacpack`, `yanu-cli`, `3dstool`, `makerom`, `ctrtool`, `oxipng`, `7za`, `nsz`, `nutdb.json.zst`) на 100% идентична `Setup.exe`, но не требует установки и не вносит записей в реестр - распакуйте в любую папку и запускайте `StormSwitchBox.exe`. |
| **`STORM_SWITCH_BOX_8.0.1.apk`** (14.38 МБ) | **Android 8.0+** (`minSdk 26`) | 64-бит: `arm64-v8a`, `x86_64` | **Android Modern (рекомендуется)**: основная облегчённая сборка для современных смартфонов, планшетов и портативных консолей (`Odin 2`, `Retroid Pocket`, `Razer Edge`, современные `Samsung`/`Xiaomi`/`Pixel`). Содержит нативные движки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) только для 64-битных процессоров и подпись APK v2/v3, поэтому весит почти вдвое меньше Legacy. |
| **`STORM_SWITCH_BOX_8.0.1_LEGACY.apk`** (25.95 МБ) | **Android 5.0+** (`minSdk 21`) | 32-бит + 64-бит: `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64` | **Android Legacy (универсальная)**: по функционалу и интерфейсу **на 100% идентична Modern**, но включает нативные библиотеки (`libzstd-jni`, `libtorrent4j`, `lib7-Zip-JBinding`) сразу для всех 4 архитектур и старую JAR-подпись v1 + v2/v3. Нужна для старых устройств на Android 5.0-7.1, 32-битных прошивок (`armeabi-v7a`), ТВ-приставок и 32-битных эмуляторов Android. |

- 💻 **Отличие Windows от Android**: версия для Windows включает полный настольный набор CLI-инструментов (включая сборку Homebrew-форвардеров, патчер эмуляторов Switch 2 `NSP-Box`, USB MTP/DBI и полную студию 3DS-баннеров), а версия для Android работает автономно без ПК на базе нативного движка Kotlin + JNI (NUP v1.0, NSZ/XCZ/NSP/XCI/3DS, SmartShrink, RuTracker, облачные моды и отправка на консоль по FTP/HTTP/QR).

---

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_8.0.1_Setup.exe** (219.60 МБ)
  - `SHA-256: 27AC8A98E7166D3A3D38D7623DE1F3E57CFB1FC484E8185FD9349DF66CA5313C`
  - Установщик для Windows 10/11 x64 (`StormInstaller`) с ярлыками, деинсталлятором и интеграцией сертификата STORM TEAM.
- 📦 **STORM_SWITCH_BOX_8.0.1_win-x64.zip** / **STORM_SWITCH_BOX_8.0.1.zip** (151.16 МБ)
  - `SHA-256: 066AD5ACDA08E5B71A14B98C9436CDC59CC3BD97CA684BAB746176E75E62F75E`
  - Портативная версия для Windows 10/11 x64 (полный функционал без установки в систему - распакуйте и запустите `StormSwitchBox.exe`).
- 📱 **STORM_SWITCH_BOX_8.0.1.apk** (14.38 МБ)
  - `SHA-256: F5C37F8EDB70E432A21826E959A6045BEED164F44B88E1D06EA0256EDAC24BC6`
  - **Android Modern (рекомендуется)**: 64-битная сборка для Android 8.0+ (`arm64-v8a`, `x86_64`, подпись v2/v3).
- 📱 **STORM_SWITCH_BOX_8.0.1_LEGACY.apk** (25.95 МБ)
  - `SHA-256: 5D90D4009C194405E8469162E424EC466FE0284455E223168422B569F6F238F3`
  - **Android Legacy (универсальная)**: 32/64-битная сборка для Android 5.0+ (`armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`, подпись v1/v2/v3) с тем же функционалом, что и Modern.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
