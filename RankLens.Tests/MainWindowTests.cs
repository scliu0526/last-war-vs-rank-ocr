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
                var application = new RankLensApplication
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                application.Startup += (_, _) =>
                {
                    application.Dispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            var window = application.MainWindow;
                            title = window?.Title;
                            if (window?.Content is Grid root && root.Children.Count >= 3)
                            {
                                var visibleText = root.Children
                                    .OfType<TextBlock>()
                                    .Select(block => block.Text);
                                var instructionText = ((root.Children[2] as Border)?.Child as StackPanel)?
                                    .Children
                                    .OfType<TextBlock>()
                                    .Select(block => block.Text)
                                    ?? Enumerable.Empty<string>();
                                content = string.Join(" ", visibleText.Concat(instructionText));
                            }
                            application.Shutdown();
                        }));
                };
                application.InitializeComponent();
                application.StartupUri = new Uri(
                    "/RankLens.App;component/MainWindow.xaml",
                    UriKind.Relative);
                application.Run();
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
