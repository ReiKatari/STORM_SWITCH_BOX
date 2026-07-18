using Microsoft.UI.Xaml;
using StormSwitchBox.Services;

namespace StormSwitchBox
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        // Глобальные сервисы
        public static Microsoft.UI.Dispatching.DispatcherQueue? MainDispatcher { get; private set; }
        public static SettingsService Settings { get; } = new SettingsService();
        public static LogService Logger { get; } = new LogService();
        public static KeysService Keys { get; } = new KeysService();
        public static SwitchFormatService SwitchFormat { get; } = new SwitchFormatService(Keys);
        public static NszCompressionService NszCompression { get; } = new NszCompressionService(SwitchFormat);
        public static MultiContentService MultiContent { get; } = new MultiContentService(Keys);
        public static HardPatchEngine HardPatch { get; } = new HardPatchEngine(Keys);
        public static TitleDbService TitleDb { get; } = new TitleDbService();
        public static TicketHarvesterService TicketHarvester { get; } = new TicketHarvesterService();
        private static StormSwitchBox.ViewModels.TasksViewModel? _tasksVM;
        public static StormSwitchBox.ViewModels.TasksViewModel TasksVM => _tasksVM ??= new StormSwitchBox.ViewModels.TasksViewModel();

        public static event Action<StormSwitchBox.Models.ProcessingTask>? TaskCompleted;

        public static void RunOnUI(Action action)
        {
            if (MainDispatcher != null)
            {
                MainDispatcher.TryEnqueue(() => action());
            }
            else
            {
                action();
            }
        }

        public static void NotifyTaskCompleted(StormSwitchBox.Models.ProcessingTask task)
        {
            RunOnUI(() => TaskCompleted?.Invoke(task));
        }

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Сохраняем UI диспетчер для возможности обновления интерфейса из фоновых потоков (Task.Run)
            MainDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // Загружаем настройки перед показом окна
            await Settings.LoadAsync();
            Logger.Log("Приложение запущено. Настройки загружены.", Models.LogLevel.Info);

            // Пытаемся автоматически загрузить ключи при старте
            string keysPath = Settings.Current.KeysPath;

            if (string.IsNullOrEmpty(keysPath) || !System.IO.File.Exists(keysPath))
            {
                keysPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "prod.keys");
                if (!System.IO.File.Exists(keysPath))
                {
                    keysPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tools", "keys.txt");
                }
            }
            
            if (System.IO.File.Exists(keysPath))
            {
                Settings.Current.KeysPath = keysPath;
                Keys.LoadKeys(keysPath);
            }
            else
            {
                Logger.Log($"Файл ключей не найден. Укажите его в параметрах.", Models.LogLevel.Error);
            }

            // AUTOMATED TEST BYPASS
            string[] cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Length > 1 && cmdArgs[1] == "--run-test")
            {
                try
                {
                    string outDir = @"P:\CONSOLES\Nintendo Switch\GAMES";
                    string outFileName = "Devil Jam [WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)";
                    string outPath = System.IO.Path.Combine(outDir, outFileName + ".nsz");
                    
                    var task = new Models.ProcessingTask
                    {
                        Operation = "Multi",
                        TargetFormat = "NSZ",
                        OutputFolder = outDir,
                        OutputFileName = outFileName,
                    };
                    
                    var inputFiles = new List<string>
                    {
                        @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4000][v0] (0.45 GB).nsz",
                        @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4800][v65536] (0.19 GB).nsz"
                    };
                    
                    await MultiContent.BuildMultiContentAsync(task, inputFiles, outPath, patchFirmware: false, CancellationToken.None);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test_result.txt"), "SUCCESS\n" + task.LogDetails);
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test_result.txt"), "FAILED: " + ex.ToString());
                }
                Environment.Exit(0);
                return;
            }

            // Check for --action argument  
            string? cliAction = null;
            string? cliFormat = null;
            System.Collections.Generic.List<string> cliPaths = new();
            for (int i = 1; i < cmdArgs.Length; i++)
            {
                if (cmdArgs[i] == "--action" && i + 1 < cmdArgs.Length)
                {
                    cliAction = cmdArgs[++i];
                }
                else if (cmdArgs[i] == "--format" && i + 1 < cmdArgs.Length)
                {
                    cliFormat = cmdArgs[++i];
                }
                else if (!cmdArgs[i].StartsWith("--"))
                {
                    cliPaths.Add(cmdArgs[i]);
                }
            }

            MainWindow = new MainWindow();
            MainWindow.Activate();

            if (cliAction != null && cliPaths.Count > 0)
            {
                ((MainWindow)MainWindow).NavigateToAction(cliAction, cliPaths.ToArray(), cliFormat);
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.Log($"CRASH: {e.Exception.Message}\n{e.Exception.StackTrace}", Models.LogLevel.Error);
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString());
        }
    }
}
