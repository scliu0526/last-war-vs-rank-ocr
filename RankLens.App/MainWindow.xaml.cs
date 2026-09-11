using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public partial class MainWindow : Window
{
    public ObservableCollection<RankingCandidate> Candidates { get; } = [];
    private string[] selectedImages = [];
    private CancellationTokenSource? batchCancellation;
    private bool hasUnsavedReview;
    private int lastRecognitionFailureCount;
    private readonly AppSettingsStore settingsStore = new();
    private readonly AppSettings settings;
    private readonly IReadOnlyList<GpuAdapterInfo> gpuAdapters;

    public MainWindow()
    {
        settings = settingsStore.Load();
        InitializeComponent();
        RankingMondayPicker.SelectedDate = RankingWeek.Current(DateOnly.FromDateTime(DateTime.Today)).Monday.ToDateTime(TimeOnly.MinValue);
        CandidatesGrid.ItemsSource = Candidates;
        CandidatesGrid.Columns.OfType<System.Windows.Controls.DataGridComboBoxColumn>().First().ItemsSource = Enum.GetValues<RankingCategory>();
        try
        {
            gpuAdapters = new GpuAdapterCatalog().Enumerate().ToArray();
        }
        catch
        {
            gpuAdapters = [];
        }
        AdapterComboBox.ItemsSource = gpuAdapters.Select(adapter => new GpuAdapterOption(
            adapter.Name,
            adapter.IsLikelyDirectMLCompatible,
            adapter.CompatibilityReason)).ToArray();
        AdapterComboBox.SelectedValue = settings.AdapterName ?? gpuAdapters.FirstOrDefault(adapter => adapter.IsLikelyDirectMLCompatible)?.Name;
        if (settings.ExecutionMode == RecognitionExecutionMode.DirectML
            && (gpuAdapters.All(adapter => !adapter.IsLikelyDirectMLCompatible)
                || !gpuAdapters.Any(adapter => adapter.IsLikelyDirectMLCompatible
                    && string.Equals(adapter.Name, settings.AdapterName, StringComparison.Ordinal))))
        {
            settings.ExecutionMode = RecognitionExecutionMode.Cpu;
            settings.AdapterName = null;
            settingsStore.Save(settings);
            BatchStatus.Text = "已保存的 GPU 無法使用，已回復 CPU 模式。";
        }
        AdapterComboBox.IsEnabled = settings.ExecutionMode == RecognitionExecutionMode.DirectML && gpuAdapters.Any(adapter => adapter.IsLikelyDirectMLCompatible);
        try
        {
            var languages = WindowsOcrRecognitionSource.RequiredLanguageAvailability();
            LanguageStatus.Text = $"OCR 語言：{string.Join("、", languages.Where(item => item.Value).Select(item => item.Key))}";
            LanguageStatus.ToolTip = string.Join(Environment.NewLine, languages.Select(item => $"{item.Key}: {(item.Value ? "可用" : "未安裝")}"));
        }
        catch
        {
            LanguageStatus.Text = "OCR 語言狀態無法讀取";
        }
        ConfidenceTextBox.Text = settings.ConfidenceThreshold.ToString("0.00", CultureInfo.InvariantCulture);
        ExecutionModeComboBox.ItemsSource = Enum.GetValues<RecognitionExecutionMode>();
        ExecutionModeComboBox.SelectedItem = settings.ExecutionMode;
        OutputFolderTextBox.Text = settings.OutputFolder;
        LogDaysTextBox.Text = settings.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        LogSizeTextBox.Text = (settings.LogRetentionBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        UpdateWeekRange();
    }

    private void SelectImagesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "排名截圖 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                var result = ImageInputDiscovery.Discover(dialog.FileNames, null);
                SetSelectedImages(result.Accepted, null);
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(exception.Message, "圖片數量超過上限", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void RankingMondayChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateWeekRange();

    private void UpdateWeekRange()
    {
        if (RankingMondayPicker?.SelectedDate is DateTime date && RankingWeek.TryCreate(DateOnly.FromDateTime(date), out var week))
        {
            WeekRangeText.Text = $"{week.Monday:yyyy-MM-dd} ～ {week.Monday.AddDays(5):yyyy-MM-dd}";
        }
        else if (WeekRangeText is not null)
        {
            WeekRangeText.Text = "請選擇星期一";
        }
    }

    private void SelectFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "選擇排名截圖資料夾" };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                var result = ImageInputDiscovery.Discover([], dialog.FolderName);
                SetSelectedImages(result.Accepted, dialog.FolderName);
                if (result.Rejected.Count > 0)
                {
                    BatchStatus.Text = $"已選取 {result.Accepted.Count} 張，略過 {result.Rejected.Count} 個不支援檔案";
                    ShowBatchFailures(result.Rejected);
                }
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(exception.Message, "圖片數量超過上限", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void SetSelectedImages(IEnumerable<string> paths, string? folder)
    {
        selectedImages = paths.ToArray();
        lastRecognitionFailureCount = 0;
        BatchProgress.Value = 0;
        BatchFailureDetails.Text = string.Empty;
        BatchFailureDetails.Visibility = Visibility.Collapsed;
        BatchStatus.Text = folder is null
            ? $"已選取 {selectedImages.Length} 張截圖"
            : $"已選取資料夾內 {selectedImages.Length} 張截圖";
        Title = $"RankLens — 已選取 {selectedImages.Length} 張截圖";
    }

    private void CancelBatchClick(object sender, RoutedEventArgs e)
    {
        batchCancellation?.Cancel();
        CancelBatchButton.IsEnabled = false;
        BatchStatus.Text = "正在取消批次處理…";
    }

    private async void RecognizeClick(object sender, RoutedEventArgs e)
    {
        if (selectedImages.Length == 0)
        {
            MessageBox.Show("請先選擇圖片或資料夾。", "尚未選取圖片", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ReviewUiPolicy.TryCreateWeek(RankingMondayPicker.SelectedDate, out var week))
        {
            MessageBox.Show("請選擇星期一作為排名週起始日。", "日期錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettingsFromControls();
        var recognitionStarted = Stopwatch.GetTimestamp();
        batchCancellation?.Dispose();
        batchCancellation = new CancellationTokenSource();
        CancelBatchButton.IsEnabled = true;
        BatchProgress.Value = 0;
        BatchStatus.Text = "正在辨識…";
        try
        {
            using var onnxRuntime = await Task.Run(() => TryCreateOcrRuntime(settings), batchCancellation.Token);
            if (settings.ExecutionMode == RecognitionExecutionMode.DirectML && onnxRuntime is null)
            {
                throw new InvalidOperationException("DirectML 需要已驗證的 PP-OCRv5 模型組；請先安裝模型或切換回 CPU 模式。");
            }
            IRecognitionSource source = onnxRuntime is null
                ? new FallbackRecognitionSource(new WindowsOcrRecognitionSource(), new SidecarTextRecognitionSource())
                : new OnnxOcrRecognitionSource(onnxRuntime, textConfidenceThreshold: settings.ConfidenceThreshold);
            var processor = new BatchRecognitionProcessor();
            var progress = new Progress<int>(value => BatchProgress.Value = value);
            var results = await processor.ProcessAsync(selectedImages, source, progress, batchCancellation.Token);
            var recognized = results.Where(item => item.Candidates is not null).SelectMany(item => item.Candidates!).ToList();
            var reconciliation = CandidateReconciler.Reconcile(recognized);
            Candidates.Clear();
            foreach (var result in reconciliation.Candidates)
            {
                Candidates.Add(result);
            }
            CandidateSelectionPolicy.Apply(Candidates, settings.ConfidenceThreshold);
            hasUnsavedReview = Candidates.Count > 0;

            var failures = results
                .Where(item => item.Error is not null)
                .Select(item => new ImageInputFailure(item.Path, item.Error!))
                .ToArray();
            lastRecognitionFailureCount = failures.Length;
            if (failures.Length > 0) ShowBatchFailures(failures);
            else
            {
                BatchFailureDetails.Text = string.Empty;
                BatchFailureDetails.Visibility = Visibility.Collapsed;
            }
            BatchStatus.Text = failures.Length == 0
                ? $"辨識完成，共 {Candidates.Count} 筆候選，{reconciliation.Conflicts.Count} 個衝突，{reconciliation.NameCollisions.Count} 個名稱碰撞"
                : $"辨識完成，共 {Candidates.Count} 筆候選，{failures.Length} 張失敗，{reconciliation.Conflicts.Count} 個衝突";
            new LocalLog(settings).WriteStage("Info", "Recognition", $"Images={results.Count} Candidates={Candidates.Count} Failures={failures.Length} Conflicts={reconciliation.Conflicts.Count} Collisions={reconciliation.NameCollisions.Count}.", Stopwatch.GetElapsedTime(recognitionStarted));
        }
        catch (OperationCanceledException)
        {
            BatchStatus.Text = "批次辨識已取消";
            new LocalLog(settings).WriteStage("Warning", "Recognition", "Cancelled.", Stopwatch.GetElapsedTime(recognitionStarted));
        }
        catch (Exception exception)
        {
            BatchStatus.Text = "辨識失敗";
            new LocalLog(settings).WriteStage("Error", "Recognition", $"Error={exception.GetType().Name}.", Stopwatch.GetElapsedTime(recognitionStarted));
            MessageBox.Show(exception.Message, "辨識失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            CancelBatchButton.IsEnabled = false;
            batchCancellation?.Dispose();
            batchCancellation = null;
        }
    }

    private static OcrRuntime? TryCreateOcrRuntime(AppSettings currentSettings)
    {
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "models");
        var manifestPath = Path.Combine(modelDirectory, "manifest.json");
        if (!File.Exists(manifestPath)) return null;
        if (File.ReadAllText(manifestPath).Contains("PENDING_", StringComparison.OrdinalIgnoreCase)) return null;
        var manifest = new OcrModelStore().LoadManifest(manifestPath);
        var adapterId = new GpuAdapterCatalog().Enumerate()
            .FirstOrDefault(adapter => string.Equals(adapter.Name, currentSettings.AdapterName, StringComparison.Ordinal))?.DeviceId ?? 0;
        return new OcrRuntimeFactory().Create(new OcrRuntimeConfiguration(currentSettings.ExecutionMode, adapterId, modelDirectory), manifest);
    }

    private void ShowBatchFailures(IEnumerable<ImageInputFailure> failures)
    {
        BatchFailureDetails.Text = ImageFailureText.Format(failures);
        BatchFailureDetails.Visibility = Visibility.Visible;
    }

    private void ChooseOutputFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "選擇 Excel 輸出資料夾" };
        if (dialog.ShowDialog(this) == true)
        {
            OutputFolderTextBox.Text = dialog.FolderName;
            SaveSettingsFromControls();
        }
    }

    private void ResetSettingsClick(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        settings.OutputFolder = defaults.OutputFolder;
        settings.ConfidenceThreshold = defaults.ConfidenceThreshold;
        settings.ExecutionMode = defaults.ExecutionMode;
        settings.AdapterName = defaults.AdapterName;
        settings.LogRetentionDays = defaults.LogRetentionDays;
        settings.LogRetentionBytes = defaults.LogRetentionBytes;
        OutputFolderTextBox.Text = settings.OutputFolder;
        ConfidenceTextBox.Text = settings.ConfidenceThreshold.ToString("0.00", CultureInfo.InvariantCulture);
        ExecutionModeComboBox.SelectedItem = settings.ExecutionMode;
        AdapterComboBox.SelectedValue = null;
        LogDaysTextBox.Text = settings.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        LogSizeTextBox.Text = (settings.LogRetentionBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        settingsStore.Save(settings);
        BatchStatus.Text = "設定已重設為預設值。";
    }

    private void ExecutionModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AdapterComboBox is not null)
        {
            AdapterComboBox.IsEnabled = ExecutionModeComboBox.SelectedItem is RecognitionExecutionMode.DirectML
                && gpuAdapters.Any(adapter => adapter.IsLikelyDirectMLCompatible);
        }
    }

    private void CandidateSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is not RankingCandidate candidate)
        {
            SourceImageLabel.Text = string.Empty;
            SourceGeometryLabel.Text = string.Empty;
            SourceImagePreview.Source = null;
            SourceList.ItemsSource = null;
            SourceHighlight.Width = 0;
            SourceHighlight.Height = 0;
            return;
        }

        if (candidate.Sources.Count == 0) candidate.AddSource(candidate);
        var availableSource = candidate.Sources.FirstOrDefault(source => File.Exists(source.ImagePath));
        if (availableSource is null)
        {
            SourceImageLabel.Text = string.Empty;
            SourceGeometryLabel.Text = "來源圖片不存在";
            SourceImagePreview.Source = null;
            SourceList.ItemsSource = candidate.Sources;
            SourceHighlight.Width = 0;
            SourceHighlight.Height = 0;
            return;
        }

        SourceList.ItemsSource = candidate.Sources;
        SourceList.SelectedItem = availableSource;
        ShowSource(availableSource);
    }

    private void SourceSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SourceList.SelectedItem is SourceObservation source) ShowSource(source);
    }

    private void SourceZoomChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (SourceCanvasHost is null || SourceZoomSlider is null) return;
        var scale = SourceZoomSlider.Value;
        SourceCanvasHost.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        SourceZoomLabel.Text = $"{scale:P0}";
    }

    private void ShowSource(SourceObservation source)
    {
        if (!File.Exists(source.ImagePath))
        {
            SourceImageLabel.Text = Path.GetFileName(source.ImagePath);
            SourceGeometryLabel.Text = "來源圖片不存在";
            SourceImagePreview.Source = null;
            SourceHighlight.Width = 0;
            SourceHighlight.Height = 0;
            return;
        }
        SourceImageLabel.Text = Path.GetFileName(source.ImagePath);
        SourceGeometryLabel.Text = source.Bottom > source.Top
            ? $"OCR 列範圍：Y={source.Top}～{source.Bottom}"
            : "OCR 列座標不可用";
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(source.ImagePath, UriKind.Absolute);
        image.EndInit();
        SourceImagePreview.Source = image;
        SourceImagePreview.Width = image.PixelWidth;
        SourceImagePreview.Height = image.PixelHeight;
        SourceOverlay.Width = image.PixelWidth;
        SourceOverlay.Height = image.PixelHeight;
        if (source.Bottom > source.Top)
        {
            Canvas.SetLeft(SourceHighlight, 0);
            Canvas.SetTop(SourceHighlight, source.Top);
            SourceHighlight.Width = image.PixelWidth;
            SourceHighlight.Height = source.Bottom - source.Top;
        }
        else
        {
            SourceHighlight.Width = 0;
            SourceHighlight.Height = 0;
        }
    }

    private RankingCandidate? SelectedCandidate => CandidatesGrid.SelectedItem as RankingCandidate;

    private void CandidateCellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is RankingCandidate candidate)
        {
            hasUnsavedReview = true;
            var header = e.Column.Header?.ToString();
            if (string.Equals(header, "分類", StringComparison.Ordinal))
            {
                var related = CandidateReconciler.ApplyCategoryToSource(Candidates, candidate.SourceImage, candidate.Category);
                CandidateSelectionPolicy.RevalidateWithoutResettingUserChoice(related, settings.ConfidenceThreshold);
            }
            else
            {
                candidate.ClassificationResolved = candidate.Category != RankingCategory.PendingClassification;
                CandidateSelectionPolicy.RevalidateWithoutResettingUserChoice([candidate], settings.ConfidenceThreshold);
            }
        }
    }

    private void CandidateSelectionCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: RankingCandidate candidate } checkBox)
        {
            candidate.IsSelected = checkBox.IsChecked == true;
            CandidateSelectionPolicy.RevalidateWithoutResettingUserChoice([candidate], settings.ConfidenceThreshold);
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
        }
    }

    private void NoAllianceCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: RankingCandidate candidate } checkBox)
        {
            candidate.NoAllianceConfirmed = checkBox.IsChecked == true;
            CandidateSelectionPolicy.RevalidateWithoutResettingUserChoice([candidate], settings.ConfidenceThreshold);
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
        }
    }

    private void SelectCandidateClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate)
        {
            CandidateSelectionPolicy.Apply([candidate], settings.ConfidenceThreshold);
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
        }
    }

    private void MoveRankClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is not { } candidate) return;
        if (candidate.Rank is < 1 or > 200)
        {
            MessageBox.Show("名次必須介於 1 到 200。", "名次錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            candidate.IsSelected = false;
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
            return;
        }
        var group = Candidates.Where(item => item.Category == candidate.Category
            && string.Equals(item.CommanderName, candidate.CommanderName, StringComparison.Ordinal)).ToArray();
        CandidateReconciler.ResolveNameCollision(group, NameCollisionResolution.MoveRank, candidate.Rank, candidate);
        hasUnsavedReview = true;
        CandidatesGrid.Items.Refresh();
    }

    private void KeepBothClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate)
        {
            var group = Candidates.Where(item => item.Category == candidate.Category && item.Rank == candidate.Rank).ToArray();
            if (candidate.RequiresConflictResolution)
            {
                CandidateReconciler.ResolveConflict(group, candidate, ConflictResolution.KeepSelected);
            }
            else
            {
                var collision = Candidates.Where(item => item.Category == candidate.Category
                    && string.Equals(item.CommanderName, candidate.CommanderName, StringComparison.Ordinal)).ToArray();
                CandidateReconciler.ResolveNameCollision(collision, NameCollisionResolution.KeepBoth, selected: candidate);
            }
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
        }
    }

    private void IgnoreCandidateClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate)
        {
            var group = Candidates.Where(item => item.Category == candidate.Category && item.Rank == candidate.Rank).ToArray();
            if (candidate.RequiresConflictResolution)
            {
                CandidateReconciler.ResolveConflict(group, candidate, ConflictResolution.IgnoreSelected);
            }
            else
            {
                var collision = Candidates.Where(item => item.Category == candidate.Category
                    && string.Equals(item.CommanderName, candidate.CommanderName, StringComparison.Ordinal)).ToArray();
                CandidateReconciler.ResolveNameCollision(collision, NameCollisionResolution.IgnoreNew, selected: candidate);
            }
            hasUnsavedReview = true;
            CandidatesGrid.Items.Refresh();
        }
    }

    private void WriteWorkbookClick(object sender, RoutedEventArgs e)
    {
        if (!ReviewUiPolicy.TryCreateWeek(RankingMondayPicker.SelectedDate, out var week))
        {
            MessageBox.Show("請選擇星期一作為排名週起始日。", "日期錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Candidates.Count == 0)
        {
            MessageBox.Show("目前沒有可確認的候選排名資料。", "尚未辨識", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Candidates.Any(candidate => candidate.IsSelected && candidate.IsValid))
        {
            MessageBox.Show("目前沒有勾選且有效的候選排名資料，未更新 Excel。", "尚未確認", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveSettingsFromControls();
        var workbookStarted = Stopwatch.GetTimestamp();
        var folder = settings.OutputFolder;
        RankLensWorkflow.WriteSummary summary;
        try
        {
            summary = new RankLensWorkflow(new RankingWorkbookWriter())
                .WriteConfirmed(folder, new ReviewSession(week, Candidates), lastRecognitionFailureCount);
        }
        catch (IOException exception)
        {
            new LocalLog(settings).WriteStage("Warning", "WorkbookWrite", $"Error={exception.GetType().Name}.", Stopwatch.GetElapsedTime(workbookStarted));
            MessageBox.Show("Excel 檔案可能正在使用中，未修改正式檔案。請關閉檔案後重新按下寫入。", "寫入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (InvalidDataException exception)
        {
            new LocalLog(settings).WriteStage("Warning", "WorkbookWrite", $"Error={exception.GetType().Name}.", Stopwatch.GetElapsedTime(workbookStarted));
            MessageBox.Show("現有 Excel 檔案格式不符合 RankLens 規格，未修改正式檔案。請先備份並移除或修正該檔案後再試。", "檔案格式錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        hasUnsavedReview = false;
        new LocalLog(settings).WriteStage("Info", "WorkbookWrite", $"Updated={summary.Updated} Skipped={summary.Skipped} Failed={summary.Failed}.", Stopwatch.GetElapsedTime(workbookStarted));
        var openFolder = MessageBox.Show($"已更新 {summary.Updated} 筆，略過 {summary.Skipped} 筆，失敗 {summary.Failed} 張。\n檔案：{summary.Path}\n是否開啟輸出資料夾？", "完成", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (openFolder == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.Combine(folder, week.FileName)}\"") { UseShellExecute = true });
        }
    }

    private void WindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ReviewUiPolicy.ShouldPromptOnClose(hasUnsavedReview, Candidates.Count)) return;
        var result = MessageBox.Show("目前有尚未寫入 Excel 的候選資料，確定要關閉嗎？", "尚未儲存", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.No) e.Cancel = true;
    }

    private void SaveSettingsFromControls()
    {
        var oldThreshold = settings.ConfidenceThreshold;
        var oldMode = settings.ExecutionMode;
        var oldAdapter = settings.AdapterName;
        var oldDays = settings.LogRetentionDays;
        var oldBytes = settings.LogRetentionBytes;
        var oldOutputFolder = settings.OutputFolder;
        if (double.TryParse(ConfidenceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
        {
            settings.ConfidenceThreshold = Math.Clamp(threshold, 0, 1);
        }
        settings.ExecutionMode = ExecutionModeComboBox.SelectedItem is RecognitionExecutionMode mode ? mode : RecognitionExecutionMode.Cpu;
        settings.AdapterName = AdapterComboBox.SelectedValue?.ToString();
        if (int.TryParse(LogDaysTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            settings.LogRetentionDays = Math.Clamp(days, 1, 3650);
        }
        if (long.TryParse(LogSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var megabytes))
        {
            settings.LogRetentionBytes = Math.Clamp(megabytes, 1, 4096) * 1024 * 1024;
        }
        if (!string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
        {
            settings.OutputFolder = OutputFolderTextBox.Text.Trim();
            Directory.CreateDirectory(settings.OutputFolder);
        }
        settingsStore.Save(settings);
        if (oldThreshold != settings.ConfidenceThreshold
            || oldMode != settings.ExecutionMode
            || !string.Equals(oldAdapter, settings.AdapterName, StringComparison.Ordinal)
            || oldDays != settings.LogRetentionDays
            || oldBytes != settings.LogRetentionBytes
            || !string.Equals(oldOutputFolder, settings.OutputFolder, StringComparison.Ordinal))
        {
            new LocalLog(settings).WriteStage("Info", "Settings", $"Threshold={settings.ConfidenceThreshold:0.00} Mode={settings.ExecutionMode} AdapterSelected={!string.IsNullOrWhiteSpace(settings.AdapterName)} LogDays={settings.LogRetentionDays} LogBytes={settings.LogRetentionBytes}.");
        }
    }
}

internal sealed class SidecarTextRecognitionSource : IRecognitionSource
{
    public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = imagePaths.Single();
        ScreenshotInputValidator.ValidatePortrait(image);
        var sidecar = Path.ChangeExtension(image, ".txt");
        if (!File.Exists(sidecar))
        {
            throw new InvalidOperationException("OCR 模型尚未安裝；可在圖片旁放置同名 .txt 文字檔進行離線預覽。正式 OCR 模型安裝後將由此介面取代文字側錄來源。");
        }

        var lines = File.ReadLines(sidecar).ToArray();
        var candidates = OcrCandidateParser.ParsePlainText(OcrCandidateParser.DetectCategory(lines), image, lines);
        foreach (var candidate in candidates)
        {
            candidate.ClassificationResolved = OcrCandidateParser.IsCategoryResolved(lines);
        }
        return Task.FromResult(candidates);
    }
}

internal sealed class FallbackRecognitionSource(params IRecognitionSource[] sources) : IRecognitionSource
{
    public async Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var source in sources)
        {
            try { return await source.RecognizeAsync(imagePaths, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { last = exception; }
        }

        throw new InvalidOperationException("沒有可用的離線 OCR 來源。", last);
    }
}
