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
    public void SettingsLoadNormalizesUnsafeRanges()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{\"ConfidenceThreshold\":2,\"LogRetentionDays\":0,\"LogRetentionBytes\":-1,\"OutputFolder\":\"\"}");
            var settings = new AppSettingsStore(path).Load();
            Assert.Equal(1, settings.ConfidenceThreshold);
            Assert.Equal(30, settings.LogRetentionDays);
            Assert.Equal(100 * 1024 * 1024, settings.LogRetentionBytes);
            Assert.False(string.IsNullOrWhiteSpace(settings.OutputFolder));
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
    public void ReconciliationKeepsAllSourceObservationsWhenMergingDuplicates()
    {
        RankingCandidate Candidate(string source) => new()
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "same",
            AllianceName = "A", Score = 10, SourceImage = source, IsSelected = true
        };
        var merged = Assert.Single(CandidateReconciler.Reconcile([Candidate("a.png"), Candidate("b.png")]).Candidates);

        Assert.Equal(2, merged.Sources.Count);
        Assert.Contains(merged.Sources, source => source.ImagePath == "a.png");
        Assert.Contains(merged.Sources, source => source.ImagePath == "b.png");
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
    public void EditedInvalidFieldUnselectsCandidateUntilCorrected()
    {
        var candidate = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "Commander",
            AllianceName = "Alliance", Score = 10
        };

        CandidateSelectionPolicy.Apply([candidate]);
        Assert.True(candidate.IsSelected);

        candidate.Score = -1;
        CandidateSelectionPolicy.Apply([candidate]);
        Assert.False(candidate.IsSelected);

        candidate.Score = 20;
        CandidateSelectionPolicy.Apply([candidate]);
        Assert.True(candidate.IsSelected);
    }

    [Fact]
    public void ExplicitNoAllianceConfirmationAllowsAutomaticSelection()
    {
        var candidate = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "Commander",
            AllianceName = string.Empty, Score = 10
        };

        CandidateSelectionPolicy.Apply([candidate]);
        Assert.False(candidate.IsSelected);
        candidate.NoAllianceConfirmed = true;
        CandidateSelectionPolicy.Apply([candidate]);
        Assert.True(candidate.IsSelected);
    }

    [Fact]
    public void ReviewUiPolicyAcceptsOnlyMondayDates()
    {
        Assert.True(ReviewUiPolicy.TryCreateWeek(new DateTime(2026, 9, 7), out var week));
        Assert.Equal(new DateOnly(2026, 9, 7), week.Monday);
        Assert.False(ReviewUiPolicy.TryCreateWeek(new DateTime(2026, 9, 8), out _));
        Assert.False(ReviewUiPolicy.TryCreateWeek(null, out _));
    }

    [Fact]
    public void ReviewUiPolicyPromptsOnlyWhenUnsavedCandidatesExist()
    {
        Assert.True(ReviewUiPolicy.ShouldPromptOnClose(true, 1));
        Assert.False(ReviewUiPolicy.ShouldPromptOnClose(false, 1));
        Assert.False(ReviewUiPolicy.ShouldPromptOnClose(true, 0));
    }

    [Fact]
    public void CategoryCorrectionAppliesToEveryCandidateFromTheSameSource()
    {
        RankingCandidate Candidate(string source, RankingCategory category) => new()
        {
            Category = category, Rank = 1, CommanderName = "Commander", AllianceName = "Alliance", Score = 1,
            SourceImage = source, ClassificationResolved = category != RankingCategory.PendingClassification
        };
        var candidates = new[]
        {
            Candidate("same.jpg", RankingCategory.PendingClassification),
            Candidate("same.jpg", RankingCategory.PendingClassification),
            Candidate("other.jpg", RankingCategory.PendingClassification)
        };

        var changed = CandidateReconciler.ApplyCategoryToSource(candidates, "same.jpg", RankingCategory.Monday);

        Assert.Equal(2, changed.Count);
        Assert.All(changed, candidate =>
        {
            Assert.Equal(RankingCategory.Monday, candidate.Category);
            Assert.True(candidate.ClassificationResolved);
        });
        Assert.Equal(RankingCategory.PendingClassification, candidates[2].Category);
    }

    [Fact]
    public void ReviewStatusExplainsWhyCandidateIsNotSelected()
    {
        var candidate = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "Commander",
            AllianceName = "Alliance", Score = 1, CommanderConfidence = 0.5
        };
        CandidateSelectionPolicy.Apply([candidate], 0.8);
        Assert.Equal("低信心，待確認", candidate.ReviewStatus);
        candidate.RequiresNameCollisionResolution = true;
        Assert.Equal("名稱碰撞，待裁決", candidate.ReviewStatus);
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

    [Fact]
    public void ConflictResolutionKeepsOnlyExplicitlySelectedCandidate()
    {
        var first = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "first",
            AllianceName = "A", Score = 10, RequiresConflictResolution = true
        };
        var second = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "second",
            AllianceName = "B", Score = 20, RequiresConflictResolution = true
        };

        CandidateReconciler.ResolveConflict([first, second], second, ConflictResolution.KeepSelected);

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.False(first.RequiresConflictResolution);
        Assert.False(second.RequiresConflictResolution);
    }

    [Fact]
    public void ConflictResolutionIgnoringOneCandidateSelectsTheOnlyRemainingCandidate()
    {
        var first = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "first",
            AllianceName = "A", Score = 10, RequiresConflictResolution = true
        };
        var second = new RankingCandidate
        {
            Category = RankingCategory.Monday, Rank = 1, CommanderName = "second",
            AllianceName = "B", Score = 20, RequiresConflictResolution = true
        };

        CandidateReconciler.ResolveConflict([first, second], second, ConflictResolution.IgnoreSelected);

        Assert.True(first.IsSelected);
        Assert.False(second.IsSelected);
        Assert.False(first.RequiresConflictResolution);
        Assert.False(second.RequiresConflictResolution);
    }
}
