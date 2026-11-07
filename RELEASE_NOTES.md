> 💡 **Релиз 7.0.6** — *Хотфикс автономного установщика StormInstaller (устранение IndexOutOfRangeException при установке на всех 11 языках и безопасный доступ к локализации T(index)), стабилизация статуса очереди RuTracker и полная мультиязычность*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[StormInstaller: Исправление критического исключения IndexOutOfRangeException]**: Устранена аварийная остановка инсталлятора `StormInstaller` на этапе `BtnInstall_Click`, вызванная отсутствием строк статусов распаковки и финализации (индексы `15..23`) в словарях переводов. Матрица локализации расширена до полных 24 строк для всех 11 языков экосистемы (`RU, EN, DE, FR, ZH, KO, JA, ES, IT, PT, HI`).
- 🌟 **[StormInstaller: Безопасный акцессор локализации T(index)]**: Внедрён защитный метод `T(index)` с автоматическим контролем границ массивов строк, гарантирующий fallback на английскую и русскую локализацию без возможности генерации исключений выхода за пределы диапазона.
- 🌟 **[RuTracker: Устранение ложного статуса «ОШИБКА» после успешной сборки]**: Межпоточная синхронизация `PropertyChanged` (`COMException 0x8001010E RPC_E_WRONG_THREAD`) при очистке временной папки раздачи после создания мульти-контентных контейнеров (`.nsz`/`.nsp`/`.xci`/`.xcz`), барьер ожидания UI-потока `StatusSyncBarrier` и защита `IsBuildCompletedOnDisk`.
- 🌟 **[RuTracker: Автоисцеление очереди и семантическая индикация SpeedBrush]**: Автоматическое восстановление сохранённых задач с готовым образом на диске в статус `ГОТОВО` (100%) при старте приложения, а также адаптивная цветовая дифференциация скорости и статуса задач под все темы оформления.
- 🌟 **[Локализация: 100% покрытие 11 языков и автоопределение языка ОС]**: Полная динамическая мультиязычность интерфейса Windows (WinUI 3), установщика `StormInstaller` и Android-клиента с мгновенным переключением на лету и автоопределением системного языка ОС.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[StormInstaller Full 24-Key Translation Matrix]**: Добавлены все недостающие системные строки стадий установки (остановка процессов, регистрация сертификата, распаковка, создание ярлыков, брандмауэр, успешное завершение и ошибки) для 11 языков.
- 🔹 **[Safe Localization Bounds Fallback]**: Метод `T(index)` защищает процесс установки от любых сбоев локализации с плавным переходом на дефолтные формулировки.
- 🔹 **[Thread-Safe Status Dispatcher]**: Маршрутизация всех уведомлений `PropertyChanged` модели задачи через `App.MainDispatcher.TryEnqueue` с защитой от вызова из пула рабочих потоков.
- 🔹 **[IsBuildCompletedOnDisk Guard]**: Проверка существования и ненулевого размера сгенерированного файла перед генерацией любых исключений в блоке пост-процессинга раздачи.
- 🔹 **[Watchdog Auto-Healing]**: Фоновый таймер очереди верифицирует целостность выходных контейнеров и переводит успешно завершённые задачи в `ГОТОВО`.
- 🔹 **[SpeedBrush Adaptive Binding]**: Привязка цвета текста в DataGrid к статусу задачи с гармоничной палитрой для всех 8 тем STORM SOFT.
- 🔹 **[Android StormLocalization]**: Полнофункциональный локализационный модуль `StormLocalization.kt` для Android с поддержкой 11 языков и динамическим обновлением UI.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[StormInstaller Hotfix: IndexOutOfRangeException Resolved]**: Fixed crash during `BtnInstall_Click` in `StormInstaller` by providing complete 24-string localization arrays across all 11 supported languages (RU, EN, DE, FR, ZH, KO, JA, ES, IT, PT, HI).
- 🔸 **[Safe Localization Accessor T(index)]**: Added bounds-safe string accessor with fallback to English/Russian strings, eliminating out-of-range indexing hazards.
- 🔸 **[RuTracker Build False-Error Elimination]**: Synchronized `PropertyChanged` notifications via `App.MainDispatcher` during torrent folder post-processing, preventing COMException 0x8001010E.
- 🔸 **[Status Sync Barrier & IsBuildCompletedOnDisk]**: Added UI status propagation barrier before disk cleanup and `IsBuildCompletedOnDisk` safeguard preventing post-cleanup error overrides.
- 🔸 **[11 Languages Ecosystem Localization]**: Full runtime localization across WinUI 3 desktop client, StormInstaller, and Android client with OS language auto-detection.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
