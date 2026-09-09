using System.IO;

namespace RankLens.App;

public sealed class RankLensWorkflow(RankingWorkbookWriter workbookWriter)
{
    public async Task<ReviewSession> RecognizeAsync(
        RankingWeek week,
        IReadOnlyList<string> imagePaths,
        IRecognitionSource recognitionSource,
        CancellationToken cancellationToken = default)
    {
        var candidates = await recognitionSource.RecognizeAsync(imagePaths, cancellationToken);
        return new ReviewSession(week, candidates);
    }

    public void WriteConfirmed(string outputFolder, ReviewSession session)
    {
        var path = Path.Combine(outputFolder, session.Week.FileName);
        if (File.Exists(path))
        {
            workbookWriter.Update(path, session.Week, session.Candidates);
        }
        else
        {
            workbookWriter.Write(path, session.Week, session.Candidates);
        }
    }
}
