namespace RankLens.App;

public enum NameCollisionResolution
{
    MoveRank,
    KeepBoth,
    IgnoreNew
}

public sealed class CandidateReconciliationResult
{
    public required IReadOnlyList<RankingCandidate> Candidates { get; init; }
    public required IReadOnlyList<IReadOnlyList<RankingCandidate>> Conflicts { get; init; }
    public required IReadOnlyList<IReadOnlyList<RankingCandidate>> NameCollisions { get; init; }
}

public static class CandidateReconciler
{
    public static CandidateReconciliationResult Reconcile(IEnumerable<RankingCandidate> input)
    {
        var candidates = input.ToList();
        var merged = new List<RankingCandidate>();
        var conflicts = new List<IReadOnlyList<RankingCandidate>>();
        foreach (var group in candidates.GroupBy(candidate => (candidate.Category, candidate.Rank)))
        {
            var distinct = group.GroupBy(candidate => (candidate.CommanderName, candidate.AllianceName, candidate.Score, candidate.NoAllianceConfirmed)).ToList();
            if (distinct.Count == 1)
            {
                merged.Add(group.First());
            }
            else
            {
                var conflict = group.ToList();
                conflicts.Add(conflict);
                foreach (var candidate in conflict)
                {
                    candidate.IsSelected = false;
                    merged.Add(candidate);
                }
            }
        }

        var collisions = merged.Where(candidate => !string.IsNullOrWhiteSpace(candidate.CommanderName))
            .GroupBy(candidate => (candidate.Category, candidate.CommanderName))
            .Where(group => group.Select(candidate => candidate.Rank).Distinct().Count() > 1)
            .Select(group => (IReadOnlyList<RankingCandidate>)group.ToList())
            .ToList();
        foreach (var collision in collisions.SelectMany(group => group))
        {
            collision.IsSelected = false;
        }

        return new CandidateReconciliationResult
        {
            Candidates = merged,
            Conflicts = conflicts,
            NameCollisions = collisions
        };
    }
}
