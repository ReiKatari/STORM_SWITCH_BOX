using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class WiiUCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(200);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Mario 3D World", "3D Кооперативный платформер", 2013, "Nintendo EAD Tokyo", "Nintendo", "WW", "Марио, Луиджи, Пич, Тоад и Розалина в костюмах кошек с колокольчиками, прозрачными трубами и экраном геймпада Wii U GamePad."),
                ("Mario Kart 8", "Антигравитационные гонки", 2014, "Nintendo EAD", "Nintendo", "WW", "Езда по стенам и потолкам, бумеранг, супер-рожок против синего панциря и онлайн-сервис Mario Kart TV."),
                ("Super Smash Bros. for Wii U", "Файтинг / Кроссовер", 2014, "Bandai Namco / Sora Ltd.", "Nintendo", "WW", "Битвы на 8 игроков на одной арене одновременно, режим настолки Smash Tour и создание арены пальцем на тачскрине."),
                ("The Legend of Zelda: The Wind Waker HD", "Action-Adventure", 2013, "Nintendo EAD", "Nintendo", "WW", "Великолепный ремейк в честном 1080p с новым парусом Swift Sail, инвентарем на экране геймпада и сообщениями в бутылках Miiverse."),
                ("The Legend of Zelda: Twilight Princess HD", "Action-Adventure", 2016, "Tantalus / Nintendo", "Nintendo", "WW", "HD-ремейк с пещерой теней Cave of Shadows для фигурки Wolf Link amiibo и поддержкой быстрого превращения в волка."),
                ("The Legend of Zelda: Breath of the Wild (Wii U)", "Action-Adventure / Открытый мир", 2017, "Nintendo EPD", "Nintendo", "WW", "Оригинальная версия революционного шедевра для двух экранов."),
                ("Xenoblade Chronicles X", "Sci-Fi RPG / Открытый мир", 2015, "Monolith Soft", "Nintendo", "WW", "Выжившие земляне колонизируют бескрайнюю планету Мира на шагающих и трансформирующихся мехах Скеллах (Skells)."),
                ("Super Mario Maker", "Конструктор уровней / Платформер", 2015, "Nintendo EAD", "Nintendo", "WW", "Создание собственных уровней Марио стилусом на экране геймпада с миллионами уровней сообщества через Course World."),
                ("Splatoon", "Командный шутер краской 4х4", 2015, "Nintendo EAD", "Nintendo", "WW", "Инклинги закрашивают территорию чернилами и плавают в них кальмарами с супер-прыжками по карте геймпада."),
                ("Bayonetta 2", "Слэшер / Ураганный экшен", 2014, "PlatinumGames", "Nintendo", "WW", "Ведьма Байонетта с пистолетами на каблуках, замедлением времени Witch Time и режимом Umbran Climax."),
                ("Donkey Kong Country: Tropical Freeze", "2D Платформер", 2014, "Retro Studios", "Nintendo", "WW", "Викинги-снежики замораживают остров Конгов: Донки, Дидди, Дикси и дедушка Крэнки с тростью-пого под музыку Дэвида Уайза."),
                ("Pikmin 3", "RTS / Головоломка", 2013, "Nintendo EAD", "Nintendo", "WW", "Три исследователя Альф, Бриттани и Чарли командуют каменными и летающими розовыми пикминами по карте KopPad."),
                ("Captain Toad: Treasure Tracker", "Изометрическая головоломка", 2014, "Nintendo EAD Tokyo", "Nintendo", "WW", "Капитан Тоад с тяжелым рюкзаком не умеет прыгать, но исследует трехмерные диорамы-коробки."),
                ("Yoshi's Woolly World", "Платформер из пряжи", 2015, "Good-Feel", "Nintendo", "WW", "Йоши из пряжи разматывает нити и бросает клубки в очаровательном рукодельном мире."),
                ("Tokyo Mirage Sessions #FE", "JRPG / Поп-идолы", 2015, "Atlus", "Nintendo", "WW", "Кроссовер Shin Megami Tensei и Fire Emblem в индустрии развлечений Токио с боевой системой связок Session Attacks."),
                ("Paper Mario: Color Splash", "Action-Adventure / RPG", 2016, "Intelligent Systems", "Nintendo", "WW", "Бумажный Марио раскрашивает молотом обесцвеченный остров Призма с боевыми карточками на тачскрине."),
                ("Rayman Legends (Wii U)", "Платформер", 2013, "Ubisoft Montpellier", "Ubisoft", "WW", "Уникальный геймплей за зеленого духа Мерфи стилусом и музыкальные ритм-уровни под Castle Rock."),
                ("ZombiU", "Survival Horror", 2012, "Ubisoft Montpellier", "Ubisoft", "WW", "Лондонский зомби-апокалипсис: рюкзак выживания на экране геймпада, перманентная смерть и поиск своего зараженного тела.")
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
