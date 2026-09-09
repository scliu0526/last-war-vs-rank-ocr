using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO;

namespace RankLens.App;

public sealed class RankingWorkbookWriter
{
    private static readonly (RankingCategory Category, string Name)[] Sheets =
    [
        (RankingCategory.Weekly, "本週排名"),
        (RankingCategory.Monday, "星期一"),
        (RankingCategory.Tuesday, "星期二"),
        (RankingCategory.Wednesday, "星期三"),
        (RankingCategory.Thursday, "星期四"),
        (RankingCategory.Friday, "星期五"),
        (RankingCategory.Saturday, "星期六")
    ];

    public void Write(string path, RankingWeek week, IEnumerable<RankingCandidate> candidates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new Sheets());

        foreach (var (category, name) in Sheets)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = CreateWorksheet(category, candidates);
            var relationshipId = workbookPart.GetIdOfPart(worksheetPart);
            ((Sheets)workbookPart.Workbook.Sheets!).Append(
                new Sheet { Name = name, SheetId = (uint)(workbookPart.Workbook.Sheets!.ChildElements.Count + 1), Id = relationshipId });
        }

        workbookPart.Workbook.AppendChild(new DefinedNames(
            new DefinedName { Name = "_RankLensFormatVersion", Text = "\"1\"" }));
        workbookPart.Workbook.Save();
    }

    private static Worksheet CreateWorksheet(RankingCategory category, IEnumerable<RankingCandidate> candidates)
    {
        var rows = new SheetData();
        rows.Append(Row(
            TextCell("排名"), TextCell("指揮官名稱"), TextCell("同盟名稱"), TextCell("積分")));
        var byRank = candidates.Where(candidate => candidate.Category == category && candidate.IsSelected && candidate.IsValid)
            .ToDictionary(candidate => candidate.Rank);
        for (var rank = 1; rank <= 200; rank++)
        {
            if (byRank.TryGetValue(rank, out var candidate))
            {
                rows.Append(Row(NumberCell(rank), TextCell(candidate.CommanderName),
                    TextCell(candidate.NoAllianceConfirmed && string.IsNullOrWhiteSpace(candidate.AllianceName) ? "無同盟" : candidate.AllianceName),
                    NumberCell(candidate.Score)));
            }
            else
            {
                rows.Append(Row(NumberCell(rank), TextCell(string.Empty), TextCell(string.Empty), TextCell(string.Empty)));
            }
        }

        return new Worksheet(rows);
    }

    private static Row Row(params Cell[] cells) => new(cells);
    private static Cell TextCell(string value) => new(new InlineString(new Text(value))) { DataType = CellValues.InlineString };
    private static Cell NumberCell(long value) => new(new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))) { DataType = CellValues.Number };
}
