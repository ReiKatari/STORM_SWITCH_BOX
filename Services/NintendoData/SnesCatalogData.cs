using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class SnesCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(500);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Mario World", "2D Платформер", 1990, "Nintendo EAD", "Nintendo", "WW", "Легендарный шедевр Сигэру Миямото, появление динозаврика Йоши и 96 выходов с уровней Динозавьего острова."),
                ("Super Mario World 2: Yoshi's Island", "2D Платформер", 1995, "Nintendo EAD", "Nintendo", "WW", "Пастельная сказка с чипом Super FX 2, яйцами динозавра Йоши и спасением малыша Марио."),
                ("Super Mario All-Stars", "Сборник ремейков", 1993, "Nintendo EAD", "Nintendo", "WW", "Великолепные 16-битные ремейки всех четырех частей Super Mario Bros. 1, 2, 3 и The Lost Levels."),
                ("Super Mario RPG: Legend of the Seven Stars", "JRPG", 1996, "Square", "Nintendo", "WW", "Грандиозный союз Square и Nintendo: Марио, Джено, Маллоу и Боузер против Смити."),
                ("The Legend of Zelda: A Link to the Past", "Action-Adventure", 1991, "Nintendo EAD", "Nintendo", "WW", "Эталон жанра Action-Adventure с параллельными мирами Света и Тьмы, мечом Master Sword и зеркалом."),
                ("Super Metroid", "Метроидвания", 1994, "Nintendo R&D1 / Intelligent Systems", "Nintendo", "WW", "Один из величайших шедевров видеоигр всех времен: Самус Аран в мрачных глубинах планеты Зебес."),
                ("Donkey Kong Country", "2D Платформер", 1994, "Rare", "Nintendo", "WW", "Технологическая революция предрендеренной 3D-графики от Rare, саундтрек Дэвида Уайза и дуэт Донки и Дидди."),
                ("Donkey Kong Country 2: Diddy's Kong Quest", "2D Платформер", 1995, "Rare", "Nintendo", "WW", "Абсолютная вершина 16-битных платформеров: Дидди и Дикси спасают Донки Конга от капитана К. Руля."),
                ("Donkey Kong Country 3: Dixie Kong's Double Trouble!", "2D Платформер", 1996, "Rare", "Nintendo", "WW", "Приключения Дикси и малыша Кидди в Северном Кремсфере с моторными лодками и ховеркрафтами."),
                ("Chrono Trigger", "JRPG", 1995, "Square", "Square", "WW", "Шедевр «Команды мечты» (Сакагути, Хории, Торияма, Мицуда, Уэмацу) с путешествиями во времени и 13 концовками."),
                ("Final Fantasy VI (Final Fantasy III)", "JRPG", 1994, "Square", "Square", "WW", "Эпическая стимпанк-драма с Террой, Локком, Кефкой Палаццо, сценой в Опере и уничтожением мира."),
                ("Final Fantasy IV (Final Fantasy II)", "JRPG", 1991, "Square", "Square", "WW", "Темный рыцарь Сесил ищет искупление и становится паладином в битве на Луне с Active Time Battle."),
                ("Final Fantasy V", "JRPG", 1992, "Square", "Square", "JPN", "Глубочайшая система профессий (Job System) и приключения Барца против колдуна Эксдеса."),
                ("EarthBound (Mother 2)", "JRPG", 1994, "Ape / HAL Laboratory", "Nintendo", "WW", "Культовая постмодернистская ролевая сказка Сигэсато Итои с Нессом, битами, НЛО и звукозаписывающим камнем."),
                ("Secret of Mana (Seiken Densetsu 2)", "Action-RPG", 1993, "Square", "Square", "WW", "Кооперативное приключение на 3 игроков с кольцевым меню, мечом Маны и дракончиком Фламми."),
                ("Trials of Mana (Seiken Densetsu 3)", "Action-RPG", 1995, "Square", "Square", "JPN", "Шесть уникальных персонажей, система смены классов на светлый/темный и ветвящийся сюжет."),
                ("Mega Man X", "Экшен / Платформер", 1993, "Capcom", "Capcom", "WW", "Революция серии: рывки, карабкание по стенам, капсулы апгрейдов брони Лайта и союзник Зеро."),
                ("Mega Man X2", "Экшен / Платформер", 1994, "Capcom", "Capcom", "WW", "Чип Cx4 для полигональной 3D-проволочной графики, мотоциклы и сбор частей тела Зеро."),
                ("Mega Man X3", "Экшен / Платформер", 1995, "Capcom", "Capcom", "WW", "Возможность играть за Зеро с его световым Z-мечом, четыре чипа усилений и золотая броня."),
                ("Mega Man 7", "Экшен / Платформер", 1995, "Capcom", "Capcom", "WW", "16-битный дебют классического Мегамена с соперником Бассом и волком Треблом."),
                ("Castlevania: Rondo of Blood (Dracula X)", "Экшен / Платформер", 1995, "Konami", "Konami", "WW", "Рихтер Бельмонт спасает возлюбленную Аннетт из замка Дракулы с суперударами Item Crash."),
                ("Super Castlevania IV", "Экшен / Платформер", 1991, "Konami", "Konami", "WW", "Саймон Бельмонт бьет кнутом во всех 8 направлениях с использованием вращений Mode 7."),
                ("Contra III: The Alien Wars (Super Probotector)", "Run and Gun", 1992, "Konami", "Konami", "WW", "Ураганный боевик: стрельба из двух пулеметов, ракеты на лету, бомбы и уровни с вращением Mode 7."),
                ("Super Mario Kart", "Гонки", 1992, "Nintendo EAD", "Nintendo", "WW", "Рождение жанра аркадных картинговых гонок с использованием технологии масштабирования Mode 7."),
                ("F-Zero", "Гонки", 1990, "Nintendo EAD", "Nintendo", "WW", "Сверхзвуковые футуристические гонки с Капитаном Фэлконом и болидом Blue Falcon со скоростью свыше 400 км/ч."),
                ("Star Fox (Starwing)", "3D Космосимулятор", 1993, "Nintendo EAD / Argonaut", "Nintendo", "WW", "Первая в мире полностью трехмерная полигональная консольная игра на революционном чипе Super FX."),
                ("Super Mario World 2: Yoshi's Island", "Платформер", 1995, "Nintendo EAD", "Nintendo", "WW", "Шедевр рисованной анимации с чипом Super FX 2."),
                ("Super Punch-Out!!", "Спорт / Бокс", 1994, "Nintendo R&D3", "Nintendo", "WW", "Литтл Мак с супер-нокаутирующими комбо против колоритных мировых боксеров."),
                ("Street Fighter II Turbo: Hyper Fighting", "Файтинг", 1993, "Capcom", "Capcom", "WW", "Эталон соревновательных файтингов с 12 бойцами, зеркальными матчами и турбо-скоростью."),
                ("Super Street Fighter II: The New Challengers", "Файтинг", 1994, "Capcom", "Capcom", "WW", "Ками, Ди Джей, Т. Хоук и Фей Лонг присоединяются к мировому турниру."),
                ("Mortal Kombat II", "Файтинг", 1994, "Sculptured Software", "Acclaim", "WW", "Шедевр кровавых фаталити, бруталити, бабалити и дружбы без цензуры."),
                ("Mortal Kombat 3 / Ultimate Mortal Kombat 3", "Файтинг", 1995, "Williams / Sculptured", "Midway", "WW", "Быстрый комбо-геймплей, кнопка бега и ниндзя-киборги Сайракс и Сектор."),
                ("Killer Instinct", "Файтинг", 1995, "Rare", "Nintendo", "WW", "Ультра-комбо на 80+ ударов от Rare на черном картридже с аудиодиском Killer Cuts."),
                ("Secret of Evermore", "Action-RPG", 1995, "Square USA", "Square", "WW", "Мальчик и его собака-трансформер путешествуют по эпохам с алхимической системой магии."),
                ("Lufia II: Rise of the Sinistrals", "JRPG", 1995, "Neverland", "Natsume", "WW", "Максим и Селан спасают мир от Зловещих с подземельями в стиле Zelda и Древней Пещерой на 99 этажей."),
                ("Breath of Fire", "JRPG", 1993, "Capcom", "Square", "WW", "Рю из клана Белого Дракона и крылатая принцесса Нина."),
                ("Breath of Fire II", "JRPG", 1994, "Capcom", "Capcom", "WW", "Трансформации в гибридных драконов, строительство собственной деревни Тауншип и шаманизм."),
                ("Tales of Phantasia", "JRPG", 1995, "Wolf Team", "Namco", "JPN", "Первая в истории игра с полноценной оцифрованной вокальной песней и линейной боевой системой LMBS."),
                ("Star Ocean", "JRPG", 1996, "tri-Ace", "Enix", "JPN", "Космическая JRPG с чипом S-DD1, озвучкой диалогов, созданием предметов и глубокой кастомизацией навыков."),
                ("Terranigma", "Action-RPG", 1995, "Quintet", "Enix", "EUR", "Арк воскрешает континенты, растения, животных, цивилизацию и технологии Земли."),
                ("Illusion of Gaia", "Action-RPG", 1993, "Quintet", "Enix", "WW", "Уилл с флейтой трансформируется в рыцаря Фрида и темного воина Тенероса."),
                ("Soul Blazer", "Action-RPG", 1992, "Quintet", "Enix", "WW", "Божественный посланник освобождает души людей и восстанавливает города."),
                ("Super Bomberman 1-5", "Аркада", 1993, "Produce / Hudson", "Hudson Soft", "WW", "Битвы бомберменов на 4 игроков через мультитап Super Multitap."),
                ("Wild Guns", "Тир от третьего лица", 1994, "Natsume", "Natsume", "WW", "Ковбои и роботы в стимпанк-вестерне с прицелом и прыжками-перекатами."),
                ("Sunset Riders", "Run and Gun", 1993, "Konami", "Konami", "WW", "Четыре охотника за головами на Диком Западе за золотом с плакатов Wanted."),
                ("Zombies Ate My Neighbors", "Экшен / Комедийный хоррор", 1993, "LucasArts", "Konami", "WW", "Зик и Джули спасают соседей, младенцев и чирлидерш водными пистолетами от монстров."),
                ("TMNT IV: Turtles in Time", "Beat 'em up", 1992, "Konami", "Konami", "WW", "Лучший битемап на SNES: броски врагов прямо в экран Mode 7 и путешествия во времени."),
                ("Kirby Super Star (Fun Pak)", "Платформер", 1996, "HAL Laboratory", "Nintendo", "WW", "Восемь полноценных игр в одном картридже со шлемами-способностями и напарником-хелпером."),
                ("Kirby's Dream Land 3", "Платформер", 1997, "HAL Laboratory", "Nintendo", "WW", "Пастельная сказка с чипом SA-1, друзьями-животными и девочкой-художницей Аделин."),
                ("Kirby's Dream Course", "Гольф", 1994, "HAL Laboratory", "Nintendo", "WW", "Изометрический гольф, где мячом выступает сам Кирби со способностями заморозки и огня."),
                ("Harvest Moon", "Симулятор фермы / Жизни", 1996, "Pack-In-Video", "Natsume", "WW", "Рождение легендарной серии фермерских симуляторов от Ясухиро Вады."),
                ("Ogre Battle: The March of the Black Queen", "Тактическая стратегия / RPG", 1993, "Quest", "Enix", "WW", "Масштабное руководство армиями отрядов на карте мира от Ясуми Мацуно."),
                ("Tactics Ogre: Let Us Cling Together", "Тактическая RPG", 1995, "Quest", "Quest", "JPN", "Основатель жанра изометрических пошаговых тактических RPG с моральными дилеммами."),
                ("Front Mission", "Тактическая RPG", 1995, "G-Craft", "Square", "JPN", "Бои настраиваемых шагающих роботов Ванзеров (Wanzers) на острове Хаффман."),
                ("Fire Emblem: Mystery of the Emblem", "Тактическая RPG", 1994, "Intelligent Systems", "Nintendo", "JPN", "Грандиозная сага о принце Марсе с двумя книгами кампании."),
                ("Fire Emblem: Genealogy of the Holy War", "Тактическая RPG", 1996, "Intelligent Systems", "Nintendo", "JPN", "Эпическая трагедия двух поколений героев с огромными картами замков и системой браков."),
                ("Fire Emblem: Thracia 776", "Тактическая RPG", 1999, "Intelligent Systems", "Nintendo", "JPN", "Хардкорная тактика принца Лифа с механикой усталости и захвата вражеского оружия.")
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
