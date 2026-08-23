<h1 align="center">
  <br>
  <img src="./logo.png" alt="STORM SWITCH BOX" width="220">
  <br>
  <b>⚡ STORM SWITCH BOX v4.7.0</b>
  <br>
</h1>

<div align="center">
  <a href="https://microsoft.com"><img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20x64-blue?style=for-the-badge&logo=windows" alt="Platform"></a>
  <a href="https://github.com/microsoft/WindowsAppSDK"><img src="https://img.shields.io/badge/framework-WinUI%203%20%2F%20Windows%20App%20SDK-purple?style=for-the-badge&logo=windows-terminal" alt="Framework"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/language-C%23%2012%20%2F%20.NET%208%20LTS-green?style=for-the-badge&logo=dotnet" alt="Language"></a>
  <a href="https://github.com/ReiKatari/STORM_SWITCH_BOX/releases"><img src="https://img.shields.io/badge/version-4.7.0-cyan?style=for-the-badge&logo=git" alt="Version"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-orange?style=for-the-badge" alt="License"></a>
</div>

<br>

<h3 align="center">
Флагманский высокопроизводительный швейцарский нож и комбайн нового поколения от <b>STORM TEAM</b> для работы с игровыми экосистемами <b>Nintendo Switch</b> и <b>Nintendo 3DS</b>, а также интерактивная энциклопедия всех игровых систем Nintendo.
</h3>

<p align="center">
Нативный C# движок на базе <b>WinUI 3</b>, <b>Windows App SDK</b>, <b>LibHac</b> и <b>ZstdSharp</b>. Обработка образов в <b>10–20 раз быстрее</b> классических Python-скриптов, сборка монолитного мульти-контента 4-в-1, жесткий HardPatch дельта-патчей RomFS, раздельная автоматизация «Умной папки», тримминг 3DS, цифровая подпись Authenticode SHA-256 и 100% безопасная гигиена временных файлов.
</p>

---

## 🌟 Ключевые возможности программы

### 1. 📦 Сборка Мульти-контента 4-в-1 (Nintendo Switch и 3DS)
* **Монолитный бандл**: Полное объединение базовой игры, файлов обновлений (патчей), всех дополнений (DLC) и модификаций/русификаторов (папки `romfs` / `exefs`) в **один готовый файл** (`.nsp`, `.nsz`, `.3ds`, `.cia`).
* **Автоматическая верификация**: Сверка TitleID, порядка и валидности PFS0/NCA заголовков с сохранением тикетов и прав доступа к DLC Unlocker.
* **Нативная сборка LibHac**: Скоростная компоновка без промежуточных сбоев файловой таблицы squirrel.

### 2. 🛠️ Жёсткое вшивание обновлений (HardPatch RomFS Engine)
* **Интеграция BKTR**: Дельта-патчи обновлений вшиваются напрямую в базовый образ игры RomFS.
* **Автономность**: Полученный образ не требует отдельной установки патчей и мгновенно запускается на консолях и любых эмуляторах.
* **Поддержка модов**: Бесшовное слияние текстур, шрифтов и переводов прямо во время патчинга.

### 3. ⚡ Сверхбыстрое сжатие и декомпрессия NSZ / NCZ / XCZ
* **Нативный Zstandard C#**: Использование порта `ZstdSharp` обеспечивает компрессию до 22 уровня со скоростью, в десятки раз превышающей `nsz.py`.
* **Многопоточность**: Полноценная параллельная обработка блоков данных и пакетный конвейер задач.

### 4. 🕹️ Полная поддержка экосистемы Nintendo 3DS
* **Все форматы 3DS**: Конвертация, распаковка, проверка и сборка контейнеров `.3ds` / `.cci` (для Citra, Lime3DS, Azahar), `.cia` (для консолей 3DS FBI/Luma) и `.cxi`.
* **3DS Trimming**: Автоматическое удаление гигабайтов пустого паддинга `0xFF` картриджа, уменьшающее вес `.3ds` файлов в **2–4 раза** без потери совместимости.
* **Движок LayeredFS**: Нативное применение обновлений и русификаторов через встроенный стек `ctrtool`, `3dstool`, `makerom` и `aes_keys.txt`.

### 5. 📚 Каталог всех поколений: «Библиотека игр Nintendo»
* **Все 19 систем Nintendo (от старых к новым)** с полными названиями через `/`:
  1. *Nintendo Color TV-Game* (1977)
  2. *Nintendo Game & Watch* (1980)
  3. *Nintendo Entertainment System / Famicom* (1983)
  4. *Nintendo Famicom Disk System* (1986)
  5. *Nintendo Game Boy* (1989)
  6. *Super Nintendo Entertainment System / Super Famicom* (1990)
  7. *Super Famicom Satellaview (BS-X)* (1995)
  8. *Nintendo Virtual Boy* (1995)
  9. *Nintendo 64 / Nintendo 64DD* (1996)
  10. *Nintendo Game Boy Color* (1998)
  11. *Nintendo Pokémon Mini* (2001)
  12. *Nintendo Game Boy Advance / Game Boy micro* (2001)
  13. *Nintendo GameCube* (2001)
  14. *Nintendo DS / Nintendo DS Lite / Nintendo DSi* (2004)
  15. *Nintendo Wii* (2006)
  16. *Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS* (2011)
  17. *Nintendo Wii U* (2012)
  18. *Nintendo Switch / Nintendo Switch Lite / OLED* (2017)
  19. *Nintendo Switch 2* (2025)
* **Интеллектуальная фильтрация**: Мгновенный поиск, фильтры по жанрам, разработчикам, издателям, годам и регионам.
* **Просмотр и сохранение обложек**: Модальное окно просмотра оригинальных обложек в высоком разрешении с кнопкой сохранения на диск (`PNG/JPG`).

### 6. 📁 Раздельная служба «Умная папка» (Watch Folder Service)
* **Изолированный мониторинг**: Независимые службы для **Nintendo Switch** и **Nintendo 3DS**.
* **Индивидуальные задачи**: Настройка автоматических операций (Сжатие, Конвертация, Распаковка, Упаковка, Мульти-контент, Проверка) и форматов.
* **Строгий ручной запуск**: Безопасный старт и остановка мониторинга по кнопкам управления.

### 7. 🏷️ Редактор Control NCA и 3DS метаданных
* **Кастомизация тайтлов**: Редактирование русских и английских названий игр, имени издателя и системных параметров NACP.
* **Кастомные иконки**: Внедрение собственных обложек и иконок (`PNG/JPEG`) в заголовки Control NCA и 3DS.

### 8. 🔍 Интеллектуальный каталог и TitleDB («Информация»)
* **Визуальные плашки платформ**: В левом нижнем углу каждой обложки выводится стильный бейдж с платформой игры (красный `🕹️ Nintendo 3DS` или синий `🎮 Nintendo Switch`).
* **Сквозной поиск**: Поиск по локально отсканированным образам Switch и 3DS, а также интеграция с онлайн-базами TitleDB и Nintendo 3DS.
* **Автоматическое обогащение**: Загрузка описаний на русском языке, жанров, скриншотов, рейтингов возрастных ограничений и списка DLC.

### 9. 🛡️ Гарантированная гигиена временных файлов (TempCleanup Engine)
* **Принудительное удаление мимо корзины**: Сброс системных атрибутов и прямое физическое удаление временных папок `STORM_TMP_*`, `StormDecomp_*` и `Storm3DS_*`.
* **Автоматическая очистка**: Сканирование корней всех дисков, выходных папок и временных каталогов при старте приложения, отмене задач и закрытии программы.

---

## 🎨 Дизайн и интерфейс

Приложение разработано в строгом соответствии с гайдлайнами **Windows 11 Fluid Design**:
* **Материалы Mica & Acrylic**: Нативная полупрозрачность и адаптация под светлую и тёмную темы Windows.
* **Премиальные таблицы**: Сортировка, расширяемые столбцы, индикация прогресса и логов в реальном времени.
* **Многоязычность**: Полная локализация на 5 языков (Русский, English, Deutsch, 中文, 日本語).

---

## 📁 Структура проекта

```text
E:\STORM SWITCH BOX\
├── Assets\                         # Иконки, векторные ресурсы и логотипы
├── Core\                           # Низкоуровневые движки LibHac, NSZ и Zstandard
│   └── NSZ\                        # StormNczCompressor, StormNczStorage
├── Models\                         # Модели данных (ProcessingTask, CatalogItem, NintendoGameEntry)
├── Services\                       # Ядро сервисов и бизнес-логики
│   ├── HardPatchEngine.cs          # Движок жесткого вшивания патчей RomFS
│   ├── MultiContentService.cs      # Сервис сборки Мульти-контента 4-в-1
│   ├── Nintendo3dsService.cs       # Стек утилит и сборщик Nintendo 3DS
│   ├── NintendoLibraryService.cs   # База данных всех 18 систем Nintendo
│   ├── TempCleanupService.cs       # Гарантированная очистка временных папок
│   ├── WatchFolderService.cs       # Раздельные службы умного мониторинга папок
│   ├── CatalogScannerService.cs    # Сканер локальных образов и метаданных
│   ├── ControlEditorService.cs     # Редактор NACP и кастомных иконок
│   ├── TitleDbService.cs           # Интеграция с базой TitleDB и eShop
│   └── LocalizationService.cs      # Многоязычная локализация (5 языков)
├── ViewModels\                     # Реактивные модели представления (CommunityToolkit MVVM)
├── Views\                          # XAML-страницы интерфейса WinUI 3
│   ├── TasksPage.xaml              # Диспетчер задач обработки образов
│   ├── GameLibraryPage.xaml        # Библиотека игр всех поколений Nintendo
│   ├── CatalogPage.xaml            # Раздел Информация и библиотека образов
│   ├── InstructionPage.xaml        # Интерактивная иллюстрированная инструкция
│   ├── HistoryPage.xaml            # Журнал выполненных операций
│   └── SettingsPage.xaml           # Параметры Switch, 3DS и общие настройки
├── tools\                          # Встроенный инструментарий (hactoolnet, 3ds утилиты)
├── installer\                      # Скрипты инсталлятора Inno Setup
│   ├── setup.iss                   # Конфигурация компилятора Inno Setup
│   └── Output\                     # Скомпилированные инсталляторы Setup.exe
├── build_installer.bat             # Конвейер публикации, подписи и сборки Setup
├── StormSwitchBox.csproj           # Конфигурация проекта .NET 8 WinUI 3
└── README.md                       # Документация проекта
```

---

## 📋 Системные требования

* **Операционная система**: Windows 10 (версия 1809+) или Windows 11 (x64).
* **Среда выполнения**: Сборка является полностью **автономной (Self-Contained)** и включает все компоненты .NET 8 и Windows App SDK.
* **Ключи**:
  * Для **Nintendo Switch**: актуальный файл `prod.keys` (настраивается в Параметрах).
  * Для **Nintendo 3DS**: файл `aes_keys.txt` (по умолчанию встроен в директорию программы).

---

## 🛠️ Сборка и компиляция

### 1. Сборка через Visual Studio 2022 / .NET CLI:
```bash
dotnet publish "StormSwitchBox.csproj" -c Release -r win-x64
```

### 2. Автоматическая полная сборка, цифровая подпись и упаковка инсталлятора:
```cmd
.\build_installer.bat
```
После завершения работы скрипта в папке `installer\Output\` будут созданы:
1. Подписанный инсталлятор: **`STORM_SWITCH_BOX_4.7.0_Setup.exe`**
2. Портативный архив: **`STORM_SWITCH_BOX_4.7.0_win-x64.zip`**

---

## 🛡️ Цифровая подпись и Smart App Control

Все исполняемые файлы и инсталляторы подписываются цифровым сертификатом **Authenticode SHA-256** издателя **STORM TEAM**. Это гарантирует подлинность кода, целостность файлов и беспрепятственный запуск в среде Windows Defender и Smart App Control.

---

## 🤝 Благодарности

Мы выражаем искреннюю благодарность сообществу разработчиков и авторам открытых инструментов:
* **[LibHac](https://github.com/Thealexbarry/LibHac)** — непревзойденная библиотека для работы с FS Nintendo Switch.
* **[ZstdSharp.Port](https://github.com/oleg-karasik/ZstdSharp.Port)** — высокопроизводительный C# порт Zstandard от Олега Карасика.
* **[3dstool](https://github.com/dnasdw/3dstool)** & **[makerom](https://github.com/3DSGuy/Project_CTR)** — фундаментальные утилиты для экосистемы Nintendo 3DS.
* **[TitleDB](https://github.com/blawar/titledb)** — база метаданных и информации об играх.
