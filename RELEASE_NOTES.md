> 💡 **Релиз 7.0.11** — *Прямой потоковый компилятор NUP без извлечения на диск NTFS, устранение дискового узкого горлышка (150,000+ файлов RomFS за доли секунды), защита папочного анализа Switch 2 дампов и сквозная интеграция NUP во все режимы на Windows и Android*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Direct Zero-NTFS-Extraction NUP Streaming Compiler]**: Реализована прямая потоковая сборка Switch 2 контейнеров (NSP, NSZ, XCI, XCZ) из открытого монолитного архива `.nup` без предварительного извлечения файлов RomFS на диск. Полностью устранено узкое горлышко файловой системы NTFS: сборка масштабных дампов (например, Donkey Kong Bananza на 159,858 файлов) больше не требует 15–30 минут на запись и чтение десятков тысяч мелких файлов, а построение виртуальной иерархии RomFS выполняется в оперативной памяти за доли секунды.
- 🌟 **[In-Memory RomFS Streaming & Zstd Decompression]**: Метод `BuildStreamedRomFs` получил нативную поддержку виртуальных дескрипторов экстентов NUP (`__NUP__|offset|extentSize|codec|size`). Чтение несжатых данных и декомпрессия блоков Zstandard выполняются на лету через `BoundedReadStream` и `ZstdSharp.DecompressionStream` непосредственно в выходной поток NCA-раздела с минимальным расходом оперативной памяти (1 МБ буфер потока).
- 🌟 **[Защита от ошибочного папочного анализа Switch 2]**: Устранена критическая ошибка в `MultiContentService`, при которой задачи с Title ID Switch 2 (`04...`) передавали родительскую директорию контейнера в анализатор распакованных дампов с ошибкой «Не удалось проанализировать дамп Nintendo Switch 2». Добавлены строгие проверки наличия файлов-контейнеров (`.nup`, `.nsp`, `.nsz`, `.xci`, `.xcz`) и приоритетная маршрутизация файлов перед папочным режимом на Windows и Android.
- 🌟 **[Сквозная маршрутизация NUP во всех операциях]**: Поддержка формата `.nup` интегрирована во все сценарии работы приложения: Конвертация (`Convert`), Распаковка (`Unpack`), Проверка и верификация (`Verify`), а также прямая конвертация контейнеров в `NupContainerService`.
- 🌟 **[Безопасное размещение временных данных STORM_TMP]**: Рабочий каталог Switch 2 компилятора (`tempWorkDir`) перенесен из системного диска `C:\` в изолированную папку `STORM_TMP` на целевом накопителе вывода, что предотвращает аварийное переполнение системного диска при сборке крупных Switch 2 образов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Zero-Extraction NUP Compiler]**: Новый специализированный метод `Switch2DumpHelper.CompileNupToSwitch2ContainerAsync` открывает `.nup` в режиме только для чтения, извлекает во временную папку исключительно исполняемые бинарники ExeFS (~6 небольших файлов), а всё дерево RomFS (Base, Update и DLC) индексирует виртуально в памяти за миллисекунды.
- 🔹 **[MultiContentService Priority Routing]**: В `MultiContentService.cs` проверка файлов `.nup` вынесена на первое место перед блоком Switch 2 папок; директория `task.InputFolders` анализируется как распакованный дамп только при отсутствии контейнеров в очереди задач.
- 🔹 **[Target-Drive STORM_TMP]**: Каталог временных файлов компилятора формируется на том же диске, куда сохраняется итоговый образ, исключая нехватку свободного места на системном диске `C:\`.
- 🔹 **[TasksViewModel Routing]**: Режим проверки (`Verify`) отображает структуру пакетов, метаданные и заголовок NUP; режим распаковки (`Unpack`) вызывает нативное извлечение архива; режим конвертации (`Convert`) транслирует `.nup` в выбранный формат Switch 2.
- 🔹 **[Android StormNczEngine Parity]**: В движке `StormNczEngine.kt` добавлены защитные гарды против ошибочного распознавания родительских папок контейнеров и обеспечена синхронная логика обработки NUP.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[Direct Zero-NTFS-Extraction NUP Streaming Compiler]**: Implemented direct in-stream compilation of Switch 2 containers (NSP, NSZ, XCI, XCZ) directly from `.nup` archives without unpacking RomFS files to NTFS. Completely eliminates file system thrashing when building massive games (150,000+ files) while indexing the RomFS hierarchy in RAM within milliseconds.
- 🔸 **[In-Memory RomFS Streaming & Zstandard Decompression]**: Enhanced `BuildStreamedRomFs` with virtual NUP extent descriptors (`__NUP__|offset|extentSize|codec|size`). Uncompressed and Zstandard-compressed file extents are streamed directly into output NCA partitions via `BoundedReadStream` and `ZstdSharp.DecompressionStream` using a lightweight 1 MB buffer.
- 🔸 **[Switch 2 Dump Directory Analysis Guard]**: Fixed an issue in `MultiContentService` where Switch 2 tasks with Title ID `04...` caused parent directories containing `.nup` files to be misrouted to raw folder dump compilation. Added strict container file guards across both Windows and Android engines.
- 🔸 **[Full NUP Integration across All Modes]**: Seamlessly routed `.nup` operations through Convert, Unpack, and Verify modes in `TasksViewModel` and `NupContainerService`.
- 🔸 **[Target Drive STORM_TMP Relocation]**: Moved compiler scratch space from `%TEMP%` (`C:\`) to `STORM_TMP` on the target volume, preventing system drive exhaustion during large multi-gigabyte builds.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
