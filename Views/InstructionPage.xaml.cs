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
                    DescriptionText = "STORM SWITCH BOX v4.3.6 — это высокопроизводительный комбайн для всесторонней обработки образов игр Nintendo Switch. Программа позволяет собирать обновления (HardPatch), распаковывать ресурсы, компилировать файлы в NSP/NSZ, конвертировать XCI в NSP, объединять игры с обновлениями и DLC в единый файл (Мульти-контент), а также осуществлять автоматический мониторинг через «Умную» папку.\n\nБлагодаря полной интеграции C# библиотек LibHac и ZstdSharp, приложение выполняет сжатие, вырезание языков и дельта-патчинг в 10–20 раз быстрее классических утилит на Python, задействуя многопоточность ЦП.",
                    Tip = "Вы можете включить автоматическое отслеживание «Умной» папки в Параметрах: файлы и папки будут мгновенно определяться и запускаться в обработку.",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "STORM SWITCH BOX v4.3.6", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Умная папка с авто-группировкой по папкам и TitleID\n• Автоматический запуск задач при поступлении новых файлов\n• Изолированная проверка RomFS / ExeFS для каждой задачи\n• Быстрый Zstandard компрессор и дельта-патчинг BKTR\n• Интерактивное удаление неиспользуемых языковых локализаций", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Симулятор группировки задач",
                    Category = "Интерактив",
                    Icon = "\uE8E5",
                    DescriptionText = "Интерактивный симулятор алгоритма группировки v4.3.6.\n\nПеретащите реальные файлы/папки в зону ниже или выберите один из готовых сценариев («Dispatch» или «Cadence of Hyrule»), чтобы увидеть, как программа сформирует изолированные комплектные задачи (ИГРА + UPDATE + DLC + ROMFS/EXEFS), определит RomFS для нужных папок и выведет полный сгруппированный результат построчно с нумерацией.",
                    Tip = "Перетаскивайте папки с несколькими релизами прямо в симулятор: вы сразу увидите, как файлы разделятся по независимым задачам!",
                    SetupPreview = container => BuildSimulatorPreview(container)
                },
                new TopicItem
                {
                    Title = "«Умная» папка",
                    Category = "Автоматизация",
                    Icon = "\uE812",
                    DescriptionText = "«Умная» папка предназначена для автоматической фоновой обработки игр.\n\n" +
                                      "Принцип работы в v4.3.6:\n" +
                                      "1. Активация — просто включите переключатель в Параметрах. Сканирование начинается мгновенно!\n" +
                                      "2. Изоляция по папкам — каждая подпапка первого уровня формирует отдельную изолированную задачу.\n" +
                                      "3. Комплектность по TitleID — внутри одной подпапки базовая игра, файлы обновления, DLC и модификации (RomFS/ExeFS) автоматически объединяются в один комплект.\n" +
                                      "4. Точечная привязка RomFS — папки модификаций RomFS/ExeFS привязываются только к той задаче, из чьей директории они происходят.\n" +
                                      "5. Автозапуск — после сканирования или появления новых файлов задачи автоматически запускаются в обработку по заданным параметрам.",
                    Tip = "Используйте поддержку Drag-and-Drop в Параметрах, чтобы легко задать «Умную» папку перетаскиванием.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Автоматическое сканирование и обработка", IsChecked = true });
                        sp.Children.Add(new TextBlock { Text = "Папка: P:\\CONSOLES\\Nintendo Switch\\DOWNLOADS", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "Режим: Мульти-контент → Формат: NSP", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "⚡ Автоматический запуск задач активирован", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Мульти-контент",
                    Category = "Компоновка",
                    Icon = "\uE7BE",
                    DescriptionText = "Наиболее продвинутый режим для сборки монолитных образов NSP или NSZ.\n\nПрограмма объединяет Базовую игру, Файл обновления, Все дополнения (DLC), Модификации RomFS/ExeFS и Unlocker в единый устанавливаемый файл.\n\nБлагодаря правилам v4.3.6 образы из разных каталогов никогда не перемешиваются, сохраняя чистую структуру комплектов.",
                    Tip = "Сборка Мульти-контента в NSZ позволяет сэкономить гигабайты места при хранении единого файла игры со всеми DLC.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 6 };
                        sp.Children.Add(new TextBlock { Text = "Комплект Мульти-контента:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "1. [ИГРА] Zelda: Breath of the Wild (10.0 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "2. [ОБНОВЛЕНИЕ] Update v1.6.0 (3.2 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "3. [DLC] The Master Trials & Ballad (150 МБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "4. [ROMFS] Русская озвучка (1.5 ГБ) [RomFS: 1]", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Обновление",
                    Category = "Патчинг",
                    Icon = "\uE72C",
                    DescriptionText = "Режим жесткой интеграции (HardPatch) обновления в базовый образ игры.\n\nПрограмма сливает RomFS-структуры и применяет дельта-патчи BKTR. Полученный образ работает без необходимости отдельной установки патча.",
                    Tip = "Используйте тумблер сжатия в NSZ для получения минимального размера итогового образа.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 10 };
                        sp.Children.Add(new CheckBox { Content = "Сжать итоговый образ в NSZ (Zstandard)", IsChecked = true });
                        sp.Children.Add(new Slider { Header = "Уровень сжатия (18 - Стандарт)", Minimum = 1, Maximum = 22, Value = 18 });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Конвертация",
                    Category = "Форматы",
                    Icon = "\uE8D4",
                    DescriptionText = "Быстрая конвертация картриджных образов (.xci) в устанавливаемые файлы (.nsp) и сжатие в (.nsz).\n\nПоддерживается пасс-фру конвертация без пережатия видео/аудио данных, занимающая считанные секунды.",
                    Tip = "XCI в NSP конвертируется без потери данных и без нагрузки на ЦП.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new CheckBox { Content = "Быстрая конвертация без пережатия", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "Игнорировать ошибки заголовков NCA", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Распаковка & Упаковка",
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
                    Title = "Параметры & Ключи",
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

        #region Interactive Task Simulator (v4.3.6)

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
            dropContent.Children.Add(new TextBlock { Text = "Симулятор проанализирует пути, разделит подпапки, подберет TitleID и роутит RomFS/ExeFS по правилам v4.3.6", FontSize = 12, Foreground = GetSecondaryBrush(), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 550, TextAlignment = TextAlignment.Center });

            dropZoneBorder.Child = dropContent;
            mainSp.Children.Add(dropZoneBorder);

            // 2. Preset Scenarios Buttons
            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnDispatch = new Button
            {
                Content = "🎬 Сценарий 1: Dispatch (2 папки)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnDispatch.Click += (s, e) => RunDispatchSimulation();

            var btnCadence = new Button
            {
                Content = "🎵 Сценарий 2: Cadence of Hyrule (3 папки)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnCadence.Click += (s, e) => RunCadenceSimulation();

            var btnClear = new Button
            {
                Content = "🧹 Очистить результат",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 0, 0, 0),
                CornerRadius = new CornerRadius(6)
            };
            btnClear.Click += (s, e) => ClearSimulationResults();

            Grid.SetColumn(btnDispatch, 0);
            Grid.SetColumn(btnCadence, 1);
            Grid.SetColumn(btnClear, 2);

            btnGrid.Children.Add(btnDispatch);
            btnGrid.Children.Add(btnCadence);
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
                Text = "результат симуляции v4.3.6 — Папка «Dispatch» (2 подпапки = 2 изолированные задачи):",
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
                Text = "результат симуляции v4.3.6 — Папка «Cadence of Hyrule» (3 подпапки = 3 изолированные задачи):",
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

        private void SimulateCustomDroppedItems(IReadOnlyList<IStorageItem> items)
        {
            if (_simulatorResultsPanel == null) return;
            _simulatorResultsPanel.Children.Clear();

            _simulatorResultsPanel.Children.Add(new TextBlock
            {
                Text = $"Результат симуляции анализа {items.Count} элементов по правилам v4.3.6:",
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
                            if (ext == ".nsp" || ext == ".nsz" || ext == ".xci" || ext == ".xcz")
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
