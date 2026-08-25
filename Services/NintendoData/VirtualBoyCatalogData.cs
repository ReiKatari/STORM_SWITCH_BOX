using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class VirtualBoyCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(65);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc, string? TitleId, string Edition, string Version, string Languages)[]
            {
                ("3-D BattleSnake (World)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: 3-D BattleSnake (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("3-D Tetris (USA)", "Головоломка", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: 3-D Tetris (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("3D Crosswords (World) (Emulator Version)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: 3D Crosswords (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("3D Crosswords (World) (Hardware Version)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: 3D Crosswords (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("BLOX (World) (v1.0)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: BLOX (WW) от Nintendo.", null, "Revision v1.0", "v1.0", "English"),
                ("BLOX (World) (v1.1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: BLOX (WW) от Nintendo.", null, "Revision v1.1", "v1.1", "English"),
                ("BLOX 2 (World) (En,Fr,De,Es,It,Sv,Fi,Cs,Sl)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: BLOX 2 (WW) от Nintendo.", null, "Standard Edition", "1.0", "Multi-5 (En, Fr, De, Es, It)"),
                ("Bound High (World) (Proto 2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Bound High (WW) от Nintendo.", null, "Prototype", "1.0", "English"),
                ("Capitan Sevilla 2 (World) (Es)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Capitan Sevilla 2 (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Elevated Speed (World) (High Resolution Version)", "Гонки", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Elevated Speed (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Elevated Speed (World) (Low Resolution Version)", "Гонки", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Elevated Speed (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Fishbone (World)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Fishbone (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Fishbone (World) (Demo 1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Fishbone (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Fishbone (World) (Demo 2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Fishbone (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Fishbone (World) (Demo 3)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Fishbone (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Galactic Pinball (Japan, USA) (En)", "Спорт", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Galactic Pinball (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Golf (USA)", "Спорт", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Golf (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Hamburgers En Route to Switzerland (World)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Hamburgers En Route to Switzerland (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Hamburgers En Route to Switzerland (World) (Demo 1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Hamburgers En Route to Switzerland (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Hamburgers En Route to Switzerland (World) (Demo 2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Hamburgers En Route to Switzerland (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Hyper Fighting (World)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Hyper Fighting (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Hyper Fighting (World) (Beta)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Hyper Fighting (WW) от Nintendo.", null, "Beta Version", "1.0", "English"),
                ("Innsmouth no Yakata (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Innsmouth no Yakata (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Insecticide (World)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Insecticide (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Jack Bros. (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Jack Bros. (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Jack Bros. no Meiro de Hiihoo! (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Jack Bros. no Meiro de Hiihoo! (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Mario Clash (Japan, USA) (En)", "Платформер", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Mario Clash (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Mario Combat (World)", "Платформер", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Mario Combat (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Mario Kart - Virtual Cup (World)", "Платформер", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Mario Kart - Virtual Cup (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Mario VB (World)", "Платформер", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Mario VB (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Mario's Tennis (Japan, USA) (En)", "Платформер", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Mario's Tennis (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Nester's Funky Bowling (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Nester's Funky Bowling (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Niko-chan Battle (Japan) (Proto)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Niko-chan Battle (JPN) от Nintendo.", null, "Prototype", "1.0", "Japanese"),
                ("Panic Bomber (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Panic Bomber (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Red Alarm (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Red Alarm (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Red Alarm (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Red Alarm (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Red Square (World)", "Приключения / Экшен", 1995, "Square Enix", "Square Enix", "WW", "Официальный коммерческий релиз для Virtual Boy: Red Square (WW) от Square Enix.", null, "Standard Edition", "1.0", "English"),
                ("SD Gundam - Dimension War (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: SD Gundam - Dimension War (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Snowball Wars (World) (Demo 1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Snowball Wars (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Snowball Wars (World) (Demo 2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Snowball Wars (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Snowball Wars (World) (Demo 3)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Snowball Wars (WW) от Nintendo.", null, "Demo Version", "1.0", "English"),
                ("Soviet Union 2010 (World) (v1.0)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2010 (WW) от Nintendo.", null, "Revision v1.0", "v1.0", "English"),
                ("Soviet Union 2010 (World) (v1.2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2010 (WW) от Nintendo.", null, "Revision v1.2", "v1.2", "English"),
                ("Soviet Union 2011 (World) (Beta 1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2011 (WW) от Nintendo.", null, "Beta Version", "1.0", "English"),
                ("Soviet Union 2011 (World) (Beta 2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2011 (WW) от Nintendo.", null, "Beta Version", "1.0", "English"),
                ("Soviet Union 2011 (World) (v1.0)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2011 (WW) от Nintendo.", null, "Revision v1.0", "v1.0", "English"),
                ("Soviet Union 2011 (World) (v1.1)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2011 (WW) от Nintendo.", null, "Revision v1.1", "v1.1", "English"),
                ("Soviet Union 2011 (World) (v1.2)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Virtual Boy: Soviet Union 2011 (WW) от Nintendo.", null, "Revision v1.2", "v1.2", "English"),
                ("Space Invaders - Virtual Collection (Japan)", "Шутер / Run 'n Gun", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Space Invaders - Virtual Collection (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Space Pinball (Japan) (En) (Proto)", "Спорт", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Space Pinball (JPN) от Nintendo.", null, "Prototype", "1.0", "Japanese"),
                ("Space Squash (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Space Squash (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("T&E Virtual Golf (Japan)", "Спорт", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: T&E Virtual Golf (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Teleroboxer (Japan, USA) (En)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Teleroboxer (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Tobidase! Panibon (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Tobidase! Panibon (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("V-Tetris (Japan) (En)", "Головоломка", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: V-Tetris (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Vertical Force (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Vertical Force (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Vertical Force (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Vertical Force (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Virtual Bowling (Japan) (En)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Virtual Bowling (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Virtual Boy Wario Land (Japan, USA) (En)", "Платформер", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Virtual Boy Wario Land (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Virtual Fishing (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Virtual Fishing (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Virtual Lab (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Virtual Lab (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Virtual League Baseball (USA)", "Спорт", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Virtual League Baseball (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Virtual League Baseball 2 (USA) (Proto)", "Спорт", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Virtual League Baseball 2 (USA) от Nintendo.", null, "Prototype", "1.0", "English / French / Spanish"),
                ("Virtual Pro Yakyuu '95 (Japan)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Virtual Boy: Virtual Pro Yakyuu '95 (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Waterworld (USA)", "Приключения / Экшен", 1995, "Nintendo", "Nintendo", "USA", "Официальный коммерческий релиз для Virtual Boy: Waterworld (USA) от Nintendo.", null, "Standard Edition", "1.0", "English / French / Spanish"),
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