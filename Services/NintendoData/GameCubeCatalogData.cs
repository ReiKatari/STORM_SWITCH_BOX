using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class GameCubeCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(400);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Smash Bros. Melee", "Файтинг / Киберспорт", 2001, "HAL Laboratory", "Nintendo", "WW", "Легендарный соревновательный файтинг с невероятной скоростью, техниками волн-дэш (Wavedash) и L-Cancel."),
                ("The Legend of Zelda: The Wind Waker", "Action-Adventure", 2002, "Nintendo EAD", "Nintendo", "WW", "Сел-шейдинговая сказка на Великом море: парусная лодка Король Красных Львов, жезл Ветров и океанские острова."),
                ("The Legend of Zelda: Twilight Princess", "Action-Adventure", 2006, "Nintendo EAD", "Nintendo", "WW", "Мрачный шедевр: Линк превращается в волка в сумеречном царстве с компаньонкой Мидной."),
                ("Super Mario Sunshine", "3D Платформер", 2002, "Nintendo EAD", "Nintendo", "WW", "Марио на тропическом острове Дельфино с водяным ранцем FLUDD, скольжением по воде и очисткой граффити."),
                ("Metroid Prime", "Action-Adventure / FPS от первого лица", 2002, "Retro Studios", "Nintendo", "WW", "Шедевр перевода 2D-метроидвании в 3D: планета Таллон IV, сканирующий визор и пушка Самус Аран."),
                ("Metroid Prime 2: Echoes", "Action-Adventure / FPS", 2004, "Retro Studios", "Nintendo", "WW", "Планета Эфир, разделенная на Светлый и Темный миры, Темная Самус и светлый/темный лучи."),
                ("Mario Kart: Double Dash!!", "Гонки", 2003, "Nintendo EAD", "Nintendo", "WW", "Два персонажа на одном карте (водитель и метатель предметов), эксклюзивные спец-предметы и сменные роли."),
                ("Luigi's Mansion", "Приключения / Экшен", 2001, "Nintendo EAD", "Nintendo", "WW", "Луиджи с пылесосом Poltergust 3000 и фонариком исследует особняк с призраками ради спасения Марио."),
                ("Paper Mario: The Thousand-Year Door", "JRPG", 2004, "Intelligent Systems", "Nintendo", "WW", "Бумажный Марио в портовом городе Трущобвилль ищет 7 Кристальных Звезд и открывает Тысячелетнюю Дверь."),
                ("F-Zero GX", "Футуристические гонки", 2003, "Amusement Vision (Sega)", "Nintendo", "WW", "Сверхзвуковые 60 FPS гонки со скоростью свыше 1000 км/ч, сюжетный режим Капитана Фэлкона и конструктор болидов."),
                ("Pikmin", "Стратегия в реальном времени", 2001, "Nintendo EAD", "Nintendo", "WW", "Капитан Олимар терпит крушение на неизведанной планете и командует армиями цветных пикминов."),
                ("Pikmin 2", "RTS / Исследование подземелий", 2004, "Nintendo EAD", "Nintendo", "WW", "Олимар и Луи исследуют глубокие подземелья, собирая сокровища с фиолетовыми и белыми пикминами без таймера."),
                ("Resident Evil 4", "Survival Horror / Экшен", 2005, "Capcom Production Studio 4", "Capcom", "WW", "Революция экшенов с камерой из-за плеча: Леон С. Кеннеди спасает дочь президента Эшли в испанской деревне."),
                ("Resident Evil (Remake)", "Survival Horror", 2002, "Capcom", "Capcom", "WW", "Великолепный эталонный ремейк с фотореалистичными задниками особняка Спенсера и Багровыми Головами."),
                ("Resident Evil Zero", "Survival Horror", 2002, "Capcom", "Capcom", "WW", "Ребекка Чемберс и беглый заключенный Билли Коэн в поезде Express с переключением персонажей на лету."),
                ("Star Wars Rogue Squadron II: Rogue Leader", "3D Космосимулятор", 2001, "Factor 5", "LucasArts", "WW", "Графическое чудо старта GameCube: битва у Звезды Смерти и каньоны планеты Татуин."),
                ("Star Wars Rogue Squadron III: Rebel Strike", "3D Авиасимулятор / Наземные бои", 2003, "Factor 5", "LucasArts", "WW", "Битва на ледяной планете Хот и полный кооператив кампании Rogue Leader на 2 игроков."),
                ("Animal Crossing", "Симулятор жизни", 2001, "Nintendo EAD", "Nintendo", "WW", "Жизнь в уютной деревне с животными в реальном времени с часами и календарем, рыбалка и выплаты Тому Нуку."),
                ("Eternal Darkness: Sanity's Requiem", "Психологический хоррор", 2002, "Silicon Knights", "Nintendo", "WW", "Эпическая сага сквозь 2000 лет истории с механикой потери рассудка Sanity Effects, ломающей четвертую стену."),
                ("Fire Emblem: Path of Radiance", "Тактическая RPG", 2005, "Intelligent Systems", "Nintendo", "WW", "Айк и наемники Грейла в войне против короля Дейна Ашнара с расами лагузов-оборотней."),
                ("Tales of Symphonia", "JRPG", 2003, "Namco Tales Studio", "Namco", "WW", "Ллойд Ирвинг и Колетт в путешествии возрождения мира Сильверант с 3D-боевой системой Multi-Line LMBS."),
                ("Baten Kaitos: Eternal Wings and the Lost Ocean", "JRPG / Карточная боевка", 2003, "Monolith Soft / tri-Crescendo", "Namco", "WW", "Калас и дух-хранитель в парящих небесных островах с глубокой карточной боевой системой Магнус."),
                ("Baten Kaitos Origins", "JRPG", 2006, "Monolith Soft / tri-Crescendo", "Nintendo", "WW", "Предыстория империи Альфаро и спиритического воина Саги от Monolith Soft."),
                ("Skies of Arcadia Legends", "JRPG", 2002, "Overworks (Sega)", "Sega", "WW", "Воздушные пираты Вайс и Айка бороздят небеса на летающих парусниках с боями кораблей."),
                ("TimeSplitters 2", "FPS Шутер", 2002, "Free Radical Design", "Eidos", "WW", "Путешествия сквозь века от создателей GoldenEye 007 с кооперативом и редактором карт."),
                ("Viewtiful Joe", "2D Экшен со стилем комиксов", 2003, "Clover Studio (Capcom)", "Capcom", "WW", "Супергерой Джо использует режимы кинокамеры Slow, Mach Speed и Zoom от Хидеки Камии."),
                ("Viewtiful Joe 2", "2D Экшен", 2004, "Clover Studio", "Capcom", "WW", "Джо и Секси Сильвия спасают кинофильмы с 7 Радужными Оскарами."),
                ("Beyond Good & Evil", "Action-Adventure", 2003, "Ubisoft Pictures", "Ubisoft", "WW", "Фотожурналистка Джейд и кабан Пей'дж разоблачают заговор инопланетян Думс на планете Хиллис."),
                ("Prince of Persia: The Sands of Time", "Action-Adventure / Акробатика", 2003, "Ubisoft Montreal", "Ubisoft", "WW", "Принц Персии бегает по стенам и отматывает время назад с Кинжалом Времени."),
                ("Metal Gear Solid: The Twin Snakes", "Стелс-экшен", 2004, "Silicon Knights / Konami", "Konami", "WW", "Кинематографичный ремейк оригинальной MGS1 на движке MGS2 с режиссурой Рюхэя Китамуры."),
                ("Soulcalibur II", "3D Файтинг", 2003, "Project Soul", "Namco", "WW", "Эксклюзивный боец версии GameCube — Линк с мечом Master Sword, луком и бомбами.")
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
