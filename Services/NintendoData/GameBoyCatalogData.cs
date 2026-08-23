using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class GameBoyCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(300);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Tetris", "Головоломка", 1989, "Nintendo R&D1", "Nintendo", "WW", "Легендарный стартовый хит карманной консоли Game Boy, разошедшийся тиражом более 35 млн копий."),
                ("Super Mario Land", "Платформер", 1989, "Nintendo R&D1", "Nintendo", "WW", "Первое портативное приключение Марио в королевстве Сарасаленд и спасение принцессы Дейзи от Татанги."),
                ("Super Mario Land 2: 6 Golden Coins", "Платформер", 1992, "Nintendo R&D1", "Nintendo", "WW", "Шедевр портативного гейминга, впервые представивший миру антигероя Варио и его замок."),
                ("Wario Land: Super Mario Land 3", "Платформер", 1994, "Nintendo R&D1", "Nintendo", "WW", "Дебют Варио в качестве главного героя с охотой за сокровищами и шляпами-трансформациями."),
                ("Wario Land II", "Платформер", 1998, "Nintendo R&D1", "Nintendo", "WW", "Бессмертный Варио решает физические головоломки, превращаясь в огонь, зомби и пружину."),
                ("The Legend of Zelda: Link's Awakening", "Action-Adventure", 1993, "Nintendo EAD", "Nintendo", "WW", "Линк терпит кораблекрушение на таинственном острове Кохолинт и будит Рыбу Ветров."),
                ("Pokemon Red Version", "JRPG", 1996, "Game Freak", "Nintendo", "WW", "Рождение глобального феномена ловли 151 покемона в регионе Канто от Сатоси Тадзири."),
                ("Pokemon Blue Version", "JRPG", 1996, "Game Freak", "Nintendo", "WW", "Оригинальная версия парной RPG с эксклюзивными покемонами и обменом через кабель Game Link."),
                ("Pokemon Yellow Version: Special Pikachu Edition", "JRPG", 1998, "Game Freak", "Nintendo", "WW", "Специальное издание по мотивам аниме: Пикачу следует за тренером по пятам."),
                ("Metroid II: Return of Samus", "Метроидвания", 1991, "Nintendo R&D1", "Nintendo", "WW", "Самус Аран спускается в недра планеты SR388 для полного уничтожения вида Метроидов."),
                ("Kirby's Dream Land", "Платформер", 1992, "HAL Laboratory", "Nintendo", "WW", "Дебют розового колобка Кирби от Масахиро Сакурая со втягиванием врагов и полетами."),
                ("Kirby's Dream Land 2", "Платформер", 1995, "HAL Laboratory", "Nintendo", "WW", "Кирби путешествует верхом на хомяке Рике, филине Ку и рыбе Кайн."),
                ("Kirby's Pinball Land", "Пинбол", 1993, "HAL Laboratory", "Nintendo", "WW", "Три многоярусных стола пинбола со звездами вселенной Кирби."),
                ("Kirby's Block Ball", "Арканоид", 1995, "HAL Laboratory / Nintendo", "Nintendo", "WW", "Разрушение блоков шариком-Кирби с уникальными способностями копирования."),
                ("Donkey Kong (Game Boy '94)", "Головоломка / Платформер", 1994, "Nintendo EAD / Pax Softnica", "Nintendo", "WW", "Грандиозное переосмысление с 101 уровнем, сальто Марио, стойками на руках и спасением Полин."),
                ("Donkey Kong Land", "Платформер", 1995, "Rare", "Nintendo", "WW", "Технологическое чудо с предрендеренной 3D-графикой от Rare на 8-битном экране."),
                ("Donkey Kong Land 2", "Платформер", 1996, "Rare", "Nintendo", "WW", "Дидди Конг и Дикси спасают Донки Конга с острова Крокодилов."),
                ("Donkey Kong Land III", "Платформер", 1997, "Rare", "Nintendo", "WW", "Поиск Затерянного мира в Северном Кремсфере с Дикси и Кидди Конгом."),
                ("Mega Man: Dr. Wily's Revenge", "Экшен / Платформер", 1991, "Minakuchi Engineering", "Capcom", "WW", "Портативный дебют Мегамена против 8 боссов и Энкер с зеркальным копьем."),
                ("Mega Man II", "Экшен / Платформер", 1992, "Japan Art Media", "Capcom", "WW", "Мегамен против Квинт и его землеройного бура Сакагар."),
                ("Mega Man III", "Экшен / Платформер", 1992, "Minakuchi Engineering", "Capcom", "WW", "Мегамен и собака Раш против Панка и Screw Crusher."),
                ("Mega Man IV", "Экшен / Платформер", 1993, "Minakuchi Engineering", "Capcom", "WW", "Появление магазина доктора Лайта и бластера Mega Arm."),
                ("Mega Man V", "Экшен / Платформер", 1994, "Minakuchi Engineering", "Capcom", "WW", "Уникальная игра с боссами-планетами Stardroids и космическим котом Танго."),
                ("Castlevania: The Adventure", "Экшен / Платформер", 1989, "Konami", "Konami", "WW", "Кристофер Бельмонт уничтожает летучих мышей и вампиров кнутом с огненными шарами."),
                ("Castlevania II: Belmont's Revenge", "Экшен / Платформер", 1991, "Konami", "Konami", "WW", "Один из лучших саундтреков Game Boy и спасение сына Солейла."),
                ("Castlevania Legends", "Экшен / Платформер", 1997, "KCE Nagoya", "Konami", "WW", "Соня Бельмонт с кнутом и силой душ предков в битве против Дракулы."),
                ("Gargoyle's Quest", "Action-RPG / Платформер", 1990, "Capcom", "Capcom", "WW", "Горгулья Файрбренд парит над лавой и спасает Мир Демонов."),
                ("Final Fantasy Adventure (Seiken Densetsu)", "Action-RPG", 1991, "Square", "Square", "WW", "Рождение легендарной саги Mana с мечами, топорами, магией и Древом Маны."),
                ("Final Fantasy Legend, The (SaGa)", "JRPG", 1989, "Square", "Square", "WW", "Первая портативная JRPG Акитоси Кавадзу с восхождением на Башню к Богу."),
                ("Final Fantasy Legend II (SaGa 2)", "JRPG", 1990, "Square", "Square", "WW", "Поиск 77 фрагментов реликвий богов Magi через разные миры."),
                ("Final Fantasy Legend III (SaGa 3)", "JRPG", 1991, "Square", "Square", "WW", "Путешествия сквозь прошлое, настоящее и будущее на летающем корабле Talon."),
                ("Bionic Commando (Game Boy)", "Экшен / Платформер", 1992, "Minakuchi Engineering", "Capcom", "WW", "Футуристический ремейк с механической рукой-крюком и лазерным бластером."),
                ("Kid Icarus: Of Myths and Monsters", "Платформер", 1991, "Nintendo R&D1 / TOSE", "Nintendo", "WW", "Пит тренируется ради спасения Ангельской земли от демона Орки."),
                ("DuckTales (Game Boy)", "Платформер", 1990, "Capcom", "Capcom", "WW", "Портативное путешествие Скруджа Макдака за сокровищами."),
                ("DuckTales 2 (Game Boy)", "Платформер", 1993, "Capcom", "Capcom", "WW", "Поиск фрагментов карты сокровищ."),
                ("Darkwing Duck (Game Boy)", "Платформер", 1993, "Capcom", "Capcom", "WW", "Черный Плащ на улицах Сен-Канара."),
                ("Ninja Gaiden Shadow", "Экшен / Платформер", 1991, "Natsume", "Tecmo", "WW", "Рю Хаябуса с ниндзя-мечом и крюком штурмует секретную цитадель."),
                ("Batman: The Video Game (Game Boy)", "Экшен / Платформер", 1990, "Sunsoft", "Sunsoft", "WW", "Бэтмен с бэтарангами и огнестрельным оружием в Готэме."),
                ("Batman: Return of the Joker (Game Boy)", "Платформер", 1992, "Sunsoft", "Sunsoft", "WW", "Бэтмен против Джокера с разнообразным оружием."),
                ("Batman: The Animated Series", "Экшен / Платформер", 1993, "Konami", "Konami", "WW", "Бэтмен и Робин по мотивам культового мультсериала."),
                ("R-Type (Game Boy)", "Горизонтальный SHMUP", 1991, "Bits Studios", "Irem", "WW", "Корабль R-9 с модулем Force против инопланетной империи Байд."),
                ("Gradius: The Interstellar Assault (Nemesis II)", "Горизонтальный SHMUP", 1991, "Konami", "Konami", "WW", "Vic Viper в космическом пространстве с апгрейдами щита и лазеров."),
                ("Parodius (Game Boy)", "Комедийный SHMUP", 1991, "Konami", "Konami", "WW", "Осьминог Тако, пингвин Пентаро и летающий корабль Vic Viper."),
                ("Avenging Spirit", "Аркадный экшен", 1992, "Jaleco", "Jaleco", "WW", "Призрак вселяется в тела врагов, чтобы спасти свою девушку."),
                ("Mole Mania", "Логическая головоломка", 1996, "Pax Softnica / Nintendo", "Nintendo", "WW", "Крот Мадди копает подземные ходы под руководством Сигэру Миямото."),
                ("Mario & Yoshi", "Головоломка", 1991, "Game Freak", "Nintendo", "WW", "Падающие скорлупки и монстры от создателей покемонов."),
                ("Yoshi's Cookie (Game Boy)", "Головоломка", 1992, "TOSE", "Nintendo", "WW", "Передвижение рядов печенья под веселую музыку."),
                ("Wario's Woods (Game Boy)", "Головоломка", 1994, "Nintendo R&D1", "Nintendo", "WW", "Тоад уничтожает монстров бомбами."),
                ("Dr. Mario (Game Boy)", "Головоломка", 1990, "Nintendo R&D1", "Nintendo", "WW", "Уничтожение вирусов цветными пилюлями."),
                ("Qix (Game Boy)", "Аркада", 1990, "Nintendo R&D1", "Nintendo", "WW", "Захват площади с танцующим Марио."),
                ("Alleyway", "Арканоид", 1989, "Nintendo R&D1", "Nintendo", "WW", "Стартовый арканоид с пилотом Марио в бите."),
                ("SolarStriker", "Вертикальный SHMUP", 1990, "Nintendo R&D1 / Minakuchi", "Nintendo", "WW", "Классический вертикальный космический шутер от Гумпэя Ёкои."),
                ("Balloon Kid", "Платформер", 1990, "Pax Softnica / Nintendo", "Nintendo", "WW", "Девочка Элис летит на воздушных шариках спасать брата Джима.")
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
