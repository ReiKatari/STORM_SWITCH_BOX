using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class NdsCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(400);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Pokemon Diamond Version", "JRPG", 2006, "Game Freak", "Nintendo", "WW", "Четвертое поколение в регионе Синно: легендарный Диалга, разделение атак на физические/специальные и Wi-Fi дуэли через Nintendo WFC."),
                ("Pokemon Pearl Version", "JRPG", 2006, "Game Freak", "Nintendo", "WW", "Повелитель пространства Палкия, подземный мир Underground и 107 новых видов покемонов."),
                ("Pokemon Platinum Version", "JRPG", 2008, "Game Freak", "Nintendo", "WW", "Искаженный мир (Distortion World) с антигравитацией, Гиратина в форме Origin и Battle Frontier."),
                ("Pokemon HeartGold Version", "JRPG", 2009, "Game Freak", "Nintendo", "WW", "Один из величайших ремейков в истории: покемон следует за тренером по пятам, шагомер Pokéwalker и два региона Джото и Канто."),
                ("Pokemon SoulSilver Version", "JRPG", 2009, "Game Freak", "Nintendo", "WW", "Ремейк серебряной версии с Лугией, сафари-зоной и мини-играми Pokéathlon."),
                ("Pokemon Black Version", "JRPG", 2010, "Game Freak", "Nintendo", "WW", "Пятое поколение в Нью-Йоркском регионе Юнова: Реширам, полностью анимированные 2D-спрайты и глубокий сюжет с N и Team Plasma."),
                ("Pokemon White Version", "JRPG", 2010, "Game Freak", "Nintendo", "WW", "Зекром, Белый лес (White Forest), динамическая смена четырех времен года и тройные битвы."),
                ("Pokemon Black Version 2", "JRPG", 2012, "Game Freak", "Nintendo", "WW", "Прямой сиквел спустя 2 года: Черный Кюрем, киностудия Pokéstar Studios и турнир чемпионов Pokémon World Tournament."),
                ("Pokemon White Version 2", "JRPG", 2012, "Game Freak", "Nintendo", "WW", "Белый Кюрем, новые города, соревновательный турнир чемпионов и дерево наград Медалей."),
                ("The Legend of Zelda: Phantom Hourglass", "Action-Adventure с сенсорным управлением", 2007, "Nintendo EAD", "Nintendo", "WW", "Линк и капитан Лайнбек бороздят океан, управляя мечом и бумерангом исключительно стилусом."),
                ("The Legend of Zelda: Spirit Tracks", "Action-Adventure", 2009, "Nintendo EAD", "Nintendo", "WW", "Линк водит паровоз по рельсам Хайрула, а призрак принцессы Зельды вселяется в фантомов-стражей."),
                ("New Super Mario Bros.", "2D Платформер", 2006, "Nintendo EAD", "Nintendo", "WW", "Возвращение 2D-Марио: Мега-Гриб, Мини-Гриб, панцирь синей черепахи и 8 миров."),
                ("Mario Kart DS", "Гонки", 2005, "Nintendo EAD", "Nintendo", "WW", "Первая онлайн-игра Nintendo с миссиями на время, сникелингом и трассами Waluigi Pinball и Delfino Square."),
                ("Super Mario 64 DS", "3D Платформер", 2004, "Nintendo EAD", "Nintendo", "WW", "Расширенный ремейк старта N64 с 4 героями: Йоши, Марио, Луиджи и Варио, 150 звездами и 30 мини-играми."),
                ("Castlevania: Dawn of Sorrow", "Метроидвания", 2005, "Konami", "Konami", "WW", "Сома Крус в замке культа с поглощением душ и запечатыванием боссов магическими печатями Magic Seals стилусом."),
                ("Castlevania: Portrait of Ruin", "Метроидвания", 2006, "Konami", "Konami", "WW", "Джонатан Моррис и волшебница Шарлотта Олин прыгают внутрь картин вампира Браунера с кооперативными атаками Dual Crush."),
                ("Castlevania: Order of Ecclesia", "Метроидвания", 2008, "Konami", "Konami", "WW", "Шаноа поглощает глифы (Glyph System) обоими руками и спиной, сражаясь за Орден Экклезии против Дракулы."),
                ("Chrono Trigger (DS)", "JRPG", 2008, "Square Enix / TOSE", "Square Enix", "WW", "Идеальное издание шедевра с сенсорным интерфейсом, дополнительными подземельями и новой концовкой связки с Chrono Cross."),
                ("Dragon Quest IX: Sentinels of the Starry Skies", "JRPG", 2009, "Level-5", "Square Enix / Nintendo", "WW", "Падший ангел Селестиан создает отряд спутников в таверне с сотнями квестов и кооперативом на 4 игроков."),
                ("Dragon Quest IV: Chapters of the Chosen", "JRPG", 2007, "ArtePiazza", "Square Enix", "WW", "3D-ремейк пяти глав с Торнеко, сестрами Минией и Манией и Героем."),
                ("Dragon Quest V: Hand of the Heavenly Bride", "JRPG", 2008, "ArtePiazza", "Square Enix", "WW", "Сага о трех поколениях жизни героя: детство, брак с выбором невесты, рождение детей-близнецов и ловля монстров."),
                ("Dragon Quest VI: Realms of Revelation", "JRPG", 2010, "ArtePiazza", "Square Enix", "WW", "Параллельные миры реальности и снов с системой смены классов."),
                ("The World Ends with You", "Action-RPG", 2007, "Square Enix / Jupiter", "Square Enix", "WW", "Нэку Сакураба играет в «Игру Жнецов» на улицах Сибуи с одновременным управлением стилусом и кнопками на двух экранах."),
                ("Radiant Historia", "JRPG", 2010, "Atlus", "Atlus", "WW", "Шпион Сток перемещается между двумя альтернативными ветками времени Хронокластера, решая геополитические кризисы."),
                ("Shin Megami Tensei: Strange Journey", "Dungeon Crawler / RPG", 2009, "Atlus", "Atlus", "WW", "Экспедиция ООН в скафандрах исследует пространственную аномалию Шварцвельт в Антарктиде."),
                ("Ghost Trick: Phantom Detective", "Головоломка / Интерактивный детектив", 2010, "Capcom", "Capcom", "WW", "Сю Такуми: дух погибшего Сисселя отматывает время за 4 минуты до смерти жертв, вселяясь в предметы."),
                ("Phoenix Wright: Ace Attorney", "Судебный детектив / Квест", 2005, "Capcom", "Capcom", "WW", "Феникс Райт кричит «OBJECTION!» в микрофон, ищет улики и разоблачает свидетелей в суде."),
                ("Phoenix Wright: Ace Attorney - Justice for All", "Судебный детектив", 2006, "Capcom", "Capcom", "WW", "Введение механики Психо-замков (Psyche-Locks) и прокурор Франциска фон Карма с кнутом."),
                ("Phoenix Wright: Ace Attorney - Trials and Tribulations", "Судебный детектив", 2007, "Capcom", "Capcom", "WW", "Грандиозный финал трилогии: прошлое Мии Фей и противостояние с загадочным Годотом."),
                ("Apollo Justice: Ace Attorney", "Судебный детектив", 2007, "Capcom", "Capcom", "WW", "Молодой адвокат Аполло Джастис с волшебным браслетом разоблачает нервные тики свидетелей."),
                ("Miles Edgeworth: Ace Attorney Investigations", "Детектив от третьего лица", 2009, "Capcom", "Capcom", "WW", "Прокурор Майлз Эджворт исследует места преступлений методом логических связок Логики (Logic System)."),
                ("Professor Layton and the Curious Village", "Головоломка / Детектив", 2007, "Level-5", "Nintendo", "WW", "Профессор Лейтон и Люк разгадывают тайну загадочной деревни Сент-Мистир с сотнями логических пазлов."),
                ("Professor Layton and the Diabolical Box", "Головоломка", 2007, "Level-5", "Nintendo", "WW", "Путешествие на роскошном поезде Молентри Экспресс в поисках шкатулки Пандоры."),
                ("Professor Layton and the Unwound Future", "Головоломка", 2008, "Level-5", "Nintendo", "WW", "Эмоциональное путешествие в будущее Лондона ради спасения возлюбленной Клэр."),
                ("Mario & Luigi: Bowser's Inside Story", "JRPG", 2009, "AlphaDream", "Nintendo", "WW", "Марио и Луиджи уменьшаются и путешествуют внутри тела Боузера, управляя его мышцами и огнем."),
                ("Mario & Luigi: Partners in Time", "JRPG", 2005, "AlphaDream", "Nintendo", "WW", "Марио и Луиджи объединяются со своими младенческими версиями Baby Mario и Baby Luigi против пришельцев Шрообов."),
                ("Hotel Dusk: Room 215", "Интерактивный нуарный роман", 2007, "Cing", "Nintendo", "WW", "Бывший детектив Кайл Хайд держит Nintendo DS вертикально как книгу, расследуя тайны мотеля."),
                ("Nine Hours, Nine Persons, Nine Doors (999)", "Визуальная новелла / Escape Room", 2009, "Chunsoft", "Aksys", "WW", "Котаро Утикоси: 9 человек заперты на тонущем лайнере в смертельной Ноннарной игре."),
                ("Advance Wars: Dual Strike", "Пошаговая тактика", 2005, "Intelligent Systems", "Nintendo", "WW", "Бои на двух экранах одновременно (земля и воздух) и парные суперсилы Dual Strike CO."),
                ("Advance Wars: Days of Ruin (Dark Conflict)", "Пошаговая тактика", 2008, "Intelligent Systems", "Nintendo", "WW", "Мрачный постапокалиптический перезапуск с новыми командирами и выверенным балансом юнитов."),
                ("Fire Emblem: Shadow Dragon", "Тактическая RPG", 2008, "Intelligent Systems", "Nintendo", "WW", "Ремейк первой части о принце Марсе с механикой смены классов Reclassing и прологом.")
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
