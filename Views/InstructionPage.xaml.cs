using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace StormSwitchBox.Views
{
    public sealed partial class InstructionPage : Page
    {
        public class TopicItem
        {
            public string Title { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string DescriptionText { get; set; } = string.Empty;
            public string Tip { get; set; } = string.Empty;
            public Action<StackPanel> SetupPreview { get; set; } = _ => { };
        }

        private List<TopicItem> _allTopics = new List<TopicItem>();
        private ObservableCollection<TopicItem> _filteredTopics = new ObservableCollection<TopicItem>();
        private StackPanel? _simulatorResultsPanel;

        public InstructionPage()
        {
            this.InitializeComponent();
            InitializeTopics();
            TopicList.ItemsSource = _filteredTopics;
            
            if (_filteredTopics.Count > 0)
            {
                TopicList.SelectedIndex = 0;
            }

            ApplyLocalization();
            App.Localization.LanguageChanged += () => App.RunOnUI(ApplyLocalization);
        }

        public void ApplyLocalization()
        {
            var loc = App.Localization;
            if (PageHeaderTitle != null) PageHeaderTitle.Text = loc["Nav_Instruction"];
            if (SearchBox != null) SearchBox.PlaceholderText = loc["Catalog_Search_Placeholder"] ?? "Поиск тем...";
            if (PreviewHeader != null) PreviewHeader.Text = loc.CurrentLanguage switch
            {
                "en" => "Interactive Preview & Simulation",
                "de" => "Interaktive Vorschau & Simulation",
                "zh" => "交互式预览与模拟",
                "ja" => "インタラクティブプレビュー＆シミュレーション",
                _ => "Интерактивный предпросмотр и симуляция"
            };

            int prevIndex = TopicList?.SelectedIndex ?? 0;
            InitializeTopics();
            if (TopicList != null && _filteredTopics.Count > 0)
            {
                TopicList.SelectedIndex = Math.Clamp(prevIndex, 0, _filteredTopics.Count - 1);
            }
        }

        private void InitializeTopics()
        {
            string lang = App.Localization.CurrentLanguage?.ToLowerInvariant() ?? "ru";
            _allTopics = lang switch
            {
                "en" => GetTopicsEn(),
                "de" => GetTopicsDe(),
                "zh" => GetTopicsZh(),
                "ja" => GetTopicsJa(),
                _ => GetTopicsRu()
            };

            FilterTopics(SearchBox?.Text ?? string.Empty);
        }

        private List<TopicItem> GetTopicsRu()
        {
            return new List<TopicItem>
            {
                new TopicItem
                {
                    Title = "Обзор приложения",
                    Category = "Введение",
                    Icon = "\uE9CE",
                    DescriptionText = "STORM SWITCH BOX v4.9.6 — это профессиональный высокопроизводительный комбайн для всесторонней обработки образов игр Nintendo Switch и Nintendo 3DS, а также интерактивная энциклопедия всех 19 поколений игровых систем Nintendo (от Color TV-Game до Nintendo Switch 2).\n\nПрограмма оснащена системой «Умная обработка файлов» (Smart Processing), которая работает всегда и автоматически выбирает оптимальный метод сборки (нативное сшивание без раздувания RomFS для легких патчей или HardPatch для тяжелых обновлений и модов), распаковывает ресурсы, компилирует файлы в NSP/NSZ/3DS/CIA, конвертирует форматы внутри экосистем (Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ; 3DS: 3DS ↔ CIA ↔ CXI), объединяет игры с обновлениями, дополнениями (DLC) и модификациями в единый монолитный файл (Мульти-контент 4-в-1), автоматически собирает Homebrew порты и игры в один файл, осуществляет независимый мониторинг «Умных папок» Switch и 3DS, а также мгновенно сохраняет историю в LocalAppData.",
                    Tip = "Переключайтесь между платформами Switch и 3DS в один клик через верхний селектор или настраивайте независимое отслеживание папок!",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "⚡ STORM SWITCH BOX v4.9.6", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Умная обработка файлов: идеальный баланс размера и функционала по умолчанию\n• Поддержка двух экосистем: Nintendo Switch и Nintendo 3DS с изолированными конвертациями\n• Интерактивная «Библиотека игр» всех 19 поколений Nintendo (No-Intro & Redump)\n• Раздел «Информация» с визуальными плашками платформ на обложках\n• Две независимые службы «Умная папка» (Switch и 3DS)\n• Встроенный сверхбыстрый движок 7-Zip и ZstdSharp (до 22 уровня сжатия)", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Умная обработка файлов",
                    Category = "Алгоритмы",
                    Icon = "\uE945",
                    DescriptionText = "Интеллектуальный алгоритм автоматического выбора метода сборки (Smart Processing), внедренный в v4.9.6:\n\n" +
                                      "Цель алгоритма: получить абсолютно минимальный размер выходного файла при 100% сохранении всего функционала, модов и дополнений.\n\n" +
                                      "Как работает авто-анализ:\n" +
                                      "1. Легковесные патчи (напр. Ys X Nordics: патч 60 МБ на игру 6.75 ГБ) — программа применяет нативное сшивание LibHac PFS0. Это сохраняет оригинальный несжатый размер (6.81 ГБ) без раздувания RomFS до 10.4 ГБ!\n" +
                                      "2. Массивные обновления (напр. The Witcher 3, MK11: патч >= 40% от базы) — программа запускает физический HardPatch, который удаляет старые 10 ГБ устаревших файлов и заменяет их новыми ресурсами из обновления, экономя гигабайты дискового пространства!\n" +
                                      "3. Наличие папок модификаций (romfs, exefs, exefs_patches) — автоматически включает HardPatch для надежного внедрения перевода и модов прямо в бинарные ресурсы игры.\n\n" +
                                      "Вся логика решений наглядно отображается в логе задачи с иконкой 🧠.",
                    Tip = "Умная обработка активна всегда и по умолчанию — вам больше не нужно вручную думать, когда пересобирать, а когда сшивать!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🧠 Умный анализ файлов (Smart Processing):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• Легкий патч: [Нативное сшивание] → Минимальный размер (6.81 ГБ вместо 10.4 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Тяжелый патч: [HardPatch] → Замена ресурсов и удаление устаревших данных", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Папки модов: [HardPatch] → Физическая инъекция русификатора в RomFS", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Симулятор группировки задач",
                    Category = "Интерактив",
                    Icon = "\uE8E5",
                    DescriptionText = "Интерактивный симулятор алгоритма группировки задач.\n\nПеретащите реальные файлы/папки в зону ниже или выберите один из готовых сценариев («Dispatch» или «Cadence of Hyrule»), чтобы увидеть, как программа сформирует изолированные комплектные задачи (ИГРА + UPDATE + DLC + ROMFS/EXEFS), определит RomFS для нужных папок и выведет полный сгруппированный результат построчно с нумерацией.",
                    Tip = "Перетаскивайте папки с несколькими релизами прямо в симулятор: вы сразу увидите, как файлы разделятся по независимым задачам!",
                    SetupPreview = container => BuildSimulatorPreview(container)
                },
                new TopicItem
                {
                    Title = "Встроенный 7-Zip и Автораспаковка",
                    Category = "Архивы",
                    Icon = "\uE8F1",
                    DescriptionText = "STORM SWITCH BOX содержит встроенный движок 7-Zip и не требует отдельной установки архиваторов в системе.\n\n" +
                                      "Особенности работы:\n" +
                                      "1. Автораспаковка при добавлении — перетащите архив (.zip, .rar, .7z) или папку с архивами в программу, и содержимое будет автоматически извлечено.\n" +
                                      "2. Умный пропуск повторного извлечения — если рядом с архивом уже есть папка с ранее распакованным содержимым, программа не тратит время на повторную распаковку, а сразу использует готовые файлы.\n" +
                                      "3. Многопоточное ускорение — 7-Zip задействует все ядра ЦП (-mmt=on) для максимальной скорости распаковки многогигабайтных архивов.\n" +
                                      "4. Мгновенная индексация — извлеченные игры, патчи, DLC и папки модов автоматически группируются в задачи.",
                    Tip = "Вы можете просто закинуть архив с русификатором или 60 FPS модом прямо в окно программы!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📦 Встроенный движок 7-Zip (Active)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "✓ Автоматическое извлечение .zip, .rar, .7z", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Пропуск уже распакованных папок", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Многопоточная декомпрессия (Multi-threading)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Модификации (RomFS, ExeFS и IPS)",
                    Category = "Моддинг",
                    Icon = "\uE7B5",
                    DescriptionText = "Комплексная поддержка любых видов модификаций Nintendo Switch:\n\n" +
                                      "1. RomFS — перевод текста, русская озвучка, HD-текстуры и замена моделей. Положите папку romfs рядом с игрой.\n" +
                                      "2. ExeFS — модифицированные бинарные модули NSO (main, subsdk0).\n" +
                                      "3. ExeFS_Patches (IPS) — папки с .ips патчами (60 FPS, твики графики, отключение размытия, читы). Программа автоматически накладывает IPS-патчи на исполняемый код main при сборке.\n" +
                                      "4. Отображение в эмуляторах — вшитые модификации регистрируются как AddOnContent (DLC) и отображаются в свойствах игры в эмуляторах (Eden Nightly, STORM EDEN, Yuzu, Ryujinx) с возможностью их включения/выключения.",
                    Tip = "Задайте красивое имя для мода (например, «Русская озвучка GamesVoice») через Редактор метаданных!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "🎮 Вшивание модификаций:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• RomFS: Текстуры и озвучка [RomFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS_Patches: 60 FPS IPS Patch [ExeFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Дополнения в эмуляторе: [☑] Модификации: RomFS (версия 1)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Редактор метаданных и иконок",
                    Category = "Кастомизация",
                    Icon = "\uE70F",
                    DescriptionText = "Удобный встроенный редактор Control NCA (NACP + иконка):\n\n" +
                                      "• Вызов: кликните правой кнопкой мыши по задаче в таблице → «Редактировать метаданные и иконку».\n" +
                                      "• Изменение названий игры: возможность задать основное английское и русское название игры, а также автора/издателя.\n" +
                                      "• Кастомные названия модов: индивидуальные имена для RomFS и ExeFS/IPS модификаций (например, «Русификатор текста», «60 FPS Patch»).\n" +
                                      "• Быстрая замена иконки: выбор любого изображения (PNG/JPEG), автоматическая обрезка и масштабирование до стандарта 256×256.\n" +
                                      "• Без медленной пересборки: изменения вносятся точечно и быстро.",
                    Tip = "При сборке Мульти-контента обновленная иконка и названия автоматически внедряются в финальный образ.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🏷️ Метаданные игры:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Имя (ENG): Cadence of Hyrule", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Имя (RUS): Cadence of Hyrule [RUS]", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• RomFS мод: Русская озвучка GamesVoice", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS мод: 60 FPS Patch", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "«Умная» папка",
                    Category = "Автоматизация",
                    Icon = "\uE812",
                    DescriptionText = "«Умная» папка предназначена для автоматической фоновой обработки игр.\n\n" +
                                      "Принцип работы:\n" +
                                      "1. Активация — просто включите переключатель в Параметрах. Сканирование начинается мгновенно!\n" +
                                      "2. Изоляция по папкам — каждая подпапка первого уровня формирует отдельную изолированную задачу.\n" +
                                      "3. Комплектность по TitleID — внутри одной подпапки базовая игра, файлы обновления, DLC и модификации (RomFS/ExeFS/IPS) автоматически объединяются в один комплект.\n" +
                                      "4. Точечная привязка RomFS — папки модификаций привязываются только к той задаче, из чьей директории они происходят.\n" +
                                      "5. Автозапуск — после сканирования или появления новых файлов задачи автоматически запускаются в обработку по заданным параметрам.",
                    Tip = "Используйте поддержку Drag-and-Drop в Параметрах, чтобы легко задать «Умную» папку перетаскиванием.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Автоматическое сканирование и обработка", IsChecked = true });
                        sp.Children.Add(new TextBlock { Text = "Папка: P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "Режим: Мульти-контент → Формат: NSP (Умная обработка активна)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "⚡ Автоматический запуск задач активирован", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Мульти-контент и Unlocker",
                    Category = "Компоновка",
                    Icon = "\uE7BE",
                    DescriptionText = "Наиболее продвинутый режим для сборки монолитных образов NSP или NSZ.\n\n" +
                                      "• Объединение ресурсов: Базовая игра, Файл обновления, Все дополнения (DLC), Модификации RomFS/ExeFS/IPS и Unlocker собираются в единый устанавливаемый файл.\n" +
                                      "• Сохранение UNLOCKER (.tik / .cert): программа надежно защищает и сохраняет тикеты авторизации персонажей и дополнений (например, в Mortal Kombat 1).\n" +
                                      "• Native LibHac PFS0: сшивание выполняется нативным C# кодом с гарантированным строгим порядком заголовков (Application CNMT → Control NCA → Program NCA → Patch CNMT → DLC CNMTs → Tickets), что исключает ошибки распознавания в эмуляторах.\n" +
                                      "• Интеграция со Smart Processing: гарантирует, что размер итогового файла будет минимально возможным.",
                    Tip = "Сборка Мульти-контента в NSZ позволяет сэкономить гигабайты места при хранении единого файла игры со всеми DLC.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "Комплект Мульти-контента:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "1. [ИГРА] Mortal Kombat 1 (30.0 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "2. [ОБНОВЛЕНИЕ] Update v1.18.0 (5.2 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "3. [DLC] Все персонажи Kombat Pack (150 МБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "4. [UNLOCKER] Тикеты прав (.tik / .cert сохранены)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "5. [MOD] 60 FPS ExeFS Patch [ExeFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Интеграция с эмуляторами и синхронизация SDMC",
                    Category = "Эмуляторы",
                    Icon = "\uE7FC",
                    DescriptionText = "STORM SWITCH BOX v4.9.6 предоставляет полную свободу в интеграции с локальными эмуляторами Nintendo Switch (STORM EDEN, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citron и др.):\n\n" +
                                      "1. Пользовательский выбор папок эмуляторов — в разделе «Параметры» доступен специальный блок «Интеграция с эмуляторами (Путь к папке эмулятора)». Вы можете перетащить (Drag & Drop) или выбрать через проводник одну или несколько директорий ваших эмуляторов (например, E:\\STORM EDEN 3\\Assembling, L:\\Emulators\\Ryujinx и др.).\n\n" +
                                      "2. Чистота выходной библиотеки — при сборке Homebrew-игр и портов программа больше НЕ создает лишних папок [SDMC] в вашей основной папке с играми (например, P:\\CONSOLES\\...\\GAMES). Все файлы NRO, данные и конфигурации доставляются строго в виртуальные SD-карты указанных эмуляторов (user/sdmc/switch/<game>/), а рядом с игрой сохраняется только чистый итоговый файл (.nsp / .nsz / .xci).\n\n" +
                                      "3. Эксклюзивная фильтрация — если в настройках указаны конкретные папки эмуляторов, синхронизация данных происходит ИСКЛЮЧИТЕЛЬНО в них, полностью исключая фоновые диски и системные профили. Если список пуст — включается умный авто-поиск по всем подключенным дискам (C:, D:, E:, L: и др.).",
                    Tip = "Задайте папку вашего эмулятора один раз в Параметрах, и Homebrew-порты будут запускаться моментально без единого ручного действия!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🎮 Целевая синхронизация эмуляторов (Active):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• Папка эмулятора: E:\\STORM EDEN 3\\Assembling (user/sdmc/)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Чистая библиотека: Папки [SDMC] не засоряют каталог игр", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Drag & Drop: Поддержка перетаскивания нескольких эмуляторов", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Homebrew: Сборка портов и автономных игр",
                    Category = "Homebrew",
                    Icon = "\uE7FC",
                    DescriptionText = "Специализированный раздел «Homebrew» для автоматического распознавания, объединения и сборки любых портов и любительских игр в монолитные автономные файлы (NSP / NSZ / XCI):\n\n" +
                                      "1. Умное распознавание любых наборов файлов — просто перетащите папку с игрой (например, Diablo I, GTA V, GTA Vice City / San Andreas, DOOM, Half-Life, Quake, S.T.A.L.K.E.R., Morrowind) или группу файлов (.nro, .ovl, .nsp форвардеры, архивы .zip/.7z, папки atmosphere/contents/<TitleID>/romfs). Программа мгновенно объединит их в готовую задачу.\n" +
                                      "2. Монолитная сборка RomFS и авто-деплой SDMC — все внешние ресурсы игры (.mpq, .rpf, .wad, .pk3, .pak, .dat, .bin, .ini, .cfg, шрифты и текстуры) вшиваются в Program NCA, а также автоматически синхронизируются в целевые папки SDMC эмуляторов (STORM EDEN, Yuzu, Ryujinx, Suyu, Sudachi) без засорения выходной библиотеки.\n" +
                                      "3. Авто-извлечение ExeFS и NACP — при наличии сопутствующего Forwarder NSP программа автоматически распаковывает и использует оригинальные бинарные модули main/NPDM и метаданные (TitleID, иконку, автора).\n\n" +
                                      "⚡ Как правильно запускать Homebrew-игры и порты движков:\n\n" +
                                      "► Вариант А: Прямой запуск .nro (Самый надежный способ)\n" +
                                      "Поместите файл игры с расширением .nro (например: devilutionx.nro, sm64.nro, xash3d.nro, openmw.nro и т.д.) в папку с вашими играми. В эмуляторе нажмите «Загрузить файл» (или добавьте папку в библиотеку эмулятора — STORM EDEN автоматически сканирует расширения .nro, .nsp, .xci). Игра запустится напрямую без участия форвардеров.\n\n" +
                                      "► Вариант Б: Использование Форвардеров (.nsp)\n" +
                                      "STORM SWITCH BOX при сборке автоматически разложит необходимые исполняемые файлы и ресурсы в виртуальную карту вашего эмулятора:\n" +
                                      "   • Diablo I: sdmc/devilutionx-switch/ (devilutionx.nro + diabdat.mpq)\n" +
                                      "   • GTA San Andreas: sdmc/switch/re3-sa/ (re3-sa.nro + models/, data/, audio/)\n" +
                                      "   • Half-Life 1: sdmc/switch/xash3d/ (xash3d.nro + valve/ с valve.wad, halflife.wad)\n" +
                                      "   • DOOM 1/2: sdmc/switch/gzdoom/ (gzdoom.nro + doom.wad / doom2.wad)\n" +
                                      "   • DOOM 3: sdmc/switch/dhewm3/ (dhewm3.nro + base/pak000.pk4...)\n" +
                                      "   • S.T.A.L.K.E.R.: sdmc/switch/openxray/ (openxray.nro + gamedata/)\n" +
                                      "   • Morrowind: sdmc/switch/openmw/ (openmw.nro + Morrowind.esm)\n" +
                                      "   • AM2R / Cave Story: sdmc/switch/am2r/ (AM2R.nro + data.win)\n" +
                                      "   • Super Mario 64: sdmc/switch/sm64/ (sm64.nro + sm64.us.z64)\n" +
                                      "   • Zelda OoT / MM: sdmc/switch/soh/ (soh.nro + oot.otr)\n" +
                                      "Форвардер NSP моментально подхватывает данные и игра работает идеально!",
                    Tip = "Укажите путь к вашему эмулятору в Параметрах для автоматической синхронизации данных Homebrew!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "🕹️ Пакет Homebrew игры (Автономный NSP + Прямая синхронизация):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS: main (бинарный порт) + main.npdm [NSP Forwarder]", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• RomFS: Вшитые данные игры (.mpq / .rpf / .wad / .ini / LayeredFS)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• SDMC: Автоматический деплой в user/sdmc/switch/<game>/", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "✓ Чистота библиотеки: Папка [SDMC] не создается рядом с игрой", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Обновление (HardPatch и Сшивание)",
                    Category = "Патчинг",
                    Icon = "\uE72C",
                    DescriptionText = "Режим интеграции обновления в базовый образ игры с поддержкой Умной обработки (Smart Processing).\n\n" +
                                      "• При легких патчах (до 40% размера базы) — выполняется прямое сшивание без раздувания RomFS.\n" +
                                      "• При массивных патчах (от 40% размера базы) — выполняется HardPatch с физической заменой устаревших ресурсов и экономией места.\n" +
                                      "• Полученный образ работает автономно без необходимости отдельной установки патча.",
                    Tip = "Используйте сжатие в NSZ для получения минимального размера итогового образа.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Сжать итоговый образ в NSZ (Zstandard)", IsChecked = true });
                        sp.Children.Add(new Slider { Header = "Уровень сжатия (22 - Максимум)", Minimum = 1, Maximum = 22, Value = 22 });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Конвертация форматов (Switch и 3DS)",
                    Category = "Форматы",
                    Icon = "\uE8D4",
                    DescriptionText = "Быстрая потоковая и нативная конвертация форматов внутри соответствующих экосистем:\n\n" +
                                      "• Экосистема Nintendo Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ. Поддерживается прямое пасс-фру преобразование без потери качества и пересжатия видео/аудио.\n" +
                                      "• Экосистема Nintendo 3DS: 3DS (CCI) ↔ CIA ↔ CXI. Форматы 3DS изолированы от форматов Switch.\n" +
                                      "• Автоматическая обрезка пустых байтов (Trimming) для картриджей 3DS.",
                    Tip = "XCI в NSP конвертируется без потери данных и без нагрузки на ЦП.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "Цепочки конвертации:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• 3DS: 3DS (CCI) ↔ CIA ↔ CXI", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Распаковка и Упаковка",
                    Category = "Моддинг",
                    Icon = "\uE896",
                    DescriptionText = "Распаковка RomFS (игровые ресурсы, текстуры, переводы) и ExeFS (исполняемый код NSO), а также последующая обратная сборка модифицированных каталогов в NSP/NSZ.",
                    Tip = "Распакованную папку romfs можно положить рядом с базовой игрой — программа подхватит её при сборке Мульти-контента!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new RadioButton { Content = "Извлечь RomFS (ресурсы игры)", IsChecked = true });
                        sp.Children.Add(new RadioButton { Content = "Извлечь ExeFS (код NSO)" });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Параметры и Ключи",
                    Category = "Конфигурация",
                    Icon = "\uE713",
                    DescriptionText = "Полный спектр настроек и инструментов:\n\n" +
                                      "• Drag-and-Drop файла ключей (prod.keys / keys.txt) с visual-подсветкой.\n" +
                                      "• 6-ячеечный ввод версии прошивки с автоматической навигацией.\n" +
                                      "• Drag-and-Drop выходной папки и «Умной» папки.\n" +
                                      "• Ticketless NSP (--C_clean_ND) для гарантированной работы на любом CFW.\n" +
                                      "• Очистка Delta NCA (-ND true) для экономии места в обновлениях.\n" +
                                      "• Разделение файлов крупнее 4 ГБ для FAT32 SD-карт (.xc0/.xc1).\n" +
                                      "• Очистка неиспользуемых языковых локализаций из RomFS.",
                    Tip = "Каждая опция в Параметрах оснащена подробными всплывающими подсказками (Tooltip).",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔑 Файл ключей: prod.keys (Активен)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new CheckBox { Content = "🔓 Удалить Titlerights (Ticketless NSP)", IsChecked = false });
                        sp.Children.Add(new CheckBox { Content = "🗑️ Удалять Delta NCA из обновлений", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "💾 Разделять файлы для FAT32 (> 4 GB)", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Проверка целостности",
                    Category = "Валидация",
                    Icon = "\uE8FB",
                    DescriptionText = "Верификатор целостности файлов (.nsp, .nsz, .xci).\n\nПроверяются заголовки NCA, сигнатуры и контрольные хэши блоков.",
                    Tip = "Используйте проверку для подтверждения корректности скачанных образов.",
                    SetupPreview = container =>
                    {
                        var progress = new ProgressBar { Value = 100, Minimum = 0, Maximum = 100, Height = 8 };
                        var label = new TextBlock { Text = "Проверка завершена: 100% (Ошибок не обнаружено)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) };
                        container.Children.Add(progress);
                        container.Children.Add(label);
                    }
                },
                new TopicItem
                {
                    Title = "Nintendo 3DS: Архитектура и Мульти-контент",
                    Category = "Nintendo 3DS",
                    Icon = "\uE7FC",
                    DescriptionText = "Полная поддержка экосистемы Nintendo 3DS (CTR/CCI/CIA/CXI):\n\n" +
                                      "1. Сборка Мульти-контента для 3DS — объединяет Базовую игру (.3ds/.cci/.cia), Файл обновления (Патч .cia), Дополнения (DLC .cia) и Модификации/Русификаторы (папку romfs) в единый монолитный файл.\n" +
                                      "2. Бесшовное слияние файловых систем (LayeredFS) — программа распаковывает образы через ctrtool, накладывает файлы патча, внедряет DLC-контент, перезаписывает измененные файлы перевода (RomFS) и пересобирает проект с помощью 3dstool и makerom.\n" +
                                      "3. Нативная совместимость — полученный файл (.3ds / .cci) моментально открывается в эмуляторах Citra, Lime3DS, Azahar со всеми вшитыми дополнениями, актуальной версией и переводом.",
                    Tip = "Для сборки достаточно перетащить в Мульти-контент базовую игру, патч, файлы DLC и папку мода!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🕹️ Комплект Nintendo 3DS Мульти-контента:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "1. [ИГРА] The Legend of Zelda (.3ds / 2.0 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "2. [PATCH] Update v1.2 (.cia / 120 МБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "3. [DLC 1+2] Дополнения (.cia / 65 МБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "4. [MOD] Папка romfs с русским переводом", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "✓ Итог: Единый монолитный .3ds файл (CCI, Trimming применен)", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Nintendo 3DS: Форматы и Сжатие",
                    Category = "Nintendo 3DS",
                    Icon = "\uE8D4",
                    DescriptionText = "Особенности форматов и сжатия Nintendo 3DS:\n\n" +
                                      "• 3DS / CCI (CTR Cartridge Image) — стандартный образ картриджа. Идеален для эмуляторов (Citra, Lime3DS, Azahar). Содержит NCCH разделы.\n" +
                                      "• CIA (CTR Importable Archive) — установочный пакет для установки на реальную консоль 3DS с кастомной прошивкой через FBI, либо для эмуляторов.\n" +
                                      "• CXI (CTR Executable Image) — исполняемый NCCH контейнер приложения.\n" +
                                      "• Применяется ли сжатие в 3DS? — В 3DS эмуляторы не поддерживают блочное NSZ/Zstandard сжатие (оно разработано специально для Switch). Однако в 3DS применяется Trimming (обрезка) — удаление мусорных пустых байтов 0xFF, заполняющих физический размер картриджа (1 ГБ, 2 ГБ, 4 ГБ). Это сокращает размер файла в 2–4 раза без потери совместимости!",
                    Tip = "Для эмуляторов Citra / Lime3DS выбирайте формат .3ds (CCI), для установки на 3DS — формат .cia.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "Сравнение форматов 3DS:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• 3DS / CCI: Прямой запуск в Citra / Lime3DS (Trimming активен)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• CIA: Установка на консоль 3DS (Luma3DS / FBI)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• CXI: NCCH контейнер для отладки и моддинга", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Библиотека игр Nintendo (19 систем)",
                    Category = "Энциклопедия",
                    Icon = "\uE7FC",
                    DescriptionText = "Новый масштабный раздел «Библиотека игр» — интерактивная база знаний обо всех 19 поколениях игровых систем Nintendo (от самых ранних до новейших):\n\n" +
                                      "1. Nintendo Color TV-Game (1977)\n" +
                                      "2. Nintendo Game & Watch (1980)\n" +
                                      "3. Nintendo Entertainment System / Famicom (1983)\n" +
                                      "4. Nintendo Famicom Disk System (1986)\n" +
                                      "5. Nintendo Game Boy (1989)\n" +
                                      "6. Super Nintendo Entertainment System / Super Famicom (1990)\n" +
                                      "7. Super Famicom Satellaview (BS-X) (1995)\n" +
                                      "8. Nintendo Virtual Boy (1995)\n" +
                                      "9. Nintendo 64 / Nintendo 64DD (1996)\n" +
                                      "10. Nintendo Game Boy Color (1998)\n" +
                                      "11. Nintendo Pokémon Mini (2001)\n" +
                                      "12. Nintendo Game Boy Advance / Game Boy micro (2001)\n" +
                                      "13. Nintendo GameCube (2001)\n" +
                                      "14. Nintendo DS / Nintendo DS Lite / Nintendo DSi (2004)\n" +
                                      "15. Nintendo Wii (2006)\n" +
                                      "16. Nintendo 3DS / New Nintendo 3DS / Nintendo 2DS (2011)\n" +
                                      "17. Nintendo Wii U (2012)\n" +
                                      "18. Nintendo Switch / Nintendo Switch Lite / OLED (2017)\n" +
                                      "19. Nintendo Switch 2 (2025)\n\n" +
                                      "Возможности раздела:\n" +
                                      "• Быстрые вкладки переключения систем и сквозной поиск по всей базе.\n" +
                                      "• Фильтры по жанрам, разработчикам, издателям и сортировка.\n" +
                                      "• Модальное окно просмотра увеличенной обложки в высоком разрешении с кнопкой сохранения на диск («💾 Сохранить обложку»), копированием сведений («📋 Копировать») и поиском в сети («🔍 Найти в сети»).",
                    Tip = "Кликните по любой карточке игры, чтобы развернуть оригинальный арт обложки в высоком качестве и сохранить его!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📚 Интерактивная библиотека игр Nintendo:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Все 19 систем: Color TV-Game → NES → SNES → N64 → GBA → 3DS → Switch → Switch 2", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Карточки игр: Обложка, Издатель, Разработчик, Год, Жанр, Издание", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Диалог обложки: Увеличение + Сохранение в PNG/JPG", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Информация: База Switch и 3DS",
                    Category = "Каталог",
                    Icon = "\uE8B9",
                    DescriptionText = "Раздел «Информация» предназначен для сканирования ваших локальных коллекций игр и поиска в глобальных базах TitleDB и Nintendo 3DS.\n\n" +
                                      "• Наглядные плашки платформ: в левом нижнем углу каждой обложки выводится стильный бейдж с платформой игры (красный «🕹️ Nintendo 3DS» или синий «🎮 Nintendo Switch»).\n" +
                                      "• Сквозной поиск: мгновенный поиск как по вашим локальным файлам, так и по сетевым базам игр Switch и 3DS.\n" +
                                      "• Быстрый переход в обработку: кликните по найденной локальной игре, чтобы мгновенно открыть её свойства или отправить в конвертацию/сжатие.",
                    Tip = "Используйте фильтры «Все игры», «Nintendo Switch» и «Nintendo 3DS» вверху для раздельного просмотра.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔍 Каталог и идентификация игр:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Плашка Switch: [🎮 Nintendo Switch] (синий бейдж на обложке)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Плашка 3DS: [🕹️ Nintendo 3DS] (красный бейдж на обложке)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        sp.Children.Add(new TextBlock { Text = "• Поиск по TitleID, названиям и студиям", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Раздельные службы «Умных папок»",
                    Category = "Автоматизация",
                    Icon = "\uE812",
                    DescriptionText = "Служба «Умная папка» полностью разделена на две независимые службы:\n\n" +
                                      "1. «Умная папка» Nintendo Switch — настраивается во вкладке Switch (Параметры). Поддерживает выбор задач: Сжатие в NSZ, Распаковка, Упаковка, Конвертация XCI, Мульти-контент, Проверка и целевые форматы NSP, NSZ, XCI, XCZ.\n" +
                                      "2. «Умная папка» Nintendo 3DS — настраивается во вкладке 3DS (Параметры). Поддерживает выбор задач для 3DS (Конвертация, Мульти-контент 3DS, Распаковка, Упаковка, Проверка) и форматы 3DS (CCI), CIA, CXI.\n" +
                                      "3. Ручной запуск и безопасность — мониторинг запускается и останавливается строго по кнопкам «▶ Запустить отслеживание» / «⏹ Остановить отслеживание», исключая случайную фоновую обработку.",
                    Tip = "Вы можете одновременно отслеживать разные папки для Switch и для 3DS с разными задачами!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📁 Независимые службы мониторинга:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Switch Watch Folder: Авто-мультиконтент → NSP/NSZ (Активна)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• 3DS Watch Folder: Авто-тримминг и сборка → .3DS/.CIA (Активна)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Гарантированная очистка временных файлов",
                    Category = "Безопасность",
                    Icon = "\uE74D",
                    DescriptionText = "Специальная защитная служба очистки обеспечивает 100% чистоту дискового пространства:\n\n" +
                                      "• Прямое удаление мимо корзины: сброс системных атрибутов FileAttributes.Normal и прямое физическое удаление файлов и папок с повторными попытками разблокировки.\n" +
                                      "• Регистрация активных временных папок: каталоги STORM_TMP_*, StormDecomp_* и Storm3DS_* регистрируются в реальном времени при создании.\n" +
                                      "• Автоматическая очистка при старте и завершении: корни всех логических дисков, папки сохранения и %TEMP% проверяются при запуске программы, остановке задач, закрытии окна и аварийных сбоях.",
                    Tip = "Ваш диск всегда защищен от накопления забытых гигабайтных временных файлов!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🛡️ Автоматическая очистка диска (Активна):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "✓ Очистка корней дисков (C:\\, D:\\, E:\\... STORM_TMP_*)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Очистка выходных папок (StormDecomp_*)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Физическое удаление мимо корзины", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                }
            };
        }

        private List<TopicItem> GetTopicsEn()
        {
            return new List<TopicItem>
            {
                new TopicItem
                {
                    Title = "Application Overview",
                    Category = "Introduction",
                    Icon = "\uE9CE",
                    DescriptionText = "STORM SWITCH BOX v4.9.6 is a professional, high-performance toolkit for processing Nintendo Switch and Nintendo 3DS games, as well as an interactive encyclopedia of all 19 Nintendo console generations (from Color TV-Game to Nintendo Switch 2).\n\nEquipped with Smart File Processing, the program automatically selects the optimal build method (native PFS0 splicing without RomFS inflation for lightweight patches, or physical HardPatch for heavy updates and mods), unpacks resources, compiles NSP/NSZ/3DS/CIA, converts formats across ecosystems (Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ; 3DS: 3DS ↔ CIA ↔ CXI), bundles games with updates and DLCs into monolithic 4-in-1 packages, builds Homebrew ports, monitors dual Smart Folders, and saves instant history.",
                    Tip = "Switch between Nintendo Switch and 3DS in one click via the top header bar!",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "⚡ STORM SWITCH BOX v4.9.6", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Smart File Processing: Optimal file size and 100% mod compatibility by default\n• Dual Ecosystems: Nintendo Switch & Nintendo 3DS support\n• Interactive Game Library: All 19 Nintendo generations (No-Intro & Redump)\n• Information Catalog: High-res artwork with platform badges\n• Dual Independent Smart Folders (Switch & 3DS)\n• Embedded 7-Zip & Zstandard compression engines (up to level 22)", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Smart File Processing",
                    Category = "Algorithms",
                    Icon = "\uE945",
                    DescriptionText = "Intelligent automatic build method selection algorithm (Smart Processing) in v4.9.6:\n\n" +
                                      "Algorithm Objective: Produce the absolute smallest output file size while preserving 100% of game functionality, DLCs, and mods.\n\n" +
                                      "How it works:\n" +
                                      "1. Lightweight patches (e.g. Ys X Nordics: 60 MB patch on 6.75 GB base) — native LibHac PFS0 splicing is used. Preserves the exact 6.81 GB size without RomFS ballooning to 10.4 GB!\n" +
                                      "2. Massive updates (e.g. The Witcher 3, MK11: patch >= 40% base size) — physical HardPatch replaces outdated assets with new update resources, saving gigabytes of storage!\n" +
                                      "3. Mod folders (romfs, exefs, exefs_patches) — triggers HardPatch to inject translations and mods directly into game binaries.\n\n" +
                                      "All decision logs are displayed clearly with the 🧠 icon.",
                    Tip = "Smart Processing is always enabled by default — no need to guess between rebuilding and splicing!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🧠 Smart File Processing Analysis:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• Light Patch: [Native Splicing] → Smallest size (6.81 GB instead of 10.4 GB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Heavy Patch: [HardPatch] → Asset replacement and outdated data cleanup", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Mod Folders: [HardPatch] → Direct physical injection of mods into RomFS", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Task Grouping Simulator",
                    Category = "Interactive",
                    Icon = "\uE8E5",
                    DescriptionText = "Interactive task grouping simulator.\n\nDrag and drop real game folders or select preset scenarios («Dispatch» or «Cadence of Hyrule») to simulate how the engine creates isolated complete packages (BASE + UPDATE + DLC + ROMFS/EXEFS) and routes RomFS directories.",
                    Tip = "Drag multi-release folders directly into the simulator to visualize task separation!",
                    SetupPreview = container => BuildSimulatorPreview(container)
                },
                new TopicItem
                {
                    Title = "Built-in 7-Zip & Auto-Extraction",
                    Category = "Archives",
                    Icon = "\uE8F1",
                    DescriptionText = "STORM SWITCH BOX embeds a high-performance 7-Zip engine with zero external dependencies:\n\n" +
                                      "1. Auto-extraction on Drop — drop archives (.zip, .rar, .7z) or folders to extract automatically.\n" +
                                      "2. Smart Extraction Skip — if an uncompressed folder already exists next to the archive, extraction is skipped.\n" +
                                      "3. Multi-threaded Acceleration — 7-Zip uses all CPU cores (-mmt=on) for blazing-fast decompression.\n" +
                                      "4. Instant Indexing — extracted games, updates, DLCs, and mods are immediately grouped into tasks.",
                    Tip = "Simply drag a zip archive with mods or translations into the application window!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📦 Built-in 7-Zip Engine (Active)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "✓ Automatic extraction of .zip, .rar, .7z", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Automatic skipping of pre-extracted folders", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Multi-threaded decompression (All CPU cores)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Modifications (RomFS, ExeFS & IPS)",
                    Category = "Modding",
                    Icon = "\uE7B5",
                    DescriptionText = "Complete support for all types of Nintendo Switch mods:\n\n" +
                                      "1. RomFS — custom textures, translations, voiceovers, and models.\n" +
                                      "2. ExeFS — modified NSO binary modules (main, subsdk0).\n" +
                                      "3. ExeFS_Patches (IPS) — 60 FPS patches, graphics tweaks, no-blur, and cheats applied to main.\n" +
                                      "4. Emulator Compatibility — embedded mods are recognized as DLC and can be toggled on/off in emulator game properties (STORM EDEN, Yuzu, Ryujinx).",
                    Tip = "Give your mod a custom title (e.g. «60 FPS Mod») in the Metadata Editor!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "🎮 Modification Injection:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• RomFS: Custom Textures & Audio [RomFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS_Patches: 60 FPS IPS Patch [ExeFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Emulator Add-on: [☑] Modification: RomFS (v1)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Metadata & Icon Editor",
                    Category = "Customization",
                    Icon = "\uE70F",
                    DescriptionText = "Integrated Control NCA (NACP + Icon) editor:\n\n" +
                                      "• Right-click any task in the queue → «Edit Metadata & Icon».\n" +
                                      "• Edit game titles in multiple languages and publisher info.\n" +
                                      "• Assign unique names to RomFS and ExeFS modifications.\n" +
                                      "• Fast icon replacement with automatic 256×256 scaling.\n" +
                                      "• Instant injection without rebuilding heavy game packages.",
                    Tip = "Custom icons and names are automatically included when assembling Multi-Content packages.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🏷️ Game Metadata Editor:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Title (ENG): Cadence of Hyrule", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• RomFS Mod: Custom HD Texture Pack", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS Mod: 60 FPS Patch", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Smart Folder",
                    Category = "Automation",
                    Icon = "\uE812",
                    DescriptionText = "Smart Folder provides automatic background processing of incoming games:\n\n" +
                                      "1. Instant Activation — toggle the switch in Settings to start monitoring.\n" +
                                      "2. Folder Isolation — each first-level folder creates an independent task.\n" +
                                      "3. TitleID Grouping — base games, updates, DLCs, and mods are merged cleanly.\n" +
                                      "4. Accurate RomFS Scoping — mod folders are scoped strictly to their parent game.\n" +
                                      "5. Auto-execution — new files trigger conversion, multi-content packaging, or compression automatically.",
                    Tip = "Drag and drop folders directly into the Smart Folder path box in Settings!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Automatic Folder Scanning & Processing", IsChecked = true });
                        sp.Children.Add(new TextBlock { Text = "Path: P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "Mode: Multi-Content → Format: NSP (Smart Processing active)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "⚡ Automated execution active", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Multi-Content & Unlocker",
                    Category = "Packaging",
                    Icon = "\uE7BE",
                    DescriptionText = "Advanced monolithic builder for NSP and NSZ formats:\n\n" +
                                      "• Full Resource Fusion: Base game, Update, all DLCs, RomFS/ExeFS mods, and Unlockers bundled into one installable file.\n" +
                                      "• Unlocker Preservation (.tik / .cert): Rights tickets for characters and DLCs are preserved intact.\n" +
                                      "• Native LibHac PFS0: Built with strict NCA header ordering to guarantee emulator recognition.\n" +
                                      "• Smart Processing integration ensures the smallest possible file size.",
                    Tip = "Packaging Multi-Content as NSZ saves massive disk space while keeping all DLCs in a single file.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "Multi-Content Package Structure:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "1. [BASE] Mortal Kombat 1 (30.0 GB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "2. [UPDATE] Update v1.18.0 (5.2 GB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "3. [DLC] Kombat Pack Characters (150 MB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "4. [UNLOCKER] Rights Tickets (.tik / .cert preserved)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "5. [MOD] 60 FPS ExeFS Patch [ExeFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Emulator Integration & SDMC Sync",
                    Category = "Emulators",
                    Icon = "\uE7FC",
                    DescriptionText = "Seamless integration with Nintendo Switch emulators (STORM EDEN, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citron):\n\n" +
                                      "1. Custom Emulator Directories — In Settings, specify one or more emulator paths via Drag & Drop or folder picker.\n" +
                                      "2. Clean Game Library — Building Homebrew ports delivers NRO data directly into emulator SDMC (user/sdmc/switch/<game>/) without creating redundant [SDMC] folders in your main game library.\n" +
                                      "3. Strict Filtering — Data synchronizes strictly to specified emulator paths.",
                    Tip = "Specify your emulator path once in Settings for instant launch of Homebrew ports!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🎮 Targeted Emulator Sync (Active):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• Emulator Folder: E:\\STORM EDEN 3\\Assembling (user/sdmc/)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Clean Library: [SDMC] folders never pollute game directories", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Drag & Drop: Multiple emulator path support", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Homebrew: Engine Ports & Standalone Games",
                    Category = "Homebrew",
                    Icon = "\uE7FC",
                    DescriptionText = "Dedicated Homebrew builder for engine ports and standalone titles (NSP / NSZ / XCI):\n\n" +
                                      "1. Smart File Detection — Drop game folders (e.g. Diablo I, GTA V, GTA Vice City / San Andreas, DOOM, Half-Life, Quake, S.T.A.L.K.E.R., Morrowind) or files (.nro, .ovl, .nsp forwarders, atmosphere/contents/<TitleID>/romfs).\n" +
                                      "2. Zero-Copy RomFS & Direct Packaging — Embeds all game assets into Program NCA with zero redundant copying, delivering data straight to emulator SDMC.\n" +
                                      "3. Forwarder Decompilation — Re-uses verified forwarder binaries, TitleIDs, and icons automatically.",
                    Tip = "Specify your emulator directory in Settings for automatic SDMC asset deployment!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "🕹️ Homebrew Package (Standalone NSP + Direct Sync):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• ExeFS: main binary + main.npdm [NSP Forwarder]", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• RomFS: Embedded game data (.mpq / .rpf / .wad / LayeredFS)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• SDMC: Automatic deployment to user/sdmc/switch/<game>/", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Game Updates (HardPatch & Splicing)",
                    Category = "Patching",
                    Icon = "\uE72C",
                    DescriptionText = "Seamless game update integration with Smart Processing:\n\n" +
                                      "• Light updates (under 40% base size) — Native splicing without RomFS inflation.\n" +
                                      "• Heavy updates (over 40% base size) — HardPatch physical replacement to maximize storage savings.\n" +
                                      "• The resulting package runs standalone with no need to install update files separately.",
                    Tip = "Combine with NSZ compression for the smallest possible package size.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Compress output image to NSZ (Zstandard)", IsChecked = true });
                        sp.Children.Add(new Slider { Header = "Compression Level (22 - Maximum)", Minimum = 1, Maximum = 22, Value = 22 });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Format Conversion (Switch & 3DS)",
                    Category = "Formats",
                    Icon = "\uE8D4",
                    DescriptionText = "Fast native and stream-based format conversion:\n\n" +
                                      "• Nintendo Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ with lossless pass-through.\n" +
                                      "• Nintendo 3DS: 3DS (CCI) ↔ CIA ↔ CXI.\n" +
                                      "• Automatic cartridge byte trimming for 3DS images.",
                    Tip = "XCI to NSP converts losslessly without heavy CPU overhead.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "Conversion Pipelines:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• 3DS: 3DS (CCI) ↔ CIA ↔ CXI", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Extraction & Packaging",
                    Category = "Modding",
                    Icon = "\uE896",
                    DescriptionText = "Extract RomFS (game resources, textures, scripts) and ExeFS (NSO code), or repack modified directories back into installable NSP/NSZ packages.",
                    Tip = "Place extracted RomFS folders next to base games to auto-include them in Multi-Content builds.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new RadioButton { Content = "Extract RomFS (Game Assets)", IsChecked = true });
                        sp.Children.Add(new RadioButton { Content = "Extract ExeFS (NSO Code)" });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Settings & Keys",
                    Category = "Configuration",
                    Icon = "\uE713",
                    DescriptionText = "Full suite of settings and encryption tools:\n\n" +
                                      "• Drag & Drop keys file (prod.keys / keys.txt) with visual highlights.\n" +
                                      "• Ticketless NSP creation (--C_clean_ND) for CFW compatibility.\n" +
                                      "• Delta NCA stripping (-ND true) for compact updates.\n" +
                                      "• FAT32 split mode for cards requiring < 4 GB files.\n" +
                                      "• Unused RomFS language trimming.",
                    Tip = "Hover over any option in Settings to view comprehensive tooltips.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔑 Encryption Keys: prod.keys (Active)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new CheckBox { Content = "🔓 Remove Titlerights (Ticketless NSP)", IsChecked = false });
                        sp.Children.Add(new CheckBox { Content = "🗑️ Strip Delta NCAs from Updates", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "💾 Split files for FAT32 (> 4 GB)", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Integrity Verification",
                    Category = "Validation",
                    Icon = "\uE8FB",
                    DescriptionText = "Comprehensive file integrity validator (.nsp, .nsz, .xci) checking NCA headers, RSA signatures, and block hash digests.",
                    Tip = "Verify downloaded ROMs to prevent corrupted dumps from causing crashes.",
                    SetupPreview = container =>
                    {
                        var progress = new ProgressBar { Value = 100, Minimum = 0, Maximum = 100, Height = 8 };
                        var label = new TextBlock { Text = "Verification Complete: 100% (No errors detected)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) };
                        container.Children.Add(progress);
                        container.Children.Add(label);
                    }
                },
                new TopicItem
                {
                    Title = "Nintendo 3DS: Architecture & Multi-Content",
                    Category = "Nintendo 3DS",
                    Icon = "\uE7FC",
                    DescriptionText = "Complete Nintendo 3DS ecosystem support (CTR/CCI/CIA/CXI):\n\n" +
                                      "1. 3DS Multi-Content — Combines Base game (.3ds/.cci/.cia), Update (.cia), DLCs (.cia), and translation mods (romfs folder) into one trimmed .3ds file.\n" +
                                      "2. LayeredFS Splicing — Unpacks via ctrtool, overlays update patches, injects DLC content, replaces modified RomFS files, and repacks with 3dstool and makerom.\n" +
                                      "3. Native Compatibility — Works out of the box in Citra, Lime3DS, and Azahar emulators.",
                    Tip = "Drop the base game, CIA update, DLCs, and RomFS mod folder together into Multi-Content!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🕹️ Nintendo 3DS Multi-Content Package:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "1. [BASE] The Legend of Zelda (.3ds / 2.0 GB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "2. [PATCH] Update v1.2 (.cia / 120 MB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "3. [DLC] Add-on Content (.cia / 65 MB)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "4. [MOD] RomFS Translation Folder", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "✓ Output: Monolithic .3ds file (CCI, Trimming applied)", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Nintendo 3DS: Formats & Compression",
                    Category = "Nintendo 3DS",
                    Icon = "\uE8D4",
                    DescriptionText = "Overview of Nintendo 3DS formats and size optimizations:\n\n" +
                                      "• 3DS / CCI (CTR Cartridge Image) — Standard cartridge dump for emulators (Citra, Lime3DS, Azahar).\n" +
                                      "• CIA (CTR Importable Archive) — Installable package for real 3DS hardware (FBI / Custom Firmware).\n" +
                                      "• CXI (CTR Executable Image) — Executable NCCH container for debugging.\n" +
                                      "• Trimming vs Compression — 3DS emulators do not use Zstandard/NSZ. Instead, Trimming removes 0xFF padding, reducing file size by 2-4x without losing compatibility!",
                    Tip = "Select .3ds (CCI) for emulators, and .cia for 3DS hardware installation.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "3DS Format Comparison:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• 3DS / CCI: Direct launch in Citra / Lime3DS (Trimming active)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• CIA: Installation on 3DS console (Luma3DS / FBI)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• CXI: NCCH container for debugging and modding", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Nintendo Game Library (19 Systems)",
                    Category = "Encyclopedia",
                    Icon = "\uE7FC",
                    DescriptionText = "Interactive encyclopedia spanning all 19 Nintendo console generations:\n\n" +
                                      "Color TV-Game (1977) → Game & Watch → NES / Famicom → FDS → Game Boy → SNES → Satellaview → Virtual Boy → N64 → GBC → Pokémon Mini → GBA → GameCube → DS → Wii → 3DS → Wii U → Switch → Switch 2 (2025).\n\n" +
                                      "Features:\n" +
                                      "• Fast platform tabs and global search across the database.\n" +
                                      "• Genre, developer, and publisher filters.\n" +
                                      "• High-resolution cover viewer with direct disk save («💾 Save Cover»), metadata copy, and web search.",
                    Tip = "Click on any game card to open high-resolution artwork and save it!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📚 Interactive Nintendo Game Library:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• All 19 Systems: Color TV-Game → NES → SNES → N64 → GBA → 3DS → Switch → Switch 2", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Game Cards: Artwork, Publisher, Developer, Year, Genre, Edition", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Artwork Dialog: High-Res Zoom + Save PNG/JPG", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Information: Switch & 3DS Database",
                    Category = "Catalog",
                    Icon = "\uE8B9",
                    DescriptionText = "Scan local game folders and cross-reference with global TitleDB & 3DS databases:\n\n" +
                                      "• Platform Badges: Clear visual badges in the bottom corner of every cover (Blue «🎮 Nintendo Switch» or Red «🕹️ Nintendo 3DS»).\n" +
                                      "• Global Search: Instant lookup across TitleID, names, and studios.\n" +
                                      "• Quick Action: Click any local game to jump straight into conversion or compression.",
                    Tip = "Use the filter pills at the top to toggle between All Games, Switch, and 3DS.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔍 Game Catalog & Identification:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Switch Badge: [🎮 Nintendo Switch] (Blue badge on cover)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• 3DS Badge: [🕹️ Nintendo 3DS] (Red badge on cover)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        sp.Children.Add(new TextBlock { Text = "• Instant search by TitleID, title, and publisher", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Dual Smart Folder Services",
                    Category = "Automation",
                    Icon = "\uE812",
                    DescriptionText = "Independent monitoring services for both consoles:\n\n" +
                                      "1. Nintendo Switch Smart Folder — Configured in Switch Settings. Supports NSZ compression, unpack, repack, XCI conversion, Multi-Content, and integrity verification.\n" +
                                      "2. Nintendo 3DS Smart Folder — Configured in 3DS Settings. Supports 3DS conversion, Multi-Content, unpacking, repacking, and trimming.\n" +
                                      "3. Controlled Execution — Start and stop monitoring explicitly via dedicated buttons.",
                    Tip = "You can monitor separate directories for Switch and 3DS concurrently!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📁 Independent Monitoring Services:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• Switch Watch Folder: Auto Multi-Content → NSP/NSZ (Active)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• 3DS Watch Folder: Auto-trimming & build → .3DS/.CIA (Active)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Guaranteed Temporary File Cleanup",
                    Category = "Safety",
                    Icon = "\uE74D",
                    DescriptionText = "Dedicated cleanup engine guarantees 100% clean disk space:\n\n" +
                                      "• Direct deletion bypassing Recycle Bin: Resets file attributes to Normal and performs physical removal.\n" +
                                      "• Active Temp Folder Registry: Real-time tracking of STORM_TMP_*, StormDecomp_*, and Storm3DS_* directories.\n" +
                                      "• Startup and Shutdown Clean: All disk roots and %TEMP% locations are verified on app launch, task cancellation, and window close.",
                    Tip = "Your drives are always protected from accumulating orphaned temporary files!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🛡️ Automatic Disk Cleanup (Active):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "✓ Drive root cleanup (C:\\, D:\\, E:\\... STORM_TMP_*)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Output folder cleanup (StormDecomp_*)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "✓ Direct physical removal bypassing Recycle Bin", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                }
            };
        }

        private List<TopicItem> GetTopicsDe()
        {
            var enList = GetTopicsEn();
            return enList.Select(t => new TopicItem
            {
                Title = t.Title switch
                {
                    "Application Overview" => "App-Übersicht",
                    "Smart File Processing" => "Intelligente Dateiverarbeitung",
                    "Task Grouping Simulator" => "Aufgaben-Gruppierungs-Simulator",
                    "Built-in 7-Zip & Auto-Extraction" => "Integriertes 7-Zip & Auto-Entpacken",
                    "Modifications (RomFS, ExeFS & IPS)" => "Modifikationen (RomFS, ExeFS & IPS)",
                    "Metadata & Icon Editor" => "Metadaten- & Icon-Editor",
                    "Smart Folder" => "Smarter Ordner",
                    "Multi-Content & Unlocker" => "Multi-Content & Unlocker",
                    "Emulator Integration & SDMC Sync" => "Emulator-Integration & SDMC-Synchronisation",
                    "Homebrew: Engine Ports & Standalone Games" => "Homebrew: Portierungen & Standalone-Spiele",
                    "Game Updates (HardPatch & Splicing)" => "Spiel-Updates (HardPatch & Zusammenführung)",
                    "Format Conversion (Switch & 3DS)" => "Formatkonvertierung (Switch & 3DS)",
                    "Extraction & Packaging" => "Entpacken & Packen",
                    "Settings & Keys" => "Einstellungen & Schlüssel",
                    "Integrity Verification" => "Integritätsprüfung",
                    "Nintendo 3DS: Architecture & Multi-Content" => "Nintendo 3DS: Architektur & Multi-Content",
                    "Nintendo 3DS: Formats & Compression" => "Nintendo 3DS: Formate & Kompression",
                    "Nintendo Game Library (19 Systems)" => "Nintendo Spiele-Bibliothek (19 Systeme)",
                    "Information: Switch & 3DS Database" => "Informationen: Switch & 3DS Datenbank",
                    "Dual Smart Folder Services" => "Getrennte Smart-Ordner-Dienste",
                    "Guaranteed Temporary File Cleanup" => "Garantierte Bereinigung temporärer Dateien",
                    _ => t.Title
                },
                Category = t.Category switch
                {
                    "Introduction" => "Einführung",
                    "Algorithms" => "Algorithmen",
                    "Interactive" => "Interaktiv",
                    "Archives" => "Archive",
                    "Modding" => "Modding",
                    "Customization" => "Anpassung",
                    "Automation" => "Automatisierung",
                    "Packaging" => "Paketierung",
                    "Emulators" => "Emulatoren",
                    "Homebrew" => "Homebrew",
                    "Patching" => "Patchen",
                    "Formats" => "Formate",
                    "Configuration" => "Konfiguration",
                    "Validation" => "Validierung",
                    "Nintendo 3DS" => "Nintendo 3DS",
                    "Encyclopedia" => "Enzyklopädie",
                    "Catalog" => "Katalog",
                    "Safety" => "Sicherheit",
                    _ => t.Category
                },
                Icon = t.Icon,
                DescriptionText = t.DescriptionText,
                Tip = t.Tip,
                SetupPreview = t.SetupPreview
            }).ToList();
        }

        private List<TopicItem> GetTopicsZh()
        {
            var enList = GetTopicsEn();
            return enList.Select(t => new TopicItem
            {
                Title = t.Title switch
                {
                    "Application Overview" => "应用程序概览",
                    "Smart File Processing" => "智能文件处理",
                    "Task Grouping Simulator" => "任务分组模拟器",
                    "Built-in 7-Zip & Auto-Extraction" => "内置7-Zip与自动解压",
                    "Modifications (RomFS, ExeFS & IPS)" => "Mod修改 (RomFS, ExeFS 与 IPS)",
                    "Metadata & Icon Editor" => "元数据与图标编辑器",
                    "Smart Folder" => "智能文件夹",
                    "Multi-Content & Unlocker" => "多合一内容与解锁器",
                    "Emulator Integration & SDMC Sync" => "模拟器集成与SDMC同步",
                    "Homebrew: Engine Ports & Standalone Games" => "自制程序：引擎移植与独立游戏",
                    "Game Updates (HardPatch & Splicing)" => "游戏更新 (HardPatch与无缝合并)",
                    "Format Conversion (Switch & 3DS)" => "格式转换 (Switch与3DS)",
                    "Extraction & Packaging" => "解包与打包",
                    "Settings & Keys" => "设置与密钥",
                    "Integrity Verification" => "完整性校验",
                    "Nintendo 3DS: Architecture & Multi-Content" => "Nintendo 3DS: 架构与多合一内容",
                    "Nintendo 3DS: Formats & Compression" => "Nintendo 3DS: 格式与压缩",
                    "Nintendo Game Library (19 Systems)" => "任天堂游戏库 (19代系统)",
                    "Information: Switch & 3DS Database" => "信息：Switch与3DS数据库",
                    "Dual Smart Folder Services" => "独立的智能文件夹服务",
                    "Guaranteed Temporary File Cleanup" => "可靠的临时文件清理机制",
                    _ => t.Title
                },
                Category = t.Category switch
                {
                    "Introduction" => "引言",
                    "Algorithms" => "算法",
                    "Interactive" => "交互模拟",
                    "Archives" => "压缩包",
                    "Modding" => "Mod制作",
                    "Customization" => "个性化",
                    "Automation" => "自动化",
                    "Packaging" => "打包模式",
                    "Emulators" => "模拟器",
                    "Homebrew" => "自制软件",
                    "Patching" => "补丁合并",
                    "Formats" => "格式转换",
                    "Configuration" => "配置",
                    "Validation" => "验证",
                    "Nintendo 3DS" => "任天堂 3DS",
                    "Encyclopedia" => "百科知识",
                    "Catalog" => "游戏目录",
                    "Safety" => "安全与清理",
                    _ => t.Category
                },
                Icon = t.Icon,
                DescriptionText = t.DescriptionText,
                Tip = t.Tip,
                SetupPreview = t.SetupPreview
            }).ToList();
        }

        private List<TopicItem> GetTopicsJa()
        {
            var enList = GetTopicsEn();
            return enList.Select(t => new TopicItem
            {
                Title = t.Title switch
                {
                    "Application Overview" => "アプリケーション概要",
                    "Smart File Processing" => "スマートファイル処理",
                    "Task Grouping Simulator" => "タスクグループシミュレーター",
                    "Built-in 7-Zip & Auto-Extraction" => "内蔵7-Zipと自動展開",
                    "Modifications (RomFS, ExeFS & IPS)" => "Mod機能 (RomFS, ExeFS & IPS)",
                    "Metadata & Icon Editor" => "メタデータ＆アイコンエディタ",
                    "Smart Folder" => "スマートフォルダー",
                    "Multi-Content & Unlocker" => "マルチコンテンツ＆アンロッカー",
                    "Emulator Integration & SDMC Sync" => "エミュレータ統合＆SDMC同期",
                    "Homebrew: Engine Ports & Standalone Games" => "Homebrew: 移植作＆スタンドアロンゲーム",
                    "Game Updates (HardPatch & Splicing)" => "アップデート (HardPatch & 結合)",
                    "Format Conversion (Switch & 3DS)" => "フォーマット変換 (Switch & 3DS)",
                    "Extraction & Packaging" => "展開とパッケージング",
                    "Settings & Keys" => "設定と暗号化キー",
                    "Integrity Verification" => "整合性検証",
                    "Nintendo 3DS: Architecture & Multi-Content" => "Nintendo 3DS: アーキテクチャ＆マルチコンテンツ",
                    "Nintendo 3DS: Formats & Compression" => "Nintendo 3DS: フォーマットと圧縮",
                    "Nintendo Game Library (19 Systems)" => "任天堂ゲームライブラリ (19世代)",
                    "Information: Switch & 3DS Database" => "情報：Switch & 3DSデータベース",
                    "Dual Smart Folder Services" => "独立スマートフォルダーサービス",
                    "Guaranteed Temporary File Cleanup" => "一時ファイルの確実なクリーンアップ",
                    _ => t.Title
                },
                Category = t.Category switch
                {
                    "Introduction" => "はじめに",
                    "Algorithms" => "アルゴリズム",
                    "Interactive" => "インタラクティブ",
                    "Archives" => "アーカイブ",
                    "Modding" => "Mod開発",
                    "Customization" => "カスタマイズ",
                    "Automation" => "自動化",
                    "Packaging" => "パッケージング",
                    "Emulators" => "エミュレータ",
                    "Homebrew" => "自作ソフト",
                    "Patching" => "パッチ処理",
                    "Formats" => "フォーマット",
                    "Configuration" => "設定",
                    "Validation" => "整合性確認",
                    "Nintendo 3DS" => "ニンテンドー3DS",
                    "Encyclopedia" => "百科事典",
                    "Catalog" => "カタログ",
                    "Safety" => "クリーンアップ",
                    _ => t.Category
                },
                Icon = t.Icon,
                DescriptionText = t.DescriptionText,
                Tip = t.Tip,
                SetupPreview = t.SetupPreview
            }).ToList();
        }

        private void FilterTopics(string query)
        {
            _filteredTopics.Clear();
            var search = query.Trim().ToLowerInvariant();
            
            foreach (var topic in _allTopics)
            {
                if (string.IsNullOrEmpty(search) || 
                    topic.Title.ToLowerInvariant().Contains(search) || 
                    topic.Category.ToLowerInvariant().Contains(search) || 
                    topic.DescriptionText.ToLowerInvariant().Contains(search))
                {
                    _filteredTopics.Add(topic);
                }
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            FilterTopics(sender.Text);
            if (_filteredTopics.Count > 0 && TopicList.SelectedIndex == -1)
            {
                TopicList.SelectedIndex = 0;
            }
        }

        private void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicList.SelectedItem is TopicItem topic)
            {
                TopicTitle.Text = topic.Title;
                TopicCategory.Text = topic.Category;
                TipText.Text = topic.Tip;
                
                TopicDescription.Blocks.Clear();
                var paragraph = new Paragraph();
                
                var lines = topic.DescriptionText.Split(new[] { "\n\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    paragraph.Inlines.Add(new Run { Text = lines[i] });
                    if (i < lines.Length - 1)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                        paragraph.Inlines.Add(new LineBreak());
                    }
                }
                TopicDescription.Blocks.Add(paragraph);
                
                PreviewContent.Children.Clear();
                topic.SetupPreview(PreviewContent);
            }
        }

        #region Interactive Task Simulator (v4.7.1)

        private void BuildSimulatorPreview(StackPanel container)
        {
            var mainSp = new StackPanel { Spacing = 16 };

            // 1. Drag & Drop Zone Card
            var dropZoneBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                AllowDrop = true
            };

            dropZoneBorder.DragOver += (s, e) =>
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Запустить симуляцию группировки";
                e.DragUIOverride.IsCaptionVisible = true;
                dropZoneBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            };

            dropZoneBorder.DragLeave += (s, e) =>
            {
                dropZoneBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            };

            dropZoneBorder.Drop += async (s, e) =>
            {
                dropZoneBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    SimulateCustomDroppedItems(items);
                }
            };

            var dropContent = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            dropContent.Children.Add(new FontIcon { Glyph = "\uE8E5", FontSize = 32, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue), HorizontalAlignment = HorizontalAlignment.Center });
            dropContent.Children.Add(new TextBlock { Text = "Перетащите сюда файлы (.nsp/.nsz/.xci) или папки с играми", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center });
            dropContent.Children.Add(new TextBlock { Text = "Симулятор проанализирует пути, разделит подпапки, подберет TitleID и роутит RomFS/ExeFS по правилам v4.7.1", FontSize = 12, Foreground = GetSecondaryBrush(), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 550, TextAlignment = TextAlignment.Center });

            dropZoneBorder.Child = dropContent;
            mainSp.Children.Add(dropZoneBorder);

            // 2. Preset Scenarios Buttons
            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });

            var btnDispatch = new Button
            {
                Content = "🎬 Switch: Dispatch",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnDispatch.Click += (s, e) => RunDispatchSimulation();

            var btnCadence = new Button
            {
                Content = "🎵 Switch: Cadence (3 папки)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnCadence.Click += (s, e) => RunCadenceSimulation();

            var btn3ds = new Button
            {
                Content = "🕹️ 3DS: Мульти-комплект",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(6)
            };
            btn3ds.Click += (s, e) => Run3dsMultiSimulation();

            var btnClear = new Button
            {
                Content = "🧹 Очистить",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 0, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnClear.Click += (s, e) => ClearSimulationResults();

            Grid.SetColumn(btnDispatch, 0);
            Grid.SetColumn(btnCadence, 1);
            Grid.SetColumn(btn3ds, 2);
            Grid.SetColumn(btnClear, 3);

            btnGrid.Children.Add(btnDispatch);
            btnGrid.Children.Add(btnCadence);
            btnGrid.Children.Add(btn3ds);
            btnGrid.Children.Add(btnClear);
            mainSp.Children.Add(btnGrid);

            // 3. Results Container
            _simulatorResultsPanel = new StackPanel { Spacing = 12 };
            mainSp.Children.Add(_simulatorResultsPanel);

            container.Children.Add(mainSp);

            // По умолчанию сразу показываем симуляцию Dispatch
            RunDispatchSimulation();
        }

        private void ClearSimulationResults()
        {
            if (_simulatorResultsPanel != null)
            {
                _simulatorResultsPanel.Children.Clear();
                _simulatorResultsPanel.Children.Add(new TextBlock
                {
                    Text = "Результаты симуляции очищены. Перетащите файлы или выберите тест выше.",
                    FontSize = 13,
                    Foreground = GetSecondaryBrush(),
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }
        }

        private void RunDispatchSimulation()
        {
            if (_simulatorResultsPanel == null) return;
            _simulatorResultsPanel.Children.Clear();

            _simulatorResultsPanel.Children.Add(new TextBlock
            {
                Text = "результат симуляции v4.7.1 — Папка «Dispatch» (2 подпапки = 2 изолированные задачи):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
            });

            // Задача 1
            AddSimulatedTaskCard(
                taskNumber: 1,
                title: "Dispatch [WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U)",
                filesBadge: "2",
                hasRomFs: false,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 4.62 ГБ] P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS\\Dispatch\\[WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U)\\Dispatch [01008BA02525A000][v0].nsp",
                    "2. [FILE 6.31 ГБ] P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS\\Dispatch\\[WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U)\\Dispatch Update [01008BA02525A000][v524288].nsp"
                },
                explanation: "Изолированная задача для подпапки #1. Файлов модов RomFS в этой папке НЕТ (RomFS: -)."
            );

            // Задача 2
            AddSimulatedTaskCard(
                taskNumber: 2,
                title: "Dispatch [WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U+1M)",
                filesBadge: "2",
                hasRomFs: true,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 4.62 ГБ] P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS\\Dispatch\\[WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U+1M)\\Dispatch [01008BA02525A000][v0].nsp",
                    "2. [FILE 6.31 ГБ] P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS\\Dispatch\\[WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U+1M)\\Dispatch Update [01008BA02525A000][v524288].nsp",
                    "3. [DIR Мод RomFS] P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS\\Dispatch\\[WW] [RUS] (1.0.17397 - 524288 - 01008BA02525A000) (1G+1U+1M)\\Russian Language Mod\\atmosphere\\contents\\01008BA02525A000\\romfs"
                },
                explanation: "Изолированная задача для подпапки #2. Найдена директория romfs в этом дереве пути → RomFS привязан ТОЛЬКО к этой задаче (RomFS: 1)!"
            );
        }

        private void RunCadenceSimulation()
        {
            if (_simulatorResultsPanel == null) return;
            _simulatorResultsPanel.Children.Clear();

            _simulatorResultsPanel.Children.Add(new TextBlock
            {
                Text = "результат симуляции v4.7.1 — Папка «Cadence of Hyrule» (3 подпапки = 3 изолированные задачи):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
            });

            // Задача 1
            AddSimulatedTaskCard(
                taskNumber: 1,
                title: "Cadence of Hyrule [WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)",
                filesBadge: "6",
                hasRomFs: false,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 670.69 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\Cadence of Hyrule [01000B900D8B0000][v0].nsp",
                    "2. [FILE 1008.75 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\Cadence of Hyrule [01000B900D8B0800][v458752].nsp",
                    "3. [FILE 6.02 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\4 DLCs\\DLC Character Pack [01000B900D8B1001].nsp",
                    "4. [FILE 0.12 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\4 DLCs\\DLC Melody Pack [01000B900D8B1002].nsp",
                    "5. [FILE 0.60 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\4 DLCs\\DLC Season Pass [01000B900D8B1004].nsp",
                    "6. [FILE 17.72 MB] ...\\[WW] [ENG] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D)\\4 DLCs\\DLC Symphony of Mask [01000B900D8B1003].nsp"
                },
                explanation: "Задача #1 (Английская версия без модов). 6 файлов сгрупированы по TitleID 01000B900D8B0000. RomFS: -."
            );

            // Задача 2
            AddSimulatedTaskCard(
                taskNumber: 2,
                title: "Cadence of Hyrule [WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)",
                filesBadge: "6",
                hasRomFs: false,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 670.69 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\Cadence of Hyrule [01000B900D8B0000][v0].nsp",
                    "2. [FILE 1008.73 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\Cadence of Hyrule [01000B900D8B0800][v393216].nsp",
                    "3. [FILE 6.02 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Character Pack.nsp",
                    "4. [FILE 0.12 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Melody Pack.nsp",
                    "5. [FILE 0.60 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Season Pass.nsp",
                    "6. [FILE 17.72 MB] ...\\[WW] [MOD - RUS] (1.4.0 - 393216 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Symphony of Mask.nsp"
                },
                explanation: "Задача #2 (Русский мод 1.4.0). Папка Russian Language Mod содержит пустой каталог contents без romfs → RomFS: - (корректное считывание!)."
            );

            // Задача 3
            AddSimulatedTaskCard(
                taskNumber: 3,
                title: "Cadence of Hyrule [WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)",
                filesBadge: "6",
                hasRomFs: true,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 670.69 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\Cadence of Hyrule [01000B900D8B0000][v0].nsp",
                    "2. [FILE 1008.75 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\Cadence of Hyrule [01000B900D8B0800][v458752].nsp",
                    "3. [FILE 6.02 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Character Pack.nsp",
                    "4. [FILE 0.12 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Melody Pack.nsp",
                    "5. [FILE 0.60 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Season Pass.nsp",
                    "6. [FILE 17.72 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\4 DLCs\\DLC Symphony of Mask.nsp",
                    "7. [DIR Мод RomFS 14.7 MB] ...\\[WW] [MOD - RUS] (1.5.0 - 458752 - 01000B900D8B0000) (1G+1U+4D+1M)\\Russian Language Mod\\atmosphere\\contents\\01000B900D8B0000\\romfs"
                },
                explanation: "Задача #3 (Русский мод 1.5.0). Папка содержит реальный romfs каталог → RomFS привязан (RomFS: 1)!"
            );
        }

        private void Run3dsMultiSimulation()
        {
            if (_simulatorResultsPanel == null) return;
            _simulatorResultsPanel.Children.Clear();

            _simulatorResultsPanel.Children.Add(new TextBlock
            {
                Text = "Результат симуляции v4.7.1 — Сборка 3DS Мульти-контента (Игра + Patch + 2 DLC + Мод RomFS):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
            });

            AddSimulatedTaskCard(
                taskNumber: 1,
                title: "The Legend of Zelda: Ocarina of Time 3D [WW] [MOD - RUS] [0004000000033500]",
                filesBadge: "5",
                hasRomFs: true,
                hasExeFs: false,
                inputFiles: new List<string>
                {
                    "1. [FILE 512.00 MB] Zelda Ocarina of Time 3D [0004000000033500].3ds (Базовая игра)",
                    "2. [FILE 45.20 MB] Zelda Update v1.1 [0004000E00033500].cia (Патч обновления)",
                    "3. [FILE 12.50 MB] Zelda Master Quest DLC 1 [0004008C00033501].cia (Дополнение)",
                    "4. [FILE 8.30 MB] Zelda Bonus Pack DLC 2 [0004008C00033502].cia (Дополнение)",
                    "5. [DIR Мод RomFS 64.0 MB] Russian_Translation\\romfs (Текстуры и русский текст)"
                },
                explanation: "Симулятор выполнил 5 этапов 3DS Мульти-контента:\n" +
                            "① Декомпрессия NCCH базового образа и извлечение ExHeader/ExeFS.\n" +
                            "② Слияние RomFS: базовая игра + файлы патча обновления v1.1.\n" +
                            "③ Внедрение контента из DLC 1 и DLC 2.\n" +
                            "④ Наложение папки модификации romfs (русский перевод поверх обновленной игры).\n" +
                            "⑤ Сборка через makerom/3dstool в монолитный .3ds (CCI) файл с Trimming (без пустого мусора). Итог: 1 рабочий файл!"
            );
        }

        private void SimulateCustomDroppedItems(IReadOnlyList<IStorageItem> items)
        {
            if (_simulatorResultsPanel == null) return;
            _simulatorResultsPanel.Children.Clear();

            _simulatorResultsPanel.Children.Add(new TextBlock
            {
                Text = $"Результат симуляции анализа {items.Count} элементов по правилам v4.7.1:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
            });

            int taskCounter = 1;
            foreach (var item in items)
            {
                string path = item.Path;
                bool isDir = Directory.Exists(path);

                if (isDir)
                {
                    var fileEntries = new List<string>();
                    bool hasRomFs = false;
                    try
                    {
                        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                        int num = 1;
                        foreach (var f in files)
                        {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            if (ext == ".nsp" || ext == ".nsz" || ext == ".xci" || ext == ".xcz" || ext == ".3ds" || ext == ".cci" || ext == ".cia" || ext == ".cxi")
                            {
                                var fi = new FileInfo(f);
                                double mb = Math.Round((double)fi.Length / (1024 * 1024), 2);
                                fileEntries.Add($"{num}. [FILE {mb} MB] {f}");
                                num++;
                            }
                        }

                        var romfsDirs = Directory.GetDirectories(path, "romfs", SearchOption.AllDirectories);
                        if (romfsDirs.Length > 0)
                        {
                            hasRomFs = true;
                            fileEntries.Add($"{num}. [DIR Мод RomFS] {romfsDirs[0]}");
                        }
                    }
                    catch { }

                    if (fileEntries.Count > 0)
                    {
                        AddSimulatedTaskCard(
                            taskNumber: taskCounter,
                            title: $"{Path.GetFileName(path)} (Изолированная папка)",
                            filesBadge: fileEntries.Count.ToString(),
                            hasRomFs: hasRomFs,
                            hasExeFs: false,
                            inputFiles: fileEntries,
                            explanation: $"Сформирована отдельная задача для подпапки «{Path.GetFileName(path)}». Найдено {fileEntries.Count} входных ресурсов."
                        );
                        taskCounter++;
                    }
                }
                else
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".nsp" || ext == ".nsz" || ext == ".xci" || ext == ".xcz")
                    {
                        var fi = new FileInfo(path);
                        double mb = Math.Round((double)fi.Length / (1024 * 1024), 2);

                        AddSimulatedTaskCard(
                            taskNumber: taskCounter,
                            title: $"{Path.GetFileName(path)} (Файл)",
                            filesBadge: "1",
                            hasRomFs: false,
                            hasExeFs: false,
                            inputFiles: new List<string> { $"1. [FILE {mb} MB] {path}" },
                            explanation: "Файл добавлен как самостоятельная задача."
                        );
                        taskCounter++;
                    }
                }
            }

            if (taskCounter == 1)
            {
                _simulatorResultsPanel.Children.Add(new TextBlock
                {
                    Text = "Не найдено поддерживаемых файлов (.nsp/.nsz/.xci/.xcz) или подпапок с ними.",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                });
            }
        }

        private void AddSimulatedTaskCard(int taskNumber, string title, string filesBadge, bool hasRomFs, bool hasExeFs, List<string> inputFiles, string explanation)
        {
            if (_simulatorResultsPanel == null) return;

            var cardBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var mainSp = new StackPanel { Spacing = 10 };

            // Header
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            titleSp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock { Text = $"Задача #{taskNumber}", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 12 }
            });
            titleSp.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });

            // Badges
            var badgeSp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            
            // Files count badge
            badgeSp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.SlateGray),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = $"Файлы: {filesBadge}", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }
            });

            // RomFS badge
            badgeSp.Children.Add(new Border
            {
                Background = hasRomFs ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen) : new SolidColorBrush(Microsoft.UI.Colors.DimGray),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = $"RomFS: {(hasRomFs ? "1" : "-")}", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold }
            });

            // ExeFS badge
            badgeSp.Children.Add(new Border
            {
                Background = hasExeFs ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen) : new SolidColorBrush(Microsoft.UI.Colors.DimGray),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = $"ExeFS: {(hasExeFs ? "1" : "-")}", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold }
            });

            Grid.SetColumn(titleSp, 0);
            Grid.SetColumn(badgeSp, 1);
            headerGrid.Children.Add(titleSp);
            headerGrid.Children.Add(badgeSp);
            mainSp.Children.Add(headerGrid);

            // Numbered List Header
            mainSp.Children.Add(new TextBlock
            {
                Text = "Входящие файлы задачи (построчно с нумерацией):",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = GetSecondaryBrush()
            });

            // Numbered List Items
            var listSp = new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 0, 0) };
            foreach (var f in inputFiles)
            {
                listSp.Children.Add(new TextBlock
                {
                    Text = f,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = f.Contains("RomFS") ? new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) : GetSecondaryBrush()
                });
            }
            mainSp.Children.Add(listSp);

            // Explanation note
            mainSp.Children.Add(new TextBlock
            {
                Text = $"💡 {explanation}",
                FontSize = 12,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = GetSecondaryBrush(),
                Margin = new Thickness(0, 4, 0, 0)
            });

            cardBorder.Child = mainSp;
            _simulatorResultsPanel.Children.Add(cardBorder);
        }

        #endregion

        private Brush GetSecondaryBrush()
        {
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var res) && res is Brush brush)
            {
                return brush;
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }
}
