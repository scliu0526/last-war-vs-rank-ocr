using RankLens.App;

namespace RankLens.Tests;

public class SettingsAndReconciliationTests
{
    [Fact]
    public void SettingsRoundTripPreservesThresholdModeAdapterAndRetention()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        try
        {
            var store = new AppSettingsStore(path);
            store.Save(new AppSettings
            {
                ConfidenceThreshold = 0.87,
                ExecutionMode = RecognitionExecutionMode.DirectML,
                AdapterName = "RTX",
                LogRetentionDays = 14,
                LogRetentionBytes = 12 * 1024 * 1024
            });
            var loaded = store.Load();
            Assert.Equal(0.87, loaded.ConfidenceThreshold);
            Assert.Equal(RecognitionExecutionMode.DirectML, loaded.ExecutionMode);
            Assert.Equal("RTX", loaded.AdapterName);
            Assert.Equal(14, loaded.LogRetentionDays);
            Assert.Equal(12 * 1024 * 1024, loaded.LogRetentionBytes);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void ReconciliationMergesDuplicatesAndUnselectsConflictsAndCollisions()
    {
        RankingCandidate Candidate(int rank, string name, long score) => new()
        {
            Category = RankingCategory.Monday, Rank = rank, CommanderName = name,
            AllianceName = "A", Score = score, IsSelected = true
        };
        var result = CandidateReconciler.Reconcile([
            Candidate(1, "same", 10), Candidate(1, "same", 10),
            Candidate(2, "conflict", 20), Candidate(2, "other", 30),
            Candidate(3, "same", 40)
        ]);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Single(result.Conflicts);
        Assert.Single(result.NameCollisions);
        Assert.All(result.Conflicts.SelectMany(group => group), item => Assert.False(item.IsSelected));
        Assert.False(result.Candidates.Single(item => item.Rank == 1).IsSelected);
        Assert.False(result.Candidates.Single(item => item.Rank == 3).IsSelected);
    }

    [Fact]
    public void BlankAllianceRequiresExplicitNoAllianceConfirmation()
    {
        var candidate = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "Commander",
            AllianceName = string.Empty, Score = 1, IsSelected = true
        };
        Assert.False(candidate.IsValid);
        candidate.NoAllianceConfirmed = true;
        Assert.True(candidate.IsValid);
    }

    [Fact]
    public void CollisionResolutionSupportsMoveKeepBothAndIgnore()
    {
        RankingCandidate Candidate(int rank) => new()
        {
            Category = RankingCategory.Monday, Rank = rank, CommanderName = "same",
            AllianceName = "A", Score = rank, IsSelected = false
        };
        var first = Candidate(1);
        var second = Candidate(2);
        CandidateReconciler.ResolveNameCollision([first, second], NameCollisionResolution.KeepBoth);
        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);

        CandidateReconciler.ResolveNameCollision([first, second], NameCollisionResolution.IgnoreNew);
        Assert.False(second.IsSelected);
        CandidateReconciler.ResolveNameCollision([first, second], NameCollisionResolution.MoveRank, 10);
        Assert.Equal(10, first.Rank);
        Assert.True(first.IsSelected);
        Assert.False(second.IsSelected);
    }
}
