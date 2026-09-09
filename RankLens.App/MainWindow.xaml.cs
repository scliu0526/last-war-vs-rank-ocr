using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace RankLens.App;

public partial class MainWindow : Window
{
    public ObservableCollection<RankingCandidate> Candidates { get; } = [];
    private string[] selectedImages = [];
    private CancellationTokenSource? batchCancellation;

    public MainWindow()
    {
        InitializeComponent();
        RankingMondayPicker.SelectedDate = RankingWeek.Current(DateOnly.FromDateTime(DateTime.Today)).Monday.ToDateTime(TimeOnly.MinValue);
        CandidatesGrid.ItemsSource = Candidates;
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

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        new RankLensWorkflow(new RankingWorkbookWriter())
            .WriteConfirmed(folder, new ReviewSession(week, Candidates));
        MessageBox.Show($"已寫入 {Path.Combine(folder, week.FileName)}。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
