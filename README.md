<h1 align="center">STORM SWITCH BOX</h1>

## **О проекте**
STORM SWITCH BOX — многофункциональный комбайн, оптимизатор размера образов без потерь и менеджер библиотеки контента Nintendo Switch и Nintendo 3DS для операционной системы Windows, совмещённый с интерактивной энциклопедией всех 19 поколений игровых систем Nintendo. Программа выполняет интеллектуальную сборку файлов с минимально возможным весом без потери данных (Lossless Shrink Engine), парсинг метаданных CNMT/Control NCA/SMDH, распаковку, блочное сжатие в NSZ/XCZ (Zstandard), слияние обновлений, DLC и модификаций в монолитные образы (Multi-Content 4-в-1 и HardPatch), конвертацию между NSP, NSZ, XCI, XCZ, 3DS, CIA и CXI, сборку Homebrew-портов и форвардеров (включая LZ4-сжатые NRO для Atmosphere и 42-битное адресное пространство), проверку целостности дампов и прямую установку на консоль по USB и сети через DBI.

## **Происхождение и форки**
STORM SWITCH BOX — комплексный менеджер и сборщик контента Nintendo Switch и Nintendo 3DS на базе открытых библиотек и утилит **LibHac** и **hactoolnet** (Thealexbarney), **nsz** (NicoBosshard), **NSCB** (JulesHM), **yanu-cli** (nozwock), **oxipng** (shnatsel), **3dstool** (dnasdw), **makerom** (3DSGuy), спецификаций **Atmosphere** (SciresM) и архитектуры форвардеров **NSP-Auto-Installer-Maker** (CNX17) с нативным графическим интерфейсом WinUI 3, движком потерь-нулевой минимизации размера и параллельным диспетчером задач.

## **Технологический стек**
- **Языки программирования**: C# (.NET 8.0), Python 3.10+, C++
- **Интерфейс пользователя**: WinUI 3 (Windows App SDK), XAML, 8 фирменных тем оформления STORM SOFT, 6 языков локализации
- **Криптографические и системные модули**: LibHac, ZstdSharp (многопоточный Zstandard Block Compression Engine), hactoolnet, NSCB Core, nsz, yanu-cli, oxipng, 3dstool, makerom
- **Парсеры метаданных и патчей**: CNMT, NACP, Control NCA, PFS0, HFS0, XCI Cartridge Header, NCCH, SMDH, CIA, NRO/ASET (включая LZ4 Frame), IPS/IPS32, IPSwitch (`.pchtxt`)

## **Ключевые возможности**
- **Движок минимального размера без потерь (Lossless Shrink Engine)**: Автоматическое удаление избыточных фрагментов `DeltaFragment` NCA, дедупликация устаревших обновлений (сохранение только наивысшей версии патча и его `Meta` NCA), исключение сервисных `.xml` и дамперских манифестов из PFS0/HFS0, защита `SmartSize Guard` при сжатии малых NCA в NCZ, обрезка пустых байтов (`0xFF` / `0x00`) в картриджах XCI и 3DS/CCI и оптимизация JPEG-иконок `256x256` без потери метаданных NACP.
- **Умная сборка обновлений и мульти-контента (4-в-1)**: Автоматический выбор между нативным сшиванием PFS0 (без раздувания RomFS на лёгких патчах) и физическим перестроением монолитного `Program` NCA (`HardPatch`) для массивных обновлений и модификаций (`romfs`, `exefs`, `exefs_patches`).
- **Поддержка новейших прошивок и Atmosphere**: Полная поддержка ключей вплоть до `master_key_16` (Horizon OS 23.0.1 и Atmosphere), понижение системных требований (KeyGen и RSV Cap), распаковка сжатых `.nro` (`LZ4F` magic `0x184D2204`), 42-битное адресное пространство (`0x07` NPDM) и генерация форвардеров RetroArch.
- **Оптимизация и проверка модификаций**: Автоматическая очистка временного мусора (`.bak`, `.tmp`, `Thumbs.db`, `.DS_Store`), сжатие PNG-текстур без потерь через `oxipng` и верификация `BuildID` модуля `main` при наложении патчей 60 FPS (`.ips` и `.pchtxt`).
- **Две независимые экосистемы (Switch и 3DS)**: Полный цикл конвертации, распаковки, упаковки, проверки и мониторинга через раздельные службы «Умная папка» для Nintendo Switch и Nintendo 3DS.
- **Прямая установка на консоль и синхронизация с эмуляторами**: Передача собранных игр на консоль через DBI MTP Responder (USB) и встроенный HTTP-сервер (Wi-Fi / LAN), а также автоматическая доставка ресурсов Homebrew в виртуальные SD-карты эмуляторов.

## **Поддерживаемые платформы и эмуляторы**
- **Операционные системы**: Windows 10, Windows 11 (x64)
- **Поддерживаемые форматы**: NSP, NSZ, XCI, XCZ, NRO, NCA, CNMT, 3DS, CCI, CIA, CXI
- **Поддерживаемые консоли**: Nintendo Switch, Nintendo 3DS и база данных 19 поколений систем Nintendo
- **Поддерживаемые эмуляторы**: STORM SWITCH, Eden, Citron, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citra, Lime3DS, Azahar

## **Установка и запуск**
1. Перейдите в раздел **Releases** на GitHub.
2. Скачайте инсталлятор `STORM_SWITCH_BOX_<версия>_Setup.exe` или портативный архив `STORM_SWITCH_BOX_<версия>.zip`.
3. Запустите установщик или распакуйте архив. Все служебные инструменты (`hactoolnet`, `nsz`, `yanu-cli`, `oxipng`, `3dstool`, `makerom`) уже встроены в дистрибутив.
4. Укажите файл ключей `prod.keys` (для Nintendo Switch) и `aes_keys.txt` (для Nintendo 3DS) во вкладке «Параметры».

## **Благодарности**
- **Thealexbarney** — за библиотеку LibHac и утилиту hactoolnet.
- **NicoBosshard** — за стандарт сжатия NSZ/NCZ и компрессор nsz.
- **JulesHM** — за инструментарий NSC_Builder и алгоритмы обработки контейнеров Switch.
- **nozwock** — за высокоскоростной движок патчинга yanu-cli.
- **Josh Holmer (shnatsel)** — за оптимизатор изображений без потерь oxipng.
- **SciresM и команда Atmosphere-NX** — за кастомную прошивку Atmosphere и документацию по архитектуре Horizon OS.
- **CNX17** — за проект NSP-Auto-Installer-Maker и архитектуру генерации NRO-форвардеров.
- **dnasdw и 3DSGuy** — за утилиты 3dstool и makerom для работы с образами Nintendo 3DS.
- **duckbill** — за утилиту DBI и протоколы установки игр по MTP и HTTP.
- **blawar** — за структуру баз метаданных TitleDB и NutDB.
