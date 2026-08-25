using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class Switch2CatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(25);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc, string? TitleId, string Edition, string Version, string Languages)[]
            {
                ("Mario Kart 9 / Next-Gen Mario Kart", "Гонки", 2025, "Nintendo EPD", "Nintendo", "WW", "Следующая масштабная часть серии Mario Kart для Nintendo Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Super Mario 3D Universe (Next 3D Mario)", "3D Платформер", 2025, "Nintendo EPD Tokyo", "Nintendo", "WW", "Флагманский 3D-платформер нового поколения про Марио для Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("The Legend of Zelda: Breath of the Wild (Switch 2 Enhanced Edition)", "Action-Adventure", 2025, "Nintendo EPD", "Nintendo", "WW", "Улучшенная версия в 4K 60FPS с DLSS и поддержкой трассировки лучей.", null, "Enhanced Edition", "2.0", "Русский / English / Japanese / Multi"),
                ("The Legend of Zelda: Tears of the Kingdom (Switch 2 Enhanced Edition)", "Action-Adventure", 2025, "Nintendo EPD", "Nintendo", "WW", "Улучшенная версия Tears of the Kingdom с повышенной частотой кадров и 4K.", null, "Enhanced Edition", "2.0", "Русский / English / Japanese / Multi"),
                ("Metroid Prime 4: Beyond (Switch 2 Edition)", "Action-Adventure / FPS", 2025, "Retro Studios", "Nintendo", "WW", "Флагманский научно-фантастический шутер от первого лица для Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Super Smash Bros. Ultimate Deluxe (Switch 2)", "Файтинг", 2025, "Bandai Namco / Sora", "Nintendo", "WW", "Полное издание со всеми бойцами, улучшенной графикой и сетевым кодом.", null, "Deluxe Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Pokemon Gen 10 (Next Generation)", "JRPG", 2026, "Game Freak", "Nintendo / The Pokémon Company", "WW", "Юбилейное 10-е поколение покемонов в бесшовном открытом мире для Switch 2.", null, "Standard Edition", "1.0", "English / Japanese / French / German / Spanish / Italian"),
                ("Pokemon Legends: Z-A (Switch 2 Edition)", "Action-RPG", 2025, "Game Freak", "Nintendo / The Pokémon Company", "WW", "Приключения в обновленном Люмиос-Сити с ультра-высокой детализацией.", null, "Standard Edition", "1.0", "English / Japanese / French / German / Spanish / Italian"),
                ("Donkey Kong Freedom (Next 3D DK)", "3D Платформер", 2025, "Nintendo EPD", "Nintendo", "WW", "Долгожданное трехмерное приключение Данки Конга нового поколения.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("Animal Crossing: Next Island", "Симулятор жизни", 2026, "Nintendo EPD", "Nintendo", "WW", "Новая эра социальной жизни и строительства на Nintendo Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Xenoblade Chronicles 4 / Next Project", "JRPG / Sci-Fi", 2026, "Monolith Soft", "Nintendo", "WW", "Грандиозная ролевая сага нового поколения от Monolith Soft.", null, "Standard Edition", "1.0", "English / Japanese / French"),
                ("Splatoon 4 (Next-Gen Turf War)", "Командный шутер", 2026, "Nintendo EPD", "Nintendo", "WW", "Новое поколение чернильных командных баталий с рейтрейсингом.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("Fire Emblem: Path of Radiance / Radiant Dawn Remake", "Тактическая RPG", 2025, "Intelligent Systems", "Nintendo", "WW", "Полный ремейк саги об Айке на движке Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("Luigi's Mansion 4", "Приключения / Экшен", 2026, "Next Level Games", "Nintendo", "WW", "Новые приключения Луиджи с пылесосом Полтергаст нового поколения.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("F-Zero GX 2 / F-Zero SX", "Футуристические гонки", 2025, "Amusement Vision / Nintendo", "Nintendo", "WW", "Возвращение сверхзвуковых антигравитационных гонок в 120 FPS.", null, "Standard Edition", "1.0", "English / Multi"),
                ("Star Fox Horizons", "3D Космосим / Шутер", 2026, "PlatinumGames / Nintendo", "Nintendo", "WW", "Масштабные космические битвы команды Фокса МакКлауда.", null, "Standard Edition", "1.0", "English / Multi"),
                ("Bayonetta 4", "Стильный слэшер", 2026, "PlatinumGames", "Nintendo", "WW", "Продолжение саги ведьмы Байонетты с ультра-высокой частотой кадров.", null, "Standard Edition", "1.0", "English / Japanese / Multi"),
                ("Monster Hunter Wilds (Switch 2 Portable Edition)", "Action-RPG", 2025, "Capcom", "Capcom", "WW", "Портативная версия Monster Hunter Wilds с поддержкой DLSS.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("Grand Theft Auto VI (Switch 2 Edition)", "Open-World Экшен", 2026, "Rockstar Studios", "Rockstar Games", "WW", "Флагманский открытый мир следующего поколения.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
                ("Elden Ring: Definitive Edition (Switch 2)", "Action-RPG / Соулслайк", 2025, "FromSoftware", "Bandai Namco", "WW", "Шедевр FromSoftware со всеми дополнениями для Switch 2.", null, "Definitive Edition", "1.0", "Русский / English / Multi"),
                ("Final Fantasy VII Remake Intergrade (Switch 2)", "Action-RPG", 2025, "Square Enix", "Square Enix", "WW", "Первая часть трилогии ремейка культовой Final Fantasy VII.", null, "Intergrade Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Cyberpunk 2077: Ultimate Edition (Switch 2)", "Sci-Fi RPG / FPS", 2025, "CD PROJEKT RED", "CD PROJEKT RED", "WW", "Полное издание с Phantom Liberty и трассировкой лучей DLSS.", null, "Ultimate Edition", "1.0", "Русский / English / Multi"),
                ("Persona 6", "JRPG / Симулятор жизни", 2026, "Atlus / P-Studio", "Sega", "WW", "Новая номерная часть культовой серии Persona для Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Japanese / Multi"),
                ("Dragon Quest XII: The Flames of Fate", "JRPG", 2026, "Square Enix", "Square Enix", "WW", "Новая темная глава в легендарной серии Dragon Quest.", null, "Standard Edition", "1.0", "English / Japanese / Multi"),
                ("Hollow Knight: Silksong", "Метроидвания", 2025, "Team Cherry", "Team Cherry", "WW", "Долгожданное приключение принцессы Хорнет на Switch 2.", null, "Standard Edition", "1.0", "Русский / English / Multi"),
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
                    Desc = g.Desc,
                    TitleId = g.TitleId,
                    Edition = g.Edition,
                    Version = g.Version,
                    Languages = g.Languages
                });
            }

            return list;
        }
    }
}