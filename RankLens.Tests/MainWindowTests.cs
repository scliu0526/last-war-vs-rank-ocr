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
                var application = new RankLensApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                typeof(RankLensApplication).GetMethod("OnStartup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(application, [null]);
                var window = (MainWindow)application.MainWindow!;
                title = window.Title;
                if (window.Content is Grid root)
                {
                    content = string.Join(" ", root.Children.OfType<TextBlock>().Select(block => block.Text));
                }
                var zoomSlider = (Slider)window.FindName("SourceZoomSlider")!;
                var zoomHost = (FrameworkElement)window.FindName("SourceCanvasHost")!;
                var mondayPicker = (DatePicker)window.FindName("RankingMondayPicker")!;
                var weekRange = (TextBlock)window.FindName("WeekRangeText")!;
                mondayPicker.SelectedDate = new DateTime(2026, 9, 8);
                if (weekRange.Text != "請選擇星期一")
                {
                    throw new InvalidOperationException("Non-Monday date was accepted by the WPF date picker.");
                }
                mondayPicker.SelectedDate = new DateTime(2026, 9, 7);
                if (weekRange.Text != "2026-09-07 ～ 2026-09-12")
                {
                    throw new InvalidOperationException("Monday date did not update the ranking week range.");
                }
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
                var secondImagePath = Path.Combine(folder, "second.png");
                var secondEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                secondEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(secondImagePath)) secondEncoder.Save(stream);
                var candidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday,
                    Rank = 1,
                    CommanderName = "Commander",
                    AllianceName = "Alliance",
                    Score = 1,
                    SourceImage = imagePath,
                    SourceTop = 10,
                    SourceBottom = 30,
                    CommanderConfidence = 0,
                    IsSelected = true
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

                grid.ScrollIntoView(candidate);
                grid.UpdateLayout();
                var candidateRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(candidate)!;
                var dirtyField = typeof(MainWindow).GetField("hasUnsavedReview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                dirtyField.SetValue(window, false);
                var editArgs = new DataGridCellEditEndingEventArgs(
                    grid.Columns[3], candidateRow, new TextBox(), DataGridEditAction.Commit);
                typeof(MainWindow).GetMethod("CandidateCellEditEnding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [grid, editArgs]);
                if (!candidate.IsSelected)
                {
                    throw new InvalidOperationException("Editing another cell reset the user's manual confirmation.");
                }
                if (dirtyField.GetValue(window) is not true)
                {
                    throw new InvalidOperationException("Editing after a saved state did not restore the unsaved-review flag.");
                }

                candidate.IsSelected = false;
                dirtyField.SetValue(window, false);
                var selectionCheckBox = new CheckBox { DataContext = candidate, IsChecked = true };
                typeof(MainWindow).GetMethod("CandidateSelectionCheckBoxClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [selectionCheckBox, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)]);
                typeof(MainWindow).GetMethod("CandidateCellEditEnding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [grid, editArgs]);
                if (!candidate.IsSelected)
                {
                    throw new InvalidOperationException("Checking a previously unchecked candidate was lost after leaving the cell.");
                }
                if (dirtyField.GetValue(window) is not true)
                {
                    throw new InvalidOperationException("Checking after a saved state did not restore the unsaved-review flag.");
                }

                grid.ScrollIntoView(candidate);
                grid.UpdateLayout();
                candidateRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(candidate)!;

                var sameSourceCandidate = new RankingCandidate
                {
                    Category = RankingCategory.PendingClassification, Rank = 8, CommanderName = "Same source",
                    AllianceName = "Alliance", Score = 8, SourceImage = imagePath,
                    ClassificationResolved = false
                };
                window.Candidates.Add(sameSourceCandidate);
                candidate.Category = RankingCategory.Tuesday;
                var categoryEditArgs = new DataGridCellEditEndingEventArgs(
                    grid.Columns[1], candidateRow, new ComboBox(), DataGridEditAction.Commit);
                typeof(MainWindow).GetMethod("CandidateCellEditEnding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [grid, categoryEditArgs]);
                if (candidate.Category != RankingCategory.Tuesday
                    || sameSourceCandidate.Category != RankingCategory.Tuesday
                    || !sameSourceCandidate.ClassificationResolved)
                {
                    throw new InvalidOperationException("Category edit did not update every candidate from the same source image.");
                }

                var noAllianceCandidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 9, CommanderName = "No alliance",
                    AllianceName = string.Empty, NoAllianceConfirmed = true, Score = 9,
                    SourceImage = imagePath, IsSelected = true
                };
                window.Candidates.Add(noAllianceCandidate);
                grid.SelectedItem = noAllianceCandidate;
                grid.UpdateLayout();
                var noAllianceRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(noAllianceCandidate)!;
                var noAllianceCheckBox = new CheckBox { DataContext = noAllianceCandidate, IsChecked = true };
                typeof(MainWindow).GetMethod("NoAllianceCheckBoxClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [noAllianceCheckBox, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)]);
                if (!noAllianceCandidate.IsValid || !noAllianceCandidate.IsSelected)
                {
                    throw new InvalidOperationException("Explicit no-alliance confirmation was not retained as valid and selected.");
                }

                var switchCandidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 3, CommanderName = "Switch",
                    AllianceName = "Alliance", Score = 7, SourceImage = imagePath
                };
                switchCandidate.Sources.Add(new SourceObservation(imagePath, 5, 15));
                switchCandidate.Sources.Add(new SourceObservation(secondImagePath, 25, 35));
                window.Candidates.Add(switchCandidate);
                grid.SelectedItem = switchCandidate;
                grid.UpdateLayout();
                var sourceList = (ListBox)window.FindName("SourceList")!;
                sourceList.SelectedIndex = 1;
                sourceLabel = ((TextBlock)window.FindName("SourceImageLabel")!).Text;
                var switchedGeometry = ((TextBlock)window.FindName("SourceGeometryLabel")!).Text;
                if (sourceLabel != "second.png" || !switchedGeometry.Contains("Y=25～35", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Preview did not switch to the selected retained source.");
                }

                var missingCandidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 5, CommanderName = "Missing",
                    AllianceName = "Alliance", Score = 8, SourceImage = Path.Combine(folder, "gone.png")
                };
                window.Candidates.Add(missingCandidate);
                grid.SelectedItem = missingCandidate;
                grid.UpdateLayout();
                var missingStatus = ((TextBlock)window.FindName("SourceGeometryLabel")!).Text;
                if (missingStatus != "來源圖片不存在")
                {
                    throw new InvalidOperationException("Preview did not clear when all sources were missing.");
                }

                var fallbackCandidate = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 4, CommanderName = "Fallback",
                    AllianceName = "Alliance", Score = 6, SourceImage = Path.Combine(folder, "moved.png")
                };
                fallbackCandidate.Sources.Add(new SourceObservation(imagePath, 5, 15));
                window.Candidates.Add(fallbackCandidate);
                grid.SelectedItem = fallbackCandidate;
                grid.UpdateLayout();
                sourceLabel = ((TextBlock)window.FindName("SourceImageLabel")!).Text;
                if (sourceLabel != "source.png")
                {
                    throw new InvalidOperationException("Preview did not select an available retained source.");
                }

                var conflictFirst = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 2, CommanderName = "First",
                    AllianceName = "Alliance", Score = 2, SourceImage = imagePath,
                    RequiresConflictResolution = true
                };
                var conflictSecond = new RankingCandidate
                {
                    Category = RankingCategory.Monday, Rank = 2, CommanderName = "Second",
                    AllianceName = "Alliance", Score = 3, SourceImage = imagePath,
                    RequiresConflictResolution = true
                };
                window.Candidates.Add(conflictFirst);
                window.Candidates.Add(conflictSecond);
                grid.SelectedItem = conflictSecond;
                grid.UpdateLayout();
                typeof(MainWindow).GetMethod("KeepBothClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [null, new RoutedEventArgs(Button.ClickEvent)]);
                if (conflictFirst.RequiresConflictResolution || conflictFirst.IsSelected
                    || conflictSecond.RequiresConflictResolution || !conflictSecond.IsSelected)
                {
                    throw new InvalidOperationException("Conflict button did not keep the selected candidate.");
                }

                var ignoredFirst = new RankingCandidate
                {
                    Category = RankingCategory.Tuesday, Rank = 3, CommanderName = "First",
                    AllianceName = "Alliance", Score = 4, SourceImage = imagePath,
                    RequiresConflictResolution = true
                };
                var ignoredSecond = new RankingCandidate
                {
                    Category = RankingCategory.Tuesday, Rank = 3, CommanderName = "Second",
                    AllianceName = "Alliance", Score = 5, SourceImage = imagePath,
                    RequiresConflictResolution = true
                };
                window.Candidates.Add(ignoredFirst);
                window.Candidates.Add(ignoredSecond);
                grid.SelectedItem = ignoredSecond;
                grid.UpdateLayout();
                typeof(MainWindow).GetMethod("IgnoreCandidateClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [null, new RoutedEventArgs(Button.ClickEvent)]);
                if (ignoredFirst.RequiresConflictResolution || !ignoredFirst.IsSelected
                    || ignoredSecond.RequiresConflictResolution || ignoredSecond.IsSelected)
                {
                    throw new InvalidOperationException("Conflict button did not ignore the selected candidate.");
                }

                var collisionFirst = new RankingCandidate
                {
                    Category = RankingCategory.Wednesday, Rank = 4, CommanderName = "Collision",
                    AllianceName = "Alliance", Score = 4, SourceImage = imagePath,
                    RequiresNameCollisionResolution = true
                };
                var collisionMoved = new RankingCandidate
                {
                    Category = RankingCategory.Wednesday, Rank = 10, CommanderName = "Collision",
                    AllianceName = "Alliance", Score = 10, SourceImage = imagePath,
                    RequiresNameCollisionResolution = true
                };
                window.Candidates.Add(collisionFirst);
                window.Candidates.Add(collisionMoved);
                grid.SelectedItem = collisionMoved;
                grid.UpdateLayout();
                typeof(MainWindow).GetMethod("MoveRankClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [null, new RoutedEventArgs(Button.ClickEvent)]);
                if (collisionFirst.RequiresNameCollisionResolution || collisionFirst.IsSelected
                    || collisionMoved.RequiresNameCollisionResolution || !collisionMoved.IsSelected
                    || collisionMoved.Rank != 10)
                {
                    throw new InvalidOperationException("Name-collision move button did not keep the explicitly moved candidate.");
                }
                Directory.Delete(folder, true);
                dirtyField.SetValue(window, false);
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
