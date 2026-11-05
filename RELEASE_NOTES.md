> 💡 **Релиз 6.9.7** — *100% 6-языковая локализация интерфейса (ru, en, de, fr, zh, ja), рекурсивный обход визуального и логического дерева XAML, очистка дублирования иконок и динамическая смена языка на лету без перезапуска приложения*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[100% 6-языковая локализация интерфейса]**: Полный перевод всех страниц, модальных окон, подсказок ToolTip и контекстных меню на 6 основных языков мира: 🇷🇺 Русский, 🇬🇧 Английский, 🇩🇪 Немецкий, 🇫🇷 Французский, 🇨🇳 Упрощенный китайский и 🇯🇵 Японский.
- 🌟 **[Рекурсивный движок `TranslateRoot`]**: Реализован интеллектуальный обход визуального и логического дерева XAML (`ConditionalWeakTable`), автоматически локализующий даже свернутые панели настроек, заголовки вкладок `TabViewItem`, строки `CommunityToolkit DataGrid`, выпадающие меню `Flyout` и `MenuFlyout`, вложенные блоки `Run` и подсказки `ToolTipService`.
- 🌟 **[Single Icon Mandate & Чистый UI]**: Устранено дублирование эмодзи и векторных иконок на кнопках фильтров платформ (`Nintendo Switch`, `Nintendo 3DS`), таблицах и тултипах в строгом соответствии с дизайн-системой STORM SOFT.
- 🌟 **[Французский раздел интерактивной инструкции]**: Добавлена полная поддержка французского языка (`GetTopicsFr`) для всех 22 тем и симулятора параметров `InstructionPage`.
- 🌟 **[Динамическое переключение на лету]**: Мгновенная реакция всех компонентов (`TasksPage`, `SettingsPage`, `CatalogPage`, `GameLibraryPage`, `HistoryPage`, `InstructionPage` и всех 6 диалоговых окон) на смену языка без необходимости перезапуска программы.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **UiTree.cs**: Создан файл `LocalizationService.UiTree.cs` с полным словарем direct translations (611 строк) для моментального перевода любых XAML и динамических текстовых элементов.
- 🔹 **Safe Content Preservation**: Механизм `TranslateRoot` проверяет тип содержимого `ContentControl` и никогда не перезаписывает сложные визуальные шаблоны или векторные иконки.
- 🔹 **Reversible Caching**: Исходные строки кэшируются через `ConditionalWeakTable`, гарантируя корректное многократное переключение между любыми парами языков без искажения текста.
- 🔹 **GameLibraryPage**: Локализован чип «Все системы» (`🌟 Все системы ({0})`) с авто-форматированием разрядов чисел.
- 🔹 **Dialogs Coverage**: Все 6 диалоговых окон (`ControlEditorDialog`, `DirectConsoleSenderDialog`, `LibraryAuditorDialog`, `MultiCartridgeDialog`, `RomFsInspectorDialog`, `RuTrackerModDownloaderDialog`) автоматически переводятся при инициализации и открытии.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **100% 6-Language UI Localization**: Full native translation of all views, dialogs, tooltips, and flyout menus across Russian, English, German, French, Simplified Chinese, and Japanese.
- 🔸 **Recursive Visual & Logical Tree Translation**: Instant runtime tree walk engine (`TranslateRoot`) automatically translates hidden panels, data grids, runs, and dialog elements.
- 🔸 **Single Icon Mandate**: Removed duplicated emojis and icons from platform filter buttons and action controls.
- 🔸 **French Instruction Topics**: Added native French documentation topics and simulator parameter matrix in `InstructionPage`.
- 🔸 **Seamless Dynamic Switching**: Switch languages instantaneously on the fly with zero restarts.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
