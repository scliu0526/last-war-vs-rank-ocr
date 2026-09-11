using System.IO;

namespace RankLens.App;

public sealed class RankLensWorkflow(RankingWorkbookWriter workbookWriter)
{
    public sealed record WriteSummary(int Updated, int Skipped, int Failed, string Path);
    public async Task<ReviewSession> RecognizeAsync(
        RankingWeek week,
        IReadOnlyList<string> imagePaths,
        IRecognitionSource recognitionSource,
        CancellationToken cancellationToken = default)
    {
        var candidates = await recognitionSource.RecognizeAsync(imagePaths, cancellationToken);
        return new ReviewSession(week, candidates);
    }

    public WriteSummary WriteConfirmed(string outputFolder, ReviewSession session, int failed = 0)
    {
        var path = Path.Combine(outputFolder, session.Week.FileName);
        var selected = session.Candidates.Count(candidate => candidate.IsSelected && candidate.IsValid);
        var skipped = session.Candidates.Count - selected;
        if (selected == 0) return new WriteSummary(0, skipped, failed, path);
        if (File.Exists(path))
        {
            workbookWriter.Update(path, session.Week, session.Candidates);
        }
        else
        {
            workbookWriter.Write(path, session.Week, session.Candidates);
        }
        return new WriteSummary(selected, skipped, failed, path);
    }
}
