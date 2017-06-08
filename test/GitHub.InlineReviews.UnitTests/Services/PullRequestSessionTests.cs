﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitHub.InlineReviews.Services;
using GitHub.InlineReviews.UnitTests.TestDoubles;
using GitHub.Models;
using GitHub.Services;
using LibGit2Sharp;
using NSubstitute;
using Rothko;
using Xunit;

namespace GitHub.InlineReviews.UnitTests.Services
{
    public class PullRequestSessionTests
    {
        const string RepoUrl = "https://foo.bar/owner/repo";
        const string FilePath = "test.cs";

        public class TheGetFileMethod
        {
            [Fact]
            public async Task MatchesReviewCommentOnOriginalLine()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(comment);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(headContents);
                    var file = await target.GetFile(FilePath, editor);
                    var thread = file.InlineCommentThreads.First();
                    Assert.Equal(2, thread.LineNumber);
                }
            }

            [Fact]
            public async Task MatchesReviewCommentOnOriginalLineGettingContentFromDisk()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(comment);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);
                    service.ReadFileAsync(FilePath).Returns(Encoding.UTF8.GetBytes(headContents));

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var file = await target.GetFile(FilePath);
                    var thread = file.InlineCommentThreads.First();

                    Assert.Equal(2, thread.LineNumber);
                }
            }

            [Fact]
            public async Task MatchesReviewCommentOnDifferentLine()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"New Line 1
New Line 2
Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");


                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(comment);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(headContents);
                    var file = await target.GetFile(FilePath, editor);
                    var thread = file.InlineCommentThreads.First();

                    Assert.Equal(4, thread.LineNumber);
                }
            }

            [Fact]
            public async Task UpdatesReviewCommentWithEditorContents()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var diskContents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var editorContents = @"New Line 1
New Line 2
Line 1
Line 2
Line 3 with comment
Line 4";

                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(comment);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(diskContents);
                    var file = await target.GetFile(FilePath, editor);
                    var thread = file.InlineCommentThreads.First();

                    Assert.Equal(2, thread.LineNumber);
                    editor.SetContent(editorContents);

                    await target.UpdateEditorContent(FilePath);

                    Assert.Equal(4, thread.LineNumber);
                }
            }

            [Fact]
            public async Task UpdatesReviewCommentWithNewBody()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var editorContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var originalComment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Original Comment");

                var updatedComment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Updated Comment");


                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(originalComment);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(editorContents);
                    var file = await target.GetFile(FilePath, editor);

                    var thread = file.InlineCommentThreads.Single();

                    Assert.Equal("Original Comment", thread.Comments.Single().Body);
                    Assert.Equal(2, thread.LineNumber);

                    pullRequest = CreatePullRequest(updatedComment);
                    await target.Update(pullRequest);
                    thread = file.InlineCommentThreads.Single();

                    Assert.Equal("Updated Comment", thread.Comments.Single().Body);
                    Assert.Equal(2, thread.LineNumber);
                }
            }

            [Fact]
            public async Task AddsNewReviewCommentToThread()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var editorContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var comment1 = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Comment1");

                var comment2 = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Comment2");


                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest(comment1);
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(editorContents);
                    var file = await target.GetFile(FilePath, editor);

                    var thread = file.InlineCommentThreads.Single();

                    Assert.Equal("Comment1", thread.Comments.Single().Body);
                    Assert.Equal(2, thread.LineNumber);

                    pullRequest = CreatePullRequest(comment1, comment2);
                    await target.Update(pullRequest);
                    thread = file.InlineCommentThreads.Single();

                    Assert.Equal(2, thread.Comments.Count);
                    Assert.Equal(new[] { "Comment1", "Comment2" }, thread.Comments.Select(x => x.Body).ToArray());
                    Assert.Equal(2, thread.LineNumber);
                }
            }

            [Fact]
            public async Task CommitShaIsSetIfUnmodified()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest();
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);
                    service.IsUnmodifiedAndPushed(Arg.Any<ILocalRepositoryModel>(), FilePath, Arg.Any<byte[]>()).Returns(true);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(headContents);
                    var file = await target.GetFile(FilePath, editor);
                    Assert.Equal("BRANCH_TIP", file.CommitSha);
                }
            }

            [Fact]
            public async Task CommitShaIsNullIfModified()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";


                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest();
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);
                    service.IsUnmodifiedAndPushed(Arg.Any<ILocalRepositoryModel>(), FilePath, Arg.Any<byte[]>()).Returns(false);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(headContents);
                    var file = await target.GetFile(FilePath, editor);
                    Assert.Null(file.CommitSha);
                }
            }

            [Fact]
            public async Task CommitShaIsNullWhenChangedToModified()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = Encoding.UTF8.GetBytes(@"Line 1
Line 2
Line 3 with comment
Line 4");
                var editorContents = Encoding.UTF8.GetBytes(@"Line 1
Line 2
Line 3 with comment
Line 4 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var pullRequest = CreatePullRequest();
                    var service = CreateService(diffService);

                    diffService.AddFile(FilePath, baseContents);
                    service.IsUnmodifiedAndPushed(Arg.Any<ILocalRepositoryModel>(), FilePath, headContents).Returns(true);
                    service.IsUnmodifiedAndPushed(Arg.Any<ILocalRepositoryModel>(), FilePath, editorContents).Returns(false);

                    var target = new PullRequestSession(
                        service,
                        Substitute.For<IAccount>(),
                        pullRequest,
                        Substitute.For<ILocalRepositoryModel>(),
                        true);

                    var editor = new FakeEditorContentSource(headContents);
                    var file = await target.GetFile(FilePath, editor);

                    Assert.Equal("BRANCH_TIP", file.CommitSha);

                    editor.SetContent(editorContents);
                    await target.UpdateEditorContent(FilePath);

                    Assert.Null(file.CommitSha);
                }
            }
        }

        public class TheAddCommentMethod
        {
            [Fact]
            public async Task UpdatesFileWithNewThread()
            {
                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "New Comment");

                using (var diffService = new FakeDiffService())
                {
                    var target = await CreateTarget(diffService);
                    var file = await target.GetFile(FilePath);

                    Assert.Empty(file.InlineCommentThreads);

                    await target.AddComment(comment);

                    Assert.Equal(1, file.InlineCommentThreads.Count);
                    Assert.Equal(2, file.InlineCommentThreads[0].LineNumber);
                    Assert.Equal(1, file.InlineCommentThreads[0].Comments.Count);
                    Assert.Equal("New Comment", file.InlineCommentThreads[0].Comments[0].Body);
                }
            }

            async Task<PullRequestSession> CreateTarget(FakeDiffService diffService)
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var headContents = @"Line 1
Line 2
Line 3 with comment
Line 4";

                var pullRequest = CreatePullRequest();
                var service = CreateService(diffService);

                diffService.AddFile(FilePath, baseContents);

                var target = new PullRequestSession(
                    service,
                    Substitute.For<IAccount>(),
                    pullRequest,
                    Substitute.For<ILocalRepositoryModel>(),
                    true);

                var editor = new FakeEditorContentSource(headContents);
                var file = await target.GetFile(FilePath, editor);
                return target;
            }
        }

        static IPullRequestReviewCommentModel CreateComment(string diffHunk, string body = "Comment")
        {
            var result = Substitute.For<IPullRequestReviewCommentModel>();
            result.Body.Returns(body);
            result.DiffHunk.Returns(diffHunk);
            result.Path.Returns(FilePath);
            result.OriginalCommitId.Returns("ORIG");
            result.OriginalPosition.Returns(1);
            return result;
        }

        static IPullRequestModel CreatePullRequest(params IPullRequestReviewCommentModel[] comments)
        {
            var changedFile = Substitute.For<IPullRequestFileModel>();
            changedFile.FileName.Returns("test.cs");

            var result = Substitute.For<IPullRequestModel>();
            result.Base.Returns(new GitReferenceModel("BASE", "master", "BASE", RepoUrl));
            result.Head.Returns(new GitReferenceModel("HEAD", "pr", "HEAD", RepoUrl));
            result.ChangedFiles.Returns(new[] { changedFile });
            result.ReviewComments.Returns(comments);

            return result;
        }

        static IRepository CreateRepository()
        {
            var result = Substitute.For<IRepository>();
            var branch = Substitute.For<Branch>();
            var commit = Substitute.For<Commit>();
            commit.Sha.Returns("BRANCH_TIP");
            branch.Tip.Returns(commit);
            result.Head.Returns(branch);
            return result;
        }

        static IPullRequestSessionService CreateService(FakeDiffService diffService)
        {
            var result = Substitute.For<IPullRequestSessionService>();
            result.Diff(
                Arg.Any<ILocalRepositoryModel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>())
                .Returns(i => diffService.Diff(
                    null,
                    i.ArgAt<string>(1),
                    i.ArgAt<string>(2),
                    i.ArgAt<byte[]>(3)));
            result.GetTipSha(Arg.Any<ILocalRepositoryModel>()).Returns("BRANCH_TIP");
            return result;
        }
    }
}
