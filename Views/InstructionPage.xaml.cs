using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        public InstructionPage()
        {
            this.InitializeComponent();
            InitializeTopics();
            TopicList.ItemsSource = _filteredTopics;
            
            // Выбираем первый элемент по умолчанию
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
                    DescriptionText = "STORM SWITCH BOX — это высокопроизводительный комбайн для всесторонней обработки образов игр Nintendo Switch. Программа позволяет собирать обновления, распаковывать ресурсы, компилировать файлы в NSP/NSZ, конвертировать XCI в NSP, а также объединять игры с обновлениями и DLC в единый файл (Мульти-контент).\n\nБлагодаря полной интеграции библиотек LibHac и ZstdSharp (на C#), приложение выполняет сжатие и патчинг в 10–20 раз быстрее классических консольных утилит на Python, задействуя многопоточность процессора.",
                    Tip = "Используйте встроенную консоль логов в реальном времени, чтобы следить за каждым этапом выполнения задач.",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "STORM SWITCH BOX v3.8.7", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Быстрый Zstandard компрессор\n• Дельта-патчинг образов в реальном времени\n• Корректное определение контрольных заголовков NCA\n• Поддержка TitleDB для отображения названий игр", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Обновление",
                    Category = "Патчинг",
                    Icon = "\uE72C",
                    DescriptionText = "Данный режим позволяет жестко интегрировать файл обновления (.nsp/.nsz) в базовый образ игры (.nsp/.nsz/.xci).\n\nИнтеграция происходит за счет слияния RomFS таблиц и применения дельта-патчей BKTR. Полученный файл не требует отдельной установки обновлений в эмулятор и запускается как единый готовый образ.",
                    Tip = "Для тяжелых игр (например, The Legend of Zelda: Tears of the Kingdom) рекомендуется использовать уровень сжатия 'Balanced' для сохранения оптимального баланса скорости и размера.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 12 };
                        sp.Children.Add(new CheckBox { Content = "Сжать готовый образ в NSZ", IsChecked = true });
                        
                        var slider = new Slider { Header = "Уровень сжатия (Zstandard)", Minimum = 1, Maximum = 22, Value = 18 };
                        sp.Children.Add(slider);
                        
                        sp.Children.Add(new CheckBox { Content = "Удалить неиспользуемые локализации", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Распаковка",
                    Category = "Моддинг",
                    Icon = "\uE896",
                    DescriptionText = "Режим предназначен для извлечения ресурсов игры из контейнеров (.nsp, .nsz, .xci).\n\nВы можете распаковать RomFS (игровые файлы: текстуры, модели, звуки) для создания модификаций, ExeFS (исполняемый код NSO, метаданные NPDM) для отладки или чит-кодов, а также извлечь чистые NCA-разделы.",
                    Tip = "Для извлечения ресурсов требуется правильно настроенный файл ключей (prod.keys/keys.txt) в параметрах.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new RadioButton { Content = "Распаковать RomFS (содержимое игры)", IsChecked = true });
                        sp.Children.Add(new RadioButton { Content = "Распаковать ExeFS (код и бинарники)" });
                        sp.Children.Add(new RadioButton { Content = "Только NCA разделы (без расшифровки)" });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Упаковка",
                    Category = "Сборка",
                    Icon = "\uE74E",
                    DescriptionText = "Позволяет упаковать ранее распакованные RomFS/ExeFS папки или отдельные файлы обратно в официальный Switch-контейнер NSP или NSZ.\n\nЭтот режим незаменим для упаковки модифицированных игр, переводов и фанатских патчей в полноценные установочные пакеты.",
                    Tip = "Имя выходного контейнера формируется автоматически на основе названия папки, но вы можете изменить его вручную.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 12 };
                        sp.Children.Add(new TextBox { Header = "Имя выходного файла:", PlaceholderText = "MyCustomModdedGame.nsz" });
                        
                        var cb = new ComboBox { Header = "Выходной контейнер:" };
                        cb.Items.Add("NSZ (Сжатый Zstd)");
                        cb.Items.Add("NSP (Стандартный без сжатия)");
                        cb.SelectedIndex = 0;
                        sp.Children.Add(cb);
                        
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Конвертация",
                    Category = "Форматы",
                    Icon = "\uE8D4",
                    DescriptionText = "Этот режим предназначен для быстрого изменения формата файлов:\n1. Конвертация картриджных образов (.xci) в устанавливаемые файлы (.nsp).\n2. Быстрое сжатие стандартных несжатых файлов (.nsp) в сжатый формат (.nsz).\n\nКонвертация из XCI в NSP может происходить без пережатия (простое извлечение NCA-файлов), что занимает считанные секунды.",
                    Tip = "Быстрая конвертация не ухудшает качество образов и экономит много процессорного времени.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new CheckBox { Content = "Быстрая конвертация (без пережатия видео/ресурсов)", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "Игнорировать ошибки заголовка (для битых дампов)", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Мульти-контент",
                    Category = "Компоновка",
                    Icon = "\uE7BE",
                    DescriptionText = "Наиболее продвинутый режим, позволяющий объединить базовую игру, файл обновления и неограниченное число дополнений (DLC) в один монолитный файл NSP или NSZ.\n\nПрограмма анализирует Title ID каждого элемента, верифицирует их принадлежность к одной базовой игре и корректно перестраивает файловую систему PFS0. Также поддерживается интеграция модифицированных разделов RomFS/ExeFS и специальных DLC-разблокировщиков (Unlocker) для активации платного контента.",
                    Tip = "Объединение DLC и обновлений позволяет избавиться от сотен мелких файлов в вашей библиотеке и ускоряет сканирование в эмуляторах.",
                    SetupPreview = container =>
                    {
                        var grid = new Grid();
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        
                        var sp = new StackPanel { Spacing = 4 };
                        sp.Children.Add(new TextBlock { Text = "Список объединения:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "[Базовая игра] Zelda: Breath of the Wild (10 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "[Обновление] v1.6.0 (3 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "[Дополнение] The Master Trials (100 МБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "[Разблокировщик] All DLC Unlocker (64 КБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "[Модификация RomFS] Russian Voiceover (1.5 ГБ)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        
                        grid.Children.Add(sp);
                        container.Children.Add(grid);
                    }
                },
                new TopicItem
                {
                    Title = "Проверка",
                    Category = "Валидация",
                    Icon = "\uE8FB",
                    DescriptionText = "Верификатор целостности файлов (.nsp, .nsz, .xci).\n\nРежим считывает внутренние хэши NCA-разделов и сравнивает их с сигнатурами заголовка. Это позволяет на 100% подтвердить, что файл не поврежден при скачивании или сборке.",
                    Tip = "Используйте полную проверку перед отправкой собранных NSZ-файлов на портативные консоли.",
                    SetupPreview = container =>
                    {
                        var progress = new ProgressBar { Value = 75, Minimum = 0, Maximum = 100, Height = 10 };
                        var label = new TextBlock { Text = "Проверка разделов: 75% завершено (Ошибок не обнаружено)", FontSize = 12, Foreground = GetSecondaryBrush(), Margin = new Thickness(0,4,0,0) };
                        container.Children.Add(progress);
                        container.Children.Add(label);
                    }
                },
                new TopicItem
                {
                    Title = "Информация",
                    Category = "Утилиты",
                    Icon = "\uE946",
                    DescriptionText = "Данный модуль позволяет мгновенно просмотреть подробные метаданные любого NSP, NSZ или XCI-образа без его распаковки.\n\nВы можете узнать официальное название игры, уникальный Title ID, точную версию, минимально требуемую версию прошивки (System Version), размер игры, а также просмотреть все встроенные языковые локализации и иконку игры.",
                    Tip = "Используйте этот режим для быстрой проверки скачанных образов перед их установкой на консоль или эмулятор.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "Супер Марио (Super Mario Odyssey)", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        sp.Children.Add(new TextBlock { Text = "Title ID: 0100000000010000", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "Версия: 1.3.0 (Update)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "Требуемая прошивка: 16.0.0+", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "История",
                    Category = "Утилиты",
                    Icon = "\uE81C",
                    DescriptionText = "Модуль истории отображает подробный список всех выполненных, выполняемых и запланированных задач.\n\nЗдесь вы можете отслеживать текущие фоновые процессы сжатия, конвертации или патчинга, просматривать подробный лог выполнения в реальном времени, а также принудительно останавливать задачи.",
                    Tip = "Вы можете скопировать лог выбранной задачи в буфер обмена для отправки отчетов об ошибках.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "Активные задачи:", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "✔ Сжатие: Zelda.nsz — Выполнено (12 мин)", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "⏳ Патчинг: TOTK.nsp — Выполняется (64%)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Параметры",
                    Category = "Конфигурация",
                    Icon = "\uE713",
                    DescriptionText = "Вкладка параметров позволяет настроить глобальные конфигурации приложения под ваши нужды:\n\n1. Путь к файлу криптографических ключей (keys.txt / prod.keys).\n2. Версию прошивки ключей (через удобные 6 квадратиков для ввода).\n3. Выходной каталог по умолчанию для сжатых и собранных образов.\n4. Уровень Zstandard-сжатия по умолчанию (Быстрый, Сбалансированный, Высокий, Максимальный).\n5. Выбор языка интерфейса.\n6. Кнопку ручной проверки и установки обновлений.",
                    Tip = "Все изменения сохраняются в файл конфигурации приложения автоматически.",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 12 };
                        var cb = new ComboBox { Header = "Язык интерфейса:" };
                        cb.Items.Add("Русский (Russian)");
                        cb.Items.Add("English (Английский)");
                        cb.SelectedIndex = 0;
                        sp.Children.Add(cb);
                        
                        sp.Children.Add(new Button { Content = "Проверить обновление", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left });
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
                
                // Наполняем описание
                TopicDescription.Blocks.Clear();
                var paragraph = new Paragraph();
                
                // Делим текст по переносам и добавляем
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
                
                // Очищаем и настраиваем интерактивное превью
                PreviewContent.Children.Clear();
                topic.SetupPreview(PreviewContent);
            }
        }

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
