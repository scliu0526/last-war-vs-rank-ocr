using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public partial class MainWindow : Window
{
    public ObservableCollection<RankingCandidate> Candidates { get; } = [];
    private string[] selectedImages = [];
    private CancellationTokenSource? batchCancellation;
    private bool hasUnsavedReview;
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
            gpuAdapters = new GpuAdapterCatalog().Enumerate().Where(adapter => adapter.IsLikelyDirectMLCompatible).ToArray();
        }
        catch
        {
            gpuAdapters = [];
        }
        AdapterComboBox.ItemsSource = gpuAdapters.Select(adapter => adapter.Name).ToArray();
        AdapterComboBox.SelectedItem = settings.AdapterName ?? gpuAdapters.FirstOrDefault()?.Name;
        AdapterComboBox.IsEnabled = settings.ExecutionMode == RecognitionExecutionMode.DirectML && gpuAdapters.Count > 0;
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
            SetSelectedImages(dialog.FileNames, null);
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
        BatchProgress.Value = 0;
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

        if (RankingMondayPicker.SelectedDate is not DateTime selectedDate
            || !RankingWeek.TryCreate(DateOnly.FromDateTime(selectedDate), out var week))
        {
            MessageBox.Show("請選擇星期一作為排名週起始日。", "日期錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettingsFromControls();
        batchCancellation?.Dispose();
        batchCancellation = new CancellationTokenSource();
        CancelBatchButton.IsEnabled = true;
        BatchProgress.Value = 0;
        BatchStatus.Text = "正在辨識…";
        try
        {
            if (settings.ExecutionMode == RecognitionExecutionMode.DirectML)
            {
                throw new InvalidOperationException("DirectML 已選取，但完整 PP-OCRv5 GPU 推論管線尚未安裝；為避免靜默改用 CPU，請切換回 CPU 模式。\n模型安裝完成後再啟用 DirectML。");
            }
            var source = new FallbackRecognitionSource(new WindowsOcrRecognitionSource(), new SidecarTextRecognitionSource());
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
            hasUnsavedReview = Candidates.Count > 0;

            var failures = results.Count(item => item.Error is not null);
            BatchStatus.Text = failures == 0
                ? $"辨識完成，共 {Candidates.Count} 筆候選，{reconciliation.Conflicts.Count} 個衝突，{reconciliation.NameCollisions.Count} 個名稱碰撞"
                : $"辨識完成，共 {Candidates.Count} 筆候選，{failures} 張失敗，{reconciliation.Conflicts.Count} 個衝突";
            new LocalLog(settings).Write("Info", $"Recognition completed: {results.Count} images, {Candidates.Count} candidates, {failures} failures, {reconciliation.Conflicts.Count} conflicts, {reconciliation.NameCollisions.Count} collisions.");
        }
        catch (OperationCanceledException)
        {
            BatchStatus.Text = "批次辨識已取消";
        }
        catch (Exception exception)
        {
            BatchStatus.Text = "辨識失敗";
            new LocalLog(settings).Write("Error", $"Recognition failed: {exception.GetType().Name}.");
            MessageBox.Show(exception.Message, "辨識失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            CancelBatchButton.IsEnabled = false;
            batchCancellation?.Dispose();
            batchCancellation = null;
        }
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

    private void ExecutionModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AdapterComboBox is not null)
        {
            AdapterComboBox.IsEnabled = ExecutionModeComboBox.SelectedItem is RecognitionExecutionMode.DirectML && gpuAdapters.Count > 0;
        }
    }

    private void CandidateSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is not RankingCandidate candidate || !File.Exists(candidate.SourceImage))
        {
            SourceImageLabel.Text = string.Empty;
            SourceImagePreview.Source = null;
            return;
        }

        SourceImageLabel.Text = Path.GetFileName(candidate.SourceImage);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(candidate.SourceImage, UriKind.Absolute);
        image.EndInit();
        SourceImagePreview.Source = image;
    }

    private RankingCandidate? SelectedCandidate => CandidatesGrid.SelectedItem as RankingCandidate;

    private void CandidateCellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is RankingCandidate candidate && e.Column.Header?.ToString() == "分類")
        {
            candidate.ClassificationResolved = candidate.Category != RankingCategory.PendingClassification;
            candidate.IsSelected = candidate.IsValid;
        }
    }

    private void SelectCandidateClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate)
        {
            candidate.IsSelected = candidate.IsValid;
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
            CandidatesGrid.Items.Refresh();
            return;
        }
        CandidateReconciler.ResolveNameCollision([candidate], NameCollisionResolution.MoveRank, candidate.Rank);
        CandidatesGrid.Items.Refresh();
    }

    private void KeepBothClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate && candidate.IsValid)
        {
            CandidateReconciler.ResolveNameCollision([candidate], NameCollisionResolution.KeepBoth);
            CandidatesGrid.Items.Refresh();
        }
    }

    private void IgnoreCandidateClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCandidate is { } candidate)
        {
            CandidateReconciler.ResolveNameCollision([candidate], NameCollisionResolution.IgnoreNew);
            CandidatesGrid.Items.Refresh();
        }
    }

    private void WriteWorkbookClick(object sender, RoutedEventArgs e)
    {
        if (RankingMondayPicker.SelectedDate is not DateTime selectedDate
            || !RankingWeek.TryCreate(DateOnly.FromDateTime(selectedDate), out var week))
        {
            MessageBox.Show("請選擇星期一作為排名週起始日。", "日期錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Candidates.Count == 0)
        {
            MessageBox.Show("目前沒有可確認的候選排名資料。", "尚未辨識", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveSettingsFromControls();
        var folder = settings.OutputFolder;
        RankLensWorkflow.WriteSummary summary;
        try
        {
            summary = new RankLensWorkflow(new RankingWorkbookWriter())
                .WriteConfirmed(folder, new ReviewSession(week, Candidates));
        }
        catch (IOException exception)
        {
            new LocalLog(settings).Write("Warning", $"Workbook update unavailable: {exception.GetType().Name}.");
            MessageBox.Show("Excel 檔案可能正在使用中，未修改正式檔案。請關閉檔案後重新按下寫入。", "寫入失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        hasUnsavedReview = false;
        new LocalLog(settings).Write("Info", $"Workbook updated for {week.FileName}.");
        var openFolder = MessageBox.Show($"已更新 {summary.Updated} 筆，略過 {summary.Skipped} 筆。\n檔案：{summary.Path}\n是否開啟輸出資料夾？", "完成", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (openFolder == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.Combine(folder, week.FileName)}\"") { UseShellExecute = true });
        }
    }

    private void WindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!hasUnsavedReview || Candidates.Count == 0) return;
        var result = MessageBox.Show("目前有尚未寫入 Excel 的候選資料，確定要關閉嗎？", "尚未儲存", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.No) e.Cancel = true;
    }

    private void SaveSettingsFromControls()
    {
        if (double.TryParse(ConfidenceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
        {
            settings.ConfidenceThreshold = Math.Clamp(threshold, 0, 1);
        }
        settings.ExecutionMode = ExecutionModeComboBox.SelectedItem is RecognitionExecutionMode mode ? mode : RecognitionExecutionMode.Cpu;
        settings.AdapterName = AdapterComboBox.SelectedItem?.ToString();
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
    }
}

internal sealed class SidecarTextRecognitionSource : IRecognitionSource
{
    public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = imagePaths.Single();
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
