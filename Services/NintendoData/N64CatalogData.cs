using System;
using System.Collections.Generic;

namespace StormSwitchBox.Services
{
    public static class N64CatalogData
    {
        public static List<NintendoCatalogItem> GetCatalog()
        {
            var list = new List<NintendoCatalogItem>(400);

            var games = new (string Title, string Genre, int Year, string Dev, string Pub, string Region, string Desc)[]
            {
                ("Super Mario 64", "3D Платформер", 1996, "Nintendo EAD", "Nintendo", "WW", "Революция трехмерных игр: 360-градусное аналоговое управление, прыжки в картины и 120 звезд замка Пич."),
                ("The Legend of Zelda: Ocarina of Time", "Action-Adventure", 1998, "Nintendo EAD", "Nintendo", "WW", "Величайшая игра в истории индустрии по версии критиков: захват цели Z-Targeting, окарина времени и путешествия сквозь эпохи."),
                ("The Legend of Zelda: Majora's Mask", "Action-Adventure", 2000, "Nintendo EAD", "Nintendo", "WW", "Мрачный шедевр с 3-дневным циклом апокалипсиса падающей Луны, масками превращений и терманом времени."),
                ("GoldenEye 007", "FPS Шутер", 1997, "Rare", "Nintendo", "WW", "Революция консольных шутеров от первого лица: скрытные убийства, снайперские винтовки и легендарный сплит-скрин на 4 игроков."),
                ("Perfect Dark", "FPS Шутер", 2000, "Rare", "Nintendo", "WW", "Агент Джоанна Дарк против инопланетной корпорации dataDyne с продвинутыми ботами-симмуляторами и тепловизором."),
                ("Banjo-Kazooie", "3D Платформер", 1998, "Rare", "Nintendo", "WW", "Медведь Банджо и птица Казуи собирают музыкальные ноты и джигги-пазлы в мире ведьмы Грантилды."),
                ("Banjo-Tooie", "3D Платформер", 2000, "Rare", "Nintendo", "WW", "Огромный взаимосвязанный мир с шутерными секциями от первого лица и разделением героев."),
                ("Donkey Kong 64", "3D Платформер", 1999, "Rare", "Nintendo", "WW", "Пять играбельных приматов, кокосовые пушки, музыкальные инструменты и требование модуля Expansion Pak 8MB."),
                ("Conker's Bad Fur Day", "Экшен / 3D Платформер 18+", 2001, "Rare", "THQ", "WW", "Белка-пьяница Конкер в черной комедии с пародиями на Матрицу, Спасение рядового Райана и поющего Great Mighty Poo."),
                ("Mario Kart 64", "Гонки", 1996, "Nintendo EAD", "Nintendo", "WW", "Четырехпользовательский сплит-скрин, синий панцирь со спикелями и трассы Rainbow Road и Block Fort."),
                ("Super Smash Bros.", "Файтинг", 1999, "HAL Laboratory", "Nintendo", "WW", "Рождение кроссовер-файтинга от Масахиро Сакурая и Сатору Иваты: выброс соперников с арены процентами урона."),
                ("Paper Mario", "JRPG", 2000, "Intelligent Systems", "Nintendo", "WW", "Бумажный визуальный стиль, интерактивные тайминги ударов в бою и сбор 7 Звездных Духов."),
                ("Star Fox 64 (Lylat Wars)", "3D Рельсовый шутер", 1997, "Nintendo EAD", "Nintendo", "WW", "Фокс МакКлауд, Фалько, Пеппи и Слиппи с голосовой озвучкой, разветвлениями планет и первым вибро-модулем Rumble Pak."),
                ("F-Zero X", "Гонки", 1998, "Nintendo EAD", "Nintendo", "WW", "Честные 60 кадров в секунду при 30 болидах на трассе одновременно, смертоносные атаки спином и генератор треков X-Cup."),
                ("Diddy Kong Racing", "Гонки / Приключения", 1997, "Rare", "Rare", "WW", "Сюжетный режим с миром-хабом, выбор карта, катера на воздушной подушке или самолета и битва с Визпигом."),
                ("Wave Race 64", "Водные гонки", 1996, "Nintendo EAD", "Nintendo", "WW", "Революционная трехмерная физика реалистичных океанских волн и дельфины."),
                ("1080° Snowboarding", "Сноубординг", 1998, "Nintendo EAD", "Nintendo", "WW", "Физика трения снега и ткани одежды от Джайлса Годдарда, вращения на 1080 градусов."),
                ("Pokemon Stadium", "Пошаговые битвы", 1999, "Nintendo EAD / HAL", "Nintendo", "WW", "3D-сражения покемонов с переносом сейвов с картриджей Game Boy через Transfer Pak."),
                ("Pokemon Stadium 2", "Пошаговые битвы", 2000, "Nintendo EAD", "Nintendo", "WW", "251 покемон, академия Эрла, викторины и мини-игры на 4 игроков."),
                ("Pokemon Snap", "Фото-симулятор", 1999, "HAL Laboratory", "Nintendo", "WW", "Тодд Снэп фотографирует диких покемонов в их естественной среде на острове профессора Оука."),
                ("Mario Party", "Настольная игра / Party", 1998, "Hudson Soft", "Nintendo", "WW", "Рождение жанра цифровых настолок со 100 мини-играми и вращением аналогового стика."),
                ("Mario Party 2", "Настольная игра / Party", 1999, "Hudson Soft", "Nintendo", "WW", "Тематические костюмы для каждого мира: вестерн, космос, пираты, магия и ужасы."),
                ("Mario Party 3", "Настольная игра / Party", 2000, "Hudson Soft", "Nintendo", "WW", "Дуэльный режим с партнерами-монстрами и предметы инвентаря."),
                ("Mario Tennis", "Спорт", 2000, "Camelot", "Nintendo", "WW", "Теннис от Camelot, впервые представивший Валуиджи миру."),
                ("Mario Golf", "Спорт", 1999, "Camelot", "Nintendo", "WW", "Реалистичная физика мяча, ветра и высоты рельефа с персонажами Марио."),
                ("Rayman 2: The Great Escape", "3D Платформер", 1999, "Ubi Pictures", "Ubi Soft", "WW", "Рэйман спасает Поляну Грез от робопиратов адмирала Остроборода."),
                ("Ogre Battle 64: Person of Lordly Caliber", "Тактическая стратегия / RPG", 1999, "Quest", "Atlus", "WW", "Магнус Галлант возглавляет революцию Палатинус с глубоким ветвлением сюжета."),
                ("Sin and Punishment", "3D Рельсовый экшен", 2000, "Treasure", "Nintendo", "JPN", "Ураганный безостановочный боевик от студии Treasure с прицелом и мечом."),
                ("Resident Evil 2", "Survival Horror", 1999, "Angel Studios / Capcom", "Capcom", "WW", "Техническое чудо: две полные FMV-кампании Леона и Клэр на 64-мегабайтном картридже без загрузок."),
                ("Turok: Dinosaur Hunter", "FPS Шутер", 1997, "Iguana Entertainment", "Acclaim", "WW", "Охота на динозавров и киборгов из лука и термоядерной пушки Chronoscepter."),
                ("Turok 2: Seeds of Evil", "FPS Шутер", 1998, "Iguana Entertainment", "Acclaim", "WW", "Знаменитый церебральный бур (Cerebral Bore) и огромные уровни в высоком разрешении Hi-Res."),
                ("Star Wars Episode I: Racer", "Гонки на подах", 1999, "LucasArts", "LucasArts", "WW", "Сверхзвуковые гонки Энакина Скайуокера и Себульбы со скоростью свыше 1000 км/ч."),
                ("Star Wars: Rogue Squadron", "3D Авиасимулятор", 1998, "Factor 5", "LucasArts", "WW", "Люк Скайуокер и эскадрилья X-Wing против Империи с чипом звука MusyX и Expansion Pak."),
                ("Blast Corps", "Экшен / Головоломка", 1997, "Rare", "Nintendo", "WW", "Разрушение зданий бульдозерами, роботами и грузовиками для расчистки пути ядерному тягачу."),
                ("Jet Force Gemini", "Экшен от третьего лица", 1999, "Rare", "Nintendo", "WW", "Юно, Вела и собака Лупус спасают инопланетных мишек Триболов от жуков Мизара."),
                ("Body Harvest", "3D Экшен с открытым миром", 1998, "DMA Design (Rockstar North)", "Midway", "WW", "Предтеча GTA 3: путешествия сквозь века, угон любого транспорта и отражение нашествия пришельцев."),
                ("Mischief Makers", "2D Платформер", 1997, "Treasure", "Nintendo", "WW", "Робот-служанка Марина хватает, трясет и швыряет любые объекты."),
                ("WWF No Mercy", "Рестлинг", 2000, "AKI Corporation", "THQ", "WW", "Лучший симулятор рестлинга с непревзойденной механикой захватов и создания бойцов."),
                ("WWF WrestleMania 2000", "Рестлинг", 1999, "AKI Corporation", "THQ", "WW", "Эпоха Attitude с The Rock, Stone Cold Стивом Остином и Гробовщиком."),
                ("Excitebike 64", "Мотокросс", 2000, "Left Field Productions", "Nintendo", "WW", "Продвинутая физика мотокросса, стадионные заезды и редактор 3D-треков."),
                ("Snowboard Kids", "Сноуборд-гонки с предметами", 1997, "Racdym", "Atlus", "WW", "Гонки на сноубордах со стрельбой бомбами, парашютами и канатными дорогами."),
                ("Snowboard Kids 2", "Сноуборд-гонки", 1999, "Racdym", "Atlus", "WW", "Новые персонажи, подводные и космические зимние трассы."),
                ("Glover", "Физический 3D-платформер", 1998, "Interactive Studios", "Hasbro", "WW", "Волшебная перчатка катит, подбрасывает и ведет резиновый и хрустальный шары.")
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
