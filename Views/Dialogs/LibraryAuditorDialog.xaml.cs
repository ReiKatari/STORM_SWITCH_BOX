using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using StormSwitchBox.ViewModels;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class LibraryAuditorDialog : ContentDialog
    {
        public ObservableCollection<LibraryAuditIssue> Issues { get; } = new();
        private readonly TasksViewModel? _tasksViewModel;
        private LibraryAuditReport? _lastReport;

        public LibraryAuditorDialog(TasksViewModel? tasksViewModel = null)
        {
            this.InitializeComponent();
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;
            _tasksViewModel = tasksViewModel;
            IssuesListView.ItemsSource = Issues;

            LibraryPathBox.Text = !string.IsNullOrWhiteSpace(App.Settings.Current.OutputFolder)
                ? App.Settings.Current.OutputFolder
                : "";
        }

        private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            string? folder = await SystemDialogService.OpenFolderDialogAsync("Выберите папку с коллекцией игр Switch / 3DS", LibraryPathBox.Text);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                LibraryPathBox.Text = folder;
            }
        }

        private async void ScanLibrary_Click(object sender, RoutedEventArgs e)
        {
            string folder = LibraryPathBox.Text.Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                StatusText.Text = "⚠ Укажите существующую директорию для сканирования.";
                return;
            }

            ScanButton.IsEnabled = false;
            Issues.Clear();

            var prog = new Progress<(int Scanned, int Total, string CurrentFile)>(p =>
            {
                ScanProgressBar.Value = p.Total > 0 ? (p.Scanned * 100.0 / p.Total) : 0;
                StatusText.Text = $"Сканирование ({p.Scanned}/{p.Total}): {p.CurrentFile}";
            });

            try
            {
                _lastReport = await LibraryAuditorService.ScanLibraryFolderAsync(folder, prog, CancellationToken.None);
                foreach (var issue in _lastReport.Issues)
                {
                    Issues.Add(issue);
                }

                CardTotalFiles.Text = _lastReport.TotalFilesScanned.ToString();
                CardTotalSize.Text = ProcessingTask.FormatSize(_lastReport.TotalLibraryBytes);
                CardPotentialSavings.Text = "~" + ProcessingTask.FormatSize(_lastReport.TotalEstimatedSavingsBytes);
                CardIssuesCount.Text = _lastReport.Issues.Count.ToString();
                ScanProgressBar.Value = 100;
                StatusText.Text = $"✅ Аудит завершён! Найдено рекомендаций по оптимизации: {_lastReport.Issues.Count}.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ Ошибка сканирования: " + ex.Message;
            }
            finally
            {
                ScanButton.IsEnabled = true;
            }
        }

        private async void SendToTasks_Click(object sender, RoutedEventArgs e)
        {
            if (_tasksViewModel == null) return;
            var selected = Issues.Where(i => i.IsSelected && (i.IssueType == "UnmergedSet" || i.IssueType == "UncompressedContainer")).ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "⚠ Отметьте галочкой несшитые или несжатые наборы для отправки в Задачник.";
                return;
            }

            int added = 0;
            foreach (var issue in selected)
            {
                await _tasksViewModel.AddDroppedFilesBatchAsync(issue.RelatedFiles);
                added++;
            }

            StatusText.Text = $"🚀 В Задачник добавлено наборов для оптимизации: {added}. Закройте окно и нажмите «Запустить всё».";
        }

        private async void QuickTrimAndDecrypt_Click(object sender, RoutedEventArgs e)
        {
            var selected = Issues.Where(i => i.IsSelected && (i.IssueType == "UntrimmedCartridge" || i.IssueType == "Encrypted3ds")).ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "⚠ Нет выбранных необрезанных картриджей (XCI/3DS) или зашифрованных 3DS образов.";
                return;
            }

            int processed = 0;
            long totalSaved = 0;
            foreach (var issue in selected)
            {
                string file = issue.PrimaryFile;
                if (!File.Exists(file)) continue;

                if (issue.IssueType == "UntrimmedCartridge")
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    long saved = 0;
                    if (ext == ".xci") saved = SmartShrinkService.TrimXciCardImage(file);
                    else if (ext == ".3ds" || ext == ".cci") saved = SmartShrinkService.Trim3dsCardImage(file);
                    if (saved > 0)
                    {
                        totalSaved += saved;
                        processed++;
                        Issues.Remove(issue);
                    }
                }
                else if (issue.IssueType == "Encrypted3ds")
                {
                    string ext = Path.GetExtension(file);
                    string decPath = Path.Combine(Path.GetDirectoryName(file) ?? "", Path.GetFileNameWithoutExtension(file) + " [Decrypted]" + ext);
                    bool ok = await Nintendo3dsCryptoService.Decrypt3dsFileAsync(file, decPath, null, null, CancellationToken.None);
                    if (ok)
                    {
                        processed++;
                        Issues.Remove(issue);
                    }
                }
            }

            StatusText.Text = $"✅ Выполнено быстрых оптимизаций (Trim / 3DS Decrypt): {processed} (освобождено: {ProcessingTask.FormatSize(totalSaved)}).";
        }

        private void DeleteSupersededPatches_Click(object sender, RoutedEventArgs e)
        {
            var selected = Issues.Where(i => i.IsSelected && i.IssueType == "SupersededUpdate").ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "⚠ Нет отмеченных устаревших дубликатов патчей.";
                return;
            }

            int deletedFiles = 0;
            long freedBytes = 0;
            foreach (var issue in selected)
            {
                foreach (var f in issue.RelatedFiles)
                {
                    try
                    {
                        if (File.Exists(f))
                        {
                            long sz = new FileInfo(f).Length;
                            File.Delete(f);
                            deletedFiles++;
                            freedBytes += sz;
                        }
                    }
                    catch { }
                }
                Issues.Remove(issue);
            }

            StatusText.Text = $"🗑 Удалено устаревших патчей: {deletedFiles} шт. Освобождено: {ProcessingTask.FormatSize(freedBytes)}.";
        }

        private async void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastReport == null) return;
            string? folder = await SystemDialogService.OpenFolderDialogAsync("Выберите папку для сохранения отчёта аудита");
            if (string.IsNullOrWhiteSpace(folder)) return;

            string reportPath = Path.Combine(folder, $"STORM_Library_Audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("======================================================================");
            sb.AppendLine("   STORM SWITCH BOX — ОТЧЁТ АУДИТОРА И ДЕДУПЛИКАТОРА КОЛЛЕКЦИИ ИГР");
            sb.AppendLine("======================================================================");
            sb.AppendLine($"Просканировано файлов: {_lastReport.TotalFilesScanned}");
            sb.AppendLine($"Общий объём коллекции: {ProcessingTask.FormatSize(_lastReport.TotalLibraryBytes)}");
            sb.AppendLine($"Потенциальная экономия: ~{ProcessingTask.FormatSize(_lastReport.TotalEstimatedSavingsBytes)}");
            sb.AppendLine($"Найдено рекомендаций: {_lastReport.Issues.Count}");
            sb.AppendLine("----------------------------------------------------------------------");
            foreach (var issue in _lastReport.Issues)
            {
                sb.AppendLine($"[{issue.Platform}] {issue.IssueLabel} | {issue.GameName} [{issue.TitleId}] | Экономия: {issue.SavingsFormatted}");
                sb.AppendLine($"  Рекомендация: {issue.Recommendation}");
                foreach (var rf in issue.RelatedFiles)
                    sb.AppendLine($"  • {rf}");
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(reportPath, sb.ToString(), Encoding.UTF8);
            StatusText.Text = $"📄 Отчёт аудита сохранён: {reportPath}";
        }
    }
}
