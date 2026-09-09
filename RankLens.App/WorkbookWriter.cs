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
        var candidateList = candidates.ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new Sheets());
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        stylesPart.Stylesheet.Save();

        foreach (var (category, name) in Sheets)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = CreateWorksheet(category, candidateList);
            var relationshipId = workbookPart.GetIdOfPart(worksheetPart);
            ((Sheets)workbookPart.Workbook.Sheets!).Append(
                new Sheet { Name = name, SheetId = (uint)(workbookPart.Workbook.Sheets!.ChildElements.Count + 1), Id = relationshipId });
        }

        workbookPart.Workbook.AppendChild(new DefinedNames(
            new DefinedName { Name = "_RankLensFormatVersion", Text = "\"1\"" }));
        workbookPart.Workbook.Save();
    }

    public void Update(string path, RankingWeek week, IEnumerable<RankingCandidate> candidates)
    {
        if (!File.Exists(path))
        {
            Write(path, week, candidates);
            return;
        }

        var temporaryPath = path + ".ranklens.tmp";
        var backupPath = path + ".bak";
        try
        {
            File.Copy(path, temporaryPath, overwrite: true);
            using (var document = SpreadsheetDocument.Open(temporaryPath, true))
            {
                var workbook = document.WorkbookPart?.Workbook
                    ?? throw new InvalidDataException("找不到 RankLens 活頁簿內容。");
                if (!HasRankLensMarker(workbook))
                {
                    throw new InvalidDataException("目標活頁簿不是可辨識的 RankLens 格式。");
                }

                var selected = candidates.Where(candidate => candidate.IsSelected && candidate.IsValid).ToList();
                var duplicatePositions = selected.GroupBy(candidate => (candidate.Category, candidate.Rank)).Where(group => group.Count() > 1).ToList();
                if (duplicatePositions.Count > 0)
                {
                    throw new InvalidDataException("同一排名位置有多筆已勾選候選，請先完成衝突裁決。");
                }

                foreach (var candidate in selected)
                {
                    var sheetName = Sheets.Single(sheet => sheet.Category == candidate.Category).Name;
                    var sheet = workbook.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
                    var worksheet = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
                    var row = worksheet.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ElementAt(candidate.Rank);
                    var cells = row.Elements<Cell>().ToList();
                    SetNumber(cells[0], candidate.Rank);
                    SetText(cells[1], candidate.CommanderName);
                    SetText(cells[2], candidate.NoAllianceConfirmed && string.IsNullOrWhiteSpace(candidate.AllianceName) ? "無同盟" : candidate.AllianceName);
                    SetNumber(cells[3], candidate.Score);
                    worksheet.Worksheet.Save();
                }

                workbook.Save();
            }

            File.Replace(temporaryPath, path, backupPath, ignoreMetadataErrors: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static Worksheet CreateWorksheet(RankingCategory category, IEnumerable<RankingCandidate> candidates)
    {
        var rows = new SheetData();
        rows.Append(Row(
            TextCell("排名", 1), TextCell("指揮官名稱", 1), TextCell("同盟名稱", 1), TextCell("積分", 1)));
        var byRank = candidates.Where(candidate => candidate.Category == category && candidate.IsSelected && candidate.IsValid)
            .GroupBy(candidate => candidate.Rank)
            .ToDictionary(group => group.Key, group => group.Last());
        for (var rank = 1; rank <= 200; rank++)
        {
            if (byRank.TryGetValue(rank, out var candidate))
            {
                rows.Append(Row(NumberCell(rank), TextCell(candidate.CommanderName),
                    TextCell(candidate.NoAllianceConfirmed && string.IsNullOrWhiteSpace(candidate.AllianceName) ? "無同盟" : candidate.AllianceName),
                    NumberCell(candidate.Score, 2)));
            }
            else
            {
                rows.Append(Row(NumberCell(rank), TextCell(string.Empty), TextCell(string.Empty), TextCell(string.Empty)));
            }
        }

        var worksheet = new Worksheet();
        worksheet.Append(new SheetViews(new SheetView
        {
            WorkbookViewId = 0,
            Pane = new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }
        }));
        worksheet.Append(new Columns(
            new Column { Min = 1, Max = 1, Width = 10, CustomWidth = true },
            new Column { Min = 2, Max = 2, Width = 24, CustomWidth = true },
            new Column { Min = 3, Max = 3, Width = 30, CustomWidth = true },
            new Column { Min = 4, Max = 4, Width = 16, CustomWidth = true }));
        worksheet.Append(rows);
        worksheet.Append(new AutoFilter { Reference = "A1:D201" });
        return worksheet;
    }

    private static Row Row(params Cell[] cells) => new(cells);
    private static Cell TextCell(string value, uint? style = null) => new(new InlineString(new Text(value))) { DataType = CellValues.InlineString, StyleIndex = style };
    private static Cell NumberCell(long value, uint? style = null) => new(new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))) { DataType = CellValues.Number, StyleIndex = style };

    private static void SetText(Cell cell, string value)
    {
        cell.RemoveAllChildren<CellValue>();
        cell.RemoveAllChildren<InlineString>();
        cell.DataType = CellValues.InlineString;
        cell.AppendChild(new InlineString(new Text(value)));
    }

    private static void SetNumber(Cell cell, long value)
    {
        cell.RemoveAllChildren<CellValue>();
        cell.RemoveAllChildren<InlineString>();
        cell.DataType = CellValues.Number;
        cell.AppendChild(new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static bool HasRankLensMarker(Workbook workbook) =>
        workbook.DefinedNames?.Elements<DefinedName>()
            .Any(name => string.Equals(name.Name?.Value, "_RankLensFormatVersion", StringComparison.Ordinal)) == true;

    private static Stylesheet CreateStylesheet() => new(
        new NumberingFormats(new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0" }),
        new Fonts(new Font(), new Font(new Bold())),
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
        new Borders(new Border()),
        new CellStyleFormats(new CellFormat()),
        new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1, ApplyFont = true },
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true }));
}
