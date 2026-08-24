using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class WiiUCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(171);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Mario 3D World", "3D Платформер", 2013, "Nintendo EAD Tokyo", "Nintendo", "WW", "Кооперативный 3D-шедевр на 4 игроков с костюмом Кошки-Марио, прозрачными трубами и Капитаном Тоадом."),
                ("Mario Kart 8", "Картинговые гонки", 2014, "Nintendo EAD", "Nintendo", "WW", "Антигравитационные гонки по стенам и потолкам, 60 FPS, оркестровый саундтрек и сервис Mario Kart TV."),
                ("Super Smash Bros. for Wii U", "Файтинг-кроссовер", 2014, "Bandai Namco / Sora", "Nintendo", "WW", "Битвы на 8 игроков на одной арене, 58 бойцов в Full HD, поддержка фигурок amiibo и кастомных уровней."),
                ("The Legend of Zelda: Breath of the Wild", "Action-Adventure с открытым миром", 2017, "Nintendo EPD", "Nintendo", "WW", "Революция открытого мира: физический движок, симуляция химии элементов, планер Параглайдер и 120 святилищ Хайрула."),
                ("The Legend of Zelda: The Wind Waker HD", "Action-Adventure", 2013, "Nintendo EAD", "Nintendo", "WW", "Шедевральный HD-ремейк морской одиссеи Линка со скоростным парусом Swift Sail и картой на геймпаде Wii U GamePad."),
                ("The Legend of Zelda: Twilight Princess HD", "Action-Adventure", 2016, "Tantalus / Nintendo", "Nintendo", "WW", "HD-ремейк эпического приключения с улучшенным сумеречным сбором слез света и подземельем пещеры теней."),
                ("Xenoblade Chronicles X", "Sci-Fi Action-RPG с открытым миром", 2015, "Monolith Soft", "Nintendo", "WW", "Колоссальная планета Мира: исследование 5 континентов, пилотирование гигантских летающих мехов Скеллов (Skells)."),
                ("Splatoon", "Командный шутер краской", 2015, "Nintendo EAD", "Nintendo", "WW", "Рождение культового онлайн-шутера 4х4: инклинги заливают карту цветными чернилами и плавают кальмарами."),
                ("Bayonetta 2", "Стильный слэшер", 2014, "PlatinumGames", "Nintendo", "WW", "Абсолютная вершина жанра слэшеров: ведьма Байонетта с пистолетами на каблуках, кульминацией Umbran Climax и 60 FPS."),
                ("Donkey Kong Country: Tropical Freeze", "2D Платформер", 2014, "Retro Studios", "Nintendo", "WW", "Шедевр платформеров: вторжение викингов Снемадцев, гениальный саундтрек Дэвида Уайза и помощь Крэнки Конга."),
                ("Super Mario Maker", "Конструктор и платформа уровней", 2015, "Nintendo EAD", "Nintendo", "WW", "Создание любых уровней Mario в 4 стилях (SMB1, SMB3, SMW, NSMBU) на сенсорном экране геймпада и миллионы онлайн-уровней."),
                ("Pikmin 3", "Стратегия в реальном времени", 2013, "Nintendo EAD", "Nintendo", "WW", "Альф, Бриттани и капитан Чарли командуют пикминами с помощью карты на GamePad, новые каменные и крылатые пикмины."),
                ("Captain Toad: Treasure Tracker", "Головоломка в диорамах", 2014, "Nintendo EAD Tokyo", "Nintendo", "WW", "Капитан Тоад и Тоадетта исследуют более 70 изометрических уровней-кубиков без возможности прыгать."),
                ("Yoshi's Woolly World", "Шерстяной платформер", 2015, "Good-Feel", "Nintendo", "WW", "Невероятно красивый мир из пряжи и пуговиц, клубки ниток вместо яиц и кооперативное прохождение."),
                ("New Super Mario Bros. U", "2D Платформер", 2012, "Nintendo EAD", "Nintendo", "WW", "Стартовый хит: костюм Белки-летяги, малыши Йоши, режим испытаний Boost Rush и карта мира с развилками."),
                ("New Super Luigi U", "Хардкорный платформер", 2013, "Nintendo EAD", "Nintendo", "WW", "82 скоростных уровня за Луиджи с высоким прыжком и таймером всего на 100 секунд."),
                ("Lego City Undercover", "Детективный экшен с открытым миром", 2013, "TT Fusion", "Nintendo", "WW", "Чейз Маккейн под прикрытием в открытом мегаполисе: вертолеты, спорткары, маскировки и юмор."),
                ("Paper Mario: Color Splash", "Action-Adventure / RPG", 2016, "Intelligent Systems", "Nintendo", "WW", "Остров Призма: Бумажный Марио раскрашивает обесцвеченный мир красящим молотом и карточными спец-атаками."),
                ("Tokyo Mirage Sessions #FE", "JRPG-кроссовер", 2015, "Atlus", "Nintendo", "WW", "Кроссовер Shin Megami Tensei и Fire Emblem: поп-айдолы в Токио с битвами в стиле мюзиклов Session Attacks."),
                ("The Wonderful 101", "Супергеройский слэшер", 2013, "PlatinumGames", "Nintendo", "WW", "Управление толпой из 100 супергероев Камией: рисование гигантского меча, кулака, пистолета и хлыста."),
                ("Hyrule Warriors", "Масштабный мусоу-слэшер", 2014, "Omega Force / Team Ninja", "Nintendo", "WW", "Эпические битвы сотен врагов: Линк, Зельда, Мидны, Импа, Ганондорф с легендарным оружием."),
                ("Monster Hunter 3 Ultimate", "Action-RPG", 2012, "Capcom", "Capcom", "WW", "Расширенная версия охоты в Full HD с онлайном, переносом сохранений с 3DS и G-рангом."),
                ("Nintendo Land", "Сборник 12 тематических парковых аттракционов", 2012, "Nintendo EAD", "Nintendo", "WW", "Идеальная демонстрация асимметричного геймплея геймпада (Mario Chase, Luigi's Ghost Mansion, Zelda)."),
                ("Wii Party U", "Вечеринка для всей семьи", 2013, "Nd Cube", "Nintendo", "WW", "Настольные игры, состязания на геймпаде на столе и 80 новых мини-игр."),
                ("Star Fox Zero", "3D Космосимулятор", 2016, "Nintendo EPD / PlatinumGames", "Nintendo", "WW", "Фокс Макклауд с раздельным видом из кабины Arwing на GamePad, шагающий дроид Walker и гирокоптер."),
                ("Affordable Space Adventures", "Кооперативный космосим с управлением кораблем через геймпад", 2015, "KnapNok", "KnapNok", "WW", "Affordable Space Adventures — популярная игра для Nintendo Wii U."),
                ("Batman: Arkham City - Armored Edition", "Экшен Бэтмена с режимом сканера и бэтаранга на GamePad", 2012, "Warner Bros.", "Warner Bros.", "WW", "Batman: Arkham City - Armored Edition — популярная игра для Nintendo Wii U."),
                ("Batman: Arkham Origins", "Приквел о становлении Бэтмена в рождественском Готэме", 2013, "Warner Bros.", "Warner Bros.", "WW", "Batman: Arkham Origins — популярная игра для Nintendo Wii U."),
                ("Bayonetta", "Первая часть ведьмы Байонетты с костюмами Link, Peach, Samus", 2014, "Nintendo", "Nintendo", "WW", "Bayonetta — популярная игра для Nintendo Wii U."),
                ("Call of Duty: Black Ops II", "FPS будущего со вторым экраном для локального мультиплеера без разделения ТВ", 2012, "Activision", "Activision", "WW", "Call of Duty: Black Ops II — популярная игра для Nintendo Wii U."),
                ("Call of Duty: Ghosts", "Военный FPS отряда Призраков", 2013, "Activision", "Activision", "WW", "Call of Duty: Ghosts — популярная игра для Nintendo Wii U."),
                ("Darksiders: Warmastered Edition", "Слэшер Всадника Апокалипсиса Войны в Full HD", 2017, "Nordic Games", "Nordic Games", "WW", "Darksiders: Warmastered Edition — популярная игра для Nintendo Wii U."),
                ("Darksiders II", "Слэшер Всадника Смерти в царстве мертвых", 2012, "THQ", "THQ", "WW", "Darksiders II — популярная игра для Nintendo Wii U."),
                ("Deus Ex: Human Revolution - Director's Cut", "Киберпанк Адама Дженсена с нейрокомпьютером на экране геймпада", 2013, "Square Enix", "Square Enix", "WW", "Deus Ex: Human Revolution - Director's Cut — популярная игра для Nintendo Wii U."),
                ("Fast Racing NEO", "Сверхзвуковые футуристические антигравитационные гонки со сменой цвета фаз от Shin'en", 2015, "Shin'en", "Shin'en", "WW", "Fast Racing NEO — популярная игра для Nintendo Wii U."),
                ("Fatal Frame: Maiden of Black Water (Project Zero)", "Японский хоррор на горе Хиками: GamePad используется как камера-обскура!", 2014, "Nintendo / Koei Tecmo", "Nintendo / Koei Tecmo", "WW", "Fatal Frame: Maiden of Black Water (Project Zero) — популярная игра для Nintendo Wii U."),
                ("Game & Wario", "Сборник 16 безумных авторских игр компании Варио (Shutter, Gamer, Islands)", 2013, "Nintendo", "Nintendo", "WW", "Game & Wario — популярная игра для Nintendo Wii U."),
                ("Guacamelee! Super Turbo Championship Edition", "Красочная мексиканская метроидвания лучадора Хуана", 2014, "DrinkBox", "DrinkBox", "WW", "Guacamelee! Super Turbo Championship Edition — популярная игра для Nintendo Wii U."),
                ("Injustice: Gods Among Us", "Файтинг супергероев DC от NetherRealm", 2013, "Warner Bros.", "Warner Bros.", "WW", "Injustice: Gods Among Us — популярная игра для Nintendo Wii U."),
                ("Kirby and the Rainbow Curse (Rainbow Paintbrush)", "Пластилиновый шедевр с рисованием радужных дорожек на геймпаде", 2015, "Nintendo", "Nintendo", "WW", "Kirby and the Rainbow Curse (Rainbow Paintbrush) — популярная игра для Nintendo Wii U."),
                ("Mario Party 10", "Вечеринка с режимом Bowser Party, где 5-й игрок на геймпаде играет за Боузера", 2015, "Nintendo", "Nintendo", "WW", "Mario Party 10 — популярная игра для Nintendo Wii U."),
                ("Mario Tennis: Ultra Smash", "Теннис с мега-грибами", 2015, "Nintendo", "Nintendo", "WW", "Mario Tennis: Ultra Smash — популярная игра для Nintendo Wii U."),
                ("Mass Effect 3: Special Edition", "Sci-Fi Action-RPG капитана Шепарда с тактическим командованием на экране", 2012, "EA", "EA", "WW", "Mass Effect 3: Special Edition — популярная игра для Nintendo Wii U."),
                ("Need for Speed: Most Wanted U", "Ультимативная версия гонок в Рокпорте с поддержкой графических ассетов PC", 2013, "EA", "EA", "WW", "Need for Speed: Most Wanted U — популярная игра для Nintendo Wii U."),
                ("NES Remix", "Модернизированные 8-битные челленджи ретро-классики Nintendo с новыми правилами", 2013, "Nintendo", "Nintendo", "WW", "NES Remix — популярная игра для Nintendo Wii U."),
                ("NES Remix 2", "Вторая часть челленджей со Super Luigi Bros.", 2014, "Nintendo", "Nintendo", "WW", "NES Remix 2 — популярная игра для Nintendo Wii U."),
                ("Ninja Gaiden 3: Razor's Edge", "Ультра-хардкорный кровавый слэшер Рю Хаябусы и Аянэ", 2012, "Nintendo / Koei Tecmo", "Nintendo / Koei Tecmo", "WW", "Ninja Gaiden 3: Razor's Edge — популярная игра для Nintendo Wii U."),
                ("Pokken Tournament", "3D файтинг покемонов в реальном времени от создателей Tekken (Bandai Namco)", 2016, "Nintendo", "Nintendo", "WW", "Pokken Tournament — популярная игра для Nintendo Wii U."),
                ("Rayman Legends", "Величайший 2D платформер с музыкальными уровнями и управлением Мерфи на GamePad", 2013, "Ubisoft", "Ubisoft", "WW", "Rayman Legends — популярная игра для Nintendo Wii U."),
                ("Resident Evil: Revelations HD", "HD версия хоррора Джилл на лайнере с инвентарем на GamePad", 2013, "Capcom", "Capcom", "WW", "Resident Evil: Revelations HD — популярная игра для Nintendo Wii U."),
                ("Rodea the Sky Soldier", "Полеты в небесах от Юдзи Наки (создателя Соника)", 2015, "NIS America", "NIS America", "WW", "Rodea the Sky Soldier — популярная игра для Nintendo Wii U."),
                ("Runbow", "Безумный платформер-гонка на 9 игроков со сменой фонового цвета", 2015, "13AM Games", "13AM Games", "WW", "Runbow — популярная игра для Nintendo Wii U."),
                ("Scribblenauts Unlimited", "Магический блокнот со спавном персонажей Mario и Zelda", 2012, "Warner Bros.", "Warner Bros.", "WW", "Scribblenauts Unlimited — популярная игра для Nintendo Wii U."),
                ("Severed", "Слэшер с фехтованием на сенсорном экране от DrinkBox", 2016, "DrinkBox", "DrinkBox", "WW", "Severed — популярная игра для Nintendo Wii U."),
                ("Shantae: 1/2 Genie Hero", "Красочный HD платформер полуджинна Шантэ", 2016, "WayForward", "WayForward", "WW", "Shantae: 1/2 Genie Hero — популярная игра для Nintendo Wii U."),
                ("Shantae and the Pirate's Curse", "Метроидвания с пиратскими предметами Риски Бутс", 2014, "WayForward", "WayForward", "WW", "Shantae and the Pirate's Curse — популярная игра для Nintendo Wii U."),
                ("Shovel Knight", "Лопатный рыцарь с инвентарем на экране", 2014, "Yacht Club", "Yacht Club", "WW", "Shovel Knight — популярная игра для Nintendo Wii U."),
                ("Sniper Elite V2", "Снайперский симулятор Второй мировой войны с рентгеновской камерой X-Ray", 2013, "505 Games", "505 Games", "WW", "Sniper Elite V2 — популярная игра для Nintendo Wii U."),
                ("Sonic & All-Stars Racing Transformed", "Трансформирующиеся гонки (Машина, Катер, Самолет) с 5 игроками", 2012, "Sega", "Sega", "WW", "Sonic & All-Stars Racing Transformed — популярная игра для Nintendo Wii U."),
                ("Sonic Boom: Rise of Lyric", "Приключенческий экшен Соника, Наклза, Тейлза и Эми", 2014, "Sega", "Sega", "WW", "Sonic Boom: Rise of Lyric — популярная игра для Nintendo Wii U."),
                ("Sonic Lost World", "Скоростной 3D/2D платформер Соника на парящем острове Затерянный Хекс", 2013, "Sega", "Sega", "WW", "Sonic Lost World — популярная игра для Nintendo Wii U."),
                ("Splinter Cell: Blacklist", "Стелс-экшен Сэма Фишера с гаджетами и дронами на экране геймпада", 2013, "Ubisoft", "Ubisoft", "WW", "Splinter Cell: Blacklist — популярная игра для Nintendo Wii U."),
                ("Star Fox Guard", "Тактическая оборона базы от роботов с 12 камерами наблюдения на экране ТВ", 2016, "Nintendo", "Nintendo", "WW", "Star Fox Guard — популярная игра для Nintendo Wii U."),
                ("SteamWorld Dig", "Стимпанк-шахтер робот Расти", 2014, "Image & Form", "Image & Form", "WW", "SteamWorld Dig — популярная игра для Nintendo Wii U."),
                ("SteamWorld Heist", "Пошаговая тактика космических стимпанк-роботов с рикошетами пуль", 2016, "Image & Form", "Image & Form", "WW", "SteamWorld Heist — популярная игра для Nintendo Wii U."),
                ("Tekken Tag Tournament 2: Wii U Edition", "Файтинг на 50 бойцов с грибными битвами Mushroom Battle и костюмами Nintendo", 2012, "Bandai Namco", "Bandai Namco", "WW", "Tekken Tag Tournament 2: Wii U Edition — популярная игра для Nintendo Wii U."),
                ("Trine 2: Director's Cut", "Сказочный кооперативный физический платформер Рыцаря, Воровки и Мага", 2012, "Frozenbyte", "Frozenbyte", "WW", "Trine 2: Director's Cut — популярная игра для Nintendo Wii U."),
                ("Wii Sports Club", "HD-реинкарнация Wii Sports с онлайн-клубами регионов и точнейшим управлением MotionPlus", 2013, "Nintendo", "Nintendo", "WW", "Wii Sports Club — популярная игра для Nintendo Wii U."),
                ("Wipeout: Create & Crash", "Полоса препятствий по телешоу", 2013, "Activision", "Activision", "WW", "Wipeout: Create & Crash — популярная игра для Nintendo Wii U."),
                ("ZombiU", "Хардкорный Survival Horror в Лондоне: инвентарь в рюкзаке в реальном времени на GamePad с пермадезом", 2012, "Ubisoft", "Ubisoft", "WW", "ZombiU — популярная игра для Nintendo Wii U."),
                ("The Legend of Zelda: Breath of the Wild (Wii U)", "Экшен с открытым миром", 2017, "Nintendo EPD", "Nintendo", "WW", "Революция открытых миров: физика, химия стихий, скалолазание и святилища."),
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
