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
                    DescriptionText = "STORM SWITCH BOX 5.0.14 — это профессиональный высокопроизводительный комбайн для всесторонней обработки образов игр Nintendo Switch и Nintendo 3DS, а также интерактивная энциклопедия всех 19 поколений игровых систем Nintendo (от Color TV-Game до Nintendo Switch 2).\n\nПрограмма оснащена системой «Умная обработка файлов» (Smart Processing), которая работает всегда и автоматически выбирает оптимальный метод сборки (нативное сшивание без раздувания RomFS для легких патчей или HardPatch для тяжелых обновлений и модов), распаковывает ресурсы, компилирует файлы в NSP/NSZ/3DS/CIA, конвертирует форматы внутри экосистем (Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ; 3DS: 3DS ↔ CIA ↔ CXI), объединяет игры с обновлениями, дополнениями (DLC) и модификациями в единый монолитный файл (Мульти-контент 4-в-1), автоматически собирает Homebrew порты и игры в один файл, осуществляет независимый мониторинг «Умных папок» Switch и 3DS, а также мгновенно сохраняет историю в LocalAppData.",
                    Tip = "Переключайтесь между платформами Switch и 3DS в один клик через верхний селектор или настраивайте независимое отслеживание папок!",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "⚡ STORM SWITCH BOX 5.0.14", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Умная обработка файлов: идеальный баланс размера и функционала по умолчанию\n• Поддержка двух экосистем: Nintendo Switch и Nintendo 3DS с изолированными конвертациями\n• Интерактивная «Библиотека игр» всех 19 поколений Nintendo (No-Intro и Redump)\n• Раздел «Информация» с визуальными плашками платформ на обложках\n• Две независимые службы «Умная папка» (Switch и 3DS)\n• Встроенный сверхбыстрый движок 7-Zip и ZstdSharp (до 22 уровня сжатия)", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Умная обработка файлов",
                    Category = "Алгоритмы",
                    Icon = "\uE945",
                    DescriptionText = "Интеллектуальный алгоритм автоматического выбора метода сборки (Smart Processing), внедренный в 5.0.14:\n\n" +
                                      "Цель алгоритма: получить абсолютно минимальный размер выходного файла при 100% сохранении всего функционала, модов и дополнений.\n\n" +
                                      "Как работает авто-анализ:\n" +
                                      "1. Легковесные патчи (напр. Ys X Nordics: патч 60 МБ на игру 6.75 ГБ) — программа применяет нативное сшивание LibHac PFS0. Это сохраняет оригинальный несжатый размер (6.81 ГБ) без раздувания RomFS до 10.4 ГБ!\n" +
                                      "2. Массивные обновления (напр. The Witcher 3, MK11: патч >= 40% от базы) — программа запускает физический HardPatch, который удаляет старые 10 ГБ устаревших файлов и заменяет их новыми ресурсами из обновления, экономя гигабайты дискового пространства!\n" +
                                      "3. Наличие папок модификаций (romfs, exefs, exefs_patches) — автоматически включает HardPatch для надежного внедрения перевода и модов прямо в бинарные ресурсы игры.\n\n" +
                                      "Вся логика решений наглядно отображается в логе задачи с иконкой 🧠.",
                    Tip = "Умная обработка активна всегда и по умолчанию — вам больше не нужно вручную думать, когда пересобирать, а когда сшивать!",
                    SetupPreview = container => BuildSmartProcessingInteractivePreview(container)
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
                    Title = "Модификации (RomFS, ExeFS и IPS)",
                    Category = "Моды",
                    Icon = "\uE7B5",
                    DescriptionText = "Комплексная поддержка любых видов модификаций Nintendo Switch:\n\n" +
                                      "1. RomFS — перевод текста, русская озвучка, HD-текстуры и замена моделей. Положите папку romfs рядом с игрой.\n" +
                                      "2. ExeFS — модифицированные бинарные модули NSO (main, subsdk0).\n" +
                                      "3. ExeFS_Patches (IPS) — папки с .ips патчами (60 FPS, твики графики, отключение размытия, читы). Программа автоматически накладывает IPS-патчи на исполняемый код main при сборке.\n" +
                                      "4. Отображение в эмуляторах — вшитые модификации регистрируются как AddOnContent (DLC) и отображаются в свойствах игры в эмуляторах (STORM SWITCH, Eden Nightly, Yuzu, Ryujinx) с возможностью их включения/выключения.",
                    Tip = "Задайте красивое имя для мода (например, «Русская озвучка GamesVoice») через Редактор метаданных!",
                    SetupPreview = container => BuildModsInteractivePreview(container)
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
                                      "• Замена иконки: поддержка загрузки любого изображения .jpg, .png (авто-масштабирование в 256x256 JPEG с сохранением пропорций).\n" +
                                      "• Редактирование версий: исправление номеров версий и отображаемой строки обновления.",
                    Tip = "Вы можете менять название и иконку даже для уже упакованных игр без полной пересборки!",
                    SetupPreview = container => BuildMetadataInteractivePreview(container)
                },
                new TopicItem
                {
                    Title = "«Умная» папка",
                    Category = "Автоматизация",
                    Icon = "\uE8B7",
                    DescriptionText = "Полностью автономный конвейер потоковой обработки игр для Nintendo Switch и Nintendo 3DS с раздельным мониторингом:\n\n" +
                                      "1. Две независимые папки мониторинга — вы можете настроить отдельный каталог для игр Switch (напр. D:\\Games\\Switch_Incoming) и отдельный каталог для 3DS (напр. D:\\Games\\3DS_Incoming). Каждая служба работает независимо со своими параметрами сжатия и целевого формата!\n" +
                                      "2. Положили файл — получили готовый результат: программа автоматически подхватывает новые файлы, объединяет обновления/DLC, пересобирает/конвертирует по выбранным настройкам и перемещает результат в указанную выходную папку.\n" +
                                      "3. Защита от недокачанных файлов — утилита ожидает полного завершения копирования файла перед началом его обработки, предотвращая повреждение данных.\n" +
                                      "4. Уведомления и история — при завершении фоновой обработки отправляется нативное уведомление Windows, а результат заносится в Историю.",
                    Tip = "Настройте входящую папку для загрузок вашего торрент-клиента или браузера, и игры будут оптимизироваться и конвертироваться автоматически сразу после загрузки!",
                    SetupPreview = container => BuildSmartFolderInteractivePreview(container)
                },
                new TopicItem
                {
                    Title = "Мульти-контент и Unlocker",
                    Category = "Сборка",
                    Icon = "\uE74C",
                    DescriptionText = "Революционный режим объединения игры, всех дополнений и обновлений в один файл (Монолитный образ):\n\n" +
                                      "• 1G + 1U + ALL DLC в одном NSP или XCI: забудьте об установке десятков отдельных файлов дополнений.\n" +
                                      "• DLC Unlocker: программа может сгенерировать билеты и заглушки для всех доступных DLC по TitleDB базе данных.\n" +
                                      "• Умная замена старых версий патчей: если в папке лежит патч v1.0.1 и патч v1.0.5, программа автоматически выберет самый свежий, исключив конфликты.\n" +
                                      "• Экономия места: устранение дублирующихся дельт и неиспользуемых файлов.",
                    Tip = "Используйте режим Multi-Content для создания идеальных архивных копий ваших любимых игр со всеми дополнениями!",
                    SetupPreview = container => BuildMultiContentInteractivePreview(container)
                },
                new TopicItem
                {
                    Title = "Интеграция с эмуляторами и синхронизация SDMC",
                    Category = "Эмуляторы",
                    Icon = "\uE7FC",
                    DescriptionText = "STORM SWITCH BOX 5.0.14 предоставляет полную свободу в интеграции с локальными эмуляторами Nintendo Switch (STORM SWITCH, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citron и др.):\n\n" +
                                      "1. Пользовательский выбор папок эмуляторов — в разделе «Параметры» доступен специальный блок «Интеграция с эмуляторами (Путь к папке эмулятора)». Вы можете перетащить (Drag and Drop) или выбрать через проводник одну или несколько директорий ваших эмуляторов (например, E:\\STORM SWITCH\\Assembling, L:\\Emulators\\Ryujinx и др.).\n\n" +
                                      "2. Чистота выходной библиотеки — при сборке Homebrew-игр и портов программа больше НЕ создает лишних папок [SDMC] в вашей основной папке с играми. Все файлы NRO, данные и конфигурации доставляются строго в виртуальные SD-карты указанных эмуляторов (user/sdmc/switch/<game>/), а рядом с игрой сохраняется только чистый итоговый файл (.nsp / .nsz / .xci).\n\n" +
                                      "3. Автономные ZIP-архивы для реальной консоли Switch — если вам требуются готовые данные SDMC для физической консоли, укажите «Каталог для архивов SDMC» в Параметрах. Программа автоматически упакует структуру карты памяти в полноценный zip-архив вида «Имя_Игры_[SDMC].zip» в указанную папку, оставив библиотеку игр чистой.\n\n" +
                                      "4. Строгая эксклюзивная доставка — данные синхронизируются строго в одну правильную целевую папку эмулятора user/sdmc/switch/<game>/, а любые устаревшие папки-дубликаты автоматически удаляются, исключая раздувание дискового пространства.",
                    Tip = "Задайте папку вашего эмулятора один раз в Параметрах, и Homebrew-порты будут запускаться моментально без единого ручного действия!",
                    SetupPreview = container => BuildEmulatorSyncInteractivePreview(container)
                },
                new TopicItem
                {
                    Title = "Homebrew: Сборка портов и автономных игр",
                    Category = "Homebrew",
                    Icon = "\uE7FC",
                    DescriptionText = "Специализированный раздел «Homebrew» для автоматического распознавания, объединения и сборки любых портов и любительских игр в монолитные автономные файлы (NSP / NSZ / XCI):\n\n" +
                                      "1. Умное распознавание любых наборов файлов — просто перетащите папку с игрой (например, Diablo I, GTA San Andreas / Vice City, DOOM, Half-Life, Quake, S.T.A.L.K.E.R., Morrowind) или группу файлов (.nro, .ovl, .nsp форвардеры, архивы .zip/.7z, папки atmosphere/contents/<TitleID>/romfs). Программа мгновенно объединит их в готовую задачу.\n" +
                                      "2. Стандартизация 5.0.14 (Без раздувания и без ошибки 0x75B):\n" +
                                      "   • Ресурсы RomFS вшиваются ровно в 1 экземпляре (Diablo I весит 718.01 МБ вместо раздутых 4.27 ГБ!).\n" +
                                      "   • В SDMC эмулятора создается ровно одна правильная папка user/sdmc/switch/<game>/.\n" +
                                      "   • Форвардер нацелен строго на sdmc:/switch/<game>/<game>.nro, что предотвращает ошибку вылета 0x75B (Userspace PANIC!).\n" +
                                      "3. Авто-деплой SDMC — все внешние ресурсы игры (.mpq, .rpf, .wad, .pk3, .pak, .dat, .bin, .ini, .cfg, шрифты и текстуры) автоматически синхронизируются в целевые папки SDMC эмуляторов (STORM SWITCH, Yuzu, Ryujinx, Suyu, Sudachi) без засорения выходной библиотеки.\n\n" +
                                      "⚡ Как правильно запускать Homebrew-игры и порты движков:\n\n" +
                                      "► Вариант А: Прямой запуск .nro (Самый надежный способ)\n" +
                                      "Поместите файл игры с расширением .nro (например: devilutionx.nro, sm64.nro, xash3d.nro, openmw.nro и т.д.) в папку с вашими играми. В эмуляторе нажмите «Загрузить файл» (или добавьте папку в библиотеку эмулятора — STORM SWITCH автоматически сканирует расширения .nro, .nsp, .xci). Игра запустится напрямую без участия форвардеров.\n\n" +
                                      "► Вариант Б: Использование Форвардеров (.nsp)\n" +
                                      "STORM SWITCH BOX при сборке автоматически разложит необходимые исполняемые файлы и ресурсы в виртуальную карту вашего эмулятора:\n" +
                                      "   • Diablo I: user/sdmc/switch/devilutionx/ (devilutionx.nro + diabdat.mpq)\n" +
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
                    SetupPreview = container => BuildHomebrewInteractivePreview(container)
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
                    Title = "Установка на консоль (DBI USB и Сеть)",
                    Category = "Консоль",
                    Icon = "\uE88E",
                    DescriptionText = "Прямая высокоскоростная установка скомпилированных игр (.nsp / .nsz / .xci / .xcz) на консоль Nintendo Switch без необходимости извлекать microSD карту:\n\n" +
                                      "1. Режим DBI MTP Responder (USB) — подключите консоль к ПК качественным кабелем Type-C, запустите homebrew DBI на Switch и выберите пункт «Run MTP Responder». В программе нажмите правой кнопкой мыши по задаче или готовому файлу и выберите «Установить на консоль по USB (DBI)». Программа мгновенно обнаружит виртуальный раздел «5: MicroSD install» и передаст файл на карту памяти со скоростью 30–45 МБ/с с установкой на лету!\n\n" +
                                      "2. Режим сетевого сервера (Wi-Fi / LAN) — если консоль не подключена по проводу, программа автоматически запустит встроенный локальный HTTP-сервер (порт 8080). В меню DBI на Switch выберите «Install title from Web / HTTP», введите IP-адрес вашего ПК и выберите нужную игру из списка для беспроводной установки!",
                    Tip = "Запустите MTP Responder в DBI перед нажатием кнопки «Установить» — программа определит консоль автоматически!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "📲 Статус DBI: Готов к передаче по USB / Сети", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new TextBlock { Text = "• USB MTP Responder: Автоматическое определение раздела 5: MicroSD install\n• HTTP Web Server: Локальная раздача игр по Wi-Fi на порт 8080", Foreground = GetSecondaryBrush() });
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
                    SetupPreview = container => BuildFormatConverterInteractivePreview(container)
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
                    DescriptionText = "Полный спектр настроек и инструментов для точного управления обработкой образов:\n\n" +
                                      "• Drag and Drop файла ключей (prod.keys / keys.txt) с подсветкой и автоматическим определением версии.\n" +
                                      "• 6-ячеечный ввод версии прошивки с автоматической навигацией курсора.\n" +
                                      "• Drag and Drop выходной папки и папок мониторинга «Умная папка».\n" +
                                      "• Очистка Delta NCA (Delta Cleaner): автоматическое удаление избыточных дельта-патчей из обновлений для экономии до 30–50% места.\n" +
                                      "• Оптимизация ассетов модов: автоматическое сжатие текстур PNG без потерь через Oxipng и удаление мусорных файлов (.bak, .tmp, Thumbs.db, .DS_Store).\n" +
                                      "• Умная обработка файлов: автоматический выбор между легким сшиванием и монолитным HardPatch RomFS.\n" +
                                      "• Ticketless NSP (--C_clean_ND) для гарантированной работы на любых CFW.\n" +
                                      "• Понижение требований к прошивке (RSV Cap Downgrade: FW 18.0, 17.0, 16.0, 15.0, 10.0).\n" +
                                      "• Разделение файлов крупнее 4 ГБ для FAT32 SD-карт (.xc0/.xc1 или .00/.01).\n" +
                                      "• Очистка неиспользуемых языковых локализаций из RomFS.\n" +
                                      "• Полная изоляция экосистем Nintendo Switch и Nintendo 3DS с собственными ключами и форматами.",
                    Tip = "Каждая опция в Параметрах оснащена подробными подсказками и сохраняется автоматически!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔑 Файл ключей: prod.keys (Активен)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new CheckBox { Content = "🗑️ Удалять Delta NCA из обновлений (Delta Cleaner)", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "🎨 Оптимизация ассетов модов (Oxipng и очистка мусора)", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "🔓 Удалить Titlerights (Ticketless NSP)", IsChecked = false });
                        sp.Children.Add(new CheckBox { Content = "💾 Разделять файлы для FAT32 (> 4 GB)", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Параметры и задачи: матрица совместимости",
                    Category = "Конфигурация",
                    Icon = "\uE802",
                    DescriptionText = "Исчерпывающий анализ всех параметров программы и их реальной привязки к типам задач:\n\n" +
                                      "1. Удаление Delta NCA из обновлений (Delta Cleaner):\n" +
                                      "   • Активен: «Мульти-контент», «Обновление», «Сжатие в NSZ/XCZ» (Конвертация), «Умная папка Switch».\n" +
                                      "   • Принцип: Парсит бинарный CNMT и вырезает мусорные DeltaFragment NCA, уменьшая вес обновлений на 30–50%.\n\n" +
                                      "2. Оптимизация ассетов модов (Oxipng и очистка мусора):\n" +
                                      "   • Активен: «Мульти-контент» и «Обновление» (при наличии пользовательских папок romfs/exefs).\n" +
                                      "   • Принцип: Удаляет мусорные файлы (.bak, .tmp, Thumbs.db, .DS_Store) и сжимает текстуры PNG без потерь через Oxipng (экономия 20–40% веса текстур).\n\n" +
                                      "3. Уровень сжатия Zstandard (NSZ / XCZ):\n" +
                                      "   • Активен: Любые задачи с целевым форматом NSZ или XCZ («Конвертация», «Мульти-контент», «Обновление», «Упаковка», «Умная папка Switch»).\n" +
                                      "   • Принцип: Передаёт уровень сжатия (3, 10, 18, 22) в блочный компрессор NCZBLOCK (1 МБ). Для NSP, XCI и 3DS игнорируется.\n\n" +
                                      "4. Комплексное выходное имя файла по папкам:\n" +
                                      "   • Активен: «Мульти-контент», «Обновление», «Конвертация», «Упаковка», «Homebrew», «Умная папка».\n" +
                                      "   • Принцип: Создает структуру [Название Игры]/[Формат] и формирует имя «Имя [TitleID] [vВерсия]».\n\n" +
                                      "5. Удалять исходные файлы после успешной обработки:\n" +
                                      "   • Активен: ВСЕ задачи («Конвертация», «Мульти-контент», «Обновление», «Упаковка», «Homebrew», «3DS задачи»).\n" +
                                      "   • Принцип: Физически удаляет файлы только при 100% успехе (статус «Успешно» или «Готово»). Для «Проверки» (Verify) файлы не удаляются никогда.\n\n" +
                                      "6. Криптографические ключи Switch (prod.keys / keys.txt):\n" +
                                      "   • Активен: «Мульти-контент», «Обновление», «Распаковка», «Конвертация», «Проверка», «Homebrew».\n" +
                                      "   • Принцип: Необходим для дешифровки заголовков NCA, распаковки PFS0/RomFS и проверки хешей.\n\n" +
                                      "7. Стратегия сборки мульти-контента (Умный режим):\n" +
                                      "   • Активен: «Мульти-контент» и «Обновление».\n" +
                                      "   • Принцип: Умный режим выбирает нативное сшивание LibHac PFS0 для легких патчей (без раздувания RomFS) либо монолитный HardPatch RomFS при тяжелых патчах и модах.\n\n" +
                                      "8. Интеграция с эмуляторами (Папки эмуляторов SDMC):\n" +
                                      "   • Активен: «Homebrew» (Сборка портов движков и любительских игр).\n" +
                                      "   • Принцип: Доставляет данные игры прямо в user/sdmc/switch/<game>/ эмуляторов, оставляя выходную библиотеку игр чистой.\n\n" +
                                      "9. Понижение системных требований (RSV Cap Downgrade):\n" +
                                      "   • Активен: «Мульти-контент», «Обновление», «Упаковка».\n" +
                                      "   • Принцип: Патчит RequiredSystemVersion в CNMT для запуска новых игр на старых прошивках.\n\n" +
                                      "10. Авто-инъекция патчей 60 FPS и твиков графики:\n" +
                                      "   • Активен: «Мульти-контент» и «Обновление» (в режиме HardPatch).\n" +
                                      "   • Принцип: Внедряет IPS-патчи 60 кадров/сек и исправления графики в main.\n\n" +
                                      "11. Инспектор полноты дополнений (DLC Completeness):\n" +
                                      "   • Активен: «Мульти-контент», «Обновление» и раздел «Информация».\n" +
                                      "   • Принцип: Сверяет список DLC с базой TitleDB и сообщает в логе о недостающих дополнениях.\n\n" +
                                      "12. Удаление неиспользуемых языков из RomFS:\n" +
                                      "   • Активен: «Мульти-контент» (HardPatch), «Обновление» (HardPatch), «Конвертация» в XCI/XCZ.\n" +
                                      "   • Принцип: Удаляет папки локализаций из RomFS, не входящие в список KeepLanguages.\n\n" +
                                      "13. Настройки Nintendo 3DS (Ключи, формат по умолчанию, умная папка 3DS):\n" +
                                      "   • Активен: Исключительно для всех задач Nintendo 3DS («Конвертация 3DS», «Мульти-контент 3DS», «Распаковка 3DS», «Упаковка 3DS», «Проверка 3DS»).\n\n" +
                                      "14. Многопоточность и максимум задач (UsedCores, ConcurrentTasks):\n" +
                                      "   • Активен: Для всех параллельных и фоновых операций в Задачнике.",
                    Tip = "Используйте интерактивный инспектор ниже для мгновенной проверки совместимости каждого параметра!",
                    SetupPreview = container => BuildSettingsMatrixInteractivePreview(container)
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
                    DescriptionText = "STORM SWITCH BOX 5.0.14 is a professional, high-performance toolkit for processing Nintendo Switch and Nintendo 3DS games, as well as an interactive encyclopedia of all 19 Nintendo console generations (from Color TV-Game to Nintendo Switch 2).\n\nEquipped with Smart File Processing, the program automatically selects the optimal build method (native PFS0 splicing without RomFS inflation for lightweight patches, or physical HardPatch for heavy updates and mods), unpacks resources, compiles NSP/NSZ/3DS/CIA, converts formats across ecosystems (Switch: NSP ↔ XCI ↔ NSZ ↔ XCZ; 3DS: 3DS ↔ CIA ↔ CXI), bundles games with updates and DLCs into monolithic 4-in-1 packages, builds Homebrew ports, monitors dual Smart Folders, and saves instant history.",
                    Tip = "Switch between Nintendo Switch and 3DS in one click via the top header bar!",
                    SetupPreview = container =>
                    {
                        container.Children.Add(new TextBlock { Text = "⚡ STORM SWITCH BOX 5.0.14", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                        container.Children.Add(new TextBlock { Text = "• Smart File Processing: Optimal file size and 100% mod compatibility by default\n• Dual Ecosystems: Nintendo Switch and Nintendo 3DS support\n• Interactive Game Library: All 19 Nintendo generations (No-Intro and Redump)\n• Information Catalog: High-res artwork with platform badges\n• Dual Independent Smart Folders (Switch and 3DS)\n• Embedded 7-Zip and Zstandard compression engines (up to level 22)", Foreground = GetSecondaryBrush() });
                    }
                },
                new TopicItem
                {
                    Title = "Smart File Processing",
                    Category = "Algorithms",
                    Icon = "\uE945",
                    DescriptionText = "Intelligent automatic build method selection algorithm (Smart Processing) in 5.0.14:\n\n" +
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
                                      "4. Emulator Compatibility — embedded mods are recognized as DLC and can be toggled on/off in emulator game properties (STORM SWITCH, Eden Nightly, Yuzu, Ryujinx).",
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
                    Title = "Emulator Integration and SDMC Sync",
                    Category = "Emulators",
                    Icon = "\uE7FC",
                    DescriptionText = "Seamless integration with Nintendo Switch emulators (STORM SWITCH, Yuzu, Ryujinx, Suyu, Sudachi, Torzu, Citron):\n\n" +
                                      "1. Custom Emulator Directories — In Settings, specify one or more emulator paths via Drag and Drop or folder picker.\n" +
                                      "2. Clean Game Library — Building Homebrew ports delivers NRO data directly into emulator SDMC (user/sdmc/switch/<game>/) without creating redundant [SDMC] folders in your main game library.\n" +
                                      "3. Standalone SDMC ZIP Archives — If you need SDMC data for physical Switch console, specify «SDMC Archive Directory» in Settings. The app will pack it into a clean .zip archive while keeping game library spotless.\n" +
                                      "4. Strict Filtering — Data synchronizes strictly to specified emulator paths.",
                    Tip = "Specify your emulator path once in Settings for instant launch of Homebrew ports!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🎮 Targeted Emulator Sync (Active):", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });
                        sp.Children.Add(new TextBlock { Text = "• Emulator Folder: E:\\STORM SWITCH\\Assembling (user/sdmc/)", FontSize = 12, Foreground = GetSecondaryBrush() });
                        sp.Children.Add(new TextBlock { Text = "• Clean Library: [SDMC] folders never pollute game directories", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
                        sp.Children.Add(new TextBlock { Text = "• Drag and Drop: Multiple emulator path support", FontSize = 12, Foreground = GetSecondaryBrush() });
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
                    Title = "Settings and Keys",
                    Category = "Configuration",
                    Icon = "\uE713",
                    DescriptionText = "Full suite of settings and encryption tools for precise image processing:\n\n" +
                                      "• Drag and Drop keys file (prod.keys / keys.txt) with visual highlights and version auto-detection.\n" +
                                      "• 6-cell firmware version input with smooth cursor navigation.\n" +
                                      "• Drag and Drop output folders and Smart Folder monitoring paths.\n" +
                                      "• Delta NCA stripping (Delta Cleaner): strips redundant delta fragments to save up to 30–50% storage.\n" +
                                      "• Mod asset optimization: lossless PNG texture compression via Oxipng and removal of junk files (.bak, .tmp, Thumbs.db, .DS_Store).\n" +
                                      "• Smart File Processing: automatic intelligent selection between native splicing and monolithic HardPatch.\n" +
                                      "• Ticketless NSP creation (--C_clean_ND) for universal CFW compatibility.\n" +
                                      "• Firmware requirement downgrade (RSV Cap Downgrade: FW 18.0, 17.0, 16.0, 15.0, 10.0).\n" +
                                      "• FAT32 split mode for cards requiring < 4 GB files (.xc0/.xc1 or .00/.01).\n" +
                                      "• Unused RomFS language trimming.\n" +
                                      "• Complete isolation between Nintendo Switch and Nintendo 3DS ecosystems.",
                    Tip = "Hover over any option in Settings to view comprehensive tooltips. Settings save automatically!",
                    SetupPreview = container =>
                    {
                        var sp = new StackPanel { Spacing = 8 };
                        sp.Children.Add(new TextBlock { Text = "🔑 Encryption Keys: prod.keys (Active)", Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                        sp.Children.Add(new CheckBox { Content = "🗑️ Strip Delta NCAs from Updates (Delta Cleaner)", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "🎨 Mod Asset Optimization (Oxipng and Junk Cleaner)", IsChecked = true });
                        sp.Children.Add(new CheckBox { Content = "🔓 Remove Titlerights (Ticketless NSP)", IsChecked = false });
                        sp.Children.Add(new CheckBox { Content = "💾 Split files for FAT32 (> 4 GB)", IsChecked = false });
                        container.Children.Add(sp);
                    }
                },
                new TopicItem
                {
                    Title = "Settings and Tasks: Compatibility Matrix",
                    Category = "Configuration",
                    Icon = "\uE802",
                    DescriptionText = "Comprehensive analysis of all application settings and their actual operational scope across task types:\n\n" +
                                      "1. Strip Delta NCAs from Updates (Delta Cleaner):\n" +
                                      "   • Active for: «Multi-Content», «Update», «NSZ/XCZ Compression» (Convert), «Switch Smart Folder».\n" +
                                      "   • Action: Parses binary CNMT to strip redundant DeltaFragment NCAs, cutting update package sizes by 30–50% without loss of data.\n\n" +
                                      "2. Mod Asset Optimization (Oxipng and Junk Cleaner):\n" +
                                      "   • Active for: «Multi-Content» and «Update» (when custom romfs/exefs folders are attached).\n" +
                                      "   • Action: Removes temp/system junk (.bak, .tmp, Thumbs.db, .DS_Store) and applies lossless PNG compression via Oxipng (20–40% texture size reduction).\n\n" +
                                      "3. Zstandard Compression Level (NSZ / XCZ):\n" +
                                      "   • Active for: Any task outputting NSZ or XCZ («Convert», «Multi-Content», «Update», «Pack», «Switch Smart Folder»).\n" +
                                      "   • Action: Sets Zstd compression level (3, 10, 18, 22) for 1 MB block streaming (NCZBLOCK). Ignored for NSP, XCI, and 3DS formats.\n\n" +
                                      "4. Complex Output File Name by Folders:\n" +
                                      "   • Active for: «Multi-Content», «Update», «Convert», «Pack», «Homebrew», «Smart Folder».\n" +
                                      "   • Action: Generates [Game Title]/[Format] folders and formats filenames as Title [TitleID] [vVersion].[ext].\n\n" +
                                      "5. Delete Source Files After Successful Processing:\n" +
                                      "   • Active for: ALL tasks («Convert», «Multi-Content», «Update», «Pack», «Homebrew», «3DS Tasks»).\n" +
                                      "   • Action: Deletes source files strictly upon 100% successful execution (status «Success» or «Done»). Never deletes files during «Verify».\n\n" +
                                      "6. Switch Cryptographic Keys (prod.keys / keys.txt):\n" +
                                      "   • Active for: «Multi-Content», «Update», «Unpack», «Convert», «Verify», «Homebrew».\n" +
                                      "   • Action: Required for NCA header decryption, PFS0 parsing, NCZ decompression, and hash verification.\n\n" +
                                      "7. Multi-Content Build Strategy (Smart Processing):\n" +
                                      "   • Active for: «Multi-Content» and «Update».\n" +
                                      "   • Action: Smart Processing automatically chooses native PFS0 splicing for lightweight patches (preventing RomFS inflation) or monolithic HardPatch for heavy updates and mods.\n\n" +
                                      "8. Emulator Integration (SDMC Folders):\n" +
                                      "   • Active for: «Homebrew» (engine ports and standalone homebrew games).\n" +
                                      "   • Action: Deploys SDMC data directly into emulator user/sdmc/switch/<game>/ directories, keeping your game library clean.\n\n" +
                                      "9. System Firmware Requirement Downgrade (RSV Cap):\n" +
                                      "   • Active for: «Multi-Content», «Update», «Pack».\n" +
                                      "   • Action: Caps RequiredSystemVersion in CNMT to allow running modern titles on older console firmware.\n\n" +
                                      "10. Auto-Inject 60 FPS Patches and Graphics Tweaks:\n" +
                                      "    • Active for: «Multi-Content» and «Update» (in HardPatch mode).\n" +
                                      "    • Action: Injects verified IPS 60 FPS and visual improvement patches directly into main ExeFS.\n\n" +
                                      "11. DLC Completeness Inspector:\n" +
                                      "    • Active for: «Multi-Content», «Update», and the «Information» catalog.\n" +
                                      "    • Action: Verifies attached DLCs against TitleDB and logs missing content in the task log.\n\n" +
                                      "12. Trim Unused RomFS Languages:\n" +
                                      "    • Active for: «Multi-Content» (HardPatch), «Update» (HardPatch), «Convert» to XCI/XCZ.\n" +
                                      "    • Action: Strips RomFS localization directories not listed in KeepLanguages.\n\n" +
                                      "13. Nintendo 3DS Settings (Keys, Default Format, 3DS Smart Folder):\n" +
                                      "    • Active for: Exclusively all Nintendo 3DS tasks («3DS Convert», «3DS Multi-Content», «3DS Unpack», «3DS Pack», «3DS Verify»).\n\n" +
                                      "14. Multithreading and Task Concurrency (UsedCores, ConcurrentTasks):\n" +
                                      "    • Active for: All parallel execution in the task queue.",
                    Tip = "Use the interactive matrix inspector below to test which settings apply to each task type!",
                    SetupPreview = container => BuildSettingsMatrixInteractivePreview(container)
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
                    "Emulator Integration and SDMC Sync" => "Emulator-Integration und SDMC-Synchronisation",
                    "Homebrew: Engine Ports & Standalone Games" => "Homebrew: Portierungen & Standalone-Spiele",
                    "Game Updates (HardPatch & Splicing)" => "Spiel-Updates (HardPatch & Zusammenführung)",
                    "Format Conversion (Switch & 3DS)" => "Formatkonvertierung (Switch & 3DS)",
                    "Extraction & Packaging" => "Entpacken & Packen",
                    "Settings & Keys" => "Einstellungen & Schlüssel",
                    "Settings and Keys" => "Einstellungen und Schlüssel",
                    "Settings and Tasks: Compatibility Matrix" => "Einstellungen und Aufgaben: Kompatibilitätsmatrix",
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
                    "Emulator Integration and SDMC Sync" => "模拟器集成与SDMC同步",
                    "Homebrew: Engine Ports & Standalone Games" => "自制程序：引擎移植与独立游戏",
                    "Game Updates (HardPatch & Splicing)" => "游戏更新 (HardPatch与无缝合并)",
                    "Format Conversion (Switch & 3DS)" => "格式转换 (Switch与3DS)",
                    "Extraction & Packaging" => "解包与打包",
                    "Settings & Keys" => "设置与密钥",
                    "Settings and Keys" => "设置与密钥",
                    "Settings and Tasks: Compatibility Matrix" => "参数与任务：兼容性矩阵",
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
                    "Emulator Integration and SDMC Sync" => "エミュレータ統合とSDMC同期",
                    "Homebrew: Engine Ports & Standalone Games" => "Homebrew: 移植作＆スタンドアロンゲーム",
                    "Game Updates (HardPatch & Splicing)" => "アップデート (HardPatch & 結合)",
                    "Format Conversion (Switch & 3DS)" => "フォーマット変換 (Switch & 3DS)",
                    "Extraction & Packaging" => "展開とパッケージング",
                    "Settings & Keys" => "設定と暗号化キー",
                    "Settings and Keys" => "設定と暗号化キー",
                    "Settings and Tasks: Compatibility Matrix" => "設定とタスク：互換性マトリックス",
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

        #region New Interactive Previews (5.0.14)

        private void BuildModsInteractivePreview(StackPanel container)
        {
            var sp = new StackPanel { Spacing = 10 };
            sp.Children.Add(new TextBlock { Text = "🎮 Интерактивный конфигуратор модов (RomFS и ExeFS):", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });

            var rbRomFs = new RadioButton { Content = "RomFS: Русификатор текста и звука (GamesVoice)", IsChecked = true };
            var rbExeFs = new RadioButton { Content = "ExeFS_Patches: Патч разблокировки 60 FPS (IPS)" };
            var rbBoth = new RadioButton { Content = "Комплексный релиз: RomFS [Озвучка] + ExeFS [60 FPS]" };

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var resultTxt = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            card.Child = resultTxt;

            void UpdateModStatus()
            {
                if (rbBoth.IsChecked == true)
                {
                    resultTxt.Text = "✓ DLC 1: [Включено] Русская озвучка GamesVoice (RomFS: 1)\n✓ ExeFS: main пропатчен 60 FPS IPS твиком\n✓ Статус в STORM SWITCH: Отображается в свойствах игры с возможностью отключения.";
                    resultTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                }
                else if (rbExeFs.IsChecked == true)
                {
                    resultTxt.Text = "✓ ExeFS: Бинарный патч 60 FPS применен к исполняемому модулю.\n✓ Статус в STORM SWITCH: Стабильные 60 кадров/сек без задержек.";
                    resultTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                }
                else
                {
                    resultTxt.Text = "✓ RomFS: Все игровые шрифты, текст и русская озвучка упакованы в виртуальный DLC тайтл.\n✓ Статус в STORM SWITCH: Полный русский язык без перезаписи оригинальной игры.";
                    resultTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                }
            }

            rbRomFs.Checked += (s, e) => UpdateModStatus();
            rbExeFs.Checked += (s, e) => UpdateModStatus();
            rbBoth.Checked += (s, e) => UpdateModStatus();
            UpdateModStatus();

            sp.Children.Add(rbRomFs);
            sp.Children.Add(rbExeFs);
            sp.Children.Add(rbBoth);
            sp.Children.Add(card);
            container.Children.Add(sp);
        }

        private void BuildMetadataInteractivePreview(StackPanel container)
        {
            var sp = new StackPanel { Spacing = 10 };
            sp.Children.Add(new TextBlock { Text = "✏️ Интерактивный редактор метаданных (NACP):", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });

            var txtName = new TextBox { Header = "Название игры (Русский / Английский):", Text = "The Legend of Zelda: Tears of the Kingdom [RUS]" };
            var txtPub = new TextBox { Header = "Издатель:", Text = "Nintendo / STORM SOFT" };

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };

            var previewSp = new StackPanel { Spacing = 4 };
            var lblTitle = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 13 };
            var lblPub = new TextBlock { FontSize = 12, Foreground = GetSecondaryBrush() };
            var lblBadge = new TextBlock { Text = "✓ Control NCA NACP: Иконка 256x256 JPEG внедрена, метаданные обновлены без полной пересборки", FontSize = 11, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) };

            previewSp.Children.Add(lblTitle);
            previewSp.Children.Add(lblPub);
            previewSp.Children.Add(lblBadge);
            card.Child = previewSp;

            void UpdateMeta()
            {
                lblTitle.Text = $"🎮 {txtName.Text}";
                lblPub.Text = $"🏢 Издатель: {txtPub.Text} | Версия: 1.2.1 [v393216]";
            }
            txtName.TextChanged += (s, e) => UpdateMeta();
            txtPub.TextChanged += (s, e) => UpdateMeta();
            UpdateMeta();

            sp.Children.Add(txtName);
            sp.Children.Add(txtPub);
            sp.Children.Add(card);
            container.Children.Add(sp);
        }

        private void BuildSmartFolderInteractivePreview(StackPanel container)
        {
            var sp = new StackPanel { Spacing = 8 };
            sp.Children.Add(new TextBlock { Text = "👁️ Интерактивный диспетчер Умных папок:", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });

            var cbSwitch = new CheckBox { Content = "Фоновый мониторинг Switch: D:\\Games\\Switch_Incoming (➔ NSZ Ultra-22)", IsChecked = true };
            var cb3ds = new CheckBox { Content = "Фоновый мониторинг 3DS: D:\\Games\\3DS_Incoming (➔ CIA / Trimming)", IsChecked = true };

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var statusTxt = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            card.Child = statusTxt;

            void UpdateFolderStatus()
            {
                bool sOn = cbSwitch.IsChecked == true;
                bool tOn = cb3ds.IsChecked == true;
                if (sOn && tOn)
                {
                    statusTxt.Text = "⚡ Активны обе независимые службы: Switch конвертируется в NSZ, 3DS обрабатывается в CIA. Конфликты форматов исключены.";
                    statusTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                }
                else if (sOn)
                {
                    statusTxt.Text = "✓ Активна служба Nintendo Switch: автоматическое сжатие и монолитная сборка входящих дампов.";
                    statusTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                }
                else if (tOn)
                {
                    statusTxt.Text = "✓ Активна служба Nintendo 3DS: конвертация образов и тримминг картриджей.";
                    statusTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson);
                }
                else
                {
                    statusTxt.Text = "⚠️ Все службы умных папок приостановлены.";
                    statusTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
                }
            }

            cbSwitch.Checked += (s, e) => UpdateFolderStatus();
            cbSwitch.Unchecked += (s, e) => UpdateFolderStatus();
            cb3ds.Checked += (s, e) => UpdateFolderStatus();
            cb3ds.Unchecked += (s, e) => UpdateFolderStatus();
            UpdateFolderStatus();

            sp.Children.Add(cbSwitch);
            sp.Children.Add(cb3ds);
            sp.Children.Add(card);
            container.Children.Add(sp);
        }

        private void BuildMultiContentInteractivePreview(StackPanel container)
        {
            var sp = new StackPanel { Spacing = 8 };
            sp.Children.Add(new TextBlock { Text = "📦 Интерактивный компоновщик Мульти-контента:", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });

            var cbGame = new CheckBox { Content = "Базовая игра (1G - Mortal Kombat 1) [30.0 ГБ]", IsChecked = true, IsEnabled = false };
            var cbUpd = new CheckBox { Content = "Обновление игры (1U - Update v1.18.0) [5.2 ГБ]", IsChecked = true };
            var cbDlc = new CheckBox { Content = "Набор DLC (Kombat Pack 1 and 2, Jean-Claude Van Damme) [6 тайтлов]", IsChecked = true };
            var cbTik = new CheckBox { Content = "Unlocker: Сохранение тикетов прав (.tik / .cert)", IsChecked = true };

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var resultTxt = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            card.Child = resultTxt;

            void UpdateMultiContentStatus()
            {
                int titles = 1;
                if (cbUpd.IsChecked == true) titles += 1;
                if (cbDlc.IsChecked == true) titles += 6;
                string tikStatus = cbTik.IsChecked == true ? "✓ Тикеты прав защищены (100% разблокировка персонажей)" : "⚠️ Без билетов прав";

                resultTxt.Text = $"⚡ Конфигурация: 1G + {(cbUpd.IsChecked == true ? "1U" : "0U")} + {(cbDlc.IsChecked == true ? "6D" : "0D")}\n" +
                                 $"📊 Всего тайтлов в монолитном NSP/XCI: {titles}\n" +
                                 $"🛡️ Статус Unlocker: {tikStatus}\n" +
                                 $"✓ Совместимость: LibHac PFS0 гарантирует строгий порядок CNMT и отсутствие конфликтов в STORM SWITCH.";
                resultTxt.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            }

            cbUpd.Checked += (s, e) => UpdateMultiContentStatus();
            cbUpd.Unchecked += (s, e) => UpdateMultiContentStatus();
            cbDlc.Checked += (s, e) => UpdateMultiContentStatus();
            cbDlc.Unchecked += (s, e) => UpdateMultiContentStatus();
            cbTik.Checked += (s, e) => UpdateMultiContentStatus();
            cbTik.Unchecked += (s, e) => UpdateMultiContentStatus();
            UpdateMultiContentStatus();

            sp.Children.Add(cbGame);
            sp.Children.Add(cbUpd);
            sp.Children.Add(cbDlc);
            sp.Children.Add(cbTik);
            sp.Children.Add(card);
            container.Children.Add(sp);
        }

        private void BuildEmulatorSyncInteractivePreview(StackPanel container)
        {
            var sp = new StackPanel { Spacing = 8 };
            sp.Children.Add(new TextBlock { Text = "🎮 Синхронизация с виртуальной картой эмулятора (SDMC 5.0.14):", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) });

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var treeSp = new StackPanel { Spacing = 4 };
            treeSp.Children.Add(new TextBlock { Text = "📁 Папка эмулятора: E:\\STORM SWITCH\\Assembling (user/sdmc/)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
            treeSp.Children.Add(new TextBlock { Text = "   └── switch/", FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = GetSecondaryBrush() });
            treeSp.Children.Add(new TextBlock { Text = "       └── devilutionx/  (Строго 1 целевая папка!)", FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            treeSp.Children.Add(new TextBlock { Text = "           ├── devilutionx.nro", FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = GetSecondaryBrush() });
            treeSp.Children.Add(new TextBlock { Text = "           └── diabdat.mpq (718.01 МБ, без дубликатов)", FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = GetSecondaryBrush() });
            treeSp.Children.Add(new TextBlock { Text = "✓ Чистота диска: Устаревшие папки devilutionx-switch автоматически удалены.", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen), Margin = new Thickness(0, 4, 0, 0) });
            treeSp.Children.Add(new TextBlock { Text = "✓ Выходной каталог: Папки [SDMC] больше не засоряют основную библиотеку игр.", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) });
            card.Child = treeSp;

            sp.Children.Add(card);
            container.Children.Add(sp);
        }

        private void BuildHomebrewInteractivePreview(StackPanel container)
        {
            var mainSp = new StackPanel { Spacing = 14 };

            var headerSp = new StackPanel { Spacing = 4 };
            headerSp.Children.Add(new TextBlock 
            { 
                Text = "🕹️ Интерактивный инспектор портов Homebrew (5.0.14)", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                FontSize = 15, 
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
            });
            headerSp.Children.Add(new TextBlock 
            { 
                Text = "Выберите порт для проверки путей SDMC, структуры данных и итогового размера:", 
                FontSize = 12, 
                Foreground = GetSecondaryBrush() 
            });
            mainSp.Children.Add(headerSp);

            var combo = new ComboBox 
            { 
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(6)
            };

            var gameData = new List<(string Name, string Folder, string Nro, string DataFiles, string Size, string Status)>
            {
                (
                    "Diablo I (DevilutionX + Hellfire + Rus)",
                    "switch/devilutionx",
                    "devilutionx.nro",
                    "diabdat.mpq, hellfire.mpq, devilutionx.mpq, diablo.ini",
                    "718.01 МБ (1 экземпляр, БЕЗ раздувания до 4.27 ГБ)",
                    "✓ 100% готов к запуску в STORM SWITCH (ошибка 0x75B устранена)"
                ),
                (
                    "Grand Theft Auto: San Andreas (re3-sa)",
                    "switch/re3-sa",
                    "re3-sa.nro",
                    "models/, data/, audio/, text/, gta-sa.set",
                    "2.10 ГБ (структура проверена)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "Half-Life 1 (Xash3D-FWGS)",
                    "switch/xash3d",
                    "xash3d.nro",
                    "valve/ (valve.wad, halflife.wad, pak0.pak)",
                    "450 МБ (нативные ресурсы)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "DOOM 1 and 2 (GZDoom Switch)",
                    "switch/gzdoom",
                    "gzdoom.nro",
                    "doom.wad, doom2.wad, gzdoom.ini",
                    "35 МБ (оптимизировано)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "DOOM 3 (dhewm3)",
                    "switch/dhewm3",
                    "dhewm3.nro",
                    "base/ (pak000.pk4 ... pak004.pk4)",
                    "1.60 ГБ (чистая распаковка)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "S.T.A.L.K.E.R.: Shadow of Chernobyl (OpenXRay)",
                    "switch/openxray",
                    "openxray.nro",
                    "gamedata/ (configs, scripts, textures, sounds)",
                    "3.80 ГБ (ресурсы сопоставлены)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "The Elder Scrolls III: Morrowind (OpenMW)",
                    "switch/openmw",
                    "openmw.nro",
                    "Data Files/ (Morrowind.esm, Morrowind.bsa)",
                    "1.10 ГБ (проверено)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "Another Metroid 2 Remake (AM2R)",
                    "switch/am2r",
                    "AM2R.nro",
                    "game.droid / data.win, music/, sound/",
                    "280 МБ (чистый порт)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "Super Mario 64 (SM64 Port)",
                    "switch/sm64",
                    "sm64.nro",
                    "sm64.us.z64 (встроенные текстуры 60 FPS)",
                    "25 МБ (автономный)",
                    "✓ Готов к запуску в STORM SWITCH"
                ),
                (
                    "The Legend of Zelda: Ocarina of Time (Ship of Harkinian)",
                    "switch/soh",
                    "soh.nro",
                    "oot.otr, soh.otr",
                    "85 МБ (сгенерированный OTR)",
                    "✓ Готов к запуску в STORM SWITCH"
                )
            };

            foreach (var g in gameData)
            {
                combo.Items.Add(new ComboBoxItem { Content = g.Name });
            }
            combo.SelectedIndex = 0;

            var resultCard = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14)
            };

            var detailSp = new StackPanel { Spacing = 8 };

            void UpdateHomebrewDetails(int idx)
            {
                if (idx < 0 || idx >= gameData.Count) return;
                var item = gameData[idx];
                detailSp.Children.Clear();

                detailSp.Children.Add(new TextBlock 
                { 
                    Text = $"📁 Целевая папка в SDMC: user/sdmc/{item.Folder}/", 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
                    FontSize = 13
                });

                detailSp.Children.Add(new TextBlock 
                { 
                    Text = $"⚡ Путь запуска форвардера: sdmc:/{item.Folder}/{item.Nro}", 
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12, 
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
                });

                detailSp.Children.Add(new TextBlock 
                { 
                    Text = $"📦 Требуемые ресурсы: {item.DataFiles}", 
                    FontSize = 12, 
                    Foreground = GetSecondaryBrush() 
                });

                detailSp.Children.Add(new TextBlock 
                { 
                    Text = $"⚖️ Итоговый вес игры: {item.Size}", 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 12, 
                    Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                });

                detailSp.Children.Add(new TextBlock 
                { 
                    Text = item.Status, 
                    FontSize = 12, 
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                });
            }

            combo.SelectionChanged += (s, e) => UpdateHomebrewDetails(combo.SelectedIndex);
            UpdateHomebrewDetails(0);

            resultCard.Child = detailSp;
            mainSp.Children.Add(combo);
            mainSp.Children.Add(resultCard);

            var checkBtn = new Button
            {
                Content = "Проверить корректность структуры папок и путей",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(6)
            };
            var checkStatus = new TextBlock
            {
                Text = "",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
                Visibility = Visibility.Collapsed
            };

            checkBtn.Click += (s, e) =>
            {
                checkStatus.Visibility = Visibility.Visible;
                checkStatus.Text = "✓ Структура идеальна: создается ровно 1 папка в SDMC, дубликаты устранены, RomFS сжат без раздувания.";
            };

            mainSp.Children.Add(checkBtn);
            mainSp.Children.Add(checkStatus);

            container.Children.Add(mainSp);
        }

        private void BuildSmartProcessingInteractivePreview(StackPanel container)
        {
            var mainSp = new StackPanel { Spacing = 12 };

            mainSp.Children.Add(new TextBlock 
            { 
                Text = "🧠 Интерактивный калькулятор Умной обработки (Smart Processing 5.0.14)", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                FontSize = 15, 
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
            });

            var baseSlider = new Slider 
            { 
                Header = "Размер базовой игры: 10.0 ГБ", 
                Minimum = 1, 
                Maximum = 30, 
                Value = 10,
                StepFrequency = 0.5
            };

            var patchSlider = new Slider 
            { 
                Header = "Размер обновления: 1.5 ГБ", 
                Minimum = 0.1, 
                Maximum = 15, 
                Value = 1.5,
                StepFrequency = 0.1
            };

            var modCheck = new CheckBox 
            { 
                Content = "Присутствуют папки модификаций (romfs / exefs / ips)" 
            };

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14)
            };

            var resultSp = new StackPanel { Spacing = 6 };
            card.Child = resultSp;

            void RecalculateSmartDecision()
            {
                double baseGb = baseSlider.Value;
                double patchGb = patchSlider.Value;
                baseSlider.Header = $"Размер базовой игры: {baseGb:F1} ГБ";
                patchSlider.Header = $"Размер обновления: {patchGb:F1} ГБ";

                double ratio = (patchGb / baseGb) * 100.0;
                bool hasMods = modCheck.IsChecked == true;
                bool isHardPatch = hasMods || ratio >= 40.0;

                resultSp.Children.Clear();

                resultSp.Children.Add(new TextBlock 
                { 
                    Text = $"Соотношение обновления к базе: {ratio:F1}% (порог: 40.0%)", 
                    FontSize = 12, 
                    Foreground = GetSecondaryBrush() 
                });

                if (isHardPatch)
                {
                    string reason = hasMods ? "Обнаружены папки модификаций (RomFS/ExeFS)" : $"Массивный патч ({ratio:F1}% >= 40%)";
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = $"Решение алгоритма: 🔨 [HardPatch] — {reason}", 
                        FontSize = 13, 
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange) 
                    });
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = "✓ Действие: Удаление устаревших ресурсов базы, физическая замена файлов и прямая инъекция мода.", 
                        FontSize = 12, 
                        Foreground = GetSecondaryBrush() 
                    });
                    double estSize = Math.Max(baseGb, patchGb * 1.1);
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = $"💾 Итоговый размер файла: ~{estSize:F1} ГБ (экономия до {Math.Min(baseGb, patchGb):F1} ГБ устаревших данных!)", 
                        FontSize = 12, 
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                    });
                }
                else
                {
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = $"Решение алгоритма: ⚡ [Нативное сшивание LibHac PFS0] — Легковесный патч ({ratio:F1}% < 40%)", 
                        FontSize = 13, 
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                    });
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = "✓ Действие: Потоковое объединение NCA блоков в единый PFS0 контейнер без физической распаковки RomFS.", 
                        FontSize = 12, 
                        Foreground = GetSecondaryBrush() 
                    });
                    double estSize = baseGb + patchGb;
                    resultSp.Children.Add(new TextBlock 
                    { 
                        Text = $"💾 Итоговый размер файла: {estSize:F1} ГБ (0% раздувания, скорость сборки ~15-30 секунд)", 
                        FontSize = 12, 
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
                    });
                }
            }

            baseSlider.ValueChanged += (s, e) => RecalculateSmartDecision();
            patchSlider.ValueChanged += (s, e) => RecalculateSmartDecision();
            modCheck.Checked += (s, e) => RecalculateSmartDecision();
            modCheck.Unchecked += (s, e) => RecalculateSmartDecision();

            RecalculateSmartDecision();

            mainSp.Children.Add(baseSlider);
            mainSp.Children.Add(patchSlider);
            mainSp.Children.Add(modCheck);
            mainSp.Children.Add(card);

            container.Children.Add(mainSp);
        }

        private void BuildFormatConverterInteractivePreview(StackPanel container)
        {
            var mainSp = new StackPanel { Spacing = 12 };

            mainSp.Children.Add(new TextBlock 
            { 
                Text = "🔄 Интерактивный конфигуратор конвертации форматов (5.0.14)", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                FontSize = 15, 
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
            });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var comboFrom = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(6) };
            var comboTo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(6) };

            string[] formats = new[] { "NSP", "NSZ", "XCI", "XCZ", "3DS (CCI)", "CIA" };
            foreach (var f in formats)
            {
                comboFrom.Items.Add(new ComboBoxItem { Content = f });
                comboTo.Items.Add(new ComboBoxItem { Content = f });
            }
            comboFrom.SelectedIndex = 2; // XCI
            comboTo.SelectedIndex = 1;   // NSZ

            var arrowTxt = new TextBlock 
            { 
                Text = "➔", 
                FontSize = 16, 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center 
            };

            Grid.SetColumn(comboFrom, 0);
            Grid.SetColumn(arrowTxt, 1);
            Grid.SetColumn(comboTo, 2);
            grid.Children.Add(comboFrom);
            grid.Children.Add(arrowTxt);
            grid.Children.Add(comboTo);
            mainSp.Children.Add(grid);

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14)
            };

            var detailSp = new StackPanel { Spacing = 6 };
            card.Child = detailSp;

            void UpdateConvertRoute()
            {
                detailSp.Children.Clear();
                string fromFmt = formats[Math.Clamp(comboFrom.SelectedIndex, 0, formats.Length - 1)];
                string toFmt = formats[Math.Clamp(comboTo.SelectedIndex, 0, formats.Length - 1)];

                if (fromFmt == toFmt)
                {
                    detailSp.Children.Add(new TextBlock { Text = "⚠️ Исходный и целевой форматы совпадают.", FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange) });
                    return;
                }

                bool is3dsFrom = fromFmt.Contains("3DS") || fromFmt == "CIA";
                bool is3dsTo = toFmt.Contains("3DS") || toFmt == "CIA";

                if (is3dsFrom != is3dsTo)
                {
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = "🚫 Несовместимые экосистемы: форматы Nintendo Switch и Nintendo 3DS изолированы друг от друга.", 
                        FontSize = 12, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
                    });
                    return;
                }

                if (!is3dsFrom)
                {
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = $"⚡ Экосистема: Nintendo Switch | Маршрут: {fromFmt} ➔ {toFmt}", 
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                        FontSize = 13, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
                    });
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = "• Сохранение метаданных: 100% Bit-exact (CNMT, NACP, Иконки и подписи сохраняются)", 
                        FontSize = 12, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                    });
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = toFmt.EndsWith("Z") ? "• Сжатие: Zstandard Ultra Block Compression (уровень до 22)" : "• Сжатие: Несжатый образ (максимальная скорость сборки)", 
                        FontSize = 12, 
                        Foreground = GetSecondaryBrush() 
                    });
                }
                else
                {
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = $"🕹️ Экосистема: Nintendo 3DS | Маршрут: {fromFmt} ➔ {toFmt}", 
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                        FontSize = 13, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson) 
                    });
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = "• Движок: ctrtool + makerom + 3dstool с авто-обрезкой пустых байтов (Trimming)", 
                        FontSize = 12, 
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                    });
                    detailSp.Children.Add(new TextBlock 
                    { 
                        Text = toFmt == "CIA" ? "• Цель: Установка на реальную 3DS (FBI) или Citra" : "• Цель: Прямой запуск в эмуляторах (Citra, Lime3DS, Azahar)", 
                        FontSize = 12, 
                        Foreground = GetSecondaryBrush() 
                    });
                }
            }

            comboFrom.SelectionChanged += (s, e) => UpdateConvertRoute();
            comboTo.SelectionChanged += (s, e) => UpdateConvertRoute();
            UpdateConvertRoute();

            mainSp.Children.Add(card);
            container.Children.Add(mainSp);
        }

        private void BuildSettingsMatrixInteractivePreview(StackPanel container)
        {
            var mainSp = new StackPanel { Spacing = 12 };

            mainSp.Children.Add(new TextBlock 
            { 
                Text = "⚙️ Интерактивная матрица совместимости параметров", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                FontSize = 15, 
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) 
            });

            mainSp.Children.Add(new TextBlock
            {
                Text = "Выберите параметр из списка, чтобы увидеть, для каких операций он реально активен в кодовой базе:",
                FontSize = 12,
                Foreground = GetSecondaryBrush(),
                TextWrapping = TextWrapping.Wrap
            });

            var paramCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = 0
            };

            var items = new (string Name, string Tab, bool Multi, bool Update, bool Convert, bool Unpack, bool Pack, bool Homebrew, bool Verify, bool ThreeDs, bool Watch, string Code, string Desc)[]
            {
                (
                    "Удаление Delta NCA из обновлений (Delta Cleaner)",
                    "Nintendo Switch",
                    true, true, true, false, false, false, false, false, true,
                    "MultiContentService.cs, NszCompressionService.cs",
                    "Находит и исключает избыточные DeltaFragment NCA из файлов обновлений через бинарный парсинг CNMT, снижая размер пакета до 30–50% без потери данных."
                ),
                (
                    "Оптимизация ассетов модов (Oxipng и очистка мусора)",
                    "Nintendo Switch",
                    true, true, false, false, false, false, false, false, false,
                    "HardPatchEngine.cs, ModOptimizerService.cs",
                    "Сжимает PNG-текстуры модов без потерь через Oxipng (экономия 20–40% веса) и очищает временные системные файлы (.bak, .tmp, Thumbs.db, .DS_Store) перед сборкой."
                ),
                (
                    "Уровень сжатия Zstandard (NSZ / XCZ)",
                    "Общие",
                    true, true, true, false, true, false, false, false, true,
                    "NszCompressionService.cs, StormNczCompressor.cs",
                    "Задаёт уровень сжатия Zstd (от 3 до 22) при формировании контейнеров NSZ и XCZ блоками по 1 МБ (NCZBLOCK). Игнорируется для несжатых NSP/XCI и 3DS."
                ),
                (
                    "Комплексное выходное имя файла по папкам",
                    "Общие",
                    true, true, true, false, true, true, false, true, true,
                    "TasksViewModel.cs, WatchFolderService.cs",
                    "Создаёт структурированные подпапки вида [Название Игры]/[Формат] и формирует стандартизированное имя: Имя [TitleID] [vВерсия].[расширение]."
                ),
                (
                    "Удалять исходные файлы после успешной обработки",
                    "Общие",
                    true, true, true, false, true, true, false, true, true,
                    "TasksViewModel.cs (ExecuteTaskSafelyAsync)",
                    "Физически удаляет исходные входные файлы только после 100% безошибочного завершения задачи (статус «Успешно» или «Готово»). Для проверки Verify исходники никогда не удаляются."
                ),
                (
                    "Криптографические ключи Switch (prod.keys / keys.txt)",
                    "Nintendo Switch",
                    true, true, true, true, true, true, true, false, true,
                    "SwitchFormatService.cs, MultiContentService.cs, HardPatchEngine.cs",
                    "Необходимы для расшифровки NCA заголовков, чтения PFS0, декомпрессии NCZ, сборки мультиконтента и верификации хешей всех образов Switch."
                ),
                (
                    "Стратегия сборки мульти-контента (Умный режим)",
                    "Nintendo Switch",
                    true, true, false, false, false, false, false, false, true,
                    "MultiContentService.cs, HardPatchEngine.cs",
                    "Автоматически выбирает нативное сшивание LibHac PFS0 для легковесных патчей (до 40% базы) без раздувания RomFS, либо HardPatch для тяжелых обновлений и модов с заменой устаревших ресурсов."
                ),
                (
                    "Интеграция с эмуляторами (Папки эмуляторов SDMC)",
                    "Общие",
                    false, false, false, false, false, true, false, false, false,
                    "HomebrewService.cs",
                    "Автоматически доставляет структуру SDMC (NRO, игровые файлы .mpq, .wad, config) прямо в виртуальные SD-карты указанных эмуляторов (user/sdmc/switch/<game>/), сохраняя выходную папку чистой."
                ),
                (
                    "Понижение системных требований (RSV Cap Downgrade)",
                    "Nintendo Switch",
                    true, true, false, false, true, false, false, false, true,
                    "MultiContentService.cs",
                    "Ограничивает проверку требуемой версии прошивки в CNMT метаданных (FW 18.0, 17.0, 16.0, 15.0, 10.0), предотвращая требования обновления консоли для запуска новых игр."
                ),
                (
                    "Авто-инъекция патчей 60 FPS и твиков графики",
                    "Nintendo Switch",
                    true, true, false, false, false, false, false, false, false,
                    "HardPatchEngine.cs",
                    "Внедряет проверенные IPS-патчи 60 кадров/сек и исправления графики в исполняемый файл main в процессе монолитной пересборки ExeFS."
                ),
                (
                    "Инспектор полноты дополнений (DLC Completeness)",
                    "Nintendo Switch",
                    true, true, false, false, false, false, false, false, false,
                    "MultiContentService.cs, TitleDbService.cs",
                    "Сверяет список прикреплённых DLC с официальной глобальной базой TitleDB и выводит в логе задачи статус полноты набора и список отсутствующих DLC."
                ),
                (
                    "Удаление неиспользуемых языков из RomFS (Trim Languages)",
                    "Nintendo Switch",
                    true, true, true, false, false, false, false, false, false,
                    "HardPatchEngine.cs, SwitchFormatService.cs",
                    "Находит каталоги локализаций (Message, Voice, Sound, Loc, Text) в RomFS и удаляет языки, не входящие в список разрешенных KeepLanguages при пересборке."
                ),
                (
                    "Сжатие через внешний движок nsz.exe",
                    "Nintendo Switch",
                    true, true, true, false, true, false, false, false, true,
                    "NszCompressionService.cs",
                    "Переключает сжатие Block NSZ (NCZBLOCK 1 МБ) со встроенного нативного движка ZstdSharp на официальный внешний nsz.exe."
                ),
                (
                    "Криптографические ключи Nintendo 3DS (aes_keys.txt)",
                    "Nintendo 3DS",
                    false, false, false, false, false, false, false, true, true,
                    "Nintendo3dsService.cs",
                    "Ключи AES для дешифровки NCCH, распаковки RomFS/ExeFS (ctrtool) и сборки CIA/CCI образов (makerom) для всех задач 3DS."
                ),
                (
                    "Выходной формат 3DS по умолчанию",
                    "Nintendo 3DS",
                    false, false, false, false, false, false, false, true, true,
                    "Nintendo3dsService.cs, TasksViewModel.cs",
                    "Задаёт расширение целевого файла по умолчанию (.3ds / .cia / .cxi) для задач конвертации, упаковки и мультиконтента 3DS."
                ),
                (
                    "«Умная» папка Switch и «Умная» папка 3DS",
                    "Nintendo Switch / 3DS",
                    true, true, true, true, true, false, true, true, true,
                    "WatchFolderService.cs",
                    "Полностью автономный конвейер: при помещении новых файлов в отслеживаемые каталоги автоматически создаёт задачи выбранного типа и формата."
                ),
                (
                    "Выходная папка по умолчанию (Switch и 3DS)",
                    "Nintendo Switch / 3DS",
                    true, true, true, true, true, true, false, true, true,
                    "TasksViewModel.cs, TasksPage.xaml.cs",
                    "Задаёт корневую выходную директорию для сохранения готовых файлов Switch или 3DS (если не указана — сохраняется в папку с исходным файлом)."
                ),
                (
                    "Каталог для архивов SDMC",
                    "Nintendo Switch",
                    false, false, false, false, false, true, false, false, false,
                    "HomebrewService.cs",
                    "Папка для упаковки внешних SDMC данных Homebrew в автономные zip-архивы для физических консолей без засорения папки с играми."
                ),
                (
                    "Многопоточность (Используемые ядра ЦП)",
                    "Общие",
                    true, true, true, true, true, true, false, false, true,
                    "StormNczCompressor.cs, ModOptimizerService.cs, SwitchFormatService.cs",
                    "Число потоков процессора, выделяемых для Zstandard сжатия, работы 7-Zip и параллельной оптимизации текстур Oxipng."
                ),
                (
                    "Максимум одновременных задач (1–5)",
                    "Общие",
                    true, true, true, true, true, true, true, true, true,
                    "TasksViewModel.cs (StartAllTasksAsync)",
                    "Определяет параллелизм выполнения фоновой очереди задач (от 1 до 5 задач одновременно)."
                )
            };

            foreach (var it in items)
            {
                paramCombo.Items.Add(new ComboBoxItem { Content = it.Name });
            }

            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14)
            };

            var detailSp = new StackPanel { Spacing = 10 };
            card.Child = detailSp;

            void UpdateMatrixView()
            {
                detailSp.Children.Clear();
                int idx = Math.Clamp(paramCombo.SelectedIndex, 0, items.Length - 1);
                var sel = items[idx];

                detailSp.Children.Add(new TextBlock
                {
                    Text = $"📌 Вкладка в Параметрах: «{sel.Tab}»",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                detailSp.Children.Add(new TextBlock
                {
                    Text = "Совместимость с типами задач:",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                });

                var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                Border MakeBadge(string label, bool active)
                {
                    return new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        Background = active 
                            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 76, 175, 80)) 
                            : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
                        BorderBrush = active 
                            ? new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                            : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(60, 128, 128, 128)),
                        BorderThickness = new Thickness(1),
                        Child = new TextBlock
                        {
                            Text = $"{(active ? "✓" : "✕")} {label}",
                            FontSize = 11,
                            FontWeight = active ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                            Foreground = active 
                                ? new SolidColorBrush(Microsoft.UI.Colors.LimeGreen) 
                                : new SolidColorBrush(Microsoft.UI.Colors.Gray)
                        }
                    };
                }

                row1.Children.Add(MakeBadge("Мульти-контент", sel.Multi));
                row1.Children.Add(MakeBadge("Обновление", sel.Update));
                row1.Children.Add(MakeBadge("Конвертация", sel.Convert));
                row1.Children.Add(MakeBadge("Распаковка", sel.Unpack));
                row1.Children.Add(MakeBadge("Упаковка", sel.Pack));

                row2.Children.Add(MakeBadge("Homebrew", sel.Homebrew));
                row2.Children.Add(MakeBadge("Проверка", sel.Verify));
                row2.Children.Add(MakeBadge("Nintendo 3DS", sel.ThreeDs));
                row2.Children.Add(MakeBadge("Умная папка", sel.Watch));

                detailSp.Children.Add(row1);
                detailSp.Children.Add(row2);

                detailSp.Children.Add(new Border { Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], Height = 1, Margin = new Thickness(0, 4, 0, 4) });

                detailSp.Children.Add(new TextBlock
                {
                    Text = $"⚡ Где работает в коде: {sel.Code}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                });

                detailSp.Children.Add(new TextBlock
                {
                    Text = $"📝 Принцип действия: {sel.Desc}",
                    FontSize = 12,
                    Foreground = GetSecondaryBrush(),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                });
            }

            paramCombo.SelectionChanged += (s, e) => UpdateMatrixView();
            UpdateMatrixView();

            mainSp.Children.Add(paramCombo);
            mainSp.Children.Add(card);
            container.Children.Add(mainSp);
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
