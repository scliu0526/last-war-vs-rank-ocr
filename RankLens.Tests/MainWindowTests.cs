using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RankLens.App;
using RankLensApplication = RankLens.App.App;

namespace RankLens.Tests;

public class MainWindowTests
{
    [Fact]
    public void MainWindowShowsRankLensBranding()
    {
        string? title = null;
        string? content = null;
        double? zoomScale = null;
        double? zoomMinimum = null;
        double? zoomMaximum = null;
        double? imageWidth = null;
        double? highlightTop = null;
        double? highlightHeight = null;
        string? sourceLabel = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var window = new MainWindow();
                title = window.Title;
                if (window.Content is Grid root)
                {
                    content = string.Join(" ", root.Children.OfType<TextBlock>().Select(block => block.Text));
                }
                var zoomSlider = (Slider)window.FindName("SourceZoomSlider")!;
                var zoomHost = (FrameworkElement)window.FindName("SourceCanvasHost")!;
                zoomMinimum = zoomSlider.Minimum;
                zoomMaximum = zoomSlider.Maximum;
                zoomSlider.Value = 2;
                zoomScale = ((System.Windows.Media.ScaleTransform)zoomHost.LayoutTransform).ScaleX;

                var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
                var imagePath = Path.Combine(folder, "source.png");
                Directory.CreateDirectory(folder);
                var pixels = new byte[20 * 40 * 4];
                var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                    20, 40, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 20 * 4);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(imagePath)) encoder.Save(stream);
                var candidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday,
                    Rank = 1,
                    CommanderName = "Commander",
                    AllianceName = "Alliance",
                    Score = 1,
                    SourceImage = imagePath,
                    SourceTop = 10,
                    SourceBottom = 30
                };
                window.Candidates.Add(candidate);
                var grid = (DataGrid)window.FindName("CandidatesGrid")!;
                grid.SelectedItem = candidate;
                grid.UpdateLayout();
                var preview = (Image)window.FindName("SourceImagePreview")!;
                var highlight = (System.Windows.Shapes.Rectangle)window.FindName("SourceHighlight")!;
                imageWidth = preview.Width;
                highlightTop = Canvas.GetTop(highlight);
                highlightHeight = highlight.Height;
                sourceLabel = ((TextBlock)window.FindName("SourceImageLabel")!).Text;
                Directory.Delete(folder, true);
                window.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "WPF application startup did not complete within 5 seconds.");

        Assert.Null(failure);
        Assert.Equal("RankLens", title);
        Assert.Contains("排名截圖辨識器", content);
        Assert.Equal(0.5, zoomMinimum);
        Assert.Equal(3, zoomMaximum);
        Assert.Equal(2, zoomScale);
        Assert.Equal(20, imageWidth);
        Assert.Equal(10, highlightTop);
        Assert.Equal(20, highlightHeight);
        Assert.Equal("source.png", sourceLabel);
    }

}
