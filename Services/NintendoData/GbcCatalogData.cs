using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class GbcCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(300);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Pokemon Gold Version", "JRPG", 1999, "Game Freak", "Nintendo", "WW", "Второе поколение: 100 новых покемонов, регионы Джото и Канто, 16 значков и битва с Редом на горе Сильвер."),
                ("Pokemon Silver Version", "JRPG", 1999, "Game Freak", "Nintendo", "WW", "Легендарная птица Лугия, часы реального времени со сменой дня и ночи, дни недели и разведение яиц."),
                ("Pokemon Crystal Version", "JRPG", 2000, "Game Freak", "Nintendo", "WW", "Анимированные спрайты покемонов, выбор девушки-протагониста Крис, Суикун и Башня Битв."),
                ("The Legend of Zelda: Oracle of Ages", "Action-Adventure", 2001, "Capcom (Flagship)", "Nintendo", "WW", "Линк спасает оракула Найру в Лабиринии с путешествиями во времени на Арфе Веков."),
                ("The Legend of Zelda: Oracle of Seasons", "Action-Adventure", 2001, "Capcom (Flagship)", "Nintendo", "WW", "Линк спасает оракула Дин в Холодруме с управлением 4 временами года Жезлом Сезонов."),
                ("The Legend of Zelda: Link's Awakening DX", "Action-Adventure", 1998, "Nintendo EAD", "Nintendo", "WW", "Цветная версия шедевра с новым подземельем цвета (Color Dungeon) и фотосалоном."),
                ("Super Mario Bros. Deluxe", "Платформер", 1999, "Nintendo R&D2", "Nintendo", "WW", "Цветной ремейк SMB1 и Lost Levels с картой мира, поиском красных монет и гонками против призрака Boo."),
                ("Wario Land 3", "Метроидвания / Платформер", 2000, "Nintendo R&D1", "Nintendo", "WW", "Бессмертный Варио исследует волшебную музыкальную шкатулку, находя новые движения и сокровища."),
                ("Metal Gear Solid (Ghost Babel)", "Стелс-экшен", 2000, "Konami (KCEJ)", "Konami", "WW", "Один из величайших стелс-боевиков: Солид Снейк проникает в крепость Галинд на миссию в Африке."),
                ("Shantae", "Метроидвания", 2002, "WayForward", "Capcom", "WW", "Дебют полуджинна Шантэ с танцами живота для превращения в обезьяну, слона, паука и гарпию."),
                ("Dragon Quest Monsters", "JRPG / Ловля монстров", 1998, "TOSE", "Enix", "WW", "Терри из DQ6 исследует порталы и скрещивает сотни монстров вселенной Dragon Quest."),
                ("Dragon Quest Monsters 2: Cobi's Journey / Tara's Adventure", "JRPG", 2001, "TOSE", "Enix", "WW", "Путешествия по ключам-мирам и скрещивание более 300 видов монстров."),
                ("Mario Tennis (GBC)", "Спортивная RPG", 2000, "Camelot", "Nintendo", "WW", "Полноценный сюжетный режим в теннисной академии с прокачкой характеристик персонажа."),
                ("Mario Golf (GBC)", "Спортивная RPG", 1999, "Camelot", "Nintendo", "WW", "Ролевой режим гольфиста с турнирами клуба и переносом персонажа на Nintendo 64."),
                ("Pokemon Trading Card Game", "Коллекционная карточная игра", 1998, "Hudson Soft / Creatures", "Nintendo", "WW", "Сбор колод, дуэли с клубными мастерами и Гранд-Мастерами за Легендарные Карты."),
                ("Pokemon Pinball", "Пинбол со встроенным вибро", 1999, "Jupiter", "Nintendo", "WW", "Картридж со встроенным мотором вибрации Rumble и ловлей покемонов на красном и синем столах."),
                ("Rayman", "Платформер", 2000, "Ubi Soft", "Ubi Soft", "WW", "Красочные приключения Рэймана с полетами на волосах-вертолетах."),
                ("Harvest Moon GBC", "Симулятор фермы", 1998, "Victor Interactive", "Natsume", "WW", "Портативная версия восстановления заброшенного дедушкиного ранчо."),
                ("Harvest Moon 2 GBC", "Симулятор фермы", 1999, "Victor Interactive", "Natsume", "WW", "Расширенная ферма с теплицами, садом и новыми животными."),
                ("Harvest Moon 3 GBC", "Симулятор фермы", 2000, "Victor Interactive", "Natsume", "WW", "Ферма на отдаленном острове с выбором парня или девушки и заведением семьи.")
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
