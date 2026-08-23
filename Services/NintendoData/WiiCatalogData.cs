using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class WiiCatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(300);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Mario Galaxy", "3D Гравитационный платформер", 2007, "Nintendo EAD Tokyo", "Nintendo", "WW", "Один из величайших шедевров видеоигр: сферическая гравитация планет, Розалина, Обсерватория Комет и симфонический оркестр."),
                ("Super Mario Galaxy 2", "3D Платформер", 2010, "Nintendo EAD Tokyo", "Nintendo", "WW", "Йоши, дрель, облачный костюм, 242 зеленые кометы и непревзойденный левел-дизайн Миямото."),
                ("The Legend of Zelda: Skyward Sword", "Action-Adventure с точным моушн-управлением", 2011, "Nintendo EAD", "Nintendo", "WW", "Линк и летающая птица Лофтвинг в небесном городе Скайлофт с дуэлями 1-в-1 через Wii MotionPlus."),
                ("The Legend of Zelda: Twilight Princess (Wii)", "Action-Adventure", 2006, "Nintendo EAD", "Nintendo", "WW", "Взмахи мечом с Wii Remote, стрельба из лука по экрану и исследование Хайрула."),
                ("Xenoblade Chronicles", "Масштабная JRPG / Открытый мир", 2010, "Monolith Soft", "Nintendo", "WW", "Шулк с мечом Монадо видит видения будущего на телах двух застывших титанов Биониса и Мехониса."),
                ("Super Smash Bros. Brawl", "Файтинг / Кроссовер", 2008, "Sora Ltd. / Game Arts", "Nintendo", "WW", "Сюжетная кампания Subspace Emissary, появление Соника и Снейка, финальные сокрушительные удары Final Smash."),
                ("Mario Kart Wii", "Гонки", 2008, "Nintendo EAD", "Nintendo", "WW", "Мотоциклы с вилли, руль Wii Wheel, 12 игроков на трассе и свыше 37 млн проданных копий по всему миру."),
                ("Donkey Kong Country Returns", "2D Платформер", 2010, "Retro Studios", "Nintendo", "WW", "Возвращение легенды от Retro Studios: племя Тики Так, вагонетки, ракетные бочки и хардкорная сложность."),
                ("Metroid Prime 3: Corruption", "Action-Adventure / FPS", 2007, "Retro Studios", "Nintendo", "WW", "Самус Аран управляет пушкой с идеальной точностью наведения инфракрасной указки Wii Remote и использует режим Hypermode."),
                ("Metroid Prime: Trilogy", "Сборник трилогии", 2009, "Retro Studios", "Nintendo", "WW", "Все три части Metroid Prime на одном диске с полным моушн-управлением и широкоформатной графикой 16:9."),
                ("Metroid: Other M", "Экшен от третьего лица / FPS", 2010, "Team Ninja / Nintendo", "Nintendo", "WW", "Кинематографичная история отношений Самус и командира Адама Малковича с переключением прицела в вид из глаз."),
                ("Wii Sports", "Спортивный симулятор", 2006, "Nintendo EAD", "Nintendo", "WW", "Революция казуального гейминга с теннисом, боулингом, бейсболом, гольфом и боксом для аватаров Mii."),
                ("Wii Sports Resort", "Спорт с Wii MotionPlus", 2009, "Nintendo EAD", "Nintendo", "WW", "Курортный остров Вуху: бои на мечах, стрельба из лука, водные лыжи, фрисби и полет на самолете с точностью 1-к-1."),
                ("Wii Fit / Wii Fit Plus", "Фитнес с баланс-бордом", 2007, "Nintendo EAD", "Nintendo", "WW", "Йога, силовые упражнения, аэробные тренировки и балансировочные игры на напольной доске Wii Balance Board."),
                ("New Super Mario Bros. Wii", "2D Кооперативный платформер", 2009, "Nintendo EAD", "Nintendo", "WW", "Синхронный безумный кооператив на 4 игроков одновременно с костюмом пропеллера и пингвина."),
                ("Kirby's Return to Dream Land (Kirby's Adventure Wii)", "2D Платформер", 2011, "HAL Laboratory", "Nintendo", "WW", "Кооператив на 4 игроков, супер-способности Super Abilities разрушают экраны гигантскими мечами."),
                ("Kirby's Epic Yarn", "Пряжечный платформер", 2010, "Good-Feel / HAL", "Nintendo", "WW", "Уютный мир ткани и пряжи: Кирби превращается в танк, парашют, поезд и дельфина из ниток."),
                ("The Last Story", "Action-RPG", 2011, "Mistwalker / AQ Interactive", "Nintendo", "WW", "Хиронобу Сакагути: Заэль и наемники на острове Лазулис со стрелковой системой укрытий и магией."),
                ("Pandora's Tower", "Action-RPG / Слэшер", 2011, "Ganbarion", "Nintendo", "WW", "Эрон штурмует 13 башен со стальной цепью Орифалк, чтобы скормить плоть монстров возлюбленной Елене против проклятия."),
                ("Sin and Punishment: Star Successor", "3D Рельсовый SHMUP", 2009, "Treasure", "Nintendo", "WW", "Иса и Качи в адреналиновом шедевре от Treasure с полетами на джетпаках и отражением пуль мечом."),
                ("Red Steel 2", "FPS / Слэшер самурая", 2010, "Ubisoft Paris", "Ubisoft", "WW", "Стимпанк-вестерн с фехтованием катаной и револьвером через идеальный отклик Wii MotionPlus."),
                ("Monster Hunter Tri", "Action-RPG / Охота на чудовищ", 2009, "Capcom", "Capcom", "WW", "Подводная охота на морского левиафана Лагиакруса и гигантов в высоком разрешении."),
                ("Silent Hill: Shattered Memories", "Психологический хоррор", 2009, "Climax Studios", "Konami", "WW", "Гарри Мейсон ищет дочь Шерил в замерзшем Сайлент Хилле с фонариком Wii Remote и мобильным телефоном."),
                ("Resident Evil: The Umbrella Chronicles", "Рельсовый кооперативный тир", 2007, "Cavia / Capcom", "Capcom", "WW", "История падения корпорации Umbrella от лица Альберта Вескера."),
                ("Resident Evil: The Darkside Chronicles", "Рельсовый тир", 2009, "Cavia / Capcom", "Capcom", "WW", "События RE2 и Code: Veronica с динамической камерой от первого лица."),
                ("No More Heroes", "Экшен / Слэшер 18+", 2007, "Grasshopper Manufacture", "Marvelous / Ubisoft", "WW", "Отаку Трэвис Тачдаун с лазерной катаной Beam Katana поднимается в топ-10 наемных убийц Санта-Дестроя от Гоити Суды (Suda51)."),
                ("No More Heroes 2: Desperate Struggle", "Экшен / Слэшер", 2010, "Grasshopper Manufacture", "Ubisoft", "WW", "Трэвис с двумя лазерными катанами мстит за друга с пиксельными ретро-мини-играми 8-бит.")
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
