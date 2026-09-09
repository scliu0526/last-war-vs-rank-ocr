using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RankLens.App;

namespace RankLens.Tests;

public class RankLensWorkflowTests
{
    [Fact]
    public async Task FixedRecognitionWritesConfirmedCandidateToReopenableWorkbook()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        Assert.False(RankingWeek.TryCreate(new DateOnly(2026, 9, 8), out _));

        var candidate = new RankingCandidate
        {
            Category = RankingCategory.Monday,
            Rank = 1,
            CommanderName = "GBgogogo",
            AllianceName = "[TFIP]965熟成魚中心",
            Score = 72138569,
            IsSelected = true
        };
        var workflow = new RankLensWorkflow(new RankingWorkbookWriter());
        var session = await workflow.RecognizeAsync(
            week,
            ["fixture.png"],
            new FixedRecognitionSource([candidate]));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);

        try
        {
            workflow.WriteConfirmed(folder, session);

            Assert.True(File.Exists(path));
            using var document = SpreadsheetDocument.Open(path, false);
            var sheets = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().ToList();
            Assert.Equal(["本週排名", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"],
                sheets.Select(sheet => sheet.Name?.Value ?? string.Empty).ToArray());

            var mondaySheet = sheets.Single(sheet => sheet.Name == "星期一");
            var worksheet = (WorksheetPart)document.WorkbookPart.GetPartById(mondaySheet.Id!);
            var rows = worksheet.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToList();
            Assert.Equal(201, rows.Count);
            Assert.Equal(["排名", "指揮官名稱", "同盟名稱", "積分"],
                rows[0].Elements<Cell>().Select(cell => cell.InnerText).ToArray());
            Assert.Equal("1", rows[1].Elements<Cell>().ElementAt(0).InnerText);
            Assert.Equal("GBgogogo", rows[1].Elements<Cell>().ElementAt(1).InnerText);
            Assert.Equal("[TFIP]965熟成魚中心", rows[1].Elements<Cell>().ElementAt(2).InnerText);
            Assert.Equal("72138569", rows[1].Elements<Cell>().ElementAt(3).InnerText);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProvidedMondaySampleTextFlowsThroughParserIntoWorkbook()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        var parsed = OcrCandidateParser.ParseRows(
            RankingCategory.Monday,
            "screenshot/288376_0.jpg",
            [
                new OcrTextLine("1", .99f, 0, 10),
                new OcrTextLine("GBgogogo", .99f, 11, 20),
                new OcrTextLine("[TFIP]965熟成魚中心", .99f, 21, 30),
                new OcrTextLine("72,138,569", .99f, 31, 40)
            ]);
        var candidate = Assert.Single(parsed);
        candidate.IsSelected = true;

        var workflow = new RankLensWorkflow(new RankingWorkbookWriter());
        var session = await workflow.RecognizeAsync(
            week,
            ["screenshot/288376_0.jpg"],
            new FixedRecognitionSource([candidate]));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);

        try
        {
            var summary = workflow.WriteConfirmed(folder, session);
            Assert.Equal(1, summary.Updated);
            Assert.True(File.Exists(path));

            using var document = SpreadsheetDocument.Open(path, false);
            var monday = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>()
                .Single(sheet => sheet.Name == "星期一");
            var worksheet = (WorksheetPart)document.WorkbookPart.GetPartById(monday.Id!);
            var row = worksheet.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ElementAt(1);
            Assert.Equal(["1", "GBgogogo", "[TFIP]965熟成魚中心", "72138569"],
                row.Elements<Cell>().Select(cell => cell.InnerText).ToArray());
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void UpdateChangesOnlySelectedRowsAndRotatesBackup()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);
        try
        {
            var writer = new RankingWorkbookWriter();
            writer.Write(path, week, [new RankingCandidate { Category = RankingCategory.Monday, Rank = 1, CommanderName = "old", AllianceName = "A", Score = 1, IsSelected = true }]);
            writer.Update(path, week, [
                new RankingCandidate { Category = RankingCategory.Monday, Rank = 1, CommanderName = "new", AllianceName = "B", Score = 2, IsSelected = true },
                new RankingCandidate { Category = RankingCategory.Monday, Rank = 2, CommanderName = "ignored", AllianceName = "C", Score = 3, IsSelected = false }
            ]);

            Assert.True(File.Exists(path + ".bak"));
            using var document = SpreadsheetDocument.Open(path, false);
            var sheet = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "星期一");
            var rows = ((WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToList();
            Assert.Equal("new", rows[1].Elements<Cell>().ElementAt(1).InnerText);
            Assert.Equal(string.Empty, rows[2].Elements<Cell>().ElementAt(1).InnerText);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ReconciledCandidatesWriteOnlyAfterExplicitConflictResolution()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);
        try
        {
            RankingCandidate Candidate(int rank, string name, long score) => new()
            {
                Category = RankingCategory.Monday,
                Rank = rank,
                CommanderName = name,
                AllianceName = "Alliance",
                Score = score,
                IsSelected = true
            };

            var duplicate = Candidate(1, "duplicate", 10);
            var duplicateObservation = Candidate(1, "duplicate", 10);
            var conflict = Candidate(2, "old", 20);
            var conflictAlternative = Candidate(2, "new", 30);
            var reconciled = CandidateReconciler.Reconcile([
                duplicate, duplicateObservation, conflict, conflictAlternative
            ]);

            Assert.Equal(3, reconciled.Candidates.Count);
            Assert.False(reconciled.Candidates.Single(item => item.Rank == 2 && item.CommanderName == "old").IsSelected);
            Assert.False(reconciled.Candidates.Single(item => item.Rank == 2 && item.CommanderName == "new").IsSelected);

            var chosen = reconciled.Candidates.Single(item => item.Rank == 2 && item.CommanderName == "new");
            CandidateReconciler.ResolveConflict(
                reconciled.Candidates.Where(item => item.Category == RankingCategory.Monday && item.Rank == 2).ToArray(),
                chosen,
                ConflictResolution.KeepSelected);

            new RankingWorkbookWriter().Write(path, week, reconciled.Candidates);
            using var document = SpreadsheetDocument.Open(path, false);
            var sheet = document.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == "星期一");
            var rows = ((WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!)).Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToList();
            Assert.Equal("duplicate", rows[1].Elements<Cell>().ElementAt(1).InnerText);
            Assert.Equal("new", rows[2].Elements<Cell>().ElementAt(1).InnerText);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LockedWorkbookRemainsUntouched()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);
        try
        {
            var writer = new RankingWorkbookWriter();
            writer.Write(path, week, [new RankingCandidate { Category = RankingCategory.Monday, Rank = 1, CommanderName = "old", AllianceName = "A", Score = 1, IsSelected = true }]);
            var before = File.ReadAllBytes(path);
            using (var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => writer.Update(path, week, [new RankingCandidate { Category = RankingCategory.Monday, Rank = 1, CommanderName = "new", AllianceName = "B", Score = 2, IsSelected = true }]));
            }
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".ranklens.tmp"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void NoConfirmedCandidatesDoNotCreateOrUpdateWorkbook()
    {
        Assert.True(RankingWeek.TryCreate(new DateOnly(2026, 9, 7), out var week));
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, week.FileName);
        try
        {
            var session = new ReviewSession(week, [new RankingCandidate
            {
                Category = RankingCategory.Monday, Rank = 1, CommanderName = "unchecked",
                AllianceName = "A", Score = 1, IsSelected = false
            }]);
            var summary = new RankLensWorkflow(new RankingWorkbookWriter()).WriteConfirmed(folder, session);
            Assert.Equal(0, summary.Updated);
            Assert.False(File.Exists(path));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
