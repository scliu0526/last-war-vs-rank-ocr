using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace RankLens.App;

public partial class MainWindow : Window
{
    public ObservableCollection<RankingCandidate> Candidates { get; } = [];
    private string[] selectedImages = [];

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
            selectedImages = dialog.FileNames;
            Title = $"RankLens — 已選取 {selectedImages.Length} 張截圖";
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

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        new RankLensWorkflow(new RankingWorkbookWriter())
            .WriteConfirmed(folder, new ReviewSession(week, Candidates));
        MessageBox.Show($"已寫入 {Path.Combine(folder, week.FileName)}。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
