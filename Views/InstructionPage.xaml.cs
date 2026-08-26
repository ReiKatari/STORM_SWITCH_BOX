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
        }

        private void InitializeTopics()
        {
            _allTopics = new List<TopicItem>
            {
                new TopicItem
                {
                    Title = "Обзор приложения",
                    Category = "Введение",
                    Icon = "\uE9CE",
                    DescriptionText = "STORM SWITCH BOX v4.8.0 — это профессиональный высокопроизводительный комбайн для всесторонней обработки образов игр Nintendo Switch и Nintendo 3DS, а также интерактивная энциклопедия всех 19 поколений игровых систем Nintendo (от Color TV-Game до Nintendo Switch 2).\n\nПрограмма оснащена системой «Умная обработка файлов» (Smart Processing), которая работает всегда и автоматически выбирает оптимальный метод сборки (нативное сшивание без раздувания RomFS для легких патчей или HardPatch для тяжелых обновлений и модов), распаковывает ресурсы, компилирует файлы в NSP/NSZ/3DS/CIA, конвертирует форматы внутри экосистем (Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ; 3DS: 3DS ↔ CIA ↔ CXI), объединяет игры с обновлениями, дополнениями (DLC) и модификациями в единый монолитный файл (Мульти-контент 4-в-1), автоматически распаковывает архивы, осуществляет независимый мониторинг «Умных папок» Switch и 3DS, а также мгновенно сохраняет историю в LocalAppData.",
                    Tip = "Переключайтесь между платформами Switch и 3DS в один клик через верхний селектор или настраивайте независимое отслеживание папок!",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "⚡ STORM SWITCH BOX v4.8.0", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Умная обработка файлов (Smart Processing): идеальный баланс размера и функционала по умолчанию\n• Поддержка двух экосистем: Nintendo Switch и Nintendo 3DS с изолированными конвертациями\n• Интерактивная «Библиотека игр» всех 19 поколений Nintendo (No-Intro & Redump)\n• Раздел «Информация» с визуальными плашками платформ на обложках\n• Две независимые службы «Умная папка» (Switch и 3DS)\n• Встроенный сверхбыстрый движок 7-Zip и ZstdSharp (до 22 уровня сжатия)", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Умная обработка файлов (Smart Processing)",
                    Category = "Алгоритмы",
                    Icon = "\uE945",
                    DescriptionText = "Интеллектуальный алгоритм автоматического выбора метода сборки (Smart Processing), внедренный в v4.8.0:\n\n" +
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
                    DescriptionText = "Интерактивный симулятор алгоритма группировки v4.7.9.\n\nПеретащите реальные файлы/папки в зону ниже или выберите один из готовых сценариев («Dispatch» или «Cadence of Hyrule»), чтобы увидеть, как программа сформирует изолированные комплектные задачи (ИГРА + UPDATE + DLC + ROMFS/EXEFS), определит RomFS для нужных папок и выведет полный сгруппированный результат построчно с нумерацией.",
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
                                      "Принцип работы в v4.7.9:\n" +
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

            FilterTopics(string.Empty);
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
