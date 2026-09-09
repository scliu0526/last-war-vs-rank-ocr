using System.Threading;
using System.Windows;
using RankLens.App;

namespace RankLens.Tests;

public class MainWindowTests
{
    [Fact]
    public void MainWindowShowsRankLensBranding()
    {
        string? title = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                var window = new MainWindow();
                title = window.Title;
                window.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal("RankLens", title);
    }
}
