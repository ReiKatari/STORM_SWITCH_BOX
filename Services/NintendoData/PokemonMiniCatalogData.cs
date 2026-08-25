using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class PokemonMiniCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(42);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc, string? TitleId, string Edition, string Version, string Languages)[]
            {
                ("Mini Running Ant (World)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Pokemon Mini: Mini Running Ant (WW) от Nintendo.", null, "Standard Edition", "1.0", "Русский / English"),
                ("Pichu Bros. Mini (Japan)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pichu Bros. Mini (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pichu Bros. Mini - Hoppip's Jump Match (Japan) (Preview)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pichu Bros. Mini - Hoppip's Jump Match (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pichu Bros. Mini - Netsukikyuu (Japan) (Preview)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pichu Bros. Mini - Netsukikyuu (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pichu Bros. Mini - Skateboard (Japan) (Preview)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pichu Bros. Mini - Skateboard (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Anime Card Daisakusen (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Anime Card Daisakusen (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Party Mini (Europe)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Party Mini (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Party Mini (USA)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Party Mini - Baseline Judge (Europe) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Baseline Judge (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Party Mini - Baseline Judge (Japan) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Baseline Judge (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Party Mini - Baseline Judge (USA) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Baseline Judge (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Party Mini - Chansey's Dribble (Europe) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Chansey's Dribble (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Party Mini - Pikachu's Rocket Start (Europe) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Pikachu's Rocket Start (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Party Mini - Ricochet Dribble (Japan) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Ricochet Dribble (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Party Mini - Ricochet Dribble (USA) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Ricochet Dribble (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Party Mini - Rocket Start (USA) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Rocket Start (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Party Mini - Slowking's Judge (Europe) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Party Mini - Slowking's Judge (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Pinball Mini (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Pinball Mini (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Pinball Mini (USA, Europe)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Pinball Mini (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Puzzle Collection (France)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "FRA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (FRA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Puzzle Collection (France) (GameCube Preview)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "FRA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (FRA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Puzzle Collection (Germany)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "GER", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (GER) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Puzzle Collection (Germany) (GameCube Preview)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "GER", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (GER) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Puzzle Collection (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Puzzle Collection (Japan) (GameCube)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Puzzle Collection (USA) (GameCube Preview)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Puzzle Collection (USA, Europe)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Pokemon Puzzle Collection Vol. 2 (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Puzzle Collection Vol. 2 (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Race Mini (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Race Mini (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Race Mini (Japan) (Preview)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Race Mini (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Shock Tetris (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Shock Tetris (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Sodateyasan Mini (Japan)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Sodateyasan Mini (JPN) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "Japanese"),
                ("Pokemon Tetris (Europe) (En,Fr,De)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Tetris (EUR) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Pokemon Zany Cards (France)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "FRA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Zany Cards (FRA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Zany Cards (Germany)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "GER", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Zany Cards (GER) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English"),
                ("Pokemon Zany Cards (USA, Europe)", "JRPG / Приключения", 2001, "Game Freak", "Nintendo / The Pokémon Company", "USA", "Официальный коммерческий релиз для Pokemon Mini: Pokemon Zany Cards (USA) от Nintendo / The Pokémon Company.", null, "Standard Edition", "1.0", "English / French / Spanish"),
                ("Silver Falls - Monsters In North Island (World)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "WW", "Официальный коммерческий релиз для Pokemon Mini: Silver Falls - Monsters In North Island (WW) от Nintendo.", null, "Standard Edition", "1.0", "English"),
                ("Snorlax's Lunch Time (Europe) (GameCube)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "EUR", "Официальный коммерческий релиз для Pokemon Mini: Snorlax's Lunch Time (EUR) от Nintendo.", null, "Standard Edition", "1.0", "English / French / German / Spanish / Italian"),
                ("Snorlax's Lunch Time (Japan) (GameCube)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Snorlax's Lunch Time (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Togepi no Daibouken (Japan)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Togepi no Daibouken (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
                ("Togepi no Daibouken (Japan) (Preview)", "Приключения / Экшен", 2001, "Nintendo", "Nintendo", "JPN", "Официальный коммерческий релиз для Pokemon Mini: Togepi no Daibouken (JPN) от Nintendo.", null, "Standard Edition", "1.0", "Japanese"),
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