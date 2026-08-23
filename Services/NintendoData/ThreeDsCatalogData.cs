using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class ThreeDsCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(400);

            var games = new (string TitleId, string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("0004000000030800", "The Legend of Zelda: Ocarina of Time 3D", "Action-Adventure / Стерео 3D", 2011, "Grezzo / Nintendo", "Nintendo", "WW", "Великолепный ремейк с текстурами высокого разрешения, 3D-глубиной без очков, гироскопическим прицелом и режимом Master Quest."),
                ("0004000000125500", "The Legend of Zelda: Majora's Mask 3D", "Action-Adventure", 2015, "Grezzo / Nintendo", "Nintendo", "WW", "Обновленная версия с блокнотом бомберов Bombers' Notebook, точками сохранения и улучшенным управлением боссами."),
                ("00040000000EC300", "The Legend of Zelda: A Link Between Worlds", "Action-Adventure / Свобода выбора", 2013, "Nintendo EPD", "Nintendo", "WW", "Линк превращается в настенный рисунок (Wall Merge), перемещаясь между мирами Хайрула и Лорула со свободной арендой предметов у Равио."),
                ("0004000000175E00", "The Legend of Zelda: Tri Force Heroes", "Кооперативный Action", 2015, "Nintendo EPD / Grezzo", "Nintendo", "WW", "Три Линка в костюмах объединяются в тотемные башни ради спасения королевства Хитпия."),
                ("0004000000054000", "Super Mario 3D Land", "3D Стереоскопический платформер", 2011, "Nintendo EAD Tokyo", "Nintendo", "WW", "Синтез 2D и 3D геймплея: костюм Тануки, бинокли, оптические иллюзии и 16 миров."),
                ("0004000000076500", "Mario Kart 7", "Гонки", 2011, "Nintendo EAD / Retro Studios", "Nintendo", "WW", "Полеты на дельтапланах, подводные гребные винты, вид от первого лица и сбор деталей картов."),
                ("000400000008C300", "Pokemon X", "3D JRPG", 2013, "Game Freak", "Nintendo", "WW", "Шестое поколение в регионе Калос (Франция): Мега-эволюции (Mega Evolution), тип фей (Fairy) и катание на роликах."),
                ("000400000008C400", "Pokemon Y", "3D JRPG", 2013, "Game Freak", "Nintendo", "WW", "Легендарная птица разрушения Ивельтал, Pokemon-Amie и супер-тренировки Super Training."),
                ("000400000011C400", "Pokemon Omega Ruby", "3D JRPG", 2014, "Game Freak", "Nintendo", "WW", "Первобытный Граудон (Primal Groudon), полеты над Хоэнном на Латиосе (Soaring in the Sky) и Дельта-эпизод с Деоксисом."),
                ("000400000011C500", "Pokemon Alpha Sapphire", "3D JRPG", 2014, "Game Freak", "Nintendo", "WW", "Первобытный Кайогр (Primal Kyogre), расширенные секретные базы и тайные острова Mirage Spots."),
                ("0004000000164800", "Pokemon Sun", "3D JRPG", 2016, "Game Freak", "Nintendo", "WW", "Седьмое поколение на островах Алола: Солгалео, Z-атаки (Z-Moves), испытания островов вместо гимов и формы Алолы."),
                ("0004000000175E00", "Pokemon Moon", "3D JRPG", 2016, "Game Freak", "Nintendo", "WW", "Лунаала, 12-часовой сдвиг времени суток, Ультра-чудовища (Ultra Beasts) и фестивальный замок."),
                ("00040000001B5000", "Pokemon Ultra Sun", "3D JRPG", 2017, "Game Freak", "Nintendo", "WW", "Некрозма, путешествия сквозь Ультра-червоточины на Солгалео, серфинг на Мантайнах и Команда Радужная Ракета."),
                ("00040000001B5100", "Pokemon Ultra Moon", "3D JRPG", 2017, "Game Freak", "Nintendo", "WW", "Ультра-Некрозма, все легендарные покемоны прошлых поколений и фото-клуб Alola Photo Club."),
                ("00040000000A0500", "Fire Emblem Awakening", "Тактическая RPG", 2012, "Intelligent Systems", "Nintendo", "WW", "Спасение серии: система связок Pair-Up, романтические отношения, дети из будущего и Хром с Робином."),
                ("000400000012DE00", "Fire Emblem Fates: Birthright", "Тактическая RPG", 2015, "Intelligent Systems", "Nintendo", "WW", "Коррин выбирает родную миролюбивую семью Хосидо в японском стиле."),
                ("000400000012DF00", "Fire Emblem Fates: Conquest", "Хардкорная тактическая RPG", 2015, "Intelligent Systems", "Nintendo", "WW", "Коррин выбирает приемную суровую семью Нор с ограниченным золотом и сложнейшими задачами."),
                ("00040000001B4600", "Fire Emblem Echoes: Shadows of Valentia", "Тактическая RPG", 2017, "Intelligent Systems", "Nintendo", "WW", "Ремейк Gaiden с полной озвучкой диалогов, подземельями от третьего лица и Колесом Милы для отмотки ходов."),
                ("000400000012A000", "Metroid: Samus Returns", "2.5D Метроидвания", 2017, "MercurySteam / Nintendo", "Nintendo", "WW", "Самус Аран на планете SR388 с механикой парирования ударов Melee Counter, прицелом на 360 градусов и способностями Эон."),
                ("000400000008F800", "Animal Crossing: New Leaf", "Симулятор жизни", 2012, "Nintendo EAD", "Nintendo", "WW", "Игрок становится мэром города: городские постановления, остров Тортимера, клуб LOL и обновление Welcome amiibo."),
                ("000400000007FE00", "Luigi's Mansion: Dark Moon", "Приключения", 2013, "Next Level Games", "Nintendo", "WW", "Луиджи исследует 5 разнообразных особняков долины Эвершейд с пылесосом Poltergust 5000 и темным фонарем Dark-Light Device."),
                ("00040000001D4100", "Luigi's Mansion (3DS Remake)", "Приключения", 2018, "Grezzo / Nintendo", "Nintendo", "WW", "3D-ремейк первой части с GameCube с картой на втором экране и кооперативом за Гуиджи (Gooigi)."),
                ("00040000000EDA00", "Super Smash Bros. for Nintendo 3DS", "Файтинг", 2014, "Bandai Namco / Sora Ltd.", "Nintendo", "WW", "Портативный Smash Bros. с 60 FPS, эксклюзивным режимом Smash Run и десятками культовых бойцов."),
                ("0004000000033600", "Kid Icarus: Uprising", "Экшен / 3D Шутер от третьего лица", 2012, "Project Sora (Сакурай)", "Nintendo", "WW", "Ангел Пит и богиня Палютена против подземной армии Медузы с воздушными и наземными битвами и сотнями пушек."),
                ("000400000019BD00", "Monster Hunter Generations", "Action-RPG / Охота", 2015, "Capcom", "Capcom", "WW", "Четыре стиля охоты Hunting Styles, супер-приемы Hunting Arts и возможность охотиться за котиков Prowler."),
                ("0004000000125600", "Monster Hunter 4 Ultimate", "Action-RPG", 2014, "Capcom", "Capcom", "WW", "Прыжки с высоты на спины монстров (Mounting), оружие Насекомый посох (Insect Glaive) и Черный дракон Горе Магала."),
                ("0004000000099400", "Bravely Default", "JRPG", 2012, "Silicon Studio", "Square Enix", "WW", "Классическая пошаговая ролевая игра с инновационной системой накопления и займа ходов Brave и Default и 24 профессиями."),
                ("0004000000156E00", "Bravely Second: End Layer", "JRPG", 2015, "Silicon Studio", "Square Enix", "WW", "Продолжение приключений трех мушкетеров ордена с новыми классами Catmancer и Exorcist."),
                ("0004000000085C00", "Shin Megami Tensei IV", "JRPG", 2013, "Atlus", "Atlus", "WW", "Самураи Восточного Королевства Микадо спускаются в постапокалиптический подземный Токио."),
                ("000400000017FD00", "Shin Megami Tensei IV: Apocalypse", "JRPG", 2016, "Atlus", "Atlus", "WW", "Охотник Нанаси заключает контракт с богом Дагдой в разрушенном демонами Токио 2038 года."),
                ("0004000000192A00", "Persona Q: Shadow of the Labyrinth", "Dungeon Crawler / JRPG", 2014, "Atlus", "Atlus", "WW", "Кроссовер команд Persona 3 и Persona 4 в таинственной старшей школе Ясогами с рисованием карт подземелий."),
                ("00040000001D4400", "Persona Q2: New Cinema Labyrinth", "Dungeon Crawler / JRPG", 2018, "Atlus", "Atlus", "WW", "Команды Persona 5 (Джокер), Persona 4 и Persona 3 исследуют кинофильмы-лабиринты."),
                ("0004000000160F00", "Dragon Quest VIII: Journey of the Cursed King", "JRPG", 2015, "Level-5 / Square Enix", "Square Enix", "WW", "Портативная версия с двумя новыми персонажами Ред и Мори, фото-квестами Кэмерона и боями без случайных стычек."),
                ("0004000000161000", "Dragon Quest VII: Fragments of the Forgotten Past", "JRPG", 2013, "ArtePiazza", "Square Enix", "WW", "Полный 3D-ремейк эпической 100-часовой саги восстановления островов мира из каменных скрижалей.")
            };

            foreach (var g in games)
            {
                list.Add(new NintendoCatalogItem
                {
                    TitleId = g.TitleId,
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
