using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Описание и рекомендации по каждому формату экосистемы Nintendo 3DS / STORM 3DS / Azahar.
    /// </summary>
    public sealed record ThreeDsFormatInfo(
        string Key,
        string Extension,
        string ShortBadgeRu,
        string ShortBadgeEn,
        string TitleRu,
        string TitleEn,
        string DescriptionRu,
        string DescriptionEn,
        string RecommendationRu,
        string RecommendationEn,
        bool IsRecommendedTop = false,
        bool IsTargetSelectable = true);

    /// <summary>
    /// Полный каталог всех 17+1 нативных форматов Nintendo 3DS с описаниями и рекомендациями.
    /// </summary>
    public static class ThreeDsFormatCatalog
    {
        public static readonly IReadOnlyList<ThreeDsFormatInfo> AllFormats = new List<ThreeDsFormatInfo>
        {
            new(
                Key: "BCCI",
                Extension: ".bcci",
                ShortBadgeRu: "★ Лучший «всё в 1»",
                ShortBadgeEn: "★ Best All-in-One",
                TitleRu: "BCCI - Бандл картриджа + патч + DLC без установки (STORM 3DS / Azahar)",
                TitleEn: "BCCI - Zero-install Cartridge + Update + DLC Bundle (STORM 3DS / Azahar)",
                DescriptionRu: "Единый контейнер (TAR + Z3DS v1), внутри которого лежат сжатая игра (.zcci), официальные обновления (.zcia) и все DLC (.zcia). Моды/русификаторы автоматически вшиваются в образ. При запуске эмулятор STORM 3DS / Azahar монтирует Update и DLC на лету без распаковки в sdmc.",
                DescriptionEn: "Unified TAR + Z3DS v1 bundle packing compressed base game (.zcci), official updates (.zcia), and all DLCs (.zcia). Embedded mods/translations are baked into the image. STORM 3DS and Azahar mount updates/DLC on the fly without installing into sdmc.",
                RecommendationRu: "Рекомендуется №1 для STORM 3DS и Azahar при наличии обновлений, DLC или модов: один файл, мгновенный старт, ноль установки и ноль двойного расхода памяти.",
                RecommendationEn: "Top pick #1 for STORM 3DS and Azahar when combining base game, updates, DLC, or mods: single file, instant boot, zero installation, zero duplicate sdmc usage.",
                IsRecommendedTop: true),
            new(
                Key: "BCXI",
                Extension: ".bcxi",
                ShortBadgeRu: "★ Минимум «всё в 1»",
                ShortBadgeEn: "★ Smallest All-in-One",
                TitleRu: "BCXI - Сверхкомпактный бандл NCCH + патч + DLC без установки (STORM 3DS / Azahar)",
                TitleEn: "BCXI - Ultra-compact NCCH + Update + DLC Bundle (STORM 3DS / Azahar)",
                DescriptionRu: "Тот же бандл «всё в одном файле» без установки в sdmc, но вместо полного картриджа (.zcci) внутри хранится очищенный исполняемый раздел NCCH (.zcxi) без служебных разделов обновления прошивки, мануала и Download Play.",
                DescriptionEn: "Same zero-install all-in-one bundle format, storing the stripped NCCH main partition (.zcxi) instead of a full cartridge (.zcci), removing firmware update, electronic manual, and Download Play partitions.",
                RecommendationRu: "Абсолютный минимум размера для сборок «Игра + Update + DLC + Мод» в STORM 3DS и Azahar (обычно ещё на 5-15% меньше .bcci).",
                RecommendationEn: "Absolute smallest size for Base + Update + DLC + Mod bundles in STORM 3DS and Azahar (typically 5-15% smaller than .bcci).",
                IsRecommendedTop: true),
            new(
                Key: "ZCCI",
                Extension: ".zcci",
                ShortBadgeRu: "★ Оптимум (1 игра)",
                ShortBadgeEn: "★ Best (Single Game)",
                TitleRu: "ZCCI - Сжатый картридж NCSD (Azahar Z3DS, Seekable Zstd 256 КиБ)",
                TitleEn: "ZCCI - Compressed NCSD Cartridge (Azahar Z3DS, 256 KiB Seekable Zstd)",
                DescriptionRu: "Расшифрованный образ картриджа с автообрезкой 0xFF (SmartShrink) и произвольным доступом к сжатым кадрам Zstd без полной распаковки перед запуском.",
                DescriptionEn: "Decrypted cartridge image with 0xFF trimming and random-access Seekable Zstd frames launched directly without extraction.",
                RecommendationRu: "Лучший выбор для одиночной игры без DLC/обновлений под STORM 3DS и Azahar: сжимает в 1.5-3 раза и запускается мгновенно без установки в sdmc.",
                RecommendationEn: "Best single-game format without updates/DLC for STORM 3DS and Azahar: 1.5-3x smaller and boots immediately without sdmc installation.",
                IsRecommendedTop: true),
            new(
                Key: "ZCXI",
                Extension: ".zcxi",
                ShortBadgeRu: "Минимум (1 игра)",
                ShortBadgeEn: "Smallest (Single Game)",
                TitleRu: "ZCXI - Сжатый исполняемый раздел NCCH (Azahar Z3DS)",
                TitleEn: "ZCXI - Compressed NCCH Executable (Azahar Z3DS)",
                DescriptionRu: "Самый компактный формат одиночной игры: извлекает только основной игровой раздел (Partition 0 NCCH) с дешифровкой и сжимает его в Z3DS.",
                DescriptionEn: "Most compact single-game format: extracts Partition 0 NCCH only, strips extra cartridge partitions, decrypts, and applies Z3DS Seekable Zstd.",
                RecommendationRu: "Выбирайте для экономии каждого мегабайта на одиночных играх в STORM 3DS / Azahar.",
                RecommendationEn: "Pick when minimizing storage size for single games in STORM 3DS and Azahar."),
            new(
                Key: "BCIA",
                Extension: ".bcia",
                ShortBadgeRu: "Пакетный инсталлятор",
                ShortBadgeEn: "Batch Installer",
                TitleRu: "BCIA - Бандл инсталляторов CIA/ZCIA (авто-установка пачкой)",
                TitleEn: "BCIA - Batch CIA/ZCIA Installer Bundle",
                DescriptionRu: "TAR-бандл, содержащий только .cia и .zcia (игру, патчи и все DLC) для последовательной установки одним файлом в sdmc эмулятора.",
                DescriptionEn: "TAR bundle containing only .cia and .zcia entries (base, updates, DLCs) installed sequentially into emulator sdmc in one step.",
                RecommendationRu: "Используйте только если принципиально нужна установка в sdmc через эмулятор (тратит 2x места; иначе предпочтительнее .bcci / .bcxi).",
                RecommendationEn: "Use only when batch sdmc installation is desired (uses 2x space; .bcci / .bcxi is usually superior)."),
            new(
                Key: "ZCIA",
                Extension: ".zcia",
                ShortBadgeRu: "Для патчей и DLC",
                ShortBadgeEn: "For Updates & DLC",
                TitleRu: "ZCIA - Сжатый архив CIA (Azahar Z3DS, Seekable Zstd 32 МиБ)",
                TitleEn: "ZCIA - Compressed CIA Package (Azahar Z3DS, 32 MiB Seekable Zstd)",
                DescriptionRu: "Сжатая версия установочного пакета .cia с таблицей метаданных TitleInfo и иконкой SMDH. Эмулятор устанавливает содержимое внутрь sdmc в несжатом виде (.app).",
                DescriptionEn: "Compressed .cia installer with TitleInfo and SMDH metadata. Emulators install its payload into sdmc uncompressed (.app).",
                RecommendationRu: "Идеален для отдельных обновлений (0004000E) и дополнений (0004008C). Для самих игр лучше .zcci / .zcxi / .bcci / .bcxi.",
                RecommendationEn: "Ideal for standalone updates and DLCs. For base games, prefer .zcci, .zcxi, .bcci, or .bcxi to avoid double sdmc storage."),
            new(
                Key: "CCI",
                Extension: ".cci",
                ShortBadgeRu: "Совместимость (NCSD)",
                ShortBadgeEn: "Universal NCSD",
                TitleRu: "CCI - Оригинальный стандарт картриджа NCSD (Decrypted + Trim)",
                TitleEn: "CCI - Standard Decrypted NCSD Cartridge (Trimmed)",
                DescriptionRu: "Расшифрованный образ картриджа Nintendo 3DS с обрезанными нулями 0xFF без Zstd-сжатия. Минимум нагрузки на слабые процессоры.",
                DescriptionEn: "Decrypted trimmed 3DS cartridge image without Zstd compression, giving zero CPU decompression overhead.",
                RecommendationRu: "Максимум совместимости со всеми эмуляторами, включая Citra, Citra MMJ, Lime3DS, RetroArch и Azahar.",
                RecommendationEn: "Maximum compatibility across Citra, Citra MMJ, Lime3DS, RetroArch, and Azahar."),
            new(
                Key: "3DS",
                Extension: ".3ds",
                ShortBadgeRu: "Классический дамп",
                ShortBadgeEn: "Classic Cartridge",
                TitleRu: "3DS - Классический расшифрованный образ картриджа (.3ds / NCSD)",
                TitleEn: "3DS - Classic Decrypted Cartridge Dump (.3ds / NCSD)",
                DescriptionRu: "Технически идентичен .cci (структура NCSD), исторически наиболее распространённое расширение дампов картриджей (авто-дешифровка + Trim 0xFF).",
                DescriptionEn: "Identical binary NCSD structure to .cci with auto-decryption and 0xFF trimming.",
                RecommendationRu: "Отлично подходит для Citra, старых форков и архивной совместимости. Для экономии места лучше .zcci или .bcci.",
                RecommendationEn: "Great for Citra and legacy forks. Use .zcci or .bcci for modern compression."),
            new(
                Key: "CXI",
                Extension: ".cxi",
                ShortBadgeRu: "Чистый NCCH",
                ShortBadgeEn: "Clean NCCH",
                TitleRu: "CXI - Исполняемый одиночный раздел игры NCCH",
                TitleEn: "CXI - Executable Single Game NCCH Image",
                DescriptionRu: "Содержит только основной исполняемый раздел (Partition 0) без лишней обёртки картриджа, мануалов и обновлений прошивки.",
                DescriptionEn: "Single Partition 0 executable NCCH container stripped of manual, DLP, and update partitions.",
                RecommendationRu: "Легче .3ds/.cci при полной совместимости с Citra / Azahar. Сжатый эквивалент - .zcxi / .bcxi.",
                RecommendationEn: "Lighter than .3ds/.cci while widely supported in Citra and Azahar. Compressed form: .zcxi / .bcxi."),
            new(
                Key: "CIA",
                Extension: ".cia",
                ShortBadgeRu: "Для консоли 3DS (FBI)",
                ShortBadgeEn: "Real 3DS Hardware",
                TitleRu: "CIA - Оригинальный установочный архив (CTR Importable Archive)",
                TitleEn: "CIA - CTR Importable Archive Installer",
                DescriptionRu: "Нативный установочный пакет Nintendo 3DS с Cert, Ticket, TMD и контентом NCCH (поддержка крупных 4 ГБ игр через нарезку NCSD).",
                DescriptionEn: "Native Nintendo 3DS installer with Cert, Ticket, TMD, and NCCH contents (supports large 4 GB titles via NCSD slicing).",
                RecommendationRu: "Обязательный формат для установки игр, патчей и DLC на реальную консоль Nintendo 3DS через FBI / Luma3DS.",
                RecommendationEn: "Required format for installing games, updates, and DLC onto real Nintendo 3DS hardware via FBI."),
            new(
                Key: "3DSX",
                Extension: ".3dsx",
                ShortBadgeRu: "Homebrew",
                ShortBadgeEn: "Homebrew",
                TitleRu: "3DSX - Исполняемый формат Homebrew Launcher",
                TitleEn: "3DSX - Homebrew Launcher Executable",
                DescriptionRu: "Открытый формат любительских программ и портов для 3DS (включает код, ром-ресурсы и опционально иконку SMDH).",
                DescriptionEn: "Standard homebrew executable container for Nintendo 3DS Homebrew Launcher and emulators.",
                RecommendationRu: "Для запуска в Homebrew Launcher на реальной консоли; для сжатия под Azahar используйте Z3DSX.",
                RecommendationEn: "Use for real 3DS Homebrew Launcher; compress to Z3DSX for Azahar."),
            new(
                Key: "Z3DSX",
                Extension: ".z3dsx",
                ShortBadgeRu: "Сжатый Homebrew",
                ShortBadgeEn: "Compressed Homebrew",
                TitleRu: "Z3DSX - Сжатый Homebrew (Azahar Z3DS v1)",
                TitleEn: "Z3DSX - Compressed Homebrew (Azahar Z3DS v1)",
                DescriptionRu: "Seekable Zstd-обёртка вокруг .3dsx с сохранением иконки и заголовков для мгновенного запуска в Azahar / STORM 3DS.",
                DescriptionEn: "Seekable Zstd container around a .3dsx executable for Azahar and STORM 3DS.",
                RecommendationRu: "Рекомендуется для хранения крупных Homebrew-портов с ресурсами RomFS.",
                RecommendationEn: "Recommended for large Homebrew ports with embedded RomFS assets."),
            new(
                Key: "Z3DS",
                Extension: ".z3ds",
                ShortBadgeRu: "Универсальный Z3DS",
                ShortBadgeEn: "Generic Z3DS",
                TitleRu: "Z3DS - Универсальное расширение сжатого формата Z3DS",
                TitleEn: "Z3DS - Generic Z3DS Compressed Extension",
                DescriptionRu: "Бинарно идентичен .zcci/.zcxi/.zcia/.z3dsx, но сохраняет общее расширение .z3ds (поддерживается STORM 3DS 1.5.0 и Azahar по магическим байтам).",
                DescriptionEn: "Identical Z3DS binary container saved with the generic .z3ds extension (detected by magic bytes in STORM 3DS 1.5.0+ and Azahar).",
                RecommendationRu: "Предпочитайте точные расширения .zcci / .zcxi / .zcia (они быстрее распознаются всеми внешними лаунчерами).",
                RecommendationEn: "Prefer specific extensions (.zcci, .zcxi, .zcia) for maximum launcher compatibility; .z3ds works via magic probing."),
            new(
                Key: "Z3DS (Авто)",
                Extension: ".z*",
                ShortBadgeRu: "Автовыбор Z3DS",
                ShortBadgeEn: "Auto Z3DS",
                TitleRu: "Z3DS (Авто) - Автоматический выбор расширения (.zcci/.zcia/.zcxi/.z3dsx)",
                TitleEn: "Z3DS (Auto) - Automatic Z3DS Target Extension",
                DescriptionRu: "Автоматически определяет тип исходника (NCSD -> .zcci, CIA -> .zcia, NCCH -> .zcxi, 3DSX -> .z3dsx) и сжимает без лишней перепаковки.",
                DescriptionEn: "Automatically selects .zcci, .zcia, .zcxi, or .z3dsx based on input container headers.",
                RecommendationRu: "Удобно для пакетной обработки разнородной папки без переключения цели.",
                RecommendationEn: "Great for batch compressing mixed 3DS files."),
            new(
                Key: "3DS + CIA",
                Extension: ".3ds + .cia",
                ShortBadgeRu: "Двойной вывод",
                ShortBadgeEn: "Dual Output",
                TitleRu: "3DS + CIA - Одновременное сохранение расшифрованного .3ds и .cia",
                TitleEn: "3DS + CIA - Dual .3ds and .cia Output",
                DescriptionRu: "Создаёт одновременно два файла: .3ds для эмуляторов и .cia для установки на реальную консоль.",
                DescriptionEn: "Produces both a trimmed .3ds image for emulators and a .cia installer for real hardware.",
                RecommendationRu: "Удобно, если вы играете одновременно на ПК/телефоне и на реальной Nintendo 3DS.",
                RecommendationEn: "Convenient when keeping builds for both emulators and real 3DS hardware."),
            new(
                Key: "3DS + ZCCI",
                Extension: ".3ds + .zcci",
                ShortBadgeRu: "Двойной вывод",
                ShortBadgeEn: "Dual Output",
                TitleRu: "3DS + ZCCI - Одновременное сохранение классического .3ds и сжатого .zcci",
                TitleEn: "3DS + ZCCI - Dual .3ds and .zcci Output",
                DescriptionRu: "Создаёт классический образ .3ds и его компактную Seekable Zstd версию .zcci.",
                DescriptionEn: "Produces both classic .3ds and Seekable Zstd .zcci files in one run.",
                RecommendationRu: "Подходит для одновременного архива под старую Citra и современный STORM 3DS / Azahar.",
                RecommendationEn: "Useful when maintaining both legacy Citra and modern STORM 3DS / Azahar copies."),
            new(
                Key: "APP",
                Extension: ".app",
                ShortBadgeRu: "Внутренний NCCH (вход)",
                ShortBadgeEn: "SDMC Content (Input)",
                TitleRu: "APP - Извлечённый файл контента NCCH (установленный title в sdmc)",
                TitleEn: "APP - Installed SDMC NCCH Content File",
                DescriptionRu: "Стандартный контейнер NCCH, хранящийся внутри установленной структуры sdmc/Nintendo 3DS/.../content/*.app.",
                DescriptionEn: "Raw NCCH content file extracted from CIA or emulator sdmc/Nintendo 3DS directories.",
                RecommendationRu: "Поддерживается для анализа, проверки, распаковки и прямой конвертации в .zcxi / .bcxi / .cci / .cia.",
                RecommendationEn: "Supported as input for inspection, extraction, and conversion to .zcxi, .bcxi, .cci, or .cia.",
                IsTargetSelectable: false),
            new(
                Key: "CFA",
                Extension: ".cfa",
                ShortBadgeRu: "Архив контента NCCH (вход)",
                ShortBadgeEn: "NCCH Archive (Input)",
                TitleRu: "CFA - CTR File Archive (мануал, DLC-контент или child NCCH)",
                TitleEn: "CFA - CTR File Archive (Manual / DLC / Child NCCH)",
                DescriptionRu: "Раздел NCCH без исполняемого кода ExeFS (используется для электронных руководств, DLP и контента дополнений).",
                DescriptionEn: "Data-only NCCH partition without executable code, used for manuals, DLP, and add-on assets.",
                RecommendationRu: "Поддерживается как входной формат для инспекции, распаковки и сжатия Z3DS.",
                RecommendationEn: "Supported as input for inspection, extraction, and Z3DS compression.",
                IsTargetSelectable: false),
            new(
                Key: "ELF",
                Extension: ".elf",
                ShortBadgeRu: "Отладка / Homebrew (вход)",
                ShortBadgeEn: "ARM11 ELF (Input)",
                TitleRu: "ELF - Исполняемый двоичный образ ARM11 (Citra / Azahar / STORM 3DS)",
                TitleEn: "ELF - ARM11 Executable ELF Binary",
                DescriptionRu: "Сырой отладочный или собранный devkitARM бинарник ARM11 с заголовком 0x7F ELF для прямого запуска в эмуляторах.",
                DescriptionEn: "Unpacked ARM11 ELF executable binary (0x7F ELF) natively runnable by 3DS emulators.",
                RecommendationRu: "Поддерживается для распознавания, верификации заголовка и просмотра метаданных.",
                RecommendationEn: "Supported for detection, header verification, and task cataloging.",
                IsTargetSelectable: false),
            new(
                Key: "AXF",
                Extension: ".axf",
                ShortBadgeRu: "ARM Debugger (вход)",
                ShortBadgeEn: "ARM AXF (Input)",
                TitleRu: "AXF - ARM eXecutable File (формат ARM RealView ELF)",
                TitleEn: "AXF - ARM eXecutable Format (ELF Debug Binary)",
                DescriptionRu: "Отладочный ELF-образ ARM для инструментов Nintendo CTR SDK, поддерживаемый эмуляторами Azahar и STORM 3DS.",
                DescriptionEn: "ARM ELF debug executable supported natively by Azahar and STORM 3DS.",
                RecommendationRu: "Поддерживается для распознавания и проверки целостности.",
                RecommendationEn: "Supported for detection and header verification.",
                IsTargetSelectable: false),
            new(
                Key: "ARTIC",
                Extension: ".artic",
                ShortBadgeRu: "Стриминг с 3DS (вход)",
                ShortBadgeEn: "Artic Base Stream (Input)",
                TitleRu: "ARTIC - Указатель сетевого стриминга Artic Base с реальной 3DS",
                TitleEn: "ARTIC - Artic Base Live Network Stream Pointer",
                DescriptionRu: "Текстовый или URI-файл вида articbase:// / articinio:// / articinin:// для запуска игры по сети напрямую с вашей реальной Nintendo 3DS.",
                DescriptionEn: "Pointer file containing articbase://, articinio://, or articinin:// stream URI from a real Nintendo 3DS running Artic Base.",
                RecommendationRu: "Поддерживается для распознавания, каталогизации и проверки URI-заголовка.",
                RecommendationEn: "Supported for recognition, cataloging, and URI validation.",
                IsTargetSelectable: false),
        };

        public static ThreeDsFormatInfo? Find(string? keyOrExt)
        {
            if (string.IsNullOrWhiteSpace(keyOrExt)) return null;
            string norm = keyOrExt.Trim().TrimStart('.').ToUpperInvariant();
            return AllFormats.FirstOrDefault(f =>
                string.Equals(f.Key, keyOrExt.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Key, norm, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Extension.TrimStart('.'), norm, StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildTooltipText(string? key, bool isRussian = true)
        {
            var info = Find(key);
            if (info == null) return string.Empty;
            if (isRussian)
            {
                return $"{info.TitleRu}\n\n{info.DescriptionRu}\n\n💡 Рекомендация: {info.RecommendationRu}";
            }
            return $"{info.TitleEn}\n\n{info.DescriptionEn}\n\n💡 Recommendation: {info.RecommendationEn}";
        }

        public static string BuildFullGuideText(bool isRussian = true)
        {
            var sb = new StringBuilder();
            if (isRussian)
            {
                sb.AppendLine("Рекомендация STORM 3DS / Azahar (кратко):");
                sb.AppendLine("1. BCCI или BCXI - лучший выбор для сборки «Игра + Update + DLC + Мод» в ОДИН файл без установки в sdmc.");
                sb.AppendLine("2. ZCCI или ZCXI (Decrypted) - лучший выбор для одиночной игры без DLC/обновлений (сжатие 1.5-3x, старт без распаковки).");
                sb.AppendLine("3. ZCIA / CIA - для отдельных обновлений/DLC (или установки на реальную консоль 3DS через FBI).");
                sb.AppendLine("4. CCI / 3DS / CXI - максимум совместимости со старыми эмуляторами (Citra / MMJ / RetroArch).");
                sb.AppendLine();
                foreach (var fmt in AllFormats)
                {
                    sb.AppendLine($"• {fmt.Key} ({fmt.Extension}) [{fmt.ShortBadgeRu}]: {fmt.DescriptionRu} -> {fmt.RecommendationRu}");
                }
            }
            else
            {
                sb.AppendLine("STORM 3DS / Azahar Quick Guide:");
                sb.AppendLine("1. BCCI or BCXI - #1 pick for Base + Update + DLC + Mod in ONE file with instant zero-install launch.");
                sb.AppendLine("2. ZCCI or ZCXI (Decrypted) - best for single games without updates/DLC (1.5-3x smaller, instant launch).");
                sb.AppendLine("3. ZCIA / CIA - for standalone updates/DLC (or real 3DS hardware install via FBI).");
                sb.AppendLine("4. CCI / 3DS / CXI - widest compatibility with legacy Citra / MMJ / RetroArch.");
                sb.AppendLine();
                foreach (var fmt in AllFormats)
                {
                    sb.AppendLine($"• {fmt.Key} ({fmt.Extension}) [{fmt.ShortBadgeEn}]: {fmt.DescriptionEn} -> {fmt.RecommendationEn}");
                }
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Поток только для чтения поверх ограниченного диапазона байтов в базовом потоке.
    /// </summary>
    public sealed class SubRangeStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _baseOffset;
        private readonly long _length;
        private readonly bool _leaveOpen;
        private long _position;
        private bool _disposed;

        public SubRangeStream(Stream baseStream, long offset, long length, bool leaveOpen = false)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            if (offset < 0 || length < 0 || offset + length > baseStream.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Границы подпотока выходят за пределы исходного файла.");
            _baseOffset = offset;
            _length = length;
            _leaveOpen = leaveOpen;
            _baseStream.Position = _baseOffset;
        }

        public override bool CanRead => !_disposed && _baseStream.CanRead;
        public override bool CanSeek => !_disposed && _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SubRangeStream));
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _position = Math.Min(value, _length);
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SubRangeStream));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (count <= 0 || _position >= _length) return 0;

            int toRead = (int)Math.Min(count, _length - _position);
            _baseStream.Position = _baseOffset + _position;
            int read = _baseStream.Read(buffer, offset, toRead);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (target < 0) throw new IOException("Попытка смещения до начала подпотока.");
            _position = Math.Min(target, _length);
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing && !_leaveOpen)
                {
                    _baseStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    public sealed record TarBundleEntry(string Name, long DataOffset, long Size);

    public sealed class TarBundleInfo
    {
        public List<TarBundleEntry> Entries { get; init; } = new();
        public Z3dsMetadata? Metadata { get; init; }
        public string? MainEntryName { get; init; }
        public int CiaDependencyCount { get; init; }

        public TarBundleEntry? MainEntry =>
            string.IsNullOrEmpty(MainEntryName)
                ? null
                : Entries.FirstOrDefault(e => string.Equals(e.Name, MainEntryName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Чтение, создание, распаковка и проверка бандлов Nintendo 3DS (.bcci / .bcxi / .bcia)
    /// в точном бинарном стандарте Azahar / STORM 3DS (microtar 512B + Z3DSMetadata "metadata").
    /// </summary>
    public static class ThreeDsBundleService
    {
        private const int TarBlockSize = 512;
        private const int MaxTarFiles = 50;
        private const ulong MaxOctalSize = 8589934591UL; // 8^11 - 1

        private static readonly HashSet<string> BundleExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bcci", ".bcxi", ".bcia"
        };

        public static bool IsBundleExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return false;
            string norm = ext.StartsWith('.') ? ext : "." + ext;
            return BundleExtensions.Contains(norm);
        }

        public static bool IsBundleFile(string? filePath)
        {
            return TryReadBundleInfo(filePath, out _);
        }

        public static bool TryReadBundleInfo(string? filePath, out TarBundleInfo info)
        {
            info = new TarBundleInfo();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return TryReadBundleInfo(fs, out info);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadBundleInfo(Stream fs, out TarBundleInfo info)
        {
            info = new TarBundleInfo();
            if (fs == null || !fs.CanSeek || fs.Length < TarBlockSize * 2) return false;

            try
            {
                fs.Position = 0;
                long archiveSize = fs.Length;
                byte[] headerBuf = new byte[TarBlockSize];
                var entries = new List<TarBundleEntry>();
                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                long offset = 0;

                while (offset + TarBlockSize <= archiveSize && entries.Count < MaxTarFiles)
                {
                    fs.Position = offset;
                    fs.ReadExactly(headerBuf, 0, TarBlockSize);

                    // microtar treats checksum[0] == '\0' as MTAR_ENULLRECORD
                    if (headerBuf[148] == 0)
                    {
                        break;
                    }

                    if (!TryParseTarHeader(headerBuf, out string name, out ulong entrySize, out byte fileType))
                    {
                        return false;
                    }

                    long dataOffset = offset + TarBlockSize;
                    if (entrySize > (ulong)(archiveSize - dataOffset))
                    {
                        return false;
                    }

                    if (!name.StartsWith('.') && (fileType == (byte)'0' || fileType == 0))
                    {
                        if (!seenNames.Add(name))
                        {
                            return false;
                        }
                        entries.Add(new TarBundleEntry(name, dataOffset, (long)entrySize));
                    }

                    long paddedData = ((long)entrySize + (TarBlockSize - 1)) & ~(long)(TarBlockSize - 1);
                    offset = dataOffset + paddedData;
                }

                if (entries.Count == 0) return false;

                // Validate that this TAR actually represents a 3DS bundle (has "metadata" or 3DS content entries)
                Z3dsMetadata? meta = null;
                var metaEntry = entries.FirstOrDefault(e => string.Equals(e.Name, "metadata", StringComparison.Ordinal));
                if (metaEntry != null && metaEntry.Size > 0 && metaEntry.Size <= 4 * 1024 * 1024)
                {
                    byte[] metaBytes = new byte[metaEntry.Size];
                    fs.Position = metaEntry.DataOffset;
                    fs.ReadExactly(metaBytes, 0, metaBytes.Length);
                    meta = Z3dsMetadata.Parse(metaBytes);
                }

                var payloadEntries = entries.Where(e => !string.Equals(e.Name, "metadata", StringComparison.Ordinal)).ToList();
                bool has3dsPayloads = payloadEntries.Any(e => IsRecognizedInner3dsName(e.Name));
                if (meta == null && !has3dsPayloads)
                {
                    return false;
                }

                string? mainEntryName = null;
                string? metaMainFile = meta?.GetString("mainfile");
                if (!string.IsNullOrWhiteSpace(metaMainFile) &&
                    payloadEntries.Any(e => string.Equals(e.Name, metaMainFile, StringComparison.Ordinal)))
                {
                    mainEntryName = metaMainFile;
                }

                if (string.IsNullOrEmpty(mainEntryName))
                {
                    mainEntryName = payloadEntries
                        .FirstOrDefault(e =>
                            e.Name.EndsWith(".zcci", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".cci", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".zcxi", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))?.Name;
                }

                if (string.IsNullOrEmpty(mainEntryName))
                {
                    // Matches STORM 3DS game_info.cpp:58-72 preference for base 00040000 in .bcia
                    mainEntryName = payloadEntries
                        .FirstOrDefault(e =>
                            (e.Name.EndsWith(".zcia", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".cia", StringComparison.OrdinalIgnoreCase)) &&
                            e.Name.Contains("00040000", StringComparison.OrdinalIgnoreCase))?.Name
                        ?? payloadEntries.FirstOrDefault(e =>
                            e.Name.EndsWith(".zcia", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.EndsWith(".cia", StringComparison.OrdinalIgnoreCase))?.Name
                        ?? payloadEntries.FirstOrDefault()?.Name;
                }

                int ciaDepCount = payloadEntries.Count(e =>
                    !string.Equals(e.Name, mainEntryName, StringComparison.Ordinal) &&
                    (e.Name.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) ||
                     e.Name.EndsWith(".zcia", StringComparison.OrdinalIgnoreCase)));

                info = new TarBundleInfo
                {
                    Entries = entries,
                    Metadata = meta,
                    MainEntryName = mainEntryName,
                    CiaDependencyCount = ciaDepCount
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRecognizedInner3dsName(string name)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".zcci" || ext == ".cci" || ext == ".3ds" ||
                   ext == ".zcxi" || ext == ".cxi" || ext == ".app" ||
                   ext == ".zcia" || ext == ".cia" ||
                   ext == ".z3dsx" || ext == ".3dsx" || ext == ".z3ds";
        }

        public static bool TryOpenMainEntryTransparentStream(
            string filePath,
            out Stream? stream,
            out TarBundleInfo? bundleInfo)
        {
            stream = null;
            bundleInfo = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

            FileStream? fs = null;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (!TryReadBundleInfo(fs, out var parsed) || parsed.MainEntry == null)
                {
                    fs.Dispose();
                    return false;
                }

                var sub = new SubRangeStream(fs, parsed.MainEntry.DataOffset, parsed.MainEntry.Size, leaveOpen: false);
                fs = null; // Ownership transferred to sub

                if (sub.Length >= Z3dsHeader.DefaultHeaderSize)
                {
                    Span<byte> hdr = stackalloc byte[Z3dsHeader.DefaultHeaderSize];
                    sub.Position = 0;
                    sub.ReadExactly(hdr);
                    sub.Position = 0;
                    if (Z3dsHeader.Parse(hdr).IsValid)
                    {
                        stream = new Z3dsReadStream(sub, leaveOpen: false);
                        bundleInfo = parsed;
                        return true;
                    }
                }

                stream = sub;
                bundleInfo = parsed;
                return true;
            }
            catch
            {
                fs?.Dispose();
                stream?.Dispose();
                stream = null;
                bundleInfo = null;
                return false;
            }
        }

        public static async Task CreateBundleAsync(
            string outputPath,
            string mainFilePath,
            IReadOnlyList<string> dependencyPaths,
            ProcessingTask? task = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(mainFilePath) || !File.Exists(mainFilePath))
                throw new FileNotFoundException("Основной файл для бандла 3DS не найден.", mainFilePath);

            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string tempOut = outputPath + $".tmp_{Guid.NewGuid():N}";
            var inFiles = new List<(string FilePath, string EntryName)>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var parser = new Nintendo3dsService();

            (string FilePath, string EntryName) PrepareEntrySpec(string path)
            {
                var parsed = parser.Parse3dsFile(path);
                Z3dsHeader? zHdr = null;
                Z3dsMetadata? zMeta = null;
                bool isZ3ds = Z3dsService.TryReadHeaderAndMetadata(path, out var zh, out var zm, out _);
                if (isZ3ds)
                {
                    zHdr = zh;
                    zMeta = zm;
                }

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (isZ3ds && zHdr != null)
                {
                    ext = zHdr.RecommendedCompressedExtension;
                }
                else if (ext == ".3ds")
                {
                    ext = ".cci";
                }
                else if (ext == ".app")
                {
                    ext = ".cxi";
                }

                ulong tidVal = 0;
                if (zMeta != null)
                {
                    byte[]? tinfo = zMeta.GetBytes("titleinfo");
                    if (tinfo != null && tinfo.Length >= 8)
                    {
                        tidVal = BitConverter.ToUInt64(tinfo, 0);
                    }
                }
                if (tidVal == 0 && !string.IsNullOrWhiteSpace(parsed.TitleId) && parsed.TitleId.Length == 16)
                {
                    ulong.TryParse(parsed.TitleId, System.Globalization.NumberStyles.HexNumber, null, out tidVal);
                }

                string baseName = $"{tidVal:X16}";
                string shortTitle = TryExtractEnglishShortTitle(path, parsed.GameName);
                if (!string.IsNullOrWhiteSpace(shortTitle))
                {
                    var sanitizedSb = new StringBuilder(shortTitle.Length);
                    foreach (char c in shortTitle)
                    {
                        if (c < 0x20 || c == 0x7F || "/\\:*?\"<>|".IndexOf(c) >= 0)
                            sanitizedSb.Append('_');
                        else
                            sanitizedSb.Append(c);
                    }
                    string safeTitle = sanitizedSb.ToString().Trim();

                    // Maximum TAR name field is 100 bytes including trailing null (99 UTF-8 bytes)
                    const int maxTarNameBytes = 99;
                    const int uniqueSuffixReserve = 5; // " (99)"
                    int fixedBytes = Encoding.UTF8.GetByteCount(baseName) + 3 + Encoding.UTF8.GetByteCount(ext) + uniqueSuffixReserve;
                    int maxTitleBytes = Math.Max(0, maxTarNameBytes - fixedBytes);
                    if (maxTitleBytes > 0 && safeTitle.Length > 0)
                    {
                        string clippedTitle = TruncateUtf8Safe(safeTitle, maxTitleBytes);
                        if (!string.IsNullOrEmpty(clippedTitle))
                        {
                            baseName += " - " + clippedTitle;
                        }
                    }
                }

                string uniqueName = baseName + ext;
                for (int i = 1; !usedNames.Add(uniqueName); i++)
                {
                    uniqueName = $"{baseName} ({i}){ext}";
                }

                return (path, uniqueName);
            }

            try
            {
                var mainSpec = PrepareEntrySpec(mainFilePath);
                inFiles.Add(mainSpec);

                if (dependencyPaths != null)
                {
                    foreach (var dep in dependencyPaths)
                    {
                        if (string.IsNullOrWhiteSpace(dep) || !File.Exists(dep)) continue;
                        inFiles.Add(PrepareEntrySpec(dep));
                    }
                }

                // Match STORM 3DS loader.cpp:242-248 metadata ("titleinfo"/"maxframesize" without "smdh", plus "mainfile", "bundler", "date")
                var bundleMeta = new Z3dsMetadata();
                if (Z3dsService.TryReadHeaderAndMetadata(mainFilePath, out _, out var mainZMeta, out _))
                {
                    foreach (var kv in mainZMeta.Items)
                    {
                        if (string.Equals(kv.Key, "smdh", StringComparison.Ordinal)) continue;
                        bundleMeta.SetBytes(kv.Key, kv.Value);
                    }
                }
                else
                {
                    var mainParsed = parser.Parse3dsFile(mainFilePath);
                    ulong mainTid = 0;
                    ulong.TryParse(mainParsed.TitleId, System.Globalization.NumberStyles.HexNumber, null, out mainTid);
                    byte[] titleInfo = new byte[0x18];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(titleInfo.AsSpan(0, 8), mainTid);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(titleInfo.AsSpan(8, 8), (ulong)new FileInfo(mainFilePath).Length);
                    bundleMeta.SetBytes("titleinfo", titleInfo);
                }

                string appVer = !string.IsNullOrWhiteSpace(App.AppVersionString) ? App.AppVersionString : "8.2.3";
                bundleMeta.SetString("mainfile", mainSpec.EntryName);
                bundleMeta.SetString("bundler", $"STORM SWITCH BOX {appVer}");
                bundleMeta.SetString("date", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                byte[] metaBytes = bundleMeta.SerializePadded16();
                long totalBytesToWrite = metaBytes.Length + inFiles.Sum(f => new FileInfo(f.FilePath).Length);
                long writtenSoFar = 0;
                long lastReportMs = 0;

                if (task != null)
                {
                    App.RunOnUI(() =>
                    {
                        task.IsRunning = true;
                        task.Progress = 75;
                        task.Status = $"Сборка бандла {Path.GetExtension(outputPath).TrimStart('.').ToUpperInvariant()}...";
                        task.LogDetails += $"\n📦 [3DS Bundle] Упаковка {Path.GetFileName(outputPath)} (TAR + Z3DS v1, файлов: {inFiles.Count})...";
                    });
                }

                byte[] copyBuf = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);
                try
                {
                    await using var outFs = new FileStream(tempOut, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan);

                    // 1. Write "metadata" entry first (required by STORM 3DS / Azahar)
                    byte[] metaTarHdr = BuildMicrotarHeader("metadata", (ulong)metaBytes.Length);
                    await outFs.WriteAsync(metaTarHdr, 0, TarBlockSize, ct);
                    await outFs.WriteAsync(metaBytes, 0, metaBytes.Length, ct);
                    await WriteTarZeroPaddingAsync(outFs, metaBytes.Length, ct);
                    writtenSoFar += metaBytes.Length;

                    // 2. Stream main file and dependencies
                    foreach (var (filePath, entryName) in inFiles)
                    {
                        long fileLen = new FileInfo(filePath).Length;
                        byte[] entryHdr = BuildMicrotarHeader(entryName, (ulong)fileLen);
                        await outFs.WriteAsync(entryHdr, 0, TarBlockSize, ct);

                        await using var inFs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4 * 1024 * 1024, FileOptions.SequentialScan);
                        long remaining = fileLen;
                        while (remaining > 0)
                        {
                            task?.PauseEvent.Wait(ct);
                            ct.ThrowIfCancellationRequested();

                            int toRead = (int)Math.Min(copyBuf.Length, remaining);
                            int got = await inFs.ReadAsync(copyBuf, 0, toRead, ct);
                            if (got <= 0) break;
                            await outFs.WriteAsync(copyBuf, 0, got, ct);
                            remaining -= got;
                            writtenSoFar += got;

                            long nowMs = Environment.TickCount64;
                            if (task != null && nowMs - lastReportMs >= 150)
                            {
                                lastReportMs = nowMs;
                                double pct = 75.0 + 23.0 * writtenSoFar / Math.Max(1L, totalBytesToWrite);
                                App.RunOnUI(() =>
                                {
                                    task.Progress = Math.Min(98.0, pct);
                                    task.Status = $"Упаковка бандла 3DS: {pct:F0}%";
                                });
                            }
                        }

                        await WriteTarZeroPaddingAsync(outFs, fileLen, ct);
                    }

                    // 3. Two 512-byte zero blocks (MTAR trailer = 1024 bytes)
                    byte[] eofTrailer = new byte[TarBlockSize * 2];
                    await outFs.WriteAsync(eofTrailer, 0, eofTrailer.Length, ct);
                    await outFs.FlushAsync(ct);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(copyBuf);
                }

                File.Move(tempOut, outputPath, overwrite: true);

                long finalSize = new FileInfo(outputPath).Length;
                if (task != null)
                {
                    App.RunOnUI(() =>
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.TargetSize = ProcessingTask.FormatSize(finalSize);
                        task.LogDetails += $"\n✅ [3DS Bundle] Создан бандл: {Path.GetFileName(outputPath)} ({ProcessingTask.FormatSize(finalSize)}, {inFiles.Count} конт.: {mainSpec.EntryName})";
                    });
                }
            }
            catch
            {
                try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
                throw;
            }
        }

        public static async Task<List<string>> ExtractBundleAsync(
            string bundlePath,
            string outputDir,
            ProcessingTask? task = null,
            CancellationToken ct = default)
        {
            if (!TryReadBundleInfo(bundlePath, out var info))
                throw new InvalidDataException($"Файл не является корректным TAR-бандлом 3DS: {Path.GetFileName(bundlePath)}");

            Directory.CreateDirectory(outputDir);
            var extractedPaths = new List<string>();
            byte[] copyBuf = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);

            try
            {
                await using var inFs = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4 * 1024 * 1024, FileOptions.SequentialScan);
                var payloadEntries = info.Entries.Where(e => !string.Equals(e.Name, "metadata", StringComparison.Ordinal)).ToList();

                for (int i = 0; i < payloadEntries.Count; i++)
                {
                    var entry = payloadEntries[i];
                    string safeFileName = SanitizeEntryFileName(entry.Name);
                    string outPath = Path.Combine(outputDir, safeFileName);

                    inFs.Position = entry.DataOffset;
                    long remaining = entry.Size;
                    await using (var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan))
                    {
                        while (remaining > 0)
                        {
                            task?.PauseEvent.Wait(ct);
                            ct.ThrowIfCancellationRequested();
                            int toRead = (int)Math.Min(copyBuf.Length, remaining);
                            int got = await inFs.ReadAsync(copyBuf, 0, toRead, ct);
                            if (got <= 0) break;
                            await outFs.WriteAsync(copyBuf, 0, got, ct);
                            remaining -= got;
                        }
                    }
                    extractedPaths.Add(outPath);

                    if (task != null)
                    {
                        double pct = 10.0 + 85.0 * (i + 1) / Math.Max(1, payloadEntries.Count);
                        App.RunOnUI(() =>
                        {
                            task.Progress = pct;
                            task.Status = $"Извлечение бандла ({i + 1}/{payloadEntries.Count})...";
                        });
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuf);
            }

            return extractedPaths;
        }

        public static (bool IsValid, string Details, TarBundleInfo? BundleInfo) VerifyBundleIntegrity(string bundlePath)
        {
            try
            {
                if (!TryReadBundleInfo(bundlePath, out var info))
                {
                    return (false, "Повреждённый TAR-заголовок или неверная контрольная сумма бандла 3DS.", null);
                }

                var payloadEntries = info.Entries.Where(e => !string.Equals(e.Name, "metadata", StringComparison.Ordinal)).ToList();
                if (payloadEntries.Count == 0 || info.MainEntry == null)
                {
                    return (false, "Бандл не содержит основного игрового файла.", info);
                }

                if (bundlePath.EndsWith(".bcia", StringComparison.OrdinalIgnoreCase))
                {
                    bool invalidBciaEntry = payloadEntries.Any(e =>
                        !e.Name.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) &&
                        !e.Name.EndsWith(".zcia", StringComparison.OrdinalIgnoreCase));
                    if (invalidBciaEntry)
                    {
                        return (false, "Бандл .bcia содержит неподдерживаемые файлы (допустимы только .cia и .zcia).", info);
                    }
                }

                using var fs = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                int z3dsChecked = 0;
                foreach (var entry in payloadEntries)
                {
                    if (entry.Size >= Z3dsHeader.DefaultHeaderSize)
                    {
                        byte[] sig = new byte[4];
                        fs.Position = entry.DataOffset;
                        fs.ReadExactly(sig, 0, 4);
                        if (sig[0] == (byte)'Z' && sig[1] == (byte)'3' && sig[2] == (byte)'D' && sig[3] == (byte)'S')
                        {
                            using var sub = new SubRangeStream(fs, entry.DataOffset, entry.Size, leaveOpen: true);
                            var (zOk, zMsg, _, _, _) = Z3dsService.VerifyZ3dsIntegrity(sub);
                            if (!zOk)
                            {
                                return (false, $"Ошибка вложенного файла «{entry.Name}»: {zMsg}", info);
                            }
                            z3dsChecked++;
                        }
                    }
                }

                string bundler = info.Metadata?.GetString("bundler") ?? "-";
                string fmt = Path.GetExtension(bundlePath).TrimStart('.').ToUpperInvariant();
                string details = $"Бандл {fmt} (microtar): {payloadEntries.Count} файлов, основной: {info.MainEntry.Name}, Z3DS-контейнеров проверено: {z3dsChecked}, сборщик: {bundler}";
                return (true, details, info);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка проверки бандла 3DS: {ex.Message}", null);
            }
        }

        private static string TryExtractEnglishShortTitle(string filePath, string? fallbackTitle)
        {
            try
            {
                using Stream fs = Z3dsService.OpenTransparentReadStream(filePath);
                if (fs is Z3dsReadStream zstream)
                {
                    byte[]? smdhMeta = zstream.Metadata.GetBytes("smdh");
                    if (smdhMeta != null && smdhMeta.Length >= 0x88 &&
                        smdhMeta[0] == (byte)'S' && smdhMeta[1] == (byte)'M' && smdhMeta[2] == (byte)'D' && smdhMeta[3] == (byte)'H')
                    {
                        string eng = Encoding.Unicode.GetString(smdhMeta, 0x08 + 1 * 0x200, 0x80).Trim('\0', ' ', '\r', '\n');
                        if (!string.IsNullOrWhiteSpace(eng)) return eng;
                    }
                }
            }
            catch { }

            return !string.IsNullOrWhiteSpace(fallbackTitle) ? fallbackTitle : Path.GetFileNameWithoutExtension(filePath);
        }

        private static string TruncateUtf8Safe(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text) || maxBytes <= 0) return string.Empty;
            if (Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;
            var sb = new StringBuilder(text.Length);
            int used = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                int b = rune.Utf8SequenceLength;
                if (used + b > maxBytes) break;
                sb.Append(rune.ToString());
                used += b;
            }
            return sb.ToString();
        }

        private static string SanitizeEntryFileName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c < 0x20 || c == 0x7F || "/\\:*?\"<>|".IndexOf(c) >= 0)
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            string res = sb.ToString().Trim();
            return string.IsNullOrEmpty(res) ? "entry.bin" : res;
        }

        private static async Task WriteTarZeroPaddingAsync(Stream outFs, long dataSize, CancellationToken ct)
        {
            int rem = (int)(dataSize % TarBlockSize);
            if (rem != 0)
            {
                int padLen = TarBlockSize - rem;
                byte[] pad = new byte[padLen];
                await outFs.WriteAsync(pad, 0, padLen, ct);
            }
        }

        private static byte[] BuildMicrotarHeader(string entryName, ulong size)
        {
            byte[] hdr = new byte[TarBlockSize];

            // name[100] (<= 99 UTF-8 bytes + null)
            byte[] nameBytes = Encoding.UTF8.GetBytes(entryName);
            int nameLen = Math.Min(99, nameBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, hdr, 0, nameLen);

            // mode[8] = "0000644\0"
            WriteAscii(hdr, 100, "0000644");
            // owner[8] = "0000000\0"
            WriteAscii(hdr, 108, "0000000");

            // size[12]: octal "%011llo" up to 8^11-1, or GNU base-256 (0x80 big-endian)
            if (size > MaxOctalSize)
            {
                hdr[124] = 0x80;
                ulong tmp = size;
                for (int i = 11; i >= 1; i--)
                {
                    hdr[124 + i] = (byte)(tmp & 0xFF);
                    tmp >>= 8;
                }
            }
            else
            {
                string sizeOct = Convert.ToString((long)size, 8).PadLeft(11, '0');
                WriteAscii(hdr, 124, sizeOct);
            }

            // mtime[12] = "00000000000\0"
            WriteAscii(hdr, 136, "00000000000");

            // type = '0' (MTAR_TREG) at offset 156
            hdr[156] = (byte)'0';

            // checksum[8]: unsigned byte sum treating checksum[0..7] as spaces (256)
            uint sum = 256;
            for (int i = 0; i < 148; i++) sum += hdr[i];
            for (int i = 156; i < TarBlockSize; i++) sum += hdr[i];
            string chkOct = Convert.ToString(sum, 8).PadLeft(6, '0');
            WriteAscii(hdr, 148, chkOct);
            hdr[154] = 0;
            hdr[155] = (byte)' ';

            return hdr;
        }

        private static void WriteAscii(byte[] dst, int offset, string ascii)
        {
            for (int i = 0; i < ascii.Length; i++)
            {
                dst[offset + i] = (byte)ascii[i];
            }
        }

        private static bool TryParseTarHeader(byte[] rh, out string name, out ulong size, out byte fileType)
        {
            name = string.Empty;
            size = 0;
            fileType = rh[156];

            if (!TryParseOctal(rh, 148, 8, out ulong expectedChk))
                return false;

            uint actualChk = 256;
            for (int i = 0; i < 148; i++) actualChk += rh[i];
            for (int i = 156; i < TarBlockSize; i++) actualChk += rh[i];
            if (actualChk != expectedChk)
                return false;

            if ((rh[124] & 0x80) != 0)
            {
                size = (ulong)(rh[124] & 0x7F);
                for (int i = 1; i < 12; i++)
                {
                    size = (size << 8) | rh[124 + i];
                }
            }
            else if (!TryParseOctal(rh, 124, 12, out size))
            {
                return false;
            }

            int endName = 0;
            while (endName < 100 && rh[endName] != 0) endName++;
            name = Encoding.UTF8.GetString(rh, 0, endName);
            return true;
        }

        private static bool TryParseOctal(byte[] buf, int offset, int len, out ulong value)
        {
            value = 0;
            int i = 0;
            int digits = 0;
            while (i < len && buf[offset + i] == (byte)' ') i++;
            for (; i < len && buf[offset + i] >= (byte)'0' && buf[offset + i] <= (byte)'7'; i++)
            {
                if (value > (ulong.MaxValue >> 3)) return false;
                value = (value << 3) | (byte)(buf[offset + i] - (byte)'0');
                digits++;
            }
            if (digits == 0 || (i < len && buf[offset + i] != 0 && buf[offset + i] != (byte)' '))
                return false;
            return true;
        }
    }
}
