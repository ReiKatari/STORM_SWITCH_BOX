> 💡 **Релиз 6.7.8** — *Автоматическое распознавание и пересжатие Solid NSZ в блочный NCZBLOCK и XCZ, атомарная защита от перезаписи одноимённых файлов при сохранении и корректное слияние полных Program NCA патчей обновлений без BKTR*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[Компрессор NCZ] Автоматическое пересжатие Solid NSZ в Блочный NCZBLOCK и XCZ**: на Android (`StormNczEngine.kt`) и Windows (`TasksViewModel.cs`, `NszCompressionService.cs`) внедрено прецизионное бинарное сканирование заголовков секций `NCZSECTN` и `NCZBLOCK` по смещению `0x4010 + sectionCount * 0x40`. При добавлении монолитных файлов `Solid .nsz` в очередь конвертации движок автоматически распаковывает потоковые секции `.ncz` в чистые `.nca` и перепаковывает их в эталонный блочный формат `NCZBLOCK` (или конвертирует в `XCZ`, `XCI`, `NSP`), а для уже блочных архивов сохраняет прямой скоростной конвейер без избыточной декомпрессии.
- 🌟 **[Защита целостности] Атомарная защита от коллизий и перезаписи одноимённых файлов (Same-Name Collision Guard)**: генерация и компрессия всех выходных контейнеров (`NSZ`, `XCZ`, `NSP`, `XCI`) на Windows и Android теперь осуществляется через защищённый временный файл `.building_<id>.tmp`. После завершения сборки производится верификация заголовков PFS0/HFS0, после чего выполняется атомарная подмена целевого файла. Это на 100% исключает повреждение или обнуление исходного файла при совпадении путей источника и назначения, а также при аварийной отмене задачи.
- 🌟 **[Сборщик 4-в-1] Корректное слияние полных Program NCA обновлений (Full Patch NCA Guard)**: в `StormNczEngine.kt` и `MultiContentService.cs` при слиянии базовой игры, патчей и DLC внедрена строгая проверка метаданных `Patch CNMT (0x81)`. Полные исполняемые разделы `Program NCA` обновлений (не использующие дельта-механизм BKTR, например, в *Celeste*) теперь гарантированно сохраняются и заменяют базовую версию без ложного срабатывания фильтра дубликатов.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Solid NSZ Scanner]**: алгоритм `containerNeedsNczRecompression` выполняет прямое чтение таблицы блоков NCZ без распаковки всего контейнера, определяя `blockSize > 0` и присутствие сжатых разделов.
- 🔹 **[Safe Temp Staging]**: все операции сборки изолированы во временных файлах `.building_*.tmp`, автоматически очищаемых при отмене или непредвиденных ошибках.
- 🔹 **[MultiContent Merger]**: разделена логика обработки дельта-фрагментов и самостоятельных обновлений базовой игры в монолитном контейнере.
- 🔹 **[Android Engine]**: унифицированы методы перепаковки и валидации блочной структуры для ARM64 и Legacy архитектур.
- 🔹 **[WPF / WinUI 3 Tasks]**: очередь задач автоматически выставляет флаг `RecompressSolidNcz` при перетаскивании неподдерживаемых консолями монолитных NSZ-образов.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **Solid to Block NCZBLOCK & XCZ Recompression**: automated header inspection (`NCZSECTN` and `NCZBLOCK` magic checks at offset `0x4010 + sectionCount * 0x40`) transparently decompresses legacy solid stream `.ncz` files into clean `.nca` partitions and repacks them into high-performance block-compressed `NCZBLOCK` or `XCZ` formats across Android and Windows.
- 🔸 **Atomic Same-Name Collision Guard**: all container compilation tasks (`NSZ`, `XCZ`, `NSP`, `XCI`) write to `.building_<id>.tmp` first with header validation before atomic replacement, eliminating file truncation when output folder matches source.
- 🔸 **Full Program NCA Patch Merge Fix**: refined `Patch CNMT (0x81)` identification in `StormNczEngine.kt` and `MultiContentService.cs`, ensuring full (non-BKTR) patch `Program NCA` packages (such as *Celeste*) are preserved and correctly supersede base titles.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
