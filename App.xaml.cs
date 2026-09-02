using Microsoft.UI.Xaml;
using StormSwitchBox.Services;

namespace StormSwitchBox
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }
        public static IntPtr MainWindowHandle { get; set; }

        // Глобальные сервисы
        public static Microsoft.UI.Dispatching.DispatcherQueue? MainDispatcher { get; private set; }
        public static SettingsService Settings { get; } = new SettingsService();
        public static LogService Logger { get; } = new LogService();
        public static KeysService Keys { get; } = new KeysService();
        public static SwitchFormatService SwitchFormat { get; } = new SwitchFormatService(Keys);
        public static NszCompressionService NszCompression { get; } = new NszCompressionService(SwitchFormat);
        public static MultiContentService MultiContent { get; } = new MultiContentService(Keys);
        public static HardPatchEngine HardPatch { get; } = new HardPatchEngine(Keys);
        public static HomebrewService Homebrew { get; } = new HomebrewService(Keys);
        public static TitleDbService TitleDb { get; } = new TitleDbService();
        public static ControlEditorService ControlEditor { get; } = new ControlEditorService();
        public static TicketHarvesterService TicketHarvester { get; } = new TicketHarvesterService();
        public static WatchFolderService WatchFolderService { get; } = new WatchFolderService();
        public static Nintendo3dsService Nintendo3ds { get; } = new Nintendo3dsService();
        public static NintendoLibraryService NintendoLibrary { get; } = new NintendoLibraryService();
        public static LocalizationService Localization => LocalizationService.Instance;
        private static StormSwitchBox.ViewModels.TasksViewModel? _tasksVM;
        public static StormSwitchBox.ViewModels.TasksViewModel TasksVM => _tasksVM ??= new StormSwitchBox.ViewModels.TasksViewModel();

        public static event Action<StormSwitchBox.Models.ProcessingTask>? TaskCompleted;

        public static void RunOnUI(Action action)
        {
            if (MainDispatcher != null)
            {
                MainDispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        string loc = ex.TargetSite != null ? $" ({ex.TargetSite.DeclaringType?.Name}.{ex.TargetSite.Name})" : "";
                        Logger?.Log($"[UI Exception Intercepted] {ex.Message}{loc}", Models.LogLevel.Warning);
                    }
                });
            }
            else
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    string loc = ex.TargetSite != null ? $" ({ex.TargetSite.DeclaringType?.Name}.{ex.TargetSite.Name})" : "";
                    Logger?.Log($"[UI Exception Intercepted] {ex.Message}{loc}", Models.LogLevel.Warning);
                }
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
            UnblockApplicationFilesAsync();
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try
                {
                    JobObjectManager.KillAllToolProcesses();
                    TempCleanupService.PurgeActiveTempDirectories();
                    var ex = ev.ExceptionObject as Exception;
                    Logger?.Log($"[Критический сбой] {ex?.Message}\n{ex?.StackTrace}", Models.LogLevel.Error);
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppDomain: {ex}\n\n");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                ev.SetObserved();
                try
                {
                    Logger?.Log($"[Фоновое исключение Task] {ev.Exception?.Message}", Models.LogLevel.Warning);
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] TaskScheduler: {ev.Exception}\n\n");
                }
                catch { }
            };
        }

        private static void UnblockApplicationFilesAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    string baseDir = System.AppContext.BaseDirectory;
                    string toolsDir = System.IO.Path.Combine(baseDir, "tools");

                    // 1. Быстро разблокируем основные исполняемые файлы в корне
                    if (System.IO.Directory.Exists(baseDir))
                    {
                        foreach (var file in System.IO.Directory.EnumerateFiles(baseDir, "*.*", System.IO.SearchOption.TopDirectoryOnly))
                        {
                            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                                file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                file.EndsWith(".pri", StringComparison.OrdinalIgnoreCase))
                            {
                                DeleteZoneStream(file);
                            }
                        }
                    }

                    // 2. Разблокируем утилиты в tools/
                    if (System.IO.Directory.Exists(toolsDir))
                    {
                        foreach (var file in System.IO.Directory.EnumerateFiles(toolsDir, "*.*", System.IO.SearchOption.AllDirectories))
                        {
                            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                                file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                DeleteZoneStream(file);
                            }
                        }
                    }
                }
                catch { }
            });
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFile(string lpFileName);

        private static void DeleteZoneStream(string filePath)
        {
            try
            {
                string streamPath = filePath + ":Zone.Identifier";
                DeleteFile(streamPath);
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static void ActivateRunningProcessWindow()
        {
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
                foreach (var p in processes)
                {
                    if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            catch { }
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            JobObjectManager.InitializeJobForCurrentProcess();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                JobObjectManager.KillAllToolProcesses();
                TempCleanupService.PurgeActiveTempDirectories();
            };

            // Single Instance Check
            bool isFirstInstance = false;
            _singleInstanceMutex = new System.Threading.Mutex(true, @"Global\STORM_SWITCH_BOX_SingleInstanceMutex", out isFirstInstance);

            if (!isFirstInstance)
            {
                // Отправляем сигнал активному экземпляру для фокусировки окна и показа уведомления
                try
                {
                    using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
                    client.Connect(800);
                    using var writer = new System.IO.StreamWriter(client, System.Text.Encoding.UTF8);
                    writer.WriteLine("__ACTIVATE_SINGLE_INSTANCE__|" + string.Join("|", Environment.GetCommandLineArgs()));
                    writer.Flush();
                }
                catch { }

                // Активируем окно первого экземпляра через Win32 API
                ActivateRunningProcessWindow();

                Environment.Exit(0);
                return;
            }

            Services.TempCleanupService.PurgeStaleTempDirectories();

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
                            if (line.StartsWith("__ACTIVATE_SINGLE_INSTANCE__"))
                            {
                                string remaining = line.Substring("__ACTIVATE_SINGLE_INSTANCE__|".Length);
                                string[] incomingArgs = remaining.Split('|');
                                RunOnUI(() =>
                                {
                                    if (MainWindow is MainWindow mw)
                                    {
                                        mw.RestoreWindow();
                                        mw.ShowSingleInstanceAlert();
                                    }
                                    else
                                    {
                                        MainWindow?.Activate();
                                    }
                                    if (incomingArgs.Length > 1)
                                    {
                                        ProcessCommandLineArgs(incomingArgs);
                                    }
                                });
                            }
                            else
                            {
                                string[] incomingArgs = line.Split('|');
                                RunOnUI(() => ProcessCommandLineArgs(incomingArgs));
                            }
                        }
                    }
                    catch { }
                }
            });

            // Сохраняем UI диспетчер для возможности обновления интерфейса из фоновых потоков (Task.Run)
            MainDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // Загружаем настройки перед показом окна
            await Settings.LoadAsync();
            Localization.CurrentLanguage = Settings.Current.Language ?? "ru";
            Logger.Log("Приложение запущено. Настройки загружены.", Models.LogLevel.Info);

            if (Settings.Current.EnableWatchFolderSwitch)
            {
                WatchFolderService.StartSwitch();
            }
            if (Settings.Current.EnableWatchFolder3ds)
            {
                WatchFolderService.Start3ds();
            }

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
                    
                    await MultiContent.BuildMultiContentAsync(task, inputFiles, outPath, patchFirmware: true, CancellationToken.None);
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
                // Вызов вынесен в отдельный NoInlining-метод, чтобы JIT не пытался
                // разрешить тип AppNotificationBuilder при компиляции ЭТОГО метода.
                // Если DLL заблокирована WDAC/Smart App Control, исключение возникает
                // при JIT-компиляции внутреннего метода и ловится здесь.
                ShowToastNotificationImpl(title, message);
            }
            catch (Exception ex)
            {
                Logger.Log($"Не удалось показать уведомление: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ShowToastNotificationImpl(string title, string message)
        {
            var notification = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
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
            e.Handled = true;
            Logger?.Log($"[Перехвачено UI исключение] {e.Exception?.Message}\n{e.Exception?.StackTrace}", Models.LogLevel.Error);
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WinUI Unhandled: {e.Exception}\n\n");
            }
            catch { }
        }

        public static void UnblockFile(string filePath)
        {
            DeleteZoneStream(filePath);
        }

        public static void UnblockAllTools()
        {
            try
            {
                string appDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string toolsDir = System.IO.Path.Combine(appDir, "tools");
                if (!System.IO.Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(appDir, "..", "..", "tools");
                    if (System.IO.Directory.Exists(parentTools)) toolsDir = parentTools;
                }

                if (System.IO.Directory.Exists(toolsDir))
                {
                    var files = System.IO.Directory.GetFiles(toolsDir, "*.*", System.IO.SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        UnblockFile(f);
                    }
                }
            }
            catch { }
        }

        public static void EnsureUserKeysAvailable()
        {
            UnblockAllTools();

            string userKeys = Settings.Current.KeysPath;
            if (string.IsNullOrEmpty(userKeys) || !System.IO.File.Exists(userKeys))
            {
                throw new Exception("Отсутствуют криптографические ключи (prod.keys / keys.txt). Пожалуйста, выберите их в Параметрах.");
            }

            try
            {
                // Резервное создание директорий temp в LocalAppData для предотвращения ошибок прав
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string stormAppData = System.IO.Path.Combine(localAppData, "StormSwitchBox");
                try { System.IO.Directory.CreateDirectory(stormAppData); } catch { }
                try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(stormAppData, "temp")); } catch { }
                try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "temp")); } catch { }

                SwitchFormat.CleanKeysFile(userKeys);
                Keys.LoadKeys(userKeys);

                // 1. Первоочередная синхронизация в профиль пользователя Windows (.switch) - всегда доступно без прав администратора
                string userProfileSwitch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
                try
                {
                    if (!System.IO.Directory.Exists(userProfileSwitch)) System.IO.Directory.CreateDirectory(userProfileSwitch);
                    System.IO.File.Copy(userKeys, System.IO.Path.Combine(userProfileSwitch, "prod.keys"), true);
                    System.IO.File.Copy(userKeys, System.IO.Path.Combine(userProfileSwitch, "keys.txt"), true);
                }
                catch { }

                // 2. Синхронизация в изолированную директорию LocalAppData
                try
                {
                    string localKeysDir = System.IO.Path.Combine(stormAppData, "keys");
                    if (!System.IO.Directory.Exists(localKeysDir)) System.IO.Directory.CreateDirectory(localKeysDir);
                    System.IO.File.Copy(userKeys, System.IO.Path.Combine(localKeysDir, "prod.keys"), true);
                    System.IO.File.Copy(userKeys, System.IO.Path.Combine(localKeysDir, "keys.txt"), true);
                }
                catch { }

                // 3. Безопасная синхронизация в каталог tools (если программа запущена в портативном режиме с правами записи)
                string appDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string toolsDir = System.IO.Path.Combine(appDir, "tools");
                if (!System.IO.Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(appDir, "..", "..", "tools");
                    if (System.IO.Directory.Exists(parentTools)) toolsDir = parentTools;
                }

                if (System.IO.Directory.Exists(toolsDir))
                {
                    try
                    {
                        string targetToolsKeys = System.IO.Path.Combine(toolsDir, "keys.txt");
                        string targetToolsProdKeys = System.IO.Path.Combine(toolsDir, "prod.keys");
                        
                        string nscbDir = System.IO.Path.Combine(toolsDir, "nscb");
                        string nscbZtoolsDir = System.IO.Path.Combine(nscbDir, "ztools");
                        if (!System.IO.Directory.Exists(nscbZtoolsDir)) try { System.IO.Directory.CreateDirectory(nscbZtoolsDir); } catch { }

                        string squirrelKeys1 = System.IO.Path.Combine(nscbDir, "keys.txt");
                        string squirrelKeys2 = System.IO.Path.Combine(nscbZtoolsDir, "keys.txt");
                        string squirrelProdKeys1 = System.IO.Path.Combine(nscbDir, "prod.keys");
                        string squirrelProdKeys2 = System.IO.Path.Combine(nscbZtoolsDir, "prod.keys");

                        string nszDir = System.IO.Path.Combine(toolsDir, "nsz");
                        if (!System.IO.Directory.Exists(nszDir)) try { System.IO.Directory.CreateDirectory(nszDir); } catch { }
                        string nszKeys1 = System.IO.Path.Combine(nszDir, "keys.txt");
                        string nszKeys2 = System.IO.Path.Combine(nszDir, "prod.keys");

                        try { System.IO.File.Copy(userKeys, targetToolsKeys, true); } catch { }
                        try { System.IO.File.Copy(userKeys, targetToolsProdKeys, true); } catch { }
                        try { System.IO.File.Copy(userKeys, squirrelKeys1, true); } catch { }
                        try { System.IO.File.Copy(userKeys, squirrelKeys2, true); } catch { }
                        try { System.IO.File.Copy(userKeys, squirrelProdKeys1, true); } catch { }
                        try { System.IO.File.Copy(userKeys, squirrelProdKeys2, true); } catch { }
                        try { System.IO.File.Copy(userKeys, nszKeys1, true); } catch { }
                        try { System.IO.File.Copy(userKeys, nszKeys2, true); } catch { }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Предупреждение при рассылке ключей: {ex.Message}", Models.LogLevel.Warning);
            }
        }
    }
}
