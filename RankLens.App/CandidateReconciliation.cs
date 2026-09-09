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
    public static void ResolveNameCollision(
        IReadOnlyList<RankingCandidate> candidates,
        NameCollisionResolution resolution,
        int? movedRank = null)
    {
        if (candidates.Count == 0) return;
        switch (resolution)
        {
            case NameCollisionResolution.MoveRank when movedRank is >= 1 and <= 200:
                candidates[0].Rank = movedRank.Value;
                foreach (var candidate in candidates) candidate.RequiresNameCollisionResolution = false;
                candidates[0].IsSelected = candidates[0].IsValid;
                foreach (var candidate in candidates.Skip(1)) candidate.IsSelected = false;
                break;
            case NameCollisionResolution.KeepBoth:
                foreach (var candidate in candidates) candidate.RequiresNameCollisionResolution = false;
                foreach (var candidate in candidates) candidate.IsSelected = candidate.IsValid;
                break;
            case NameCollisionResolution.IgnoreNew:
                foreach (var candidate in candidates) candidate.RequiresNameCollisionResolution = false;
                candidates[^1].IsSelected = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(movedRank), "移動名次必須介於 1 到 200。");
        }
    }

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
                var representative = group.First();
                foreach (var observation in group) representative.AddSource(observation);
                merged.Add(representative);
            }
            else
            {
                var conflict = group.ToList();
                conflicts.Add(conflict);
                foreach (var candidate in conflict)
                {
                    candidate.AddSource(candidate);
                    candidate.RequiresConflictResolution = true;
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
            collision.RequiresNameCollisionResolution = true;
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
