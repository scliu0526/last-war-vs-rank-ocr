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
}
