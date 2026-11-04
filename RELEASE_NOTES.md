> 💡 **Релиз 6.9.1** — *Группировка файлов и ссылок по играм в RuTracker Mod Downloader, центрирование текста полей ввода, синхронизация Android-клиента и оптимизация производительности*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker Блочная Группировка по Играм] Наглядное разделение контента раздачи**: в диалоге загрузки модов RuTracker (`RuTrackerModDownloaderDialog`) реализована интеллектуальная группировка файлов торрента и облачных ссылок по игровым слотам (`RuTrackerFileGroupBlock` и `RuTrackerLinkGroupBlock`). Каждый блок оформлен индивидуальным цветным бейджем (`ИГРА #1`, `ИГРА #2`, `ОБЩИЕ ФАЙЛЫ`), содержит название игры и сводку по объёму и количеству файлов.
- 🌟 **[Умная Сортировка и Фильтрация] Естественный порядок частей и ролей**: внедрена естественная буквенно-цифровая сортировка (Natural Sort) заголовков частей и разделов, а также иерархический приоритет ролей файлов (`ИГРА` → `ОБНОВЛЕНИЕ` → `DLC` → `ФАЙЛ`) с автоматическим выделением чистых названий игр из путей и архивов.
- 🌟 **[Интерфейс и Типографика] Вертикальное центрирование текста в полях ввода**: оптимизировано отображение текстовых полей ввода и строк поиска с выравниванием `VerticalContentAlignment="Center"`, исключающее смещение курсора и текста к верхней границе контейнера.
- 🌟 **[Android Паритет и Синхронизация] Релиз 6.9.1 для Android**: обновлены версия и код сборки (`versionCode = 691`, `versionName = "6.9.1"`), обеспечена 100% совместимость с сервисами RuTracker и синхронизация обеих редакций пакетов (`Modern` 64-bit и `Legacy` 32-bit).

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[Блочная визуализация RuTracker]**: реализованы контейнеры `RuTrackerFileGroupBlock` и `RuTrackerLinkGroupBlock` с поддержкой динамического обновления без сброса позиции скролла при переключении слотов.
- 🔹 **[Ролевые индикаторы файлов]**: добавлены контрастные бейджи категорий файлов (`RoleBadgeBackground` и `RoleBadgeForeground`) для быстрого визуального контроля структуры раздачи.
- 🔹 **[Центрирование текстовых полей]**: обновлены стили и разметка `TextBox` в диалогах поиска и параметров.
- 🔹 **[Синхронизация Android]**: актуализированы метаданные сборки и строковые ресурсы для версии 6.9.1.
- 🔹 **[Инсталлятор StormInstaller]**: обновлена конфигурация инсталлятора под релиз 6.9.1 с сохранением цифровой подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker Grouped Per-Game Blocks**: implemented structured per-game block visualization (`RuTrackerFileGroupBlock` and `RuTrackerLinkGroupBlock`) with dedicated color badges, slot titles, file counts, and aggregated size counters.
- 🔸 **Natural Sorting & Smart Role Hierarchy**: added natural alphanumeric sorting for franchise titles and hierarchical role prioritization (`GAME` → `UPDATE` → `DLC` → `FILE`) with clean title extraction.
- 🔸 **Vertical Text Centering**: adjusted `TextBox` vertical content alignment across dialogs for optimal typography and caret placement.
- 🔸 **Android Parity 6.9.1**: bumped version to 6.9.1 (`versionCode 691`) across Modern and Legacy editions.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
