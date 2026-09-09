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
    }

}
