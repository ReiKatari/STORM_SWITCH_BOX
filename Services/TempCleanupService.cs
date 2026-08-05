using System;
using System.IO;
using System.Threading.Tasks;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Сервис гигиенической очистки устаревших временных папок STORM_TMP_*.
    /// </summary>
    public static class TempCleanupService
    {
        public static void PurgeStaleTempDirectories(int maxAgeHours = 24)
        {
            Task.Run(() =>
            {
                try
                {
                    string tempBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                    if (!Directory.Exists(tempBase)) return;

                    var dirs = Directory.GetDirectories(tempBase, "STORM_TMP_*");
                    DateTime cutoff = DateTime.Now.AddHours(-maxAgeHours);

                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var info = new DirectoryInfo(dir);
                            if (info.CreationTime < cutoff || info.LastWriteTime < cutoff)
                            {
                                Directory.Delete(dir, true);
                                App.Logger.Log($"[TempCleanup] Cleaned stale temp folder: {dir}", Models.LogLevel.Info);
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[TempCleanup] Error purging temp directories: {ex.Message}", Models.LogLevel.Warning);
                }
            });
        }
    }
}
