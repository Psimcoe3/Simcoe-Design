using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services.Export;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowFileOperationsTests
{
    [Fact]
    public void BuildXfdfImportConflictSummaryForTesting_IncludesCountsAndParticipants()
    {
        var summary = MainWindow.BuildXfdfImportConflictSummaryForTesting(new XfdfImportMergeResult
        {
            ImportedCount = 2,
            Conflicts =
            {
                new XfdfMergeConflict
                {
                    MarkupId = "issue-1",
                    Kind = XfdfMergeConflictKind.GeometryMismatch | XfdfMergeConflictKind.ReviewStateMismatch,
                    Summary = "Markup 'issue-1' had differing geometry, review state during XFDF merge."
                }
            },
            ParticipantNames = { "Reviewer A", "Reviewer B" }
        });

        Assert.Contains("2 markup(s)", summary);
        Assert.Contains("1 conflicting markup(s)", summary);
        Assert.Contains("Geometry conflicts: 1", summary);
        Assert.Contains("Review-state conflicts: 1", summary);
        Assert.Contains("Reviewer A", summary);
        Assert.Contains("Reviewer B", summary);
    }

    [Fact]
    public void BuildXfdfImportResultSummaryForTesting_IncludesReplyAndConflictTelemetry()
    {
        var summary = MainWindow.BuildXfdfImportResultSummaryForTesting(new XfdfImportMergeResult
        {
            ImportedCount = 2,
            AddedCount = 1,
            UpdatedCount = 1,
            RepliesAddedCount = 3,
            ManualRepliesAddedCount = 2,
            AuditRepliesAddedCount = 1,
            StatusNotesAppliedCount = 1,
            Conflicts =
            {
                new XfdfMergeConflict
                {
                    MarkupId = "issue-1",
                    Kind = XfdfMergeConflictKind.GeometryMismatch,
                    Summary = "Markup 'issue-1' had differing geometry during XFDF merge."
                }
            },
            ParticipantNames = { "Reviewer A" }
        }, XfdfImportMergeMode.PreferExisting);

        Assert.Contains("PreferExisting", summary);
        Assert.Contains("Added 1, merged 1, duplicated 0", summary);
        Assert.Contains("3 new reply entries", summary);
        Assert.Contains("geometry 1", summary);
        Assert.Contains("Reviewer A", summary);
    }

    [Fact]
    public void BuildXfdfImportDetailLinesForTesting_IncludesMarkupIdsAndConflictSummaries()
    {
        var details = MainWindow.BuildXfdfImportDetailLinesForTesting(new XfdfImportMergeResult
        {
            ImportedCount = 3,
            AddedMarkupIds = { "issue-1", "issue-2" },
            UpdatedMarkupIds = { "issue-3" },
            DuplicatedMarkupIds = { "issue-4" },
            Conflicts =
            {
                new XfdfMergeConflict
                {
                    MarkupId = "issue-3",
                    Kind = XfdfMergeConflictKind.GeometryMismatch,
                    Summary = "Markup 'issue-3' had differing geometry during XFDF merge."
                }
            }
        });

        Assert.Contains(details, line => line.Contains("Added markups", StringComparison.Ordinal));
        Assert.Contains(details, line => line.Contains("issue-3", StringComparison.Ordinal));
        Assert.Contains(details, line => line.Contains("Duplicated markups", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportXfdfForTesting_PreferExisting_RefreshesReviewContextAndKeepsExistingStatus()
    {
        RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var markup = new MarkupRecord
            {
                Id = "issue-1",
                Type = MarkupType.Rectangle,
                Vertices = { new Point(0, 0), new Point(10, 10) },
                Status = MarkupStatus.Open,
                Metadata = new MarkupMetadata { Label = "Issue 1" },
                Replies =
                {
                    new MarkupReply
                    {
                        Id = "reply-root",
                        Author = "Reviewer A",
                        Text = "Original note"
                    }
                }
            };
            markup.UpdateBoundingRect();
            viewModel.Markups.Add(markup);
            viewModel.MarkupTool.SelectedMarkup = markup;

            var importedMarkup = new MarkupRecord
            {
                Id = "issue-1",
                Type = MarkupType.Rectangle,
                Vertices = { new Point(5, 5), new Point(25, 25) },
                Status = MarkupStatus.Resolved,
                StatusNote = "Resolved in imported review.",
                Replies =
                {
                    new MarkupReply
                    {
                        Id = "reply-root",
                        Author = "Reviewer A",
                        Text = "Original note"
                    },
                    new MarkupReply
                    {
                        ParentReplyId = "reply-root",
                        Author = "Reviewer B",
                        Kind = MarkupReplyKind.StatusAudit,
                        Text = "Imported status update"
                    }
                }
            };
            importedMarkup.UpdateBoundingRect();
            var xfdf = viewModel.XfdfService.Export(new[] { importedMarkup });

            var window = new MainWindow(viewModel);
            try
            {
                var preview = window.PreviewXfdfImportForTesting(xfdf);
                var mergeResult = window.ImportXfdfForTesting(xfdf, XfdfImportMergeMode.PreferExisting);

                Assert.Equal(1, preview.ConflictCount);
                Assert.Contains("Reviewer B", preview.ParticipantNames);
                Assert.Equal(1, mergeResult.UpdatedCount);
                Assert.Equal(1, mergeResult.RepliesAddedCount);
                Assert.Equal(1, mergeResult.AuditRepliesAddedCount);
                Assert.Equal(MarkupStatus.Open, markup.Status);
                Assert.Null(markup.StatusNote);
                Assert.Equal(2, markup.Replies.Count);
                Assert.Equal(2, viewModel.MarkupTool.SelectedMarkupReplies.Count);
                Assert.Equal(1, viewModel.MarkupTool.SelectedMarkupReplies[1].ThreadDepth);
                Assert.Equal("reply-root", viewModel.MarkupTool.SelectedMarkupReplies[1].ParentReplyId);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        lock (WpfStaTestSynchronization.MainWindowLock)
        {
            Exception? exception = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw new Xunit.Sdk.XunitException($"STA test failed: {exception}");
        }
    }
}