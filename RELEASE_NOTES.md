> 💡 **Релиз 6.9.9** — *Строгая фильтрация файлов раздач RuTracker, неблокирующие параллельные загрузки, восстановление конвейеров распаковки/упаковки/инспектора Windows и нативная криптография билетов `.tik` с упаковщиком NCA3 на Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker: строгая передача только выбранных файлов и очистка граничных кусков]**: Исправлена фильтрация контейнеров и архивов модов строго по отмеченным чекбоксам (`IsSelected == true`) во всех режимах (1 слот и мульти-слот), мгновенное применение `Priority.DoNotDownload` в момент `MetadataReceived`, автоматическое удаление частичных граничных файлов BitTorrent и живой пересчёт выбранной версии и размера в карточке игры.
- 🌟 **[RuTracker: неблокирующий пакетный запуск нескольких загрузок подряд]**: Добавлены изолированные сессии `CreateIsolatedDownloadSession()` с динамическим портом `0` и отдельными подпапками пакетов — теперь сразу после старта скачивания можно выбрать другой набор файлов или следующую раздачу и запустить новую загрузку без отмены текущей и без переоткрытия окна.
- 🌟 **[Windows: полное восстановление Распаковки, Упаковки, Обновления, Конвертации и Инспектора RomFS/ExeFS]**: Устранено отравление `ExternalKeySet` через `RegisterRightsIdFallback`, исправлена расшифровка `TitleKey` через `TitleKek` в `StormNczStorage` и устранено двойное шифрование билетов `.tik`, возвращена прямая маршрутизация режимов `Convert`, `Pack` и `Update` в `TasksViewModel` без перехвата в `Multi`, устранены дедлоки пайпов и поддержана обратная сборка распакованных папок `exefs/` + `romfs/` в загружаемые `NSP`/`NSZ`/`XCI`.
- 🌟 **[Android: полноценная криптография `.tik` (`TitleKey`), потоковая Распаковка 13+ ГБ, сборка `NCA3` (`IVFC Lv1–Lv6`) и Инспектор RomFS]**: Внедрён сбор и расшифровка билетов `.tik` (`titlekek_XX` AES-128-ECB) перед распаковкой и Zstd-сжатием `NCZ`, прямое чтение слайсов `RomFS`/`ExeFS` без переполнения памяти, замена заглушек упаковки и виртуального проводника на полноценную компиляцию `Program/Control/Meta NCA3` и чтение реального дерева `IVFC Level 6`.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **RuTrackerModService.cs**: Добавлена очистка граничных файлов BitTorrent (piece boundary leftovers), селективное применение `Priority.DoNotDownload` по маске отмеченных элементов и динамическое выделение изолированных торрент-сессий `CreateIsolatedDownloadSession()` с отдельными рабочими директориями.
- 🔹 **RuTrackerModDownloaderDialog.xaml.cs**: Обеспечен неблокирующий запуск загрузок с сохранением открытого окна и возможностью параллельного скачивания других раздач, реализован реактивный пересчёт выбранного объёма данных и версии игры при переключении чекбоксов.
- 🔹 **KeysService.cs & TicketHarvesterService.cs**: Защита `ExternalKeySet` от фиктивных RightsID через метод `RegisterRightsIdFallback`, корректное дешифрование зашифрованного TitleKeyBlock через соответствующий master/title kek и предотвращение записи нулевых ключей.
- 🔹 **StormNczStorage.cs & SwitchFormatService.cs**: Восстановление корректной последовательности расшифровки заголовков NCA и сжатия блоков NCZ с сохранением валидной структуры билетов `.tik` без повторного шифрования.
- 🔹 **TasksViewModel.cs & MultiContentService.cs**: Исправлена маршрутизация задач в очереди: режимы `Convert`, `Pack` и `Update` теперь исполняются через соответствующие целевые конвейеры без принудительного перехвата в multi-merge.
- 🔹 **VirtualRomFsInspectorService.cs**: Полная ликвидация блокировок анонимных каналов при чтении списков файлов RomFS/ExeFS, корректная распаковка выбранных поддиректорий и сборка готовых контейнеров из папок.
- 🔹 **Android StormNczEngine.kt & MainActivity.kt**: Реализация аппаратной расшифровки билетов `.tik` по таблице `titlekek_XX`, потоковое чтение больших контейнеров (13+ ГБ) с нулевым оверхедом по памяти, компилятор `NCA3` с генерацией структур `IVFC` уровней 1–6 и интеграция инспектора RomFS в мобильный интерфейс.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker: Strict Selected-Only File Ingestion & Boundary File Pruning**: Fixed container and mod archive filtering strictly matching enabled checkboxes (`IsSelected == true`) across single-slot and multi-slot modes, instantaneous `Priority.DoNotDownload` on `MetadataReceived`, automatic deletion of partial boundary BitTorrent files, and dynamic recalculated version/size display in game cards.
- 🔸 **RuTracker: Non-Blocking Concurrent Batch Downloads**: Added isolated download sessions (`CreateIsolatedDownloadSession()`) with dynamic ephemeral port `0` and dedicated subdirectories per batch — users can queue multiple downloads successively without canceling ongoing tasks or reopening dialogs.
- 🔸 **Windows: Complete Restoration of Unpack, Pack, Update, Convert & RomFS/ExeFS Inspector**: Resolved `ExternalKeySet` corruption via `RegisterRightsIdFallback`, fixed `TitleKey` decryption via `TitleKek` in `StormNczStorage` preventing duplicate `.tik` encryption, restored direct routing of `Convert`, `Pack`, and `Update` pipelines in `TasksViewModel` without erroneous multi-redirects, eliminated pipe deadlocks, and supported reverse packaging of unpacked `exefs/` + `romfs/` folders into launchable `NSP`/`NSZ`/`XCI` packages.
- 🔸 **Android: Complete `.tik` (`TitleKey`) Cryptography, Zero-Copy Streaming Unpack for 13+ GB, `NCA3` (`IVFC Lv1–Lv6`) Compiler & RomFS Inspector**: Implemented `.tik` ticket collection and hardware AES-128-ECB decryption via `titlekek_XX` prior to decompression/NCZ Zstandard compression, direct memory-safe slicing of `RomFS`/`ExeFS` without OOM, replaced packaging and virtual browser stubs with true `Program/Control/Meta NCA3` compilation and real `IVFC Level 6` directory tree parsing.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
