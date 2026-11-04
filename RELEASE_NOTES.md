> 💡 **Релиз 6.9.2** — *HTTP Pipelining DPI bypass для трекеров RuTracker, вертикальное центрирование полей ввода, очистка типографики интерфейса и синхронизация Android-клиента*

---

### 🚀 Ключевые изменения и улучшения
- 🌟 **[RuTracker DPI Bypass & Pipelining Engine] Стабильный анонс и разблокировка трекеров**: в сетевом движке RuTracker (`RuTrackerCloudEngine.kt` и `RuTrackerModService.cs`) реализован механизм HTTP/1.1 Pipelining к пулу анонсеров `bt.t-ru.org`–`bt4.t-ru.org` через доверенные граничные IP Cloudflare. Метод обходит фильтрацию ТСПУ/DPI без необходимости использования внешних VPN и сторонних прокси-серверов.
- 🌟 **[Эргономика и Типографика Полей Ввода] Идеальное вертикальное центрирование**: в диалоге загрузки модификаций RuTracker (`RuTrackerModDownloaderDialog`) реализовано автоматическое вертикальное центрирование текста и курсора ввода (`VerticalContentAlignment="Center"` и обработчик `CenteredInput_Loaded`) для полей прокси, логина и пароля.
- 🌟 **[Интерфейс и Векторные Иконки] Очистка кнопок от эмодзи и векторная графика меню**: в соответствии со стандартами STORM UI устранены эмодзи из кнопок действий («Найти в сети» в `LocalizationService`), а в выпадающее меню добавления файлов и папок в `TasksPage` интегрированы нативные векторные глифы Fluent (`&#xE8E5;`, `&#xE8B7;`).
- 🌟 **[Android Паритет и Синхронизация] Полнофункциональный релиз 6.9.2**: обновлены версия и код сборки (`versionCode = 692`, `versionName = "6.9.2"`) для Modern (64-бит ARMv8/x86_64) и Legacy (32-бит ARMv7/x86) с полной поддержкой Pipelining DPI bypass и ускоренного поиска по базе модификаций.

<details>
<summary><b>📋 Полный список изменений (нажмите, чтобы развернуть)</b></summary>

- 🔹 **[HTTP/1.1 Pipelining к RuTracker]**: внедрена отправка сдвоенных HTTP-запросов к Cloudflare edge IP для безопасного получения списка пиров с `bt.t-ru.org` без деградации скорости.
- 🔹 **[Центрирование текстовых полей]**: подключён единый загрузчик `CenteredInput_Loaded` для точной фиксации внутреннего контейнера редактирования текста по вертикали.
- 🔹 **[Векторные элементы меню]**: в диалоге задач `TasksPage` добавлены векторные иконки в элементы меню добавления файлов и папок.
- 🔹 **[Строковые ресурсы и локализация]**: обновлены локализованные строки меню библиотеки и параметров для 6 языков.
- 🔹 **[Инсталлятор StormInstaller]**: конфигурация сборки и метаданные развёртывания обновлены до версии 6.9.2 с сохранением цифровой подписи STORM TEAM.

</details>

<details>
<summary><b>🌐 English Changelog (click to expand)</b></summary>

- 🔸 **RuTracker HTTP Pipelining DPI Bypass**: integrated HTTP/1.1 pipelining to tracker announcers (`bt.t-ru.org` through `bt4.t-ru.org`) via Cloudflare edge nodes, neutralizing DPI inspection and peer announcement timeouts.
- 🔸 **Vertical Input Caret & Content Centering**: enforced `VerticalContentAlignment="Center"` with dynamic template centering on proxy, username, and password fields.
- 🔸 **UI Typography & Vector Fluent Icons**: stripped non-standard emojis from action buttons and added native vector Fluent glyphs to task flyout menus.
- 🔸 **Android Parity 6.9.2**: synchronized Modern and Legacy editions to version 6.9.2 (`versionCode 692`) with tracker pipelining and updated dataset.

</details>

<details>
<summary><b>📦 Файлы и вложения к релизу (нажмите, чтобы развернуть)</b></summary>

- 📁 **Прикреплённые файлы**: Исполняемые файлы, инсталляторы и архивы доступны в секции **Assets** ниже.
- 🛡️ **Контроль целостности**: Все бинарные файлы собраны из официального исходного кода и проверены перед публикацией.
- 💻 **Установка**: Скачайте соответствующий архив/инсталлятор из списка Assets и следуйте стандартным инструкциям.

</details>
