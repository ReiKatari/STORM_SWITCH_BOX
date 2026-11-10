> 💡 **Релиз 7.0.12** - *Рабочая станция RuTracker AURORA FLUID: ликвидация двойных рамок ContentDialog (+115px полезной высоты), адаптивный экранный лейаут, 4-режимный переключатель рабочего пространства, полновысотный каталог с мгновенным поиском по 7,275+ релизам, клиентская сортировка и фильтрация, складные карточки слотов, расширенные облачные ссылки, складная очередь загрузок, мобильный диспетчер фоновых задач и строгое соблюдение Single Icon Mandate*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker AURORA FLUID Workstation Layout]**: Полностью переработана архитектура окна загрузчика модов и релизов RuTracker. Устранено паразитное двойное обрамление ContentDialog, высвободившее свыше 115px полезного пространства по высоте. Диалог теперь адаптивно заполняет окно на 96% ширины и высоты, предоставляя просторное рабочее пространство на экранах любого разрешения.
- 🌟 **[4-Mode Workspace Switcher]**: Реализован удобный селектор режимов отображения рабочей области [Обзор / Overview, Игры и файлы 50/50, Облако и моды, Загрузки], позволяющий сфокусироваться на нужной задаче в один клик.
- 🌟 **[Мгновенный поиск и клиентская фильтрация каталога]**: Полновысотный список релизов RuTracker оснащен фильтрацией на лету без задержек по базе из 7,275+ Switch-релизов. Добавлены быстрые пресеты, клиентская сортировка [По сидам, По размеру, По названию] и переключатель релизов с русской локализацией.
- 🌟 **[Складные карточки и умное управление файлами]**: Карточки игровых слотов поддерживают компактный 1-строчный вид и детальное раскрытие. Список файлов раздачи получил фильтрацию, переключение групп в один клик и кнопку снятия выделения со всех файлов.
- 🌟 **[Нативные облачные ссылки и складная очередь загрузок]**: Устранена проблема вложенных ScrollViewer в блоке внешних облачных хранилищ - ссылки прокручиваются плавно и нативно. Очередь загрузок Torrent получила виртуализированный компактный вид с разделением телеметрии статуса и очереди.
- 🌟 **[Android Background Task Manager]**: В мобильной версии на Android реализован складной диспетчер фоновых задач с чипами мгновенной сортировки и фильтрации, отображением прогресса и телеметрией.
- 🌟 **[100% Single Icon Mandate Compliance]**: Проведен полный аудит интерфейса загрузчика на ПК и мобильных устройствах. Устранены дублирования иконка + эмодзи и парные иконки, на каждом элементе управления используется строго не более одной векторной иконки.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[AURORA FLUID Chrome Elimination]**: Ликвидированы внешние отступы и заголовочные плашки ContentDialog, интерфейс использует внутреннюю панель инструментов с бесшовной интеграцией в стиль приложения.
- 🔹 **[Zero-Latency Instant Search]**: Поиск по каталогу RuTracker фильтрует коллекцию из 7,275+ релизов за 0 мс при вводе текста без блокировки UI потока.
- 🔹 **[Torrent File Selection Suite]**: Добавлены фильтр списка файлов, кнопка «Снять все» и пакетное переключение чекбоксов файлов торрента.
- 🔹 **[Separated Telemetry HUD]**: Скорость загрузки, сиды, пиры и статус очереди вынесены в компактные специализированные чипы.
- 🔹 **[Android Parity]**: Мобильный клиент MainActivity получил синхронные фильтры, быстрые чипы сортировки и оптимизированный адаптер списка.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **[RuTracker AURORA FLUID Workstation Layout]**: Completely overhauled RuTracker mod and release downloader dialog architecture. Eliminated redundant double ContentDialog chrome, reclaiming over 115px vertical height, expanding the workstation to 96% of window viewport.
- 🔸 **[4-Mode Workspace Switcher]**: Added instant workspace switcher [Overview, Games & Files 50/50, Cloud & Mods, Downloads] for quick navigation and task focus.
- 🔸 **[Zero-Latency Search & Client-Side Sorting]**: Full-height RuTracker releases list features instantaneous 0ms client-side search across 7,275+ Nintendo Switch releases with quick presets and sorting [By seeds, By size, By title].
- 🔸 **[Collapsible Game Cards & File Selection Suite]**: Implemented collapsible 1-row compact and detailed game slot cards, file filter, one-click group toggles, and Deselect All controls.
- 🔸 **[Smooth Cloud Links & Telemetry HUD]**: Resolved nested ScrollViewer traps for external cloud links. Torrent queue features separated telemetry indicators for speeds, seeds, peers, and queue state.
- 🔸 **[Android Collapsible Task Manager]**: Modern Android interface equipped with collapsible background task cards, instant sort chips, and Russian language filter.
- 🔸 **[100% Single Icon Mandate Compliance]**: Enforced strict single icon rule across all desktop and mobile controls with zero dual icon or icon+emoji duplicates.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 💻 **STORM_SWITCH_BOX_7.0.12_Setup.exe** (230.49 МБ)
  - `SHA-256: E995139C880B9B83E263AECF1FE5347FBA680B008D1642DC9A1CDBA658974A38`
  - Установщик для Windows с интеграцией в систему и единым стилем оформления.
- 📦 **STORM_SWITCH_BOX_7.0.12_Portable.zip** / **STORM_SWITCH_BOX_7.0.12.zip** (162.05 МБ)
  - `SHA-256: BB3AA2C97BB6B6D51E1646A44243ACDFAF68D423BC5394E8F9A3A64898FCF035`
  - Портативная версия для Windows x64 (распакуйте и запустите без установки).
- 📱 **STORM_SWITCH_BOX_7.0.12_Android.apk** / **STORM_SWITCH_BOX_7.0.12.apk** (14.36 МБ)
  - `SHA-256: 1B592EA46F0B042F25A8ACFAC8FA1A81983A2C14893467B5AC5F3A88C9AD53B6`
  - Мобильная версия для 64-битных устройств под управлением Android 8.0+.
- 📱 **STORM_SWITCH_BOX_7.0.12_Android_Legacy.apk** / **STORM_SWITCH_BOX_7.0.12_LEGACY.apk** (25.93 МБ)
  - `SHA-256: F370A7F99E8013D776D976A05A4B220F016E4D45D82BE45AD28249B64B829AAB`
  - Универсальная сборка для Android 5.0+ и 32/64-битных архитектур (ARMv7, ARM64, x86, x86_64).
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и подписаны цифровой подписью STORM TEAM.

</details>
