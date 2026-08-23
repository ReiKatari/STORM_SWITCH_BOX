using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class GbaCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(400);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Pokemon Ruby Version", "JRPG", 2002, "Game Freak", "Nintendo", "WW", "Третье поколение в тропическом регионе Хоэнн: Граудон, командные битвы 2х2, базы Secret Bases и конкурсы покемонов."),
                ("Pokemon Sapphire Version", "JRPG", 2002, "Game Freak", "Nintendo", "WW", "Владыка океана Кайогр, команда Аква и погружения под воду с механикой Dive."),
                ("Pokemon Emerald Version", "JRPG", 2004, "Game Freak", "Nintendo", "WW", "Ультимативная версия с Рэйквазой, укрощением двух легенд и непревзойденной боевой зоной Battle Frontier."),
                ("Pokemon FireRed Version", "JRPG", 2004, "Game Freak", "Nintendo", "WW", "Ремейк оригинального Канто с островами Sevii Islands, беспроводным адаптером Wireless Adapter и современной графикой."),
                ("Pokemon LeafGreen Version", "JRPG", 2004, "Game Freak", "Nintendo", "WW", "Ремейк классики с Венузавром и исследованием островов архипелага Севии."),
                ("The Legend of Zelda: The Minish Cap", "Action-Adventure", 2004, "Capcom (Flagship)", "Nintendo", "WW", "Линк и говорящая птичья шапка Эзло уменьшаются до размера песчинки в мире крошечных пиклов."),
                ("The Legend of Zelda: A Link to the Past & Four Swords", "Action-Adventure", 2002, "Nintendo / Capcom", "Nintendo", "WW", "Классический шедевр плюс первое в истории кооперативное приключение на 4 Линков с кабелем Link Cable."),
                ("Metroid Fusion (Metroid 4)", "Метроидвания", 2002, "Nintendo R&D1", "Nintendo", "WW", "Самус Аран в органическом костюме Fusion Suit на зараженной паразитами X станции B.S.L. спасается от смертоносного клона SA-X."),
                ("Metroid: Zero Mission", "Метроидвания", 2004, "Nintendo R&D1", "Nintendo", "WW", "Эталонный ремейк первой Metroid с новой стелс-главой за Самус в синем комбинезоне Zero Suit."),
                ("Castlevania: Aria of Sorrow", "Метроидвания", 2003, "Konami (KCET)", "Konami", "WW", "Сома Крус в замке Дракулы внутри солнечного затмения 2035 года с поглощением душ врагов Tactical Soul."),
                ("Castlevania: Circle of the Moon", "Метроидвания", 2001, "Konami (KCEK)", "Konami", "WW", "Натан Грейвз с кнутом Hunter Whip и карточной комбинационной системой магии DSS."),
                ("Castlevania: Harmony of Dissonance", "Метроидвания", 2002, "Konami (KCET)", "Konami", "WW", "Жюст Бельмонт с магическими книгами заклинаний исследует раздвоенный замок Дракулы."),
                ("Golden Sun", "JRPG", 2001, "Camelot", "Nintendo", "WW", "Исаак и адепты стихий спасают мир Вейярд от алхимии с магией Псинергии и духами Джиннами."),
                ("Golden Sun: The Lost Age", "JRPG", 2002, "Camelot", "Nintendo", "WW", "Продолжение саги от лица Феликса с кораблем, новыми классами магии и переносом всех данных паролем."),
                ("Advance Wars", "Пошаговая тактика", 2001, "Intelligent Systems", "Nintendo", "WW", "Командующие Энди, Сэми и Макс ведут танки, вертолеты и артиллерию на поле боя с суперсилами CO Powers."),
                ("Advance Wars 2: Black Hole Rising", "Пошаговая тактика", 2003, "Intelligent Systems", "Nintendo", "WW", "Оборона 4 континентов от армии Черной Дыры Штурма с суперсилами Super CO Powers."),
                ("Fire Emblem: The Blazing Blade (Fire Emblem)", "Тактическая RPG", 2003, "Intelligent Systems", "Nintendo", "WW", "Первый западный релиз серии: Лин, Эливуд и Гектор на континенте Элиб с треугольником оружия."),
                ("Fire Emblem: The Sacred Stones", "Тактическая RPG", 2004, "Intelligent Systems", "Nintendo", "WW", "Принц Эфраим и принцесса Эрика против армий нежити с разветвленной прокачкой классов."),
                ("Fire Emblem: The Binding Blade", "Тактическая RPG", 2002, "Intelligent Systems", "Nintendo", "JPN", "История принца Роя (звезды Super Smash Bros. Melee) в войне против короля Берна Зефиэля."),
                ("Mario & Luigi: Superstar Saga", "JRPG / Комедия", 2003, "AlphaDream", "Nintendo", "WW", "Марио и Луиджи отправляются в Бобовое королевство возвращать украденный голос принцессы Пич."),
                ("Super Mario Advance 4: Super Mario Bros. 3", "Платформер", 2003, "Nintendo EAD", "Nintendo", "WW", "Идеальная версия SMB3 с голосом Марио и 38 бонусными эксклюзивными уровнями e-Reader."),
                ("Mother 3", "JRPG", 2006, "Brownie Brown / HAL", "Nintendo", "JPN", "Шедевр Сигэсато Итои о Лукасе, Клаусе и собаке Бонго на Тасманийских островах с ритмичной боевой системой."),
                ("Mega Man Battle Network 1-6", "Action-RPG / Карточная тактика", 2001, "Capcom", "Capcom", "WW", "Лан Хикари и сетевой навигатор MegaMan.EXE в киберпространстве с боевыми чипами на сетке 3х3."),
                ("Mega Man Zero 1-4", "Экшен / Платформер", 2002, "Inti Creates", "Capcom", "WW", "Легендарный мечник Зеро с Z-мечом, кибер-эльфами и рейтинговой системой рангов."),
                ("Sonic Advance 1-3", "Сверхскоростной платформер", 2001, "Dimps / Sonic Team", "Sega", "WW", "Классический Соник, Тейлз, Наклз и Эми со связками персонажей."),
                ("Final Fantasy Tactics Advance", "Тактическая RPG", 2003, "Square Enix", "Square Enix", "WW", "Марш попадает в сказочную страну Ивалис с правилами судьи Судейской системы (Judge System)."),
                ("Kingdom Hearts: Chain of Memories", "Action-RPG / Карточная боевка", 2004, "Square Enix / Jupiter", "Square Enix", "WW", "Сора и Дональд в Замке Забвения (Castle Oblivion) с карточной колодой атак Организации XIII."),
                ("WarioWare, Inc.: Mega Microgame$!", "Сборник микро-игр", 2003, "Nintendo R&D1", "Nintendo", "WW", "Сотни безумных 5-секундных мини-игр от компании Варио."),
                ("WarioWare: Twisted!", "Микро-игры с гироскопом", 2004, "Nintendo SPD / Intelligent", "Nintendo", "WW", "Картридж со встроенным физическим гироскопом и тактильной отдачей."),
                ("Rhythm Tengoku", "Музыкальная ритм-игра", 2006, "Nintendo SPD", "Nintendo", "JPN", "Основоположник серии ритм-игр от продюсера Цунку с непревзойденным чувством ритма.")
            };

            foreach (var g in games)
            {
                list.Add(new NintendoCatalogItem
                {
                    Title = g.Title,
                    Genre = g.Genre,
                    Year = g.Year,
                    Developer = g.Dev,
                    Publisher = g.Pub,
                    Region = g.Region,
                    Desc = g.Desc
                });
            }

            return list;
        }
    }
}
