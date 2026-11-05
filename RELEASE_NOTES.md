> 💡 **Релиз 6.9.5** — *Студия метаданных для каждого слота раздачи RuTracker, мгновенное вшивание NACP/SMDH в готовые образы без пересборки и двуязычные Control NCA для Nintendo Switch 2*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker Multi-Game Slot Metadata Studio] Студия метаданных для каждого игрового слота**: в диалоге загрузки и конвертации многофайловых раздач RuTracker внедрена индивидуальная карточка настройки метаданных для каждого слота. Доступно редактирование названий на английском и русском языках, издателя, TitleID, отображаемой версии, бейджей кастомизации иконок (`RUS`, `60FPS`, `MOD`, `ALL DLC`, произвольный текст и позиция) с автообогащением из TitleDB.
- 🌟 **[Tasks Page In-Place Post-Build Patching] Мгновенное обновление NACP и иконок без пересборки**: если контейнер (`.nsp`, `.nsz`, `.xci`, `.xcz`, `.3ds`, `.cia`) уже собран на диске, сохранение изменений в студии метаданных на вкладке «Задачи» сразу же производит прямое бесшовное вшивание обновлённого `control.nacp` и иконки в файл без повторной полной перепаковки.
- 🌟 **[Switch 2 Dump Compiler & Двуязычный Control NACP] Интеграция кастомных метаданных**: при компиляции дампов Nintendo Switch 2 (`Switch2DumpCompilerService`) метаданные из задачи и слотов раздачи автоматически запекаются в `Control NCA` (английское название во всех слотах по умолчанию и отдельное русское название для локали Horizon OS в слоте 11), с оптимизацией JPEG-иконки и бейджей.
- 🌟 **[ControlEditor Prefix Matching & NCZ Order] Надежность модификации контейнеров**: реализовано сопоставление TitleID по 13-значному префиксу для исключения конфликтов с DLC/обновлениями, а также скорректирован порядок записей PFS0 для сжатых файлов `.ncz`.
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.5**: обновлены версия и код сборки (`versionCode = 695`, `versionName = "6.9.5"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) редакций.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[RuTracker Metadata Studio Overlay]**: разработан интерактивный оверлей редактирования метаданных и иконок внутри диалога RuTracker с предварительным просмотром в реальном времени.
- 🔹 **[EnrichGameSlotsFromFilesAndTitleDb]**: реализован автоматический парсинг TitleID, номеров обновлений и сопоставление с локальной базой TitleDB при анализе торрента.
- 🔹 **[Прямой патч NACP/SMDH в TasksPage]**: применён асинхронный вызов `ApplyCustomMetadataAsync` / `Patch3dsSmdhInPlace` для уже существующих на диске файлов с отображением статуса в логе задачи.
- 🔹 **[Двуязычный NACP для Switch 2]**: обновлён генератор `BuildNacpBytes` в `Switch2DumpCompilerService` с поддержкой раздельных UTF-8 строк для слота 11 (русский) и остальных языковых слотов.
- 🔹 **[Поддержка NCZ в ControlEditor]**: обеспечено корректное позиционирование сжатых Program разделов `.ncz` в заголовке PFS0 при инъекции кастомного Control NCA.
- 🔹 **[Инсталлятор StormInstaller]**: сборка инсталлятора и метаданные развёртывания обновлены до версии 6.9.5 с сохранением цифровой подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker Multi-Game Slot Metadata Studio**: integrated per-slot metadata studio in RuTracker download dialog, supporting dual-language English/Russian titles, publisher, TitleID & version badges, icon badges (RUS, 60FPS, MOD, ALL DLC), and automatic TitleDB enrichment.
- 🔸 **Tasks Page In-Place Post-Build Patching**: editing metadata on the Tasks page for already-built containers (`.nsp`, `.nsz`, `.xci`, `.xcz`, `.3ds`, `.cia`) immediately patches `control.nacp` and icons in place without requiring a complete rebuild.
- 🔸 **Switch 2 Dump Compiler Custom Metadata & Bilingual Control NACP**: injected task metadata directly into Switch 2 Control NCA with default English title and dedicated slot 11 Russian title, plus optimized badge-rendered cover icons.
- 🔸 **ControlEditor 13-Char TitleID Prefix Matching & NCZ Ordering**: enhanced TitleID matching logic to prevent DLC/Update collisions and corrected PFS0 partition ordering for `.ncz` files.
- 🔸 **Android Parity 6.9.5**: synchronized Modern and Legacy Android packages to version 6.9.5 (`versionCode 695`).

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
