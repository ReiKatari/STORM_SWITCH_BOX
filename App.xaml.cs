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

        private static System.Threading.Mutex? _singleInstanceMutex;
        private static readonly string PipeName = "StormSwitchBox_SingleInstancePipe";

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Single Instance Check
            bool isFirstInstance = false;
            _singleInstanceMutex = new System.Threading.Mutex(true, "StormSwitchBox_SingleInstanceMutex", out isFirstInstance);

            if (!isFirstInstance)
            {
                // Send arguments to first instance
                try
                {
                    using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
                    client.Connect(2000);
                    using var writer = new System.IO.StreamWriter(client, System.Text.Encoding.UTF8);
                    writer.WriteLine(string.Join("|", Environment.GetCommandLineArgs()));
                    writer.Flush();
                }
                catch { }
                Environment.Exit(0);
                return;
            }

            // Start NamedPipe Server to listen for other instances
            _ = Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new System.IO.Pipes.NamedPipeServerStream(PipeName, System.IO.Pipes.PipeDirection.In);
                        server.WaitForConnection();
                        using var reader = new System.IO.StreamReader(server, System.Text.Encoding.UTF8);
                        string? line = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string[] incomingArgs = line.Split('|');
                            RunOnUI(() => ProcessCommandLineArgs(incomingArgs));
                        }
                    }
                    catch { }
                }
            });

            // Сохраняем UI диспетчер для возможности обновления интерфейса из фоновых потоков (Task.Run)
            MainDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // Загружаем настройки перед показом окна
            await Settings.LoadAsync();
            Logger.Log("Приложение запущено. Настройки загружены.", Models.LogLevel.Info);

            try
            {
                Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка регистрации AppNotificationManager: {ex.Message}", Models.LogLevel.Warning);
            }

            // Проверяем и загружаем ключи пользователя
            string keysPath = Settings.Current.KeysPath;
            if (!string.IsNullOrEmpty(keysPath) && System.IO.File.Exists(keysPath))
            {
                try
                {
                    EnsureUserKeysAvailable();
                }
                catch { }
            }
            else
            {
                Logger.Log("Файл криптографических ключей не найден. Пожалуйста, укажите его в параметрах.", Models.LogLevel.Warning);
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

            // Handle current process args
            if (!ProcessCommandLineArgs(cmdArgs))
            {
                MainWindow = new MainWindow();
                MainWindow.Activate();
            }
        }

        private static bool ProcessCommandLineArgs(string[] cmdArgs)
        {
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

            if (cliAction != null && cliPaths.Count > 0)
            {
                if (MainWindow == null)
                {
                    MainWindow = new MainWindow();
                }
                if (MainWindow is MainWindow mw1) mw1.RestoreWindow(); else MainWindow.Activate();
                
                // Initialize the page and add tasks visually instead of background execution
                InitializeTasksFromCommandLine(cliAction, cliPaths.ToArray(), cliFormat);
                return true;
            }
            
            if (MainWindow != null)
            {
                if (MainWindow is MainWindow mw2) mw2.RestoreWindow(); else MainWindow.Activate();
            }
            return false;
        }

        public static void ShowToastNotification(string title, string message)
        {
            try
            {
                var notification = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .BuildNotification();
                Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                Logger.Log($"Не удалось показать уведомление: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private static void InitializeTasksFromCommandLine(string action, string[] paths, string? format)
        {
            Logger.Log($"Запуск операции из командной строки: Action={action}, Format={format}", Models.LogLevel.Info);
            
            string tag = action switch
            {
                "update"  => "Update",
                "unpack"  => "Unpack",
                "pack"    => "Pack",
                "convert" => "Convert",
                "multi"   => "Multi",
                "verify"  => "Verify",
                _         => "Multi"
            };

            // Setup TasksViewModel
            var vm = TasksVM;
            
            // Wait for main window to be fully initialized before navigating
            RunOnUI(async () =>
            {
                vm.SetPageType(tag);
                
                if (!string.IsNullOrEmpty(format))
                {
                    int formatIndex = format.ToUpper() switch
                    {
                        "NSP" => 0,
                        "NSZ" => 1,
                        "XCI" => 2,
                        "XCZ" => 3,
                        _ => -1
                    };
                    if (formatIndex >= 0)
                    {
                        vm.SelectedFormatIndex = formatIndex;
                    }
                }
                
                // Add paths asynchronously so UI doesn't freeze
                await vm.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string>(paths));
                
                if (vm.Tasks.Count > 0 || vm.VerifyTasks.Count > 0)
                {
                    ShowToastNotification("STORM SWITCH BOX", "Задача выполняется. Программа запущена.");
                    await vm.StartAllTasksAsync();
                }
            });
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.Log($"CRASH: {e.Exception.Message}\n{e.Exception.StackTrace}", Models.LogLevel.Error);
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString());
        }

        public static void EnsureUserKeysAvailable()
        {
            string userKeys = Settings.Current.KeysPath;
            if (string.IsNullOrEmpty(userKeys) || !System.IO.File.Exists(userKeys))
            {
                throw new Exception("Отсутствуют криптографические ключи (prod.keys / keys.txt). Пожалуйста, выберите их в Параметрах.");
            }

            try
            {
                // Резервное создание директорий temp для предотвращения FileNotFoundError утилиты squirrel.exe
                try { System.IO.Directory.CreateDirectory(@"E:\STORM SWITCH BOX\temp"); } catch { }
                try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "temp")); } catch { }

                SwitchFormat.CleanKeysFile(userKeys);
                Keys.LoadKeys(userKeys);

                string appDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string toolsDir = System.IO.Path.Combine(appDir, "tools");
                if (!System.IO.Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(appDir, "..", "..", "tools");
                    if (System.IO.Directory.Exists(parentTools)) toolsDir = parentTools;
                }

                if (System.IO.Directory.Exists(toolsDir))
                {
                    string targetToolsKeys = System.IO.Path.Combine(toolsDir, "keys.txt");
                    string targetToolsProdKeys = System.IO.Path.Combine(toolsDir, "prod.keys");
                    
                    string nscbDir = System.IO.Path.Combine(toolsDir, "nscb");
                    string nscbZtoolsDir = System.IO.Path.Combine(nscbDir, "ztools");
                    if (!System.IO.Directory.Exists(nscbZtoolsDir)) System.IO.Directory.CreateDirectory(nscbZtoolsDir);

                    string squirrelKeys1 = System.IO.Path.Combine(nscbDir, "keys.txt");
                    string squirrelKeys2 = System.IO.Path.Combine(nscbZtoolsDir, "keys.txt");
                    string squirrelProdKeys1 = System.IO.Path.Combine(nscbDir, "prod.keys");
                    string squirrelProdKeys2 = System.IO.Path.Combine(nscbZtoolsDir, "prod.keys");

                    string nszDir = System.IO.Path.Combine(toolsDir, "nsz");
                    if (!System.IO.Directory.Exists(nszDir)) System.IO.Directory.CreateDirectory(nszDir);
                    string nszKeys1 = System.IO.Path.Combine(nszDir, "keys.txt");
                    string nszKeys2 = System.IO.Path.Combine(nszDir, "prod.keys");

                    System.IO.File.Copy(userKeys, targetToolsKeys, true);
                    System.IO.File.Copy(userKeys, targetToolsProdKeys, true);
                    System.IO.File.Copy(userKeys, squirrelKeys1, true);
                    System.IO.File.Copy(userKeys, squirrelKeys2, true);
                    System.IO.File.Copy(userKeys, squirrelProdKeys1, true);
                    System.IO.File.Copy(userKeys, squirrelProdKeys2, true);
                    System.IO.File.Copy(userKeys, nszKeys1, true);
                    System.IO.File.Copy(userKeys, nszKeys2, true);
                }

                // Синхронизация в профиль пользователя Windows (.switch)
                string userProfileSwitch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
                if (!System.IO.Directory.Exists(userProfileSwitch)) System.IO.Directory.CreateDirectory(userProfileSwitch);
                System.IO.File.Copy(userKeys, System.IO.Path.Combine(userProfileSwitch, "prod.keys"), true);
                System.IO.File.Copy(userKeys, System.IO.Path.Combine(userProfileSwitch, "keys.txt"), true);
            }
            catch (Exception ex)
            {
                Logger.Log($"Предупреждение при рассылке ключей: {ex.Message}", Models.LogLevel.Warning);
            }
        }
    }
}
