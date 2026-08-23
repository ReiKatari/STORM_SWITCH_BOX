using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class NintendoPlatformInfo
    {
        public string FullName { get; set; } = "";
        public string ShortName { get; set; } = "";
        public int ReleaseYear { get; set; }
        public string IconGlyph { get; set; } = "\uE7FC";
    }

    /// <summary>
    /// Высокопроизводительный оптимизированный сервис базы данных и библиотеки игр всех 19 поколений Nintendo,
    /// поддерживающий мгновенную индексацию сотен тысяч тайтлов, фоновое кэширование фильтров,
    /// форматирование чисел с разделителями тысяч (ru-RU), стандартизацию дат (dd.MM.yyyy) и чистые версии.
    /// </summary>
    public class NintendoLibraryService
    {
        public static readonly List<NintendoPlatformInfo> Platforms = new()
        {
            new() { FullName = "Nintendo Color TV-Game", ShortName = "TVG", ReleaseYear = 1977, IconGlyph = "\uE790" },
            new() { FullName = "Nintendo Game & Watch", ShortName = "GW", ReleaseYear = 1980, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Entertainment System / Famicom", ShortName = "NES", ReleaseYear = 1983, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Famicom Disk System", ShortName = "FDS", ReleaseYear = 1986, IconGlyph = "\uE8B7" },
            new() { FullName = "Nintendo Game Boy", ShortName = "GB", ReleaseYear = 1989, IconGlyph = "\uE8EA" },
            new() { FullName = "Super Nintendo Entertainment System / Super Famicom", ShortName = "SNES", ReleaseYear = 1990, IconGlyph = "\uE7FC" },
            new() { FullName = "Super Famicom Satellaview (BS-X)", ShortName = "BS-X", ReleaseYear = 1995, IconGlyph = "\uE753" },
            new() { FullName = "Nintendo Virtual Boy", ShortName = "VB", ReleaseYear = 1995, IconGlyph = "\uE7F4" },
            new() { FullName = "Nintendo 64 / Nintendo 64DD", ShortName = "N64", ReleaseYear = 1996, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Game Boy Color", ShortName = "GBC", ReleaseYear = 1998, IconGlyph = "\uE8EA" },
            new() { FullName = "Nintendo Pokémon Mini", ShortName = "PM", ReleaseYear = 2001, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Game Boy Advance / Game Boy micro", ShortName = "GBA", ReleaseYear = 2001, IconGlyph = "\uE8EA" },
            new() { FullName = "Nintendo GameCube", ShortName = "GC", ReleaseYear = 2001, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo DS / Nintendo DS Lite / Nintendo DSi", ShortName = "NDS", ReleaseYear = 2004, IconGlyph = "\uE8EA" },
            new() { FullName = "Nintendo Wii", ShortName = "Wii", ReleaseYear = 2006, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", ShortName = "3DS", ReleaseYear = 2011, IconGlyph = "\uE8EA" },
            new() { FullName = "Nintendo Wii U", ShortName = "WiiU", ReleaseYear = 2012, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Switch / Nintendo Switch Lite / OLED", ShortName = "Switch", ReleaseYear = 2017, IconGlyph = "\uE7FC" },
            new() { FullName = "Nintendo Switch 2", ShortName = "Switch 2", ReleaseYear = 2025, IconGlyph = "\uE7FC" }
        };

        private static readonly CultureInfo RussianCulture = new CultureInfo("ru-RU");

        private readonly object _dbLock = new object();
        private readonly List<NintendoGameEntry> _database = new(65536);
        private readonly Dictionary<string, List<NintendoGameEntry>> _bySystem = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadedIds = new(StringComparer.OrdinalIgnoreCase);

        // Precomputed distinct values for instant dropdown populating
        private readonly Dictionary<string, List<string>> _cachedGenres = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _cachedDevelopers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _cachedPublishers = new(StringComparer.OrdinalIgnoreCase);

        private bool _isInitialized = false;

        public event Action? LibraryUpdated;

        public NintendoLibraryService()
        {
            InitializeDatabase();
        }

        #region Formatting Helpers

        /// <summary>
        /// Форматирует число с пробелом в качестве разделителя тысяч (например: 47 425, 1 223, 121 234)
        /// </summary>
        public static string FormatNumber(int number)
        {
            return number.ToString("N0", RussianCulture);
        }

        /// <summary>
        /// Форматирует любую дату или год в строгий стандарт dd.MM.yyyy
        /// </summary>
        public static string FormatDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr) || dateStr.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                return "N/A";

            string s = dateStr.Trim();

            if (DateTime.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExact))
                return parsedExact.ToString("dd.MM.yyyy");

            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd", "yyyy-M-d", "d.M.yyyy", "dd-MM-yyyy", "MM/dd/yyyy" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.ToString("dd.MM.yyyy");
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var generalDt))
            {
                return generalDt.ToString("dd.MM.yyyy");
            }

            if (int.TryParse(s, out int year) && year >= 1970 && year <= 2035)
            {
                return $"01.01.{year}";
            }

            return s;
        }

        /// <summary>
        /// Форматирует номер версии, удаляя префикс v/V (например: 1.0.0 вместо v1.0.0)
        /// </summary>
        public static string FormatVersion(string? verStr)
        {
            if (string.IsNullOrWhiteSpace(verStr)) return "1.0";
            string s = verStr.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(1).Trim();
            }
            return string.IsNullOrEmpty(s) ? "1.0" : s;
        }

        #endregion

        public async Task EnsureSwitchGamesLoadedAsync(TitleDbService? titleDb)
        {
            if (titleDb == null) return;

            await Task.Run(() =>
            {
                var entries = titleDb.GetAllEntries();
                if (entries == null) return;

                bool addedAny = false;
                lock (_dbLock)
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Name)) continue;
                        if (_loadedIds.Contains(entry.Id)) continue;

                        string genre = (entry.Category != null && entry.Category.Count > 0) ? string.Join(" / ", entry.Category) : "Приключения / Экшен";
                        string date = entry.ReleaseDate.HasValue ? FormatDate(entry.ReleaseDate.Value.ToString()) : "03.03.2017";
                        string dev = !string.IsNullOrWhiteSpace(entry.Developer) ? entry.Developer : (!string.IsNullOrWhiteSpace(entry.Publisher) ? entry.Publisher : "Nintendo");
                        string pub = !string.IsNullOrWhiteSpace(entry.Publisher) ? entry.Publisher : "Nintendo";
                        string ver = FormatVersion(entry.Version);
                        string desc = !string.IsNullOrWhiteSpace(entry.Description) ? entry.Description : (!string.IsNullOrWhiteSpace(entry.Intro) ? entry.Intro : "Официальная игра для Nintendo Switch.");
                        string rating = entry.Rating.HasValue ? $"{entry.Rating.Value}+" : "Для всех";
                        string cover = CoverCacheService.ResolveCoverUrl(entry.IconUrl, "Nintendo Switch / Nintendo Switch Lite / OLED", entry.Name, entry.Id);

                        var gameEntry = new NintendoGameEntry
                        {
                            Id = entry.Id,
                            Title = entry.Name,
                            System = "Nintendo Switch / Nintendo Switch Lite / OLED",
                            SystemShort = "Switch",
                            Genre = genre,
                            ReleaseDate = date,
                            Developer = dev,
                            Publisher = pub,
                            Version = ver,
                            Edition = "Standard Edition",
                            Region = !string.IsNullOrWhiteSpace(entry.Regions) ? entry.Regions : "WW",
                            CoverUrl = cover,
                            Description = desc,
                            Players = "1-4 игрока",
                            Rating = rating
                        };

                        _database.Add(gameEntry);
                        _loadedIds.Add(entry.Id);

                        IndexGameBySystem(gameEntry);
                        addedAny = true;
                    }

                    if (addedAny)
                    {
                        InvalidateFilterCaches();
                    }
                }

                if (addedAny)
                {
                    LibraryUpdated?.Invoke();
                }
            });
        }

        public void EnsureSwitchGamesLoaded(TitleDbService? titleDb)
        {
            _ = EnsureSwitchGamesLoadedAsync(titleDb);
        }

        private void IndexGameBySystem(NintendoGameEntry g)
        {
            if (!_bySystem.TryGetValue(g.System, out var list1))
            {
                list1 = new List<NintendoGameEntry>(2048);
                _bySystem[g.System] = list1;
            }
            list1.Add(g);

            if (!string.IsNullOrEmpty(g.SystemShort) && !_bySystem.TryGetValue(g.SystemShort, out var list2))
            {
                list2 = new List<NintendoGameEntry>(2048);
                _bySystem[g.SystemShort] = list2;
            }
            if (!string.IsNullOrEmpty(g.SystemShort) && _bySystem.TryGetValue(g.SystemShort, out var existingList) && existingList != list1)
            {
                existingList.Add(g);
            }
        }

        private void InvalidateFilterCaches()
        {
            _cachedGenres.Clear();
            _cachedDevelopers.Clear();
            _cachedPublishers.Clear();
        }

        private void InitializeDatabase()
        {
            if (_isInitialized) return;

            lock (_dbLock)
            {
                if (_isInitialized) return;

                // =========================================================================
                // 1. Nintendo Color TV-Game (1977)
                // =========================================================================
                AddGame("TVG-001", "Color TV-Game 6", "Nintendo Color TV-Game", "TVG", "Спорт / Pong", "1977-06-01", "Nintendo R&D2", "Nintendo", "1.0", "Original Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer/master/Named_Boxarts/Color%20TV-Game%206%20(Japan).png", "Первая в истории игровая приставка от Nintendo со встроенными вариациями классического тенниса.", "1-2 игрока", "Для всех");
                AddGame("TVG-002", "Color TV-Game 15", "Nintendo Color TV-Game", "TVG", "Спорт / Pong", "1977-06-08", "Nintendo R&D2", "Nintendo", "1.0", "Original Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer/master/Named_Boxarts/Color%20TV-Game%2015%20(Japan).png", "Расширенная версия консоли с проводными контроллерами и 15 спортивными играми.", "1-2 игрока", "Для всех");
                AddGame("TVG-003", "Color TV-Game Racing 112", "Nintendo Color TV-Game", "TVG", "Гонки", "1978-06-01", "Nintendo R&D2", "Nintendo", "1.0", "Original Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer/master/Named_Boxarts/Color%20TV-Game%20Racing%20112%20(Japan).png", "Гоночная приставка с настоящим физическим рулем и рычагом переключения передач от Сигэру Миямото.", "1 игрок", "Для всех");
                AddGame("TVG-004", "Color TV-Game Block Kuzushi", "Nintendo Color TV-Game", "TVG", "Аркада / Breakout", "1979-04-23", "Nintendo R&D2", "Nintendo", "1.0", "Original Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer/master/Named_Boxarts/Color%20TV-Game%20Block%20Kuzushi%20(Japan).png", "Аркадный блок-брейкер, дизайн корпуса которого лично разработал Сигэру Миямото.", "1 игрок", "Для всех");
                AddGame("TVG-005", "Computer TV-Game", "Nintendo Color TV-Game", "TVG", "Стратегия / Отелло", "1980-01-01", "Nintendo R&D2", "Nintendo", "1.0", "Original Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer/master/Named_Boxarts/Computer%20TV-Game%20(Japan).png", "Электронная адаптация популярной настольной игры Реверси / Отелло против искусственного интеллекта.", "1-2 игрока", "Для всех");

                // =========================================================================
                // 2. Nintendo Game & Watch (1980)
                // =========================================================================
                AddGame("GW-001", "Ball (Toss-Up)", "Nintendo Game & Watch", "GW", "Аркада", "1980-04-28", "Nintendo R&D1", "Nintendo", "1.0", "Silver Series", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Ball%20(Silver).png", "Самая первая карманная игра от Гумпэя Ёкои с жонглированием шариками.", "1 игрок", "Для всех");
                AddGame("GW-002", "Donkey Kong (Multi Screen)", "Nintendo Game & Watch", "GW", "Платформер", "1982-06-03", "Nintendo R&D1", "Nintendo", "1.0", "Multi Screen Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Donkey%20Kong%20(Multi%20Screen).png", "Легендарная двухэкранная раскладушка, впервые представившая миру крестовину D-Pad.", "1 игрок", "Для всех");
                AddGame("GW-003", "Mario Bros. (Multi Screen)", "Nintendo Game & Watch", "GW", "Аркада", "1983-03-14", "Nintendo R&D1", "Nintendo", "1.0", "Multi Screen Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Mario%20Bros.%20(Multi%20Screen).png", "Марио и Луиджи работают на упаковочной фабрике бутылок в горизонтальной двухэкранной модели.", "1-2 игрока", "Для всех");
                AddGame("GW-004", "The Legend of Zelda (Multi Screen)", "Nintendo Game & Watch", "GW", "Приключения", "1989-08-26", "Nintendo R&D1", "Nintendo", "1.0", "Multi Screen Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Zelda%20(Multi%20Screen).png", "Портативное приключение Линка по подземельям с мечом и щитом против драконов.", "1 игрок", "Для всех");
                AddGame("GW-005", "Super Mario Bros.", "Nintendo Game & Watch", "GW", "Платформер", "1986-06-01", "Nintendo R&D1", "Nintendo", "1.0", "Crystal Screen / Wide Screen", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Super%20Mario%20Bros.%20(Wide%20Screen).png", "Карманная адаптация приключений Марио через Грибное королевство.", "1 игрок", "Для всех");
                AddGame("GW-006", "Fire (Silver Series)", "Nintendo Game & Watch", "GW", "Аркада", "1980-07-31", "Nintendo R&D1", "Nintendo", "1.0", "Silver Series", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Fire%20(Silver).png", "Спасение прыгающих из горящего здания человечков с помощью натяжного батута.", "1 игрок", "Для всех");
                AddGame("GW-007", "Octopus", "Nintendo Game & Watch", "GW", "Аркада / Дайвинг", "1981-07-16", "Nintendo R&D1", "Nintendo", "1.0", "Wide Screen", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Octopus%20(Wide%20Screen).png", "Водолазы спускаются за сокровищами на дно океана, уворачиваясь от щупалец гигантского осьминога.", "1 игрок", "Для всех");
                AddGame("GW-008", "Parachute", "Nintendo Game & Watch", "GW", "Аркада", "1981-06-19", "Nintendo R&D1", "Nintendo", "1.0", "Wide Screen", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_and_Watch/master/Named_Boxarts/Parachute%20(Wide%20Screen).png", "Ловля парашютистов в спасательную лодку над кишащими акулами водами.", "1 игрок", "Для всех");

                // =========================================================================
                // 3. NES / Famicom (1983)
                // =========================================================================
                AddGame("NES-001", "Super Mario Bros.", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1985-09-13", "Nintendo R&D4", "Nintendo", "1.0", "USA Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20Bros.%20(USA).png", "Культовый платформер, изменивший мировую игровую индустрию навсегда.", "1-2 игрока", "Для всех");
                AddGame("NES-002", "Super Mario Bros. (Europe)", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1987-05-15", "Nintendo R&D4", "Nintendo", "1.1", "PAL European Edition", "EUR", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20Bros.%20(Europe).png", "Европейская версия великого платформера с оптимизацией таймингов PAL 50Hz.", "1-2 игрока", "Для всех");
                AddGame("NES-003", "Super Mario Bros. 2 (USA)", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1988-10-09", "Nintendo R&D4", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20Bros.%202%20(USA).png", "Приключения Марио, Луиджи, Пич и Тоада в мире снов Субкон с выдергиванием редиски.", "1 игрок", "Для всех");
                AddGame("NES-004", "Super Mario Bros. 3", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1988-10-23", "Nintendo R&D4", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20Bros.%203%20(USA).png", "Вершина эволюции 8-битных платформеров с костюмом Тануки и картой миров.", "1-2 игрока", "Для всех");
                AddGame("NES-005", "The Legend of Zelda", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Приключения", "1986-02-21", "Nintendo R&D4", "Nintendo", "1.0", "Gold Cartridge Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20(USA).png", "Эпическое приключение по Хайрулу в поисках фрагментов Трифорса.", "1 игрок", "Для всех");
                AddGame("NES-006", "Zelda II: The Adventure of Link", "Nintendo Entertainment System / Famicom", "NES", "Экшен / RPG", "1987-01-14", "Nintendo R&D4", "Nintendo", "1.0", "Gold Cartridge Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Zelda%20II%20-%20The%20Adventure%20of%20Link%20(USA).png", "Сайд-скроллерная RPG о пробуждении принцессы Зельды и сражении с Тенью Линка.", "1 игрок", "Для всех");
                AddGame("NES-007", "Metroid", "Nintendo Entertainment System / Famicom", "NES", "Метроидвания", "1986-08-06", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Metroid%20(USA).png", "Родоначальник жанра с Самус Аран в глубинах мрачной планеты Зебес.", "1 игрок", "Для всех");
                AddGame("NES-008", "Castlevania", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1986-09-26", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Castlevania%20(USA).png", "Саймон Бельмонт штурмует замок Дракулы с кнутом Убийца Вампиров.", "1 игрок", "10+");
                AddGame("NES-009", "Castlevania II: Simon's Quest", "Nintendo Entertainment System / Famicom", "NES", "Экшен / RPG", "1987-08-28", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Castlevania%20II%20-%20Simon's%20Quest%20(USA).png", "Поиск останков Дракулы для снятия проклятия со сменой дня и ночи.", "1 игрок", "10+");
                AddGame("NES-010", "Castlevania III: Dracula's Curse", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1989-12-22", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Castlevania%20III%20-%20Dracula's%20Curse%20(USA).png", "Шедевр от Konami с разветвленным сюжетом и напарниками Тревора Бельмонта.", "1 игрок", "10+");
                AddGame("NES-011", "Contra", "Nintendo Entertainment System / Famicom", "NES", "Run and Gun", "1988-02-02", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Contra%20(USA).png", "Легендарный ураганный экшен для двоих игроков с кодом Konami.", "1-2 игрока", "10+");
                AddGame("NES-012", "Super C (Contra II)", "Nintendo Entertainment System / Famicom", "NES", "Run and Gun", "1990-04-01", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20C%20(USA).png", "Продолжение легендарного боевика с уровнями с видом сверху и вертолетными десантами.", "1-2 игрока", "10+");
                AddGame("NES-013", "Mega Man", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1987-12-17", "Capcom", "Capcom", "1.0", "Original Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%20(USA).png", "Дебют синего бомбардира против 6 роботов-мастеров доктора Вайли.", "1 игрок", "Для всех");
                AddGame("NES-014", "Mega Man 2", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1988-12-24", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%202%20(USA).png", "Золотой стандарт платформеров Capcom с незабываемым саундтреком.", "1 игрок", "Для всех");
                AddGame("NES-015", "Mega Man 3", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1990-09-28", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%203%20(USA).png", "Появление собаки Раша, подката и загадочного Протомена.", "1 игрок", "Для всех");
                AddGame("NES-016", "Mega Man 4", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1991-12-06", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%204%20(USA).png", "Внедрение заряжаемого мега-бластера Mega Buster и доктора Казака.", "1 игрок", "Для всех");
                AddGame("NES-017", "Mega Man 5", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1992-12-04", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%205%20(USA).png", "Птица-помощник Бит, сбор букв MEGAMANV и гравитационный переворот.", "1 игрок", "Для всех");
                AddGame("NES-018", "Mega Man 6", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1993-11-05", "Capcom", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%206%20(USA).png", "Адаптеры реактивного и силового костюмов Раша на мировом турнире роботов.", "1 игрок", "Для всех");
                AddGame("NES-019", "DuckTales", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1989-09-14", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/DuckTales%20(USA).png", "Приключения Скруджа Макдака по всему миру с тростью и прыжками на ней.", "1 игрок", "Для всех");
                AddGame("NES-020", "DuckTales 2", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1993-04-23", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/DuckTales%202%20(USA).png", "Поиск карты сокровищ сэра Дрейка Макдака с улучшениями трости от Винта Разболтайло.", "1 игрок", "Для всех");
                AddGame("NES-021", "Chip 'n Dale Rescue Rangers", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1990-06-08", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Chip%20'n%20Dale%20-%20Rescue%20Rangers%20(USA).png", "Знаменитый кооперативный платформер про спасателей Чипа и Дейла с бросанием ящиков.", "1-2 игрока", "Для всех");
                AddGame("NES-022", "Chip 'n Dale Rescue Rangers 2", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1993-12-10", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Chip%20'n%20Dale%20-%20Rescue%20Rangers%202%20(USA).png", "Спасатели останавливают побег кота Толстопуза и обезвреживают бомбу в клубе.", "1-2 игрока", "Для всех");
                AddGame("NES-023", "Darkwing Duck", "Nintendo Entertainment System / Famicom", "NES", "Платформер / Экшен", "1992-06-01", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Darkwing%20Duck%20(USA).png", "«Я ужас, летящий на крыльях ночи!» — Черный Плащ спасает Сен-Канар от банды Стального Клюва.", "1 игрок", "Для всех");
                AddGame("NES-024", "Battletoads", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up / Платформер", "1991-06-01", "Rare", "Tradewest", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Battletoads%20(USA).png", "Ультра-хардкорный легендарный боевик с боевыми жабами Рашем и Зитцем и турбо-туннелем.", "1-2 игрока", "12+");
                AddGame("NES-025", "Battletoads & Double Dragon", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up", "1993-06-01", "Rare", "Tradewest", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Battletoads-Double%20Dragon%20(USA).png", "Кроссовер боевых жаб и братьев Ли на борту космического крейсера Тёмной Королевы.", "1-2 игрока", "12+");
                AddGame("NES-026", "Teenage Mutant Ninja Turtles", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1989-05-12", "Konami", "Ultra Games", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Teenage%20Mutant%20Ninja%20Turtles%20(USA).png", "Первая игра о Черепашках с подводным уровнем обезвреживания мин на плотине.", "1 игрок", "Для всех");
                AddGame("NES-027", "Teenage Mutant Ninja Turtles II: The Arcade Game", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up", "1990-12-01", "Konami", "Ultra Games", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Teenage%20Mutant%20Ninja%20Turtles%20II%20-%20The%20Arcade%20Game%20(USA).png", "Аркадный хит для двоих игроков с пиццами, Бибопом, Рокстеди и Шреддером.", "1-2 игрока", "Для всех");
                AddGame("NES-028", "Teenage Mutant Ninja Turtles III: The Manhattan Project", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up", "1991-12-13", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Teenage%20Mutant%20Ninja%20Turtles%20III%20-%20The%20Manhattan%20Project%20(USA).png", "Лучшая часть Черепашек-Ниндзя на NES с уникальными спецприемами каждого героя.", "1-2 игрока", "Для всех");
                AddGame("NES-029", "Ninja Gaiden", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Ниндзя", "1988-12-09", "Tecmo", "Tecmo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Ninja%20Gaiden%20(USA).png", "Кинематографичный экшен о мести Рю Хаябусы с потрясающими кат-сценами.", "1 игрок", "12+");
                AddGame("NES-030", "Ninja Gaiden II: The Dark Sword of Chaos", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Ниндзя", "1990-04-06", "Tecmo", "Tecmo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Ninja%20Gaiden%20II%20-%20The%20Dark%20Sword%20of%20Chaos%20(USA).png", "Рю Хаябуса использует огненных клонов-теней против императора Аштара.", "1 игрок", "12+");
                AddGame("NES-031", "Ninja Gaiden III: The Ancient Ship of Doom", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Ниндзя", "1991-06-21", "Tecmo", "Tecmo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Ninja%20Gaiden%20III%20-%20The%20Ancient%20Ship%20of%20Doom%20(USA).png", "Финальная глава 8-битной трилогии о космическом корабле Рока и бионоидах.", "1 игрок", "12+");
                AddGame("NES-032", "Kirby's Adventure", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1993-03-23", "HAL Laboratory", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Kirby's%20Adventure%20(USA).png", "Полноценный цветной дебют Кирби на NES с копированием десятков способностей врагов.", "1 игрок", "Для всех");
                AddGame("NES-033", "Mike Tyson's Punch-Out!!", "Nintendo Entertainment System / Famicom", "NES", "Спорт / Бокс", "1987-10-18", "Nintendo R&D3", "Nintendo", "1.0", "Mike Tyson Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Mike%20Tyson's%20Punch-Out!!%20(USA).png", "Маленький Мак пробивается к чемпионскому поясу против самого Майка Тайсона.", "1 игрок", "Для всех");
                AddGame("NES-034", "Punch-Out!! (Featuring Mr. Dream)", "Nintendo Entertainment System / Famicom", "NES", "Спорт / Бокс", "1990-08-01", "Nintendo R&D3", "Nintendo", "1.1", "Mr. Dream Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Punch-Out!!%20(USA).png", "Переиздание боксерского симулятора с финальным чемпионом Мистером Дримом.", "1 игрок", "Для всех");
                AddGame("NES-035", "EarthBound Beginnings (Mother)", "Nintendo Entertainment System / Famicom", "NES", "JRPG", "1989-07-27", "Ape Inc. / Nintendo", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/EarthBound%20Beginnings%20(USA).png", "Родоначальница культовой серии Mother с музыкой Кеити Судзуки и пси-способностями Нинтена.", "1 игрок", "Для всех");
                AddGame("NES-036", "Final Fantasy", "Nintendo Entertainment System / Famicom", "NES", "JRPG", "1987-12-18", "Square", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Final%20Fantasy%20(USA).png", "Легендарная игра Хиронобу Сакагути, спасшая компанию Square от банкротства.", "1 игрок", "Для всех");
                AddGame("NES-037", "Dragon Warrior (Dragon Quest)", "Nintendo Entertainment System / Famicom", "NES", "JRPG", "1986-05-27", "Chunsoft", "Enix / Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Dragon%20Warrior%20(USA).png", "Прародительница всех японских ролевых игр с иллюстрациями Акиры Ториямы.", "1 игрок", "Для всех");
                AddGame("NES-038", "Dragon Warrior III", "Nintendo Entertainment System / Famicom", "NES", "JRPG", "1988-02-10", "Chunsoft", "Enix", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Dragon%20Warrior%20III%20(USA).png", "Абсолютный триумф 8-битных RPG со сменой профессий и спутниками.", "1 игрок", "Для всех");
                AddGame("NES-039", "Bionic Commando", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1988-07-20", "Capcom", "Capcom", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Bionic%20Commando%20(USA).png", "Спецназовец Рэд с механической рукой-крюком вместо привычных прыжков.", "1 игрок", "10+");
                AddGame("NES-040", "Blaster Master", "Nintendo Entertainment System / Famicom", "NES", "Метроидвания / Танк", "1988-06-17", "Sunsoft", "Sunsoft", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Blaster%20Master%20(USA).png", "Боевой вездеход СОФИЯ-3 и уровни с видом сверху внутри подземелий.", "1 игрок", "Для всех");
                AddGame("NES-041", "Batman: The Video Game", "Nintendo Entertainment System / Famicom", "NES", "Экшен / Платформер", "1989-12-22", "Sunsoft", "Sunsoft", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Batman%20-%20The%20Video%20Game%20(USA).png", "Атмосферный готэмский боевик с отскоками от стен и битвами с Джокером.", "1 игрок", "Для всех");
                AddGame("NES-042", "River City Ransom", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up / RPG", "1989-04-25", "Technōs Japan", "Technōs Japan", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/River%20City%20Ransom%20(USA).png", "Уличные драки Алекса и Райана с покупкой книг приемов в магазинах и еды.", "1-2 игрока", "10+");
                AddGame("NES-043", "Double Dragon II: The Revenge", "Nintendo Entertainment System / Famicom", "NES", "Beat 'em up", "1989-12-22", "Technōs Japan", "Acclaim", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Double%20Dragon%20II%20-%20The%20Revenge%20(USA).png", "Братья Билли и Джимми Ли мстят за Мэриан с ударами коленом в воздухе.", "1-2 игрока", "10+");
                AddGame("NES-044", "Adventure Island II", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1991-04-26", "Now Production", "Hudson Soft", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Adventure%20Island%20II%20(USA).png", "Мастер Хиггинс оседлал динозавров и собирает фрукты на тропических островах.", "1 игрок", "Для всех");
                AddGame("NES-045", "Gradius", "Nintendo Entertainment System / Famicom", "NES", "Шмуп", "1986-04-25", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Gradius%20(USA).png", "Истребитель Vic Viper против бактерионской угрозы с гибкой шкалой апгрейдов.", "1-2 игрока", "Для всех");
                AddGame("NES-046", "Life Force (Salamander)", "Nintendo Entertainment System / Famicom", "NES", "Шмуп", "1987-09-01", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Life%20Force%20(USA).png", "Кооперативный космошутер с чередованием горизонтальной и вертикальной прокрутки.", "1-2 игрока", "Для всех");
                AddGame("NES-047", "Tetris (Nintendo)", "Nintendo Entertainment System / Famicom", "NES", "Головоломка", "1989-11-01", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Tetris%20(USA).png", "Официальная 8-битная версия Тетриса с запуском ракеты на 9 уровне.", "1 игрок", "Для всех");
                AddGame("NES-048", "Dr. Mario", "Nintendo Entertainment System / Famicom", "NES", "Головоломка", "1990-07-27", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Dr.%20Mario%20(USA).png", "Доктор Марио уничтожает цветные вирусы разноцветными капсулами лекарств.", "1-2 игрока", "Для всех");
                AddGame("NES-049", "Tiny Toon Adventures", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1991-12-01", "Konami", "Konami", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Tiny%20Toon%20Adventures%20(USA).png", "Бастер Банни, Плаки Дак, Диззи Девил и Фербал спасают Бэбс Банни от Монтаны Макса.", "1 игрок", "Для всех");
                AddGame("NES-050", "Felix the Cat", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1992-10-01", "Shimada Kikaku", "Hudson Soft", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Felix%20the%20Cat%20(USA).png", "Кот Феликс трансформирует волшебную сумку в танк, самолет и дельфина.", "1 игрок", "Для всех");
                AddGame("NES-051", "Little Samson", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1992-06-26", "Takeru", "Taito", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Little%20Samson%20(USA).png", "Редчайший шедевр поздней эпохи NES со сменой 4 уникальных героев.", "1 игрок", "Для всех");
                AddGame("NES-052", "Excitebike", "Nintendo Entertainment System / Famicom", "NES", "Гонки", "1984-11-30", "Nintendo R&D1", "Nintendo", "1.0", "Black Box Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Excitebike%20(USA).png", "Мотокросс с трамплинами, грязью, перегревом двигателя и редактором трасс.", "1-2 игрока", "Для всех");
                AddGame("NES-053", "Balloon Fight", "Nintendo Entertainment System / Famicom", "NES", "Аркада", "1985-01-22", "Nintendo R&D1 / Satoru Iwata", "Nintendo", "1.0", "Black Box Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Balloon%20Fight%20(USA).png", "Полеты на воздушных шариках с безупречной физикой полета от Сатору Иваты.", "1-2 игрока", "Для всех");
                AddGame("NES-054", "Ice Climber", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1985-01-30", "Nintendo R&D1", "Nintendo", "1.0", "Black Box Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Ice%20Climber%20(USA).png", "Попо и Нана взбираются молотками на вершину ледяной горы за баклажанами.", "1-2 игрока", "Для всех");
                AddGame("NES-055", "Kid Icarus", "Nintendo Entertainment System / Famicom", "NES", "Платформер", "1986-12-19", "Nintendo R&D1 / TOSE", "Nintendo", "1.0", "Standard Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/Kid%20Icarus%20(USA).png", "Ангел Пит взбирается из подземного царства на Олимп к богине Палютене.", "1 игрок", "Для всех");

                // =========================================================================
                // 4. Famicom Disk System (1986)
                // =========================================================================
                AddGame("FDS-001", "The Legend of Zelda (FDS)", "Nintendo Famicom Disk System", "FDS", "Приключения", "1986-02-21", "Nintendo R&D4", "Nintendo", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Zelda%20no%20Densetsu%20-%20The%20Hyrule%20Fantasy%20(Japan).png", "Оригинальный дебют Zelda с улучшенным звуковым синтезатором FM FDS.", "1 игрок", "Для всех");
                AddGame("FDS-002", "Super Mario Bros. 2 (The Lost Levels)", "Nintendo Famicom Disk System", "FDS", "Платформер", "1986-06-03", "Nintendo R&D4", "Nintendo", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Super%20Mario%20Bros.%202%20(Japan).png", "Настоящий хардкорный сиквел Super Mario Bros. с ядовитыми грибами и ветром.", "1 игрок", "Для всех");
                AddGame("FDS-003", "Akumajou Dracula (Castlevania FDS)", "Nintendo Famicom Disk System", "FDS", "Экшен", "1986-09-26", "Konami", "Konami", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Akumajou%20Dracula%20(Japan).png", "Первая в мире версия Castlevania с возможностью сохранения прогресса на дискету.", "1 игрок", "10+");
                AddGame("FDS-004", "Metroid (FDS)", "Nintendo Famicom Disk System", "FDS", "Метроидвания", "1986-08-06", "Nintendo R&D1", "Nintendo", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Metroid%20(Japan).png", "Дебют Самус Аран с поддержкой сохранений и расширенного звукового канала.", "1 игрок", "Для всех");
                AddGame("FDS-005", "Kid Icarus (FDS)", "Nintendo Famicom Disk System", "FDS", "Платформер", "1986-12-19", "Nintendo R&D1 / TOSE", "Nintendo", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Hikari%20Shinwa%20-%20Palthena%20no%20Kagami%20(Japan).png", "Ангел Пит взбирается из подземного царства на Олимп к богине Палютене.", "1 игрок", "Для всех");
                AddGame("FDS-006", "Doki Doki Panic", "Nintendo Famicom Disk System", "FDS", "Платформер", "1987-07-10", "Nintendo R&D4", "Fuji Television", "1.0", "Disk Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Family_Computer_Disk_System/master/Named_Boxarts/Yume%20Koujou%20Doki%20Doki%20Panic%20(Japan).png", "Оригинальная игра с арабской семьей, позже ставшая западной Super Mario Bros. 2.", "1 игрок", "Для всех");

                // =========================================================================
                // 5. Nintendo Game Boy (1989)
                // =========================================================================
                AddGame("GB-001", "Tetris", "Nintendo Game Boy", "GB", "Головоломка", "1989-06-14", "Nintendo R&D1 / Alexey Pajitnov", "Nintendo", "1.0", "Pack-in Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Tetris%20(World).png", "Главный систем-селлер карманной консоли от Алексея Пажитнова с легендарной темой Коробейники.", "1-2 игрока", "Для всех");
                AddGame("GB-002", "Pokemon Red and Blue", "Nintendo Game Boy", "GB", "JRPG", "1996-02-27", "Game Freak", "Nintendo", "1.0", "Red / Blue Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Pokemon%20-%20Red%20Version%20(USA,%20Europe).png", "Рождение глобального феномена ловли 151 покемона в регионе Канто.", "1-2 игрока", "Для всех");
                AddGame("GB-003", "Pokemon Yellow: Special Pikachu Edition", "Nintendo Game Boy", "GB", "JRPG", "1998-09-12", "Game Freak", "Nintendo", "1.0", "Pikachu Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Pokemon%20-%20Yellow%20Version%20-%20Special%20Pikachu%20Edition%20(USA,%20Europe).png", "Специальная аниме-версия, где верный Пикачу ходит за тренером по пятам.", "1-2 игрока", "Для всех");
                AddGame("GB-004", "The Legend of Zelda: Link's Awakening", "Nintendo Game Boy", "GB", "Приключения", "1993-06-06", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Link's%20Awakening%20(USA,%20Europe).png", "Удивительное сюрреалистичное приключение на таинственном острове Кохолинт.", "1 игрок", "Для всех");
                AddGame("GB-005", "Super Mario Land", "Nintendo Game Boy", "GB", "Платформер", "1989-04-21", "Nintendo R&D1", "Nintendo", "1.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Super%20Mario%20Land%20(World).png", "Марио спасает принцессу Дейзи в королевстве Сарасаленд с подводными лодками и самолетами.", "1 игрок", "Для всех");
                AddGame("GB-006", "Super Mario Land 2: 6 Golden Coins", "Nintendo Game Boy", "GB", "Платформер", "1992-10-21", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Super%20Mario%20Land%202%20-%206%20Golden%20Coins%20(USA,%20Europe).png", "Первое появление Варио в качестве главного антагониста в грандиозном платформере.", "1 игрок", "Для всех");
                AddGame("GB-007", "Wario Land: Super Mario Land 3", "Nintendo Game Boy", "GB", "Платформер", "1994-01-21", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Wario%20Land%20-%20Super%20Mario%20Land%203%20(World).png", "Варио ищет сокровища и гигантскую золотую статую принцессы Пич.", "1 игрок", "Для всех");
                AddGame("GB-008", "Kirby's Dream Land", "Nintendo Game Boy", "GB", "Платформер", "1992-04-27", "HAL Laboratory", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Kirby's%20Dream%20Land%20(USA,%20Europe).png", "Дебют розового героя Кирби от Масахиро Сакураи.", "1 игрок", "Для всех");
                AddGame("GB-009", "Metroid II: Return of Samus", "Nintendo Game Boy", "GB", "Метроидвания", "1991-11-01", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Metroid%20II%20-%20Return%20of%20Samus%20(USA,%20Europe).png", "Самус уничтожает метроидов на их родной планете SR388 и спасает личинку-младенца.", "1 игрок", "Для всех");
                AddGame("GB-010", "Donkey Kong Land", "Nintendo Game Boy", "GB", "Платформер", "1995-05-24", "Rare", "Nintendo", "1.0", "Yellow Cartridge Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/Donkey%20Kong%20Land%20(USA,%20Europe).png", "Портативное 3D-приключение Донки Конга в фирменном желтом картридже.", "1 игрок", "Для всех");

                // =========================================================================
                // 6. Super Nintendo (SNES / Super Famicom) (1990)
                // =========================================================================
                AddGame("SNES-001", "Super Mario World", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Платформер", "1990-11-21", "Nintendo EAD", "Nintendo", "1.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20World%20(USA).png", "Шедевр 16-битной эпохи с дебютом динозаврика Йоши и 96 секретными выходами.", "1-2 игрока", "Для всех");
                AddGame("SNES-002", "Super Mario World 2: Yoshi's Island", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Платформер / Super FX 2", "1995-08-05", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20World%202%20-%20Yoshi's%20Island%20(USA).png", "Художественный триумф в стиле пастельного рисунка с малышом Марио верхом на Йоши.", "1 игрок", "Для всех");
                AddGame("SNES-003", "The Legend of Zelda: A Link to the Past", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Экшен / Приключения", "1991-11-21", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20A%20Link%20to%20the%20Past%20(USA).png", "Эталон жанра Action-Adventure с параллельными мирами Света и Тьмы.", "1 игрок", "Для всех");
                AddGame("SNES-004", "Super Metroid", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Метроидвания", "1994-03-19", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Metroid%20(Japan,%20USA).png", "Атмосферный триумф геймдизайна и один из величайших платформеров в истории.", "1 игрок", "10+");
                AddGame("SNES-005", "Chrono Trigger", "Super Nintendo Entertainment System / Super Famicom", "SNES", "JRPG", "1995-03-11", "Square", "Square", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Chrono%20Trigger%20(USA).png", "Культовая RPG Dream Team (Сакагути, Хории, Торияма) с путешествиями во времени.", "1 игрок", "10+");
                AddGame("SNES-006", "Donkey Kong Country", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Платформер", "1994-11-21", "Rare", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Donkey%20Kong%20Country%20(USA).png", "Революционная предварительно отрендеренная 3D-графика от студии Rare.", "1-2 игрока", "Для всех");
                AddGame("SNES-007", "Donkey Kong Country 2: Diddy's Kong Quest", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Платформер", "1995-11-20", "Rare", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Donkey%20Kong%20Country%202%20-%20Diddy's%20Kong%20Quest%20(USA).png", "Пиратское приключение Дидди и Дикси с непревзойденной музыкой Дэвида Уайза.", "1-2 игрока", "Для всех");
                AddGame("SNES-008", "Donkey Kong Country 3: Dixie Kong's Double Trouble!", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Платформер", "1996-11-22", "Rare", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Donkey%20Kong%20Country%203%20-%20Dixie%20Kong's%20Double%20Trouble!%20(USA).png", "Дикси и малыш Кидди исследуют северные леса и спасают Донки Конга.", "1-2 игрока", "Для всех");
                AddGame("SNES-009", "Super Mario Kart", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Гонки / Mode 7", "1992-08-27", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20Kart%20(USA).png", "Основатель жанра карт-гонок с использованием псевдотрехмерного режима Mode 7.", "1-2 игрока", "Для всех");
                AddGame("SNES-010", "Super Mario RPG: Legend of the Seven Stars", "Super Nintendo Entertainment System / Super Famicom", "SNES", "RPG", "1996-03-09", "Square", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Mario%20RPG%20-%20Legend%20of%20the%20Seven%20Stars%20(USA).png", "Коллаборация Square и Nintendo с Джино, Маллоу и бандой Смити.", "1 игрок", "Для всех");
                AddGame("SNES-011", "EarthBound (Mother 2)", "Super Nintendo Entertainment System / Super Famicom", "SNES", "JRPG", "1994-08-27", "Ape / HAL Laboratory", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/EarthBound%20(USA).png", "Уникальная ироничная RPG Сигэсато Итои в современной Америке против инопланетянина Гийгаса.", "1 игрок", "Для всех");
                AddGame("SNES-012", "Final Fantasy VI (III US)", "Super Nintendo Entertainment System / Super Famicom", "SNES", "JRPG", "1994-04-02", "Square", "Square", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Final%20Fantasy%20III%20(USA).png", "Эпическая стимпанк-драма с Кефкой Палаццо, оперой Марии и Драко и саундтреком Нобуо Уэмацу.", "1-2 игрока", "10+");
                AddGame("SNES-013", "Star Fox", "Super Nintendo Entertainment System / Super Famicom", "SNES", "3D Шутер / Super FX", "1993-02-21", "Argonaut Games / Nintendo", "Nintendo", "1.0", "Super FX Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Star%20Fox%20(USA).png", "Революция 3D-полигонов на 16-битной консоли с Фоксом Макклаудом.", "1 игрок", "Для всех");
                AddGame("SNES-014", "Mega Man X", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Экшен / Платформер", "1993-12-17", "Capcom", "Capcom", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Mega%20Man%20X%20(USA).png", "Взросление серии Mega Man с рывками, лазанием по стенам и броней доктора Лайта.", "1 игрок", "Для всех");
                AddGame("SNES-015", "Super Castlevania IV", "Super Nintendo Entertainment System / Super Famicom", "SNES", "Экшен / Платформер", "1991-10-31", "Konami", "Konami", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/Super%20Castlevania%20IV%20(USA).png", "Вращение кнута в 8 направлениях и вращающиеся комнаты Mode 7.", "1 игрок", "10+");

                // =========================================================================
                // 7. Satellaview BS-X (1995)
                // =========================================================================
                AddGame("BSX-001", "BS The Legend of Zelda", "Super Famicom Satellaview (BS-X)", "BS-X", "Приключения", "1995-08-06", "Nintendo EAD", "Nintendo / St.GIGA", "1.0", "Broadcast Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Satellaview/master/Named_Boxarts/BS%20Zelda%20no%20Densetsu%20(Japan).png", "Спутниковая 16-битная версия Zelda с живой голосовой озвучкой через радиоканал St.GIGA.", "1 игрок", "Для всех");
                AddGame("BSX-002", "BS F-Zero Grand Prix 2", "Super Famicom Satellaview (BS-X)", "BS-X", "Гонки", "1997-08-01", "Nintendo EAD", "Nintendo / St.GIGA", "1.0", "Broadcast Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Satellaview/master/Named_Boxarts/BS%20F-Zero%20Grand%20Prix%202%20(Japan).png", "Эксклюзивные спутниковые гоночные трассы и новые антигравитационные болиды.", "1 игрок", "Для всех");

                // =========================================================================
                // 8. Nintendo Virtual Boy (1995)
                // =========================================================================
                AddGame("VB-001", "Virtual Boy Wario Land", "Nintendo Virtual Boy", "VB", "Платформер", "1995-11-27", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Virtual_Boy/master/Named_Boxarts/Virtual%20Boy%20Wario%20Land%20(USA).png", "Лучшая игра платформы с прыжками между передним и задним планами в 3D.", "1 игрок", "Для всех");
                AddGame("VB-002", "Mario's Tennis", "Nintendo Virtual Boy", "VB", "Спорт", "1995-07-21", "Nintendo R&D1", "Nintendo", "1.0", "Pack-in Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Virtual_Boy/master/Named_Boxarts/Mario's%20Tennis%20(USA).png", "Стереоскопический 3D-теннис со звездами Марио-вселенной.", "1 игрок", "Для всех");
                AddGame("VB-003", "Jack Bros.", "Nintendo Virtual Boy", "VB", "Экшен / Megami Tensei", "1995-09-29", "Atlus", "Atlus", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Virtual_Boy/master/Named_Boxarts/Jack%20Bros.%20(USA).png", "Стильный лабиринтный экшен от Atlus с Джеком Фростом.", "1 игрок", "10+");

                // =========================================================================
                // 9. Nintendo 64 (1996)
                // =========================================================================
                AddGame("N64-001", "Super Mario 64", "Nintendo 64 / Nintendo 64DD", "N64", "3D Платформер", "1996-06-23", "Nintendo EAD", "Nintendo", "1.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Super%20Mario%2064%20(USA).png", "Революция трехмерного геймдизайна и свободного аналогового управления камерой.", "1 игрок", "Для всех");
                AddGame("N64-002", "The Legend of Zelda: Ocarina of Time", "Nintendo 64 / Nintendo 64DD", "N64", "Приключения / 3D Action", "1998-11-21", "Nintendo EAD", "Nintendo", "1.0", "Gold Collector's Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Ocarina%20of%20Time%20(USA).png", "Высокооцененный шедевр всех времен с механикой Z-таргетинга и песней времени.", "1 игрок", "10+");
                AddGame("N64-003", "The Legend of Zelda: Majora's Mask", "Nintendo 64 / Nintendo 64DD", "N64", "Приключения", "2000-04-27", "Nintendo EAD", "Nintendo", "1.0", "Expansion Pak Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Majora's%20Mask%20(USA).png", "Темная жемчужина N64 с 3-дневным циклом апокалипсиса и масками трансформации.", "1 игрок", "10+");
                AddGame("N64-004", "GoldenEye 007", "Nintendo 64 / Nintendo 64DD", "N64", "FPS Шутер", "1997-08-25", "Rare", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/GoldenEye%20007%20(USA).png", "Прорыв консольных шутеров от первого лица с легендарным сплит-скрином на 4 игрока.", "1-4 игрока", "13+");
                AddGame("N64-005", "Banjo-Kazooie", "Nintendo 64 / Nintendo 64DD", "N64", "3D Платформер", "1998-06-29", "Rare", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Banjo-Kazooie%20(USA).png", "Юмористическое приключение медведя и птицы от волшебников из студии Rare.", "1 игрок", "Для всех");
                AddGame("N64-006", "Super Smash Bros.", "Nintendo 64 / Nintendo 64DD", "N64", "Файтинг", "1999-01-21", "HAL Laboratory", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Super%20Smash%20Bros.%20(USA).png", "Рождение кроссовер-файтинга всех времен от Масахиро Сакураи.", "1-4 игрока", "Для всех");
                AddGame("N64-007", "Paper Mario", "Nintendo 64 / Nintendo 64DD", "N64", "RPG", "2000-08-11", "Intelligent Systems", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Paper%20Mario%20(USA).png", "Очаровательная бумажная RPG с динамичными тайминговыми атаками.", "1 игрок", "Для всех");
                AddGame("N64-008", "Star Fox 64", "Nintendo 64 / Nintendo 64DD", "N64", "Рельсовый шутер", "1997-04-27", "Nintendo EAD", "Nintendo", "1.0", "Rumble Pak Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Star%20Fox%2064%20(USA).png", "«Do a barrel roll!» — кинематографичный космический шутер с виброотдачей Rumble Pak.", "1-4 игрока", "Для всех");
                AddGame("N64-009", "Conker's Bad Fur Day", "Nintendo 64 / Nintendo 64DD", "N64", "Платформер / Черный юмор", "2001-03-05", "Rare", "Rare", "1.0", "Mature 17+ Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Conker's%20Bad%20Fur%20Day%20(USA).png", "Взрослая сатира с поющим Великим Могучим Дерьмом и пародиями на Голливуд.", "1-4 игрока", "18+");
                AddGame("N64-010", "Mario Kart 64", "Nintendo 64 / Nintendo 64DD", "N64", "Гонки", "1996-12-14", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/Mario%20Kart%2064%20(USA).png", "Первые 3D-гонки серии с синим колючим панцирем и легендарным мультиплеером на четверых.", "1-4 игрока", "Для всех");

                // =========================================================================
                // 10. Game Boy Color (1998)
                // =========================================================================
                AddGame("GBC-001", "Pokemon Gold and Silver", "Nintendo Game Boy Color", "GBC", "JRPG", "1999-11-21", "Game Freak", "Nintendo", "1.0", "Gold / Silver Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/Pokemon%20-%20Gold%20Version%20(USA,%20Europe).png", "Второе поколение покемонов с регионом Джото, часами реального времени и возвращением в Канто.", "1-2 игрока", "Для всех");
                AddGame("GBC-002", "Pokemon Crystal", "Nintendo Game Boy Color", "GBC", "JRPG", "2000-12-14", "Game Freak", "Nintendo", "1.0", "Crystal Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/Pokemon%20-%20Crystal%20Version%20(USA,%20Europe).png", "Первая игра серии с анимацией покемонов и возможностью выбора женского персонажа (Крис).", "1-2 игрока", "Для всех");
                AddGame("GBC-003", "The Legend of Zelda: Oracle of Ages & Seasons", "Nintendo Game Boy Color", "GBC", "Приключения", "2001-02-27", "Capcom (Flagship)", "Nintendo", "1.0", "Twin Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Oracle%20of%20Ages%20(USA).png", "Связанная дилогия приключений со сменой времен года и путешествиями во времени.", "1 игрок", "Для всех");
                AddGame("GBC-004", "Super Mario Bros. Deluxe", "Nintendo Game Boy Color", "GBC", "Платформер", "1999-05-10", "Nintendo R&D2", "Nintendo", "1.0", "Deluxe Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/Super%20Mario%20Bros.%20Deluxe%20(USA,%20Europe).png", "Цветной ремастер оригинального Mario с испытаниями, картой мира и The Lost Levels.", "1-2 игрока", "Для всех");
                AddGame("GBC-005", "Shantae", "Nintendo Game Boy Color", "GBC", "Метроидвания", "2002-06-02", "WayForward", "Capcom", "1.0", "Collector's Edition", "USA", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/Shantae%20(USA).png", "Графический шедевр на закате GBC с полуджинном Шантэ и танцами преображения.", "1 игрок", "Для всех");

                // =========================================================================
                // 11. Pokémon Mini (2001)
                // =========================================================================
                AddGame("PM-001", "Pokémon Party mini", "Nintendo Pokémon Mini", "PM", "Мини-игры", "2001-12-14", "Denyusha", "Nintendo", "1.0", "Pack-in Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Pokemon_Mini/master/Named_Boxarts/Pokemon%20Party%20mini%20(USA).png", "Набор динамичных мини-игр с Пикачу с использованием встроенного гироскопа.", "1-4 игрока", "Для всех");
                AddGame("PM-002", "Pokémon Pinball Mini", "Nintendo Pokémon Mini", "PM", "Пинбол", "2002-04-10", "Jupiter", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Pokemon_Mini/master/Named_Boxarts/Pokemon%20Pinball%20Mini%20(USA).png", "Компактный поке-пинбол с сотней столов и ловлей карманных монстров.", "1 игрок", "Для всех");

                // =========================================================================
                // 12. Game Boy Advance (2001)
                // =========================================================================
                AddGame("GBA-001", "Pokemon Emerald", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "JRPG", "2004-09-16", "Game Freak", "Nintendo", "1.0", "Emerald Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Pokemon%20-%20Emerald%20Version%20(USA,%20Europe).png", "Вершина третьего поколения покемонов в регионе Хоэнн с Боевым Рубежом (Battle Frontier).", "1-2 игрока", "Для всех");
                AddGame("GBA-002", "Pokemon FireRed & LeafGreen", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "JRPG", "2004-01-29", "Game Freak", "Nintendo", "1.0", "Wireless Adapter Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Pokemon%20-%20FireRed%20Version%20(USA,%20Europe).png", "Красочный 32-битный ремейк оригинальных приключений в Канто и на Островах Севии.", "1-2 игрока", "Для всех");
                AddGame("GBA-003", "The Legend of Zelda: The Minish Cap", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "Приключения", "2004-11-04", "Capcom (Flagship)", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20The%20Minish%20Cap%20(USA).png", "Линк уменьшается до размеров микроскопического народца Пикори с говорящей шляпой Эзло.", "1 игрок", "Для всех");
                AddGame("GBA-004", "Metroid Fusion", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "Метроидвания", "2002-11-17", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Metroid%20Fusion%20(USA).png", "Мрачный научно-фантастический хоррор, в котором Самус спасается от паразита SA-X.", "1 игрок", "10+");
                AddGame("GBA-005", "Metroid: Zero Mission", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "Метроидвания", "2004-02-09", "Nintendo R&D1", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Metroid%20-%20Zero%20Mission%20(USA).png", "Блестящий ремейк первой части с эпическим стелс-эпилогом без экзокостюма.", "1 игрок", "10+");
                AddGame("GBA-006", "Castlevania: Aria of Sorrow", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "Метроидвания", "2003-05-06", "Konami", "Konami", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Castlevania%20-%20Aria%20of%20Sorrow%20(USA).png", "Сома Круз поглощает души монстров в замке Дракулы во время солнечного затмения 2035 года.", "1 игрок", "10+");
                AddGame("GBA-007", "Golden Sun", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "JRPG", "2001-08-01", "Camelot", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Golden%20Sun%20(USA,%20Europe).png", "Шедевр портативных RPG с магией Псинергии и спутниками-Джиннами.", "1-2 игрока", "Для всех");
                AddGame("GBA-008", "Mother 3", "Nintendo Game Boy Advance / Game Boy micro", "GBA", "JRPG", "2006-04-20", "Brownie Brown / HAL Laboratory", "Nintendo", "1.0", "Special Edition", "JPN", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/Mother%203%20(Japan).png", "Глубокая эмоциональная история Лукаса и его семьи на Островах Нигде с ритмической боевой системой.", "1 игрок", "12+");

                // =========================================================================
                // 13. Nintendo GameCube (2001)
                // =========================================================================
                AddGame("GC-001", "Super Smash Bros. Melee", "Nintendo GameCube", "GC", "Файтинг", "2001-11-21", "HAL Laboratory", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Super%20Smash%20Bros.%20Melee%20(USA).png", "Молниеносный киберспортивный файтинг со сверхвысоким порогом мастерства.", "1-4 игрока", "10+");
                AddGame("GC-002", "The Legend of Zelda: The Wind Waker", "Nintendo GameCube", "GC", "Приключения", "2002-12-13", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20The%20Wind%20Waker%20(USA).png", "Морские странствия на говорящей лодке Красный Лев в неподражаемом сел-шейдинге.", "1 игрок", "Для всех");
                AddGame("GC-003", "The Legend of Zelda: Twilight Princess (GameCube)", "Nintendo GameCube", "GC", "Приключения", "2006-12-11", "Nintendo EAD", "Nintendo", "1.0", "Collector's Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Twilight%20Princess%20(USA).png", "Оригинальная версия с классическим управлением геймпадом и правильной ориентацией Линка-левши.", "1 игрок", "12+");
                AddGame("GC-004", "Metroid Prime", "Nintendo GameCube", "GC", "Action-Adventure / FPS", "2002-11-17", "Retro Studios", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Metroid%20Prime%20(USA).png", "Идеальный перенос классического Metroid в трехмерный мир от первого лица.", "1 игрок", "10+");
                AddGame("GC-005", "Super Mario Sunshine", "Nintendo GameCube", "GC", "3D Платформер", "2002-07-19", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Super%20Mario%20Sunshine%20(USA).png", "Тропические каникулы Марио на острове Дельфино с водяным ранцем FLUDD.", "1 игрок", "Для всех");
                AddGame("GC-006", "Resident Evil 4", "Nintendo GameCube", "GC", "Survival Horror", "2005-01-11", "Capcom Production Studio 4", "Capcom", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Resident%20Evil%204%20(USA).png", "Революция экшенов от третьего лица: Леон Кеннеди спасает дочь президента в испанской глуши.", "1 игрок", "18+");
                AddGame("GC-007", "Paper Mario: The Thousand-Year Door", "Nintendo GameCube", "GC", "RPG", "2004-07-22", "Intelligent Systems", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/Paper%20Mario%20-%20The%20Thousand-Year%20Door%20(USA).png", "Признанный абсолютный шедевр серии Paper Mario с театральной сценой и городом Разбойников.", "1 игрок", "Для всех");

                // =========================================================================
                // 14. Nintendo DS (2004)
                // =========================================================================
                AddGame("NDS-001", "Pokemon HeartGold and SoulSilver", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "JRPG", "2009-09-12", "Game Freak", "Nintendo", "1.0", "Special Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Pokemon%20-%20HeartGold%20Version%20(USA).png", "Эталонный ремейк золотой классики со спутниками-покемонами и шагомером Pokéwalker.", "1-2 игрока", "Для всех");
                AddGame("NDS-002", "New Super Mario Bros.", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "Платформер", "2006-05-15", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/New%20Super%20Mario%20Bros.%20(USA).png", "Возвращение 2D-Марио с мега-грибами и мультиплеером через сенсорный экран.", "1-4 игрока", "Для всех");
                AddGame("NDS-003", "Chrono Trigger DS", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "JRPG", "2008-11-20", "Square Enix", "Square Enix", "1.0", "Enhanced Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Chrono%20Trigger%20(USA).png", "Лучшая версия легендарной RPG с новыми подземельями и сенсорным управлением.", "1 игрок", "10+");
                AddGame("NDS-004", "The Legend of Zelda: Phantom Hourglass", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "Приключения", "2007-06-23", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Phantom%20Hourglass%20(USA).png", "Полное управление стилусом по океану и храму Морского Короля.", "1-2 игрока", "Для всех");
                AddGame("NDS-005", "Pokemon Platinum", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "JRPG", "2008-09-13", "Game Freak", "Nintendo", "1.0", "Platinum Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Pokemon%20-%20Platinum%20Version%20(USA).png", "Расширенная версия 4-го поколения с Гиратиной и Искаженным миром.", "1-4 игрока", "Для всех");
                AddGame("NDS-006", "Pokemon Black and White", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "JRPG", "2010-09-18", "Game Freak", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Pokemon%20-%20Black%20Version%20(USA).png", "Зрелая сюжетная линия с N и Командой Плазма в регионе Юнова.", "1-4 игрока", "Для всех");
                AddGame("NDS-007", "Mario Kart DS", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "Гонки", "2005-11-14", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Mario%20Kart%20DS%20(USA).png", "Дебют онлайн-мультиплеера Wi-Fi Connection и режим миссий.", "1-8 игроков", "Для всех");
                AddGame("NDS-008", "Castlevania: Order of Ecclesia", "Nintendo DS / Nintendo DS Lite / Nintendo DSi", "NDS", "Метроидвания", "2008-10-21", "Konami", "Konami", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/Castlevania%20-%20Order%20of%20Ecclesia%20(USA).png", "Шаноа поглощает глифы в мрачном хардкорном готическом приключении.", "1 игрок", "13+");

                // =========================================================================
                // 15. Nintendo Wii (2006)
                // =========================================================================
                AddGame("Wii-001", "Super Mario Galaxy", "Nintendo Wii", "Wii", "3D Платформер", "2007-11-01", "Nintendo EAD Tokyo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Super%20Mario%20Galaxy%20(USA).png", "Гравитационное космическое приключение с оркестровым саундтреком.", "1-2 игрока", "Для всех");
                AddGame("Wii-002", "Super Mario Galaxy 2", "Nintendo Wii", "Wii", "3D Платформер", "2010-05-23", "Nintendo EAD Tokyo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Super%20Mario%20Galaxy%202%20(USA).png", "Продолжение шедевра с космическим кораблем Марио, Йоши и новыми способностями.", "1-2 игрока", "Для всех");
                AddGame("Wii-003", "The Legend of Zelda: Twilight Princess", "Nintendo Wii", "Wii", "Приключения", "2006-11-19", "Nintendo EAD", "Nintendo", "1.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Twilight%20Princess%20(USA).png", "Мрачный Хайрул, Сумеречное царство, превращение в волка и управление мечом через Wiimote.", "1 игрок", "12+");
                AddGame("Wii-004", "The Legend of Zelda: Skyward Sword", "Nintendo Wii", "Wii", "Приключения / MotionPlus", "2011-11-18", "Nintendo EAD", "Nintendo", "1.0", "Gold Wiimote Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Skyward%20Sword%20(USA).png", "Начало хронологии Zelda с парящим городом Небоземь и 1:1 фехтованием мечом.", "1 игрок", "10+");
                AddGame("Wii-005", "Xenoblade Chronicles", "Nintendo Wii", "Wii", "JRPG", "2010-06-10", "Monolith Soft", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Xenoblade%20Chronicles%20(USA).png", "Грандиозный мир на телах титанов Биониса и Мехониса с клинком Монадо.", "1 игрок", "12+");
                AddGame("Wii-006", "Wii Sports Resort", "Nintendo Wii", "Wii", "Спорт / MotionPlus", "2009-06-25", "Nintendo EAD", "Nintendo", "1.0", "MotionPlus Bundle", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Wii%20Sports%20Resort%20(USA).png", "Высокоточный моушн-спорт на острове Вуху с фехтованием, стрельбой из лука и вейкбордингом.", "1-4 игрока", "Для всех");
                AddGame("Wii-007", "Super Smash Bros. Brawl", "Nintendo Wii", "Wii", "Файтинг", "2008-01-31", "Ad hoc Development / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Super%20Smash%20Bros.%20Brawl%20(USA).png", "Сюжетная кампания Subspace Emissary и дебют Соника и Снейка.", "1-4 игрока", "12+");
                AddGame("Wii-008", "Metroid Prime Trilogy", "Nintendo Wii", "Wii", "Action / FPS", "2009-08-24", "Retro Studios", "Nintendo", "1.0", "Collector's SteelBook Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Metroid%20Prime%20Trilogy%20(USA).png", "Все три части легендарной трилогии с идеальным прицеливанием Wiimote.", "1 игрок", "12+");
                AddGame("Wii-009", "Mario Kart Wii", "Nintendo Wii", "Wii", "Гонки / Моушн-руль", "2008-04-10", "Nintendo EAD", "Nintendo", "1.0", "Wii Wheel Bundle", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/Mario%20Kart%20Wii%20(USA).png", "Дебют мотоциклов, выполнение трюков в воздухе и гонки на 12 участников.", "1-12 игроков", "Для всех");

                // =========================================================================
                // 16. Nintendo 3DS (2011)
                // =========================================================================
                AddGame("0004000000140100", "Xenoblade Chronicles 3D", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG / Открытый мир", "2015-04-02", "Monster Games / Monolith Soft", "Nintendo", "1.1", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Xenoblade%20Chronicles%203D%20(USA).png", "Грандиозная JRPG эксклюзивно для New Nintendo 3DS на телах гигантских титанов с клинком Монадо.", "1 игрок", "12+");
                AddGame("0004000000030800", "Mario Kart 7", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Гонки", "2011-12-01", "Retro Studios / Nintendo", "Nintendo", "1.2", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Mario%20Kart%207%20(USA).png", "Полеты на дельтапланах и подводные гонки в настоящем автостереоскопическом 3D.", "1-8 игроков", "Для всех");
                AddGame("00040000000EC300", "Super Smash Bros. for Nintendo 3DS", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Файтинг", "2014-09-13", "Bandai Namco / Sora Ltd.", "Nintendo", "1.1.7", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Super%20Smash%20Bros.%20for%20Nintendo%203DS%20(USA).png", "Полноценный Smash Bros. в кармане со стабильными 60 кадрами в секунду.", "1-4 игрока", "10+");
                AddGame("00040000000EC400", "The Legend of Zelda: A Link Between Worlds", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Приключения", "2013-11-22", "Nintendo EAD", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20A%20Link%20Between%20Worlds%20(USA).png", "Линк превращается в рисунок на стенах для перемещения между Хайрулом и Лорулом.", "1 игрок", "Для всех");
                AddGame("0004000000055D00", "Pokemon X", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2013-10-12", "Game Freak", "Nintendo", "1.5", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20X%20(USA).png", "Революция перехода серии в полноценное 3D, регион Калос и Мега-эволюции покемонов.", "1-4 игрока", "Для всех");
                AddGame("0004000000055E00", "Pokemon Y", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2013-10-12", "Game Freak", "Nintendo", "1.5", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Y%20(USA).png", "Легендарный покемон Ивельтал, Мега-эволюции и приключения в регионе Калос.", "1-4 игрока", "Для всех");
                AddGame("000400000011C400", "Pokemon Omega Ruby", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2014-11-21", "Game Freak", "Nintendo", "1.4", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Omega%20Ruby%20(USA).png", "Грандиозный 3D-ремейк третьего поколения с Первобытным Гроудоном и полетами на Латиосе.", "1-4 игрока", "Для всех");
                AddGame("000400000011C500", "Pokemon Alpha Sapphire", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2014-11-21", "Game Freak", "Nintendo", "1.4", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Alpha%20Sapphire%20(USA).png", "Первобытный Кайогр, исследование глубин океана Хоэнна и Дельта-эпизод.", "1-4 игрока", "Для всех");
                AddGame("0004000000175E00", "Pokemon Sun", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2016-11-18", "Game Freak", "Nintendo", "1.2", "Sun Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Sun%20(USA).png", "Тропический регион Алола с Z-атаками, региональными формами и испытаниями островов.", "1-4 игрока", "Для всех");
                AddGame("0004000000175F00", "Pokemon Moon", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2016-11-18", "Game Freak", "Nintendo", "1.2", "Moon Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Moon%20(USA).png", "Ночной мир Алолы, Лунала, ультра-чудовища и Z-кристаллы.", "1-4 игрока", "Для всех");
                AddGame("00040000001B5000", "Pokemon Ultra Sun", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2017-11-17", "Game Freak", "Nintendo", "1.2", "Ultra Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Ultra%20Sun%20(USA).png", "Расширенная версия с Некрозмой, путешествиями через Ультра-червоточины и Командой Радужная Ракета.", "1-4 игрока", "Для всех");
                AddGame("00040000001B5100", "Pokemon Ultra Moon", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2017-11-17", "Game Freak", "Nintendo", "1.2", "Ultra Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Pokemon%20Ultra%20Moon%20(USA).png", "Ультра-приключение по спасению света во всей мультивселенной покемонов.", "1-4 игрока", "Для всех");
                AddGame("000400000008F100", "Animal Crossing: New Leaf", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Симулятор жизни", "2012-11-08", "Nintendo EAD", "Nintendo", "1.5", "Welcome amiibo Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Animal%20Crossing%20-%20New%20Leaf%20(USA).png", "Игрок становится мэром уютного городка с возможностью обустраивать улицы и дома.", "1-4 игрока", "Для всех");
                AddGame("000400000019C800", "Metroid: Samus Returns", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Метроидвания", "2017-09-15", "MercurySteam / Nintendo", "Nintendo", "1.0", "Special Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Metroid%20-%20Samus%20Returns%20(USA).png", "Шикарное переосмысление Metroid II с парированием ближнего боя и свободным прицеливанием 360°.", "1 игрок", "10+");
                AddGame("0004000000033400", "The Legend of Zelda: Ocarina of Time 3D", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Приключения", "2011-06-16", "Grezzo / Nintendo", "Nintendo", "1.0", "Master Quest Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Ocarina%20of%20Time%203D%20(USA).png", "Великолепный ремастер великой игры с гироскопическим прицеливанием и режимом Master Quest.", "1 игрок", "10+");
                AddGame("0004000000125600", "The Legend of Zelda: Majora's Mask 3D", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Приключения", "2015-02-13", "Grezzo / Nintendo", "Nintendo", "1.1", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20Majora's%20Mask%203D%20(USA).png", "Трехдневный цикл Термины с улучшенным журналом бомберов и новыми механиками боссов.", "1 игрок", "10+");
                AddGame("0004000000054000", "Super Mario 3D Land", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "3D Платформер", "2011-11-03", "Nintendo EAD Tokyo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Super%20Mario%203D%20Land%20(USA).png", "Идеальное сочетание механик 2D и 3D Марио с объемным стереоскопическим эффектом.", "1 игрок", "Для всех");
                AddGame("000400000007AF00", "New Super Mario Bros. 2", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Платформер", "2012-07-28", "Nintendo EAD", "Nintendo", "1.0", "Gold Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/New%20Super%20Mario%20Bros.%202%20(USA).png", "Охота за миллионом золотых монет с золотыми цветками и блоками.", "1-2 игрока", "Для всех");
                AddGame("0004000000076500", "Luigi's Mansion: Dark Moon", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Приключения / Экшен", "2013-03-20", "Next Level Games / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Luigi's%20Mansion%20-%20Dark%20Moon%20(USA).png", "Луиджи ловит призраков пылесосом Полтергаст 5000 в долине Эвершейд.", "1-4 игрока", "Для всех");
                AddGame("00040000000A0500", "Fire Emblem Awakening", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Тактическая RPG", "2012-04-19", "Intelligent Systems / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Fire%20Emblem%20-%20Awakening%20(USA).png", "Спасительница серии Fire Emblem с системой парных боев, браков и наследников.", "1-2 игрока", "12+");
                AddGame("000400000012DE00", "Fire Emblem Fates", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Тактическая RPG", "2015-06-25", "Intelligent Systems / Nintendo", "Nintendo", "1.1", "Special Edition (Birthright/Conquest/Revelation)", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Fire%20Emblem%20Fates%20-%20Special%20Edition%20(USA).png", "Три грандиозных пути выбора между родным королевством Хошидо и приемной семьей Нор.", "1-2 игрока", "12+");
                AddGame("00040000001B4000", "Fire Emblem Echoes: Shadows of Valentia", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Тактическая RPG", "2017-04-20", "Intelligent Systems / Nintendo", "Nintendo", "1.1", "Limited Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Fire%20Emblem%20Echoes%20-%20Shadows%20of%20Valentia%20(USA).png", "Ремейк Fire Emblem Gaiden с полной озвучкой и 3D-исследованием подземелий Альма и Селики.", "1 игрок", "12+");
                AddGame("0004000000078800", "Kid Icarus: Uprising", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Action / Шутер", "2012-03-22", "Project Sora / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Kid%20Icarus%20-%20Uprising%20(USA).png", "Динамичный экшен от Масахиро Сакураи с полетами ангела Пита и богиней Палютеной.", "1-6 игроков", "10+");
                AddGame("000400000008E300", "Donkey Kong Country Returns 3D", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Платформер", "2013-05-24", "Monster Games / Retro Studios", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Donkey%20Kong%20Country%20Returns%203D%20(USA).png", "Портативный 3D-платформер с Донки Конгом и Дидди, вагонетками и бочками.", "1-2 игрока", "Для всех");
                AddGame("0004000000186D00", "Kirby: Planet Robobot", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Платформер", "2016-04-28", "HAL Laboratory / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Kirby%20-%20Planet%20Robobot%20(USA).png", "Кирби пилотирует гигантского меха Robobot Armor, копируя способности врагов в механизированном мире.", "1-4 игрока", "Для всех");
                AddGame("000400000010E800", "Kirby: Triple Deluxe", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Платформер", "2014-01-11", "HAL Laboratory / Nintendo", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Kirby%20-%20Triple%20Deluxe%20(USA).png", "Гипернова-засасывание и перемещение между передним и задним планами 3D-экрана.", "1-4 игрока", "Для всех");
                AddGame("0004000000125500", "Monster Hunter 4 Ultimate", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "Экшен / RPG", "2014-10-11", "Capcom", "Capcom", "1.1", "Ultimate Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Monster%20Hunter%204%20Ultimate%20(USA).png", "Охота на исполинских монстров с вертикальным геймплеем и кооперативом онлайн.", "1-4 игрока", "13+");
                AddGame("00040000000FC500", "Bravely Default", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2012-10-11", "Silicon Studio / Square Enix", "Square Enix / Nintendo", "1.0", "Collector's Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Bravely%20Default%20(USA).png", "Духовная наследница классических Final Fantasy с уникальной боевой механикой Brave/Default.", "1 игрок", "12+");
                AddGame("00040000000E3400", "Shin Megami Tensei IV", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2013-05-23", "Atlus", "Atlus / Nintendo", "1.0", "Limited Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Shin%20Megami%20Tensei%20IV%20(USA).png", "Мрачный постапокалиптический Токио, переговоры с демонами и слияние персон.", "1 игрок", "17+");
                AddGame("0004000000161400", "Dragon Quest VIII: Journey of the Cursed King", "Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS", "3DS", "JRPG", "2015-08-27", "Square Enix / Level-5", "Square Enix", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/Dragon%20Quest%20VIII%20-%20Journey%20of%20the%20Cursed%20King%20(USA).png", "Шедевр JRPG с открытым миром, видимыми монстрами и оркестровым саундтреком.", "1 игрок", "12+");

                // =========================================================================
                // 17. Nintendo Wii U (2012)
                // =========================================================================
                AddGame("WiiU-001", "Super Mario 3D World", "Nintendo Wii U", "WiiU", "3D Платформер", "2013-11-21", "Nintendo EAD Tokyo", "Nintendo", "1.2", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Super%20Mario%203D%20World%20(USA).png", "Кооперативное приключение для 4 игроков с костюмом котика и прозрачными трубами.", "1-4 игрока", "Для всех");
                AddGame("WiiU-002", "Mario Kart 8", "Nintendo Wii U", "WiiU", "Гонки", "2014-05-29", "Nintendo EAD", "Nintendo", "4.1", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Mario%20Kart%208%20(USA).png", "Антигравитационные треки, графика в 60 FPS 1080p и живой оркестр.", "1-12 игроков", "Для всех");
                AddGame("WiiU-003", "Super Smash Bros. for Wii U", "Nintendo Wii U", "WiiU", "Файтинг", "2014-11-21", "Bandai Namco / Sora Ltd.", "Nintendo", "1.1.7", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Super%20Smash%20Bros.%20for%20Wii%20U%20(USA).png", "Грандиозные бои на 8 игроков на одной арене со всеми звездами видеоигр.", "1-8 игроков", "10+");
                AddGame("WiiU-004", "Xenoblade Chronicles X", "Nintendo Wii U", "WiiU", "Sci-Fi Action RPG", "2015-04-29", "Monolith Soft", "Nintendo", "1.0.2", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Xenoblade%20Chronicles%20X%20(USA).png", "Гигантская планета Мира, пилотируемые мехи Skell и бесшовный открытый мир.", "1-4 игрока", "12+");
                AddGame("WiiU-005", "Bayonetta 2", "Nintendo Wii U", "WiiU", "Слэшер / Экшен", "2014-09-20", "PlatinumGames", "Nintendo", "1.0", "Special Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Bayonetta%202%20(USA).png", "Ураганный экшен от PlatinumGames с ведьминым временем и боями в аду.", "1-2 игрока", "18+");
                AddGame("WiiU-006", "The Legend of Zelda: The Wind Waker HD", "Nintendo Wii U", "WiiU", "Приключения", "2013-09-20", "Nintendo EAD", "Nintendo", "1.0", "HD Remaster", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Legend%20of%20Zelda,%20The%20-%20The%20Wind%20Waker%20HD%20(USA).png", "Великолепный ремастер в 1080p со скоростным парусом и улучшенным квестом Трифорса.", "1 игрок", "Для всех");
                AddGame("WiiU-007", "Donkey Kong Country: Tropical Freeze", "Nintendo Wii U", "WiiU", "Платформер", "2014-02-13", "Retro Studios", "Nintendo", "1.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/Donkey%20Kong%20Country%20-%20Tropical%20Freeze%20(USA).png", "Непревзойденный 2D-платформер с потрясающей динамической музыкой Дэвида Уайза.", "1-2 игрока", "Для всех");

                // =========================================================================
                // 18. Nintendo Switch (2017)
                // =========================================================================
                AddGame("0100000000010000", "Super Mario Odyssey", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "3D Платформер", "2017-10-27", "Nintendo EPD", "Nintendo", "1.3.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Super%20Mario%20Odyssey.png", "Марио путешествует по планетам на корабле Одиссея с кепкой Кеппи, вселяясь в любые объекты и врагов.", "1-2 игрока", "Для всех");
                AddGame("01007EF00011E000", "The Legend of Zelda: Breath of the Wild", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Action-Adventure / Открытый мир", "2017-03-03", "Nintendo EPD", "Nintendo", "1.6.0", "Master Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/The%20Legend%20of%20Zelda%20Breath%20of%20the%20Wild.png", "Шедевр нового поколения с полной свободой исследований и законами физики.", "1 игрок", "12+");
                AddGame("0100F2C0115B6000", "The Legend of Zelda: Tears of the Kingdom", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Action-Adventure / Открытый мир", "2023-05-12", "Nintendo EPD", "Nintendo", "1.2.1", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/The%20Legend%20of%20Zelda%20Tears%20of%20the%20Kingdom.png", "Небесные острова, глубины Хайрула и безграничное творчество со способностями Автосборки и Комбинации.", "1 игрок", "12+");
                AddGame("01006F8002326000", "Animal Crossing: New Horizons", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Симулятор жизни", "2020-03-20", "Nintendo EPD", "Nintendo", "2.0.6", "Happy Home Paradise Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Animal%20Crossing%20New%20Horizons.png", "Обустройте собственный необитаемый райский остров с друзьями в реальном времени.", "1-8 игроков", "Для всех");
                AddGame("0100152000022000", "Mario Kart 8 Deluxe", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Гонки", "2017-04-28", "Nintendo EPD", "Nintendo", "3.0.3", "Booster Course Pass Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Mario%20Kart%208%20Deluxe.png", "Самая полная версия лучших гонок в мире с 96 трассами со всех частей серии.", "1-12 игроков", "Для всех");
                AddGame("01006A800016E000", "Super Smash Bros. Ultimate", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Файтинг", "2018-12-07", "Bandai Namco / Sora Ltd.", "Nintendo", "13.0.2", "Fighters Pass Vol. 2 Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Super%20Smash%20Bros%20Ultimate.png", "«Здесь абсолютно все!» — свыше 80 легендарных бойцов и сотен знаковых игровых арен.", "1-8 игроков", "12+");
                AddGame("01007300020FA000", "Metroid Dread", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "Метроидвания", "2021-10-08", "MercurySteam / Nintendo", "Nintendo", "2.1.0", "Special Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Metroid%20Dread.png", "Динамичный финал оригинальной 2D-саги Самус Аран и роботов Э.М.М.И. на планете ZDR.", "1 игрок", "12+");
                AddGame("0100D870045B6000", "Super Mario Bros. Wonder", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "2D Платформер", "2023-10-20", "Nintendo EPD", "Nintendo", "1.0.1", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Super%20Mario%20Bros%20Wonder.png", "Чудо-цветы, слоновый Марио и невероятные метаморфозы Цветочного королевства.", "1-4 игрока", "Для всех");
                AddGame("0100E95004038000", "Xenoblade Chronicles 2", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "JRPG", "2017-12-01", "Monolith Soft", "Nintendo", "2.1.0", "Torna - The Golden Country", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Xenoblade%20Chronicles%202.png", "Рекс и Пайра ищут легендарный Элизиум на вершине Мирового древа в облачном море Альреста.", "1 игрок", "12+");
                AddGame("010074600E900000", "Xenoblade Chronicles 3", "Nintendo Switch / Nintendo Switch Lite / OLED", "Switch", "JRPG", "2022-07-29", "Monolith Soft", "Nintendo", "2.2.0", "Future Redeemed Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Xenoblade%20Chronicles%203.png", "Шесть солдат из враждующих наций Кевис и Агнус объединяются ради разорванного мира Айониос.", "1 игрок", "16+");

                // =========================================================================
                // 19. Nintendo Switch 2 (2025)
                // =========================================================================
                AddGame("0100999000000001", "Mario Kart Next", "Nintendo Switch 2", "Switch 2", "Гонки", "2025-10-15", "Nintendo EPD", "Nintendo", "1.0.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Mario%20Kart%208%20Deluxe.png", "Новейшее поколение гоночной саги Mario Kart с поддержкой 4K 60FPS и новыми динамическими треками.", "1-12 игроков", "Для всех");
                AddGame("0100999000000002", "Metroid Prime 4: Beyond (Switch 2 Edition)", "Nintendo Switch 2", "Switch 2", "Action-Adventure / FPS", "2025-11-20", "Retro Studios", "Nintendo", "1.0.0", "Enhanced Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Metroid%20Dread.png", "Грандиозное возвращение Самус Аран и охотника Силукса в сверхвысоком разрешении с трассировкой лучей.", "1 игрок", "12+");
                AddGame("0100999000000003", "The Legend of Zelda: Tears of the Kingdom (Switch 2 Edition)", "Nintendo Switch 2", "Switch 2", "Action-Adventure / Открытый мир", "2025-06-01", "Nintendo EPD", "Nintendo", "2.0.0", "Next-Gen Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/The%20Legend%20of%20Zelda%20Tears%20of%20the%20Kingdom.png", "Обновленная версия шедевра с HDR, мгновенными загрузками и поддержкой 60 кадров в секунду.", "1 игрок", "12+");
                AddGame("0100999000000004", "Super Mario Universe", "Nintendo Switch 2", "Switch 2", "3D Платформер", "2025-12-05", "Nintendo EPD Tokyo", "Nintendo", "1.0.0", "Launch Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Super%20Mario%20Odyssey.png", "Новое грандиозное трехмерное приключение Марио в бесшовных космических мирах.", "1-2 игрока", "Для всех");
                AddGame("0100999000000005", "Pokemon Legends: Z-A (Switch 2 Edition)", "Nintendo Switch 2", "Switch 2", "Action RPG", "2025-09-10", "Game Freak", "Nintendo", "1.0.0", "Enhanced Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Pokemon%20Legends%20Arceus.png", "План градостроительного обновления города Люмиос-Сити в регионе Калос в высоком разрешении.", "1-2 игрока", "Для всех");
                AddGame("0100999000000006", "Xenoblade Chronicles 4", "Nintendo Switch 2", "Switch 2", "JRPG / Открытый мир", "2026-03-20", "Monolith Soft", "Nintendo", "1.0.0", "Standard Edition", "WW", "https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/Xenoblade%20Chronicles%203.png", "Новая эпоха вселенной Ксеноблейд на некст-ген движке Monolith Soft.", "1 игрок", "12+");

                _isInitialized = true;
            }
        }

        private void AddGame(string id, string title, string system, string sysShort, string genre, string date, string dev, string pub, string ver, string edition, string region, string cover, string desc, string players, string rating)
        {
            string formattedDate = FormatDate(date);
            string formattedVer = FormatVersion(ver);
            string resolvedCover = CoverCacheService.ResolveCoverUrl(cover, system, title, id);

            var entry = new NintendoGameEntry
            {
                Id = id,
                Title = title,
                System = system,
                SystemShort = sysShort,
                Genre = genre,
                ReleaseDate = formattedDate,
                Developer = dev,
                Publisher = pub,
                Version = formattedVer,
                Edition = edition,
                Region = region,
                CoverUrl = resolvedCover,
                Description = desc,
                Players = players,
                Rating = rating
            };

            _database.Add(entry);
            _loadedIds.Add(id);
            IndexGameBySystem(entry);
        }

        public int GetGameCountForSystem(string systemFullName)
        {
            lock (_dbLock)
            {
                if (string.IsNullOrEmpty(systemFullName) || systemFullName == "Все системы")
                    return _database.Count;

                if (_bySystem.TryGetValue(systemFullName, out var list))
                    return list.Count;

                return _database.Count(g => g.System.Equals(systemFullName, StringComparison.OrdinalIgnoreCase) ||
                                           g.SystemShort.Equals(systemFullName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public List<NintendoGameEntry> QueryGames(
            string? systemFullName = null,
            string? genre = null,
            string? developer = null,
            string? publisher = null,
            string? searchQuery = null,
            string? sortBy = "Title")
        {
            List<NintendoGameEntry> source;

            lock (_dbLock)
            {
                if (!string.IsNullOrEmpty(systemFullName) && systemFullName != "Все системы")
                {
                    if (_bySystem.TryGetValue(systemFullName, out var sysList))
                    {
                        source = new List<NintendoGameEntry>(sysList);
                    }
                    else
                    {
                        source = _database.Where(g => g.System.Equals(systemFullName, StringComparison.OrdinalIgnoreCase) ||
                                                      g.SystemShort.Equals(systemFullName, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }
                else
                {
                    source = new List<NintendoGameEntry>(_database);
                }
            }

            IEnumerable<NintendoGameEntry> result = source;

            if (!string.IsNullOrEmpty(genre) && genre != "Все жанры")
            {
                result = result.Where(g => g.Genre.Contains(genre, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(developer) && developer != "Все разработчики")
            {
                result = result.Where(g => g.Developer.Equals(developer, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(publisher) && publisher != "Все издатели")
            {
                result = result.Where(g => g.Publisher.Equals(publisher, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string[] words = searchQuery.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                result = result.Where(g =>
                    words.All(w =>
                        g.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Id.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Developer.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Publisher.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Genre.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Description.Contains(w, StringComparison.OrdinalIgnoreCase)));
            }

            result = sortBy switch
            {
                "ReleaseDate" => result.OrderByDescending(g => g.ReleaseDate),
                "System" => result.OrderBy(g => g.System).ThenBy(g => g.Title),
                _ => result.OrderBy(g => g.Title)
            };

            return result.ToList();
        }

        public List<string> GetDistinctGenres(string? systemFullName = null)
        {
            string key = systemFullName ?? "Все системы";
            lock (_cachedGenres)
            {
                if (_cachedGenres.TryGetValue(key, out var cached))
                    return cached;
            }

            List<NintendoGameEntry> source;
            lock (_dbLock)
            {
                if (!string.IsNullOrEmpty(systemFullName) && systemFullName != "Все системы" && _bySystem.TryGetValue(systemFullName, out var sysList))
                {
                    source = sysList;
                }
                else
                {
                    source = _database;
                }
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("Все жанры");
            foreach (var g in source)
            {
                var parts = g.Genre.Split('/', ',');
                foreach (var p in parts)
                {
                    string trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) set.Add(trimmed);
                }
            }

            var res = set.OrderBy(x => x == "Все жанры" ? "" : x).ToList();
            lock (_cachedGenres)
            {
                _cachedGenres[key] = res;
            }
            return res;
        }

        public List<string> GetDistinctDevelopers(string? systemFullName = null)
        {
            string key = systemFullName ?? "Все системы";
            lock (_cachedDevelopers)
            {
                if (_cachedDevelopers.TryGetValue(key, out var cached))
                    return cached;
            }

            List<NintendoGameEntry> source;
            lock (_dbLock)
            {
                if (!string.IsNullOrEmpty(systemFullName) && systemFullName != "Все системы" && _bySystem.TryGetValue(systemFullName, out var sysList))
                {
                    source = sysList;
                }
                else
                {
                    source = _database;
                }
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("Все разработчики");
            foreach (var g in source)
            {
                if (!string.IsNullOrEmpty(g.Developer)) set.Add(g.Developer);
            }

            var res = set.OrderBy(x => x == "Все разработчики" ? "" : x).ToList();
            lock (_cachedDevelopers)
            {
                _cachedDevelopers[key] = res;
            }
            return res;
        }

        public List<string> GetDistinctPublishers(string? systemFullName = null)
        {
            string key = systemFullName ?? "Все системы";
            lock (_cachedPublishers)
            {
                if (_cachedPublishers.TryGetValue(key, out var cached))
                    return cached;
            }

            List<NintendoGameEntry> source;
            lock (_dbLock)
            {
                if (!string.IsNullOrEmpty(systemFullName) && systemFullName != "Все системы" && _bySystem.TryGetValue(systemFullName, out var sysList))
                {
                    source = sysList;
                }
                else
                {
                    source = _database;
                }
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add("Все издатели");
            foreach (var g in source)
            {
                if (!string.IsNullOrEmpty(g.Publisher)) set.Add(g.Publisher);
            }

            var res = set.OrderBy(x => x == "Все издатели" ? "" : x).ToList();
            lock (_cachedPublishers)
            {
                _cachedPublishers[key] = res;
            }
            return res;
        }

        public List<NintendoGameEntry> Search3dsGames(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<NintendoGameEntry>();

            string[] words = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            lock (_dbLock)
            {
                return _database.Where(g => g.SystemShort == "3DS" && (
                    words.All(w =>
                        g.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Id.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Publisher.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Developer.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                        g.Genre.Contains(w, StringComparison.OrdinalIgnoreCase))
                )).ToList();
            }
        }
    }
}
