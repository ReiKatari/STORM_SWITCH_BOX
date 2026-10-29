<h1 align="center">STORM SWITCH BOX</h1>

## **О проекте**
STORM SWITCH BOX — высокопроизводительный комбайн, графическая рабочая станция для сборки и пакетной обработки контента Nintendo Switch и Nintendo 3DS под операционные системы Windows и Android, дополненная интерактивной энциклопедической базой 19 поколений игровых систем Nintendo. Программа объединяет интеллектуальное сжатие данных и устранение мусорного объёма без потери данных (Lossless Shrink Engine), нативную автоматическую дешифровку образов Nintendo 3DS (SeedDB и NCCH Crypto), кастомизацию Switch-иконок и 3DS-баннеров со звуковыми джинглами (3D Banner and Animated Audio Studio), сборку мульти-игровых картриджей XCI/XCZ (Multi-Game Cartridge Builder), виртуальное инспектирование и выборочное извлечение RomFS/ExeFS без полной распаковки образов, аудит и дедупликацию игровых коллекций (Library Health and Space Saver), парсинг метаданных CNMT/Control NCA/SMDH, блочное сжатие в NSZ/XCZ (Zstandard), слияние обновлений, DLC и модификаций в монолитные пакеты (Multi-Content 4-в-1 и HardPatch), конвертацию между NSP, NSZ, XCI, XCZ, 3DS, CIA и CXI, сборку Homebrew-портов и форвардеров, встроенный загрузчик раздач и облачных модификаций (RuTracker, Яндекс.Диск, Google Drive, GitHub Releases) с автопривязкой русификаторов к задачам, а также прямую беспроводную и проводную отправку игр на консоли Switch и 3DS (FTP, HTTP Tinfoil/DBI, USB MTP и QR-коды FBI).

## **Происхождение и основа**
STORM SWITCH BOX — комплексная оболочка и комбайн контента Nintendo Switch и Nintendo 3DS на базе нативных библиотек и утилит **LibHac** и **hactoolnet** (Thealexbarney), **nsz** (NicoBosshard), **yanu-cli** (nozwock), **oxipng** (shnatsel), **3dstool** (dnasdw), **makerom** (3DSGuy), загрузчиков **Atmosphere** (SciresM) и архитектуры форвардеров **NSP-Auto-Installer-Maker** (CNX17) с нативным графическим интерфейсом WinUI 3, движком мульти-контент инжектинга модов и высокоскоростным конвейером сжатия.

## **Технологический стек**
- **Языки программирования**: C# (.NET 8.0), Kotlin (Android SDK), C++
- **Интерфейс пользователя**: WinUI 3 (Windows App SDK), XAML, Android Volumetric UI, 8 фирменных тем оформления STORM SOFT, 6 языков локализации
- **Криптографическое и сжимающее ядро**: LibHac, нативная векторная криптография `AES-128-CTR` с аппаратным ускорением SIMD `AVX2` (`Vector256<byte>`) и пулированием памяти `ArrayPool<byte>` (0 байт аллокаций в LOH), нативная 128-битная реализация крипто-движка 3DS (KeyScrambler, SeedDB, AES-CTR/CBC), ZstdSharp и Zstandard JNI (многопоточный Zstandard Block Compression Engine), hactoolnet, yanu-cli, oxipng, 3dstool, makerom
- **Форматы контейнеров и патчей**: CNMT, NACP, Control NCA, PFS0, HFS0, XCI Cartridge Header (`HEAD`), NCCH, NCSD, CIA, SMDH (Morton Z-order RGB565), CBMD/BCWAV (CWAV PCM16), IVFC Level 3 RomFS, NRO/ASET (сжатие LZ4 Frame), IPS/IPS32, BPS, IPSwitch (`.pchtxt`)

## **Ключевые возможности**
- **Встроенный мульти-трекерный загрузчик RuTracker и облачных модификаций (Яндекс.Диск, Google Drive, GitHub Releases)**: прямое подключение к базе раздач Nintendo Switch и Nintendo 3DS (встроенный индекс проверенных релизов RuTracker + живой параллельный опрос публичных индексов `ApiBay`, `Rutor`, `TheRarbg` и `TorrentsCSV`), обход DNS-блокировок через **DNS-over-HTTPS (DoH Cloudflare 1.1.1.1 / Google 8.8.8.8)**, выборочная загрузка отдельных файлов внутри раздачи на базе движка **MonoTorrent**, а также автоматический парсинг шапки темы на внешние ссылки (Яндекс.Диск, Google Drive, GitHub), прямое скачивание архивов модов/русификаторов, автораспаковка `romfs`/`exefs`/`ips` и мгновенная привязка к задаче.
- **Нативная мобильная версия для Android (ARM64 NEON Block NSZ Engine)**: полноценный пакет для смартфонов, планшетов и портативных консолей на базе Android (`Odin 2`, `Retroid Pocket`, `Razer Edge`), выполняющий блочное сжатие `.NSP` ↔ `.NSZ` (`NCZSECTN` + `NCZBLOCK` 1 МБ на базе `Zstandard` JNI), обрезку пустых областей картриджей `.XCI` и `.3DS` (`SmartShrink`), инспекцию контейнеров `PFS0`/`XCI`, мульти-трекерный поиск раздач RuTracker и прямое скачивание русификаторов с Яндекс.Диска и Google Drive напрямую в корневую директорию `/storage/emulated/0/STORM SWITCH BOX` с поддержкой `Samsung Game Booster` и режимов `120/144 Гц`.
- **Сверхбыстрое нативное ядро NSZ/XCZ и векторная криптография SIMD AVX2**: переработанный движок `StormNczCompressor` выполняет пакетную генерацию ключевого потока `AES-128-CTR` пачками по 64 КБ с наложением гаммы через 256-битные инструкции `Vector256<byte>` и полным переиспользованием буферов через `ArrayPool<byte>.Shared` (0 аллокаций в Large Object Heap), а база из 40 349 игр 19 консолей упакована в единый сжатый бинарный ресурс `catalogs.zst`.
- **Прямая отправка игр на консоль по сети и USB (Direct Console Sender)**: встроенный не требующий прав администратора HTTP/1.1 Range-сервер (`206 Partial Content`) с автогенерацией JSON-индекса для **Tinfoil** и **DBI**, автоматический сканер локальной подсети и асинхронный FTP-клиент (`PASV`) для прямой заливки файлов на Switch (`ftpd` / DBI FTP), USB MTP-инсталлятор, а также встроенный генератор матричных QR-кодов (`ISO/IEC 18004`) для мгновенной установки `.cia` на **Nintendo 3DS** через камеру в **FBI** (`Remote Install -> Scan QR Code`).
- **Полная автоматическая дешифровка 3DS (SeedDB и NCCH Crypto) без внешних утилит**: встроенный 128-битный скремблер аппаратных ключей 3DS (`KeyX` / `KeyY` / `NormalKey` по слотам `0x2C`, `0x25`, `0x18`, `0x1B`), автоматический поиск сидов новой криптографии 9.6+ в `seeddb.bin` с проверкой `SeedCheck`, расшифровка `TitleKey` из тикетов `.cia` (`AES-128-CBC`) и потоковая дешифровка разделов `ExHeader`, `ExeFS` и `RomFS` (`AES-128-CTR`) со сбросом флагов контейнера (`NoCrypto = 0x04`).
- **Студия кастомизации 3DS баннеров и Switch иконок (3D Banner and Animated Audio Studio)**: наложение визуальных бейджей (`[RUS]`, `[60 FPS]`, `[MOD]`, `[ALL DLC]` и произвольного текста) с Live-превью на иконки Switch (`256x256` JPEG) и 3DS (`48x48` и `24x24` с ускоренным декодированием через `LockBits`), нативная генерация паспорта `SMDH` (тайлы Morton Z-order `RGB565` и 16 языков UTF-16LE) и автоматическая конвертация `.WAV` (PCM) в `BCWAV` (`CWAV`) с инъекцией звукового джингла в 3DS-баннер `banner.bin` (`CBMD`).
- **Пакетный конструктор кастомных XCI/XCZ мульти-картриджей (Multi-Game Cartridge Builder)**: объединение нескольких разных игр Nintendo Switch (`.nsp`, `.nsz`, `.xci`, `.xcz`), обновлений и DLC в единый мульти-игровой картридж `.xci` или сжатый `.xcz` с дедупликацией NCA, выравниванием HFS0 разделов и формированием заголовка картриджа `HEAD`.
- **Визуальный инспектор и выборочный распаковщик RomFS / ExeFS (без полной распаковки)**: мгновенное виртуальное сканирование контейнеров `.nsp`, `.nsz`, `.xci`, `.xcz`, `.nca`, `.3ds`, `.cci`, `.cia` и `.cxi`, чтение дерева файлов `RomFS` и `ExeFS` на лету, фильтрация по маске, встроенный предпросмотр (`HEX + ASCII` и изображения) и выборочное извлечение отдельных файлов в 1 клик.
- **Аудитор и дедупликатор коллекции игр (Library Health and Space Saver)**: многопоточное пакетное сканирование директорий Switch и 3DS на предмет несшитых комплектов, устаревших дубликатов обновлений, несжатых контейнеров, необрезанных картриджей с пустыми хвостами (`0xFF` / `0x00` как для Switch `.xci` по `ValidDataEndPage`, так и для 3DS `.3ds`/`.cci`) и зашифрованных 3DS-образов с автоматическим исправлением в 1 клик.
- **Умная минимизация размера без потерь (Lossless Shrink Engine)**: автоматическое удаление избыточных фрагментов `DeltaFragment` NCA, опциональное исключение видеороликов, очистка системных `.xml` и дамперских сертификатов из PFS0/HFS0, защита `SmartSize Guard` при выборе между NCA и NCZ, обрезка пустых блоков в картриджах XCI и 3DS/CCI и оптимизация JPEG-иконок `256x256`.
- **Умная сборка модификаций и мульти-контента Switch и 3DS (4-в-1)**: автоматический выбор между склейкой контейнера PFS0 и физическим перепаковыванием разделов `Program` NCA (`HardPatch`) для Switch, а также полная сборка `Игра + Обновление + Все DLC + Русификатор/Мод (RomFS/ExeFS/IPS/BPS)` в единый файл `.3ds` или `.cia` для Nintendo 3DS.

## **Поддерживаемые платформы и эмуляторы**
- **Операционная система**: Windows 10, Windows 11 (x64), Android 8.0–17+ (ARM64 / x86_64) и Android 5.0+ (Legacy ARMv7 / ARM64)
- **Поддерживаемые форматы**: NSP, NSZ, XCI, XCZ, NRO, NCA, CNMT, 3DS, CCI, CIA, CXI
- **Поддерживаемые консоли**: Nintendo Switch, Nintendo 3DS и база знаний 19 поколений систем Nintendo
- **Поддерживаемые эмуляторы**: STORM SWITCH, Eden, Citron, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citra, Lime3DS, Azahar

## **Установка и запуск**
1. Перейдите в раздел **Releases** на GitHub.
2. Для **Windows**: скачайте инсталлятор `STORM_SWITCH_BOX_6.3.0_Setup.exe` или портативный архив `STORM_SWITCH_BOX_6.3.0_win-x64.zip`.
3. Для **Android**: скачайте пакет `STORM_SWITCH_BOX_6.3.0.apk` (для современных 64-битных устройств Android 8.0+) или `STORM_SWITCH_BOX_6.3.0_LEGACY.apk` (универсальная сборка для Android 5.0+ и 32/64-битных процессоров). При первом запуске приложение создаст рабочую структуру папок в `/storage/emulated/0/STORM SWITCH BOX/`.
4. На ПК запустите установщик или распакуйте архив. Все системные инструменты (`hactoolnet`, `yanu-cli`, `oxipng`, `3dstool`, `makerom`, `7za`) и сжатая база каталогов (`catalogs.zst`, `nutdb.json`) уже включены в дистрибутив.
5. Укажите файл ключей `prod.keys` (для Nintendo Switch) и при необходимости `seeddb.bin` / `aes_keys.txt` (для Nintendo 3DS) во вкладке параметров.

## **Благодарности**
- **Thealexbarney** — за библиотеку LibHac и утилиту hactoolnet.
- **NicoBosshard** — за стандарт сжатия NSZ/NCZ и утилиту nsz.
- **JulesHM** — за инструментарий NSC_Builder и концепцию пакетной перепаковки Switch.
- **nozwock** — за высокоскоростной движок патчинга yanu-cli.
- **Josh Holmer (shnatsel)** — за оптимизатор изображений без потерь oxipng.
- **SciresM и команда Atmosphere-NX** — за кастомную прошивку Atmosphere и документацию по архитектуре Horizon OS.
- **CNX17** — за проект NSP-Auto-Installer-Maker и методологию генерации NRO-форвардеров.
- **dnasdw и 3DSGuy** — за утилиты 3dstool и makerom для работы с образами Nintendo 3DS.
- **duckbill** — за утилиту DBI и протоколы установки игр по MTP и HTTP.
- **Steveice10** — за менеджер пакетов FBI и протокол установки CIA по QR-кодам на Nintendo 3DS.
- **blawar** — за поддержку баз метаданных TitleDB и NutDB.
- **Команда MonoTorrent (Alan McGovern)** — за высокопроизводительную кроссплатформенную библиотеку протокола BitTorrent.
- **Lubomir Marinov (luben)** — за высокопроизводительный интерфейс Zstandard JNI для Android.
