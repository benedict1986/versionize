using System;
using System.IO;
using Xunit;
using Versionize.Tests.TestSupport;
using Versionize.CommandLine;
using LibGit2Sharp;
using Shouldly;

namespace Versionize.Tests
{
    public class WorkingCopyTests : IDisposable
    {
        private readonly TestSetup _testSetup;
        private readonly TestPlatformAbstractions _testPlatformAbstractions;

        public WorkingCopyTests()
        {
            _testSetup = TestSetup.Create();

            _testPlatformAbstractions = new TestPlatformAbstractions();
            CommandLineUI.Platform = _testPlatformAbstractions;
        }

        [Fact]
        public void ShouldDiscoverGitWorkingCopies()
        {
            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);

            workingCopy.ShouldNotBeNull();
        }

        [Fact]
        public void ShouldExitIfNoWorkingCopyCouldBeDiscovered()
        {
            var directoryWithoutWorkingCopy =
                Path.Combine(Path.GetTempPath(), "ShouldExitIfNoWorkingCopyCouldBeDiscovered");
            Directory.CreateDirectory(directoryWithoutWorkingCopy);

            Should.Throw<CommandLineExitException>(() => WorkingCopy.Discover(directoryWithoutWorkingCopy));
        }

        [Fact]
        public void ShouldExitIfWorkingCopyDoesNotExist()
        {
            var directoryWithoutWorkingCopy = Path.Combine(Path.GetTempPath(), "ShouldExitIfWorkingCopyDoesNotExist");

            Should.Throw<CommandLineExitException>(() => WorkingCopy.Discover(directoryWithoutWorkingCopy));
        }

        [Fact]
        public void ShouldPreformADryRun()
        {
            TempCsProject.Create(_testSetup.WorkingDirectory);

            File.WriteAllText(Path.Join(_testSetup.WorkingDirectory, "hello.txt"), "First commit");
            CommitAll(_testSetup.Repository);

            File.WriteAllText(Path.Join(_testSetup.WorkingDirectory, "hello.txt"), "Second commit");
            CommitAll(_testSetup.Repository);

            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            workingCopy.Versionize(dryrun: true, skipDirtyCheck: true);

            _testPlatformAbstractions.Messages.Count.ShouldBe(4);
            _testPlatformAbstractions.Messages[0].ShouldBe("Discovered 1 versionable projects");
        }

        [Fact]
        public void ShouldExitIfWorkingCopyIsDirty()
        {
            TempCsProject.Create(_testSetup.WorkingDirectory);

            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            Should.Throw<CommandLineExitException>(() => workingCopy.Versionize());

            _testPlatformAbstractions.Messages.ShouldHaveSingleItem();
            _testPlatformAbstractions.Messages[0].ShouldBe($"Repository {_testSetup.WorkingDirectory} is dirty. Please commit your changes.");
        }

        [Fact]
        public void ShouldExitGracefullyIfNoGitInitialized()
        {
            var workingDirectory = TempDir.Create();
            Should.Throw<CommandLineExitException>(() => WorkingCopy.Discover(workingDirectory));

            _testPlatformAbstractions.Messages[0].ShouldBe($"Directory {workingDirectory} or any parent directory do not contain a git working copy");

            Cleanup.DeleteDirectory(workingDirectory);
        }

        [Fact]
        public void ShouldExitIfWorkingCopyContainsNoProjects()
        {
            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            Should.Throw<CommandLineExitException>(() => workingCopy.Versionize());

            _testPlatformAbstractions.Messages[0].ShouldBe($"Could not find any projects files in {_testSetup.WorkingDirectory} that have a <Version> defined in their csproj file.");
        }

        [Fact]
        public void ShouldExitIfProjectsUseInconsistentNaming()
        {
            TempCsProject.Create(Path.Join(_testSetup.WorkingDirectory, "project1"), "1.1.0");
            TempCsProject.Create(Path.Join(_testSetup.WorkingDirectory, "project2"), "2.0.0");

            CommitAll(_testSetup.Repository);

            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            Should.Throw<CommandLineExitException>(() => workingCopy.Versionize());
            _testPlatformAbstractions.Messages[0].ShouldBe($"Some projects in {_testSetup.WorkingDirectory} have an inconsistent <Version> defined in their csproj file. Please update all versions to be consistent or remove the <Version> elements from projects that should not be versioned");
        }

        [Fact]
        public void ShouldIgnoreInsignificantCommits()
        {
            TempCsProject.Create(_testSetup.WorkingDirectory);

            var workingFilePath = Path.Join(_testSetup.WorkingDirectory, "hello.txt");

            // Create and commit a test file
            File.WriteAllText(workingFilePath, "First line of text");
            CommitAll(_testSetup.Repository);

            // Run versionize
            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            workingCopy.Versionize();

            // Add insignificant change
            File.AppendAllText(workingFilePath, "This is another line of text");
            CommitAll(_testSetup.Repository, "chore: Added line of text");

            // Get last commit
            var lastCommit = _testSetup.Repository.Head.Tip;

            // Run versionize, ignoring insignificant commits
            try
            {
                workingCopy.Versionize(ignoreInsignificant: true);

                throw new InvalidOperationException("Expected to throw in Versionize call");
            }
            catch (CommandLineExitException ex)
            {
                ex.ExitCode.ShouldBe(0);
            }

            lastCommit.ShouldBe(_testSetup.Repository.Head.Tip);
        }

        [Theory]
        [InlineData(VersionSource.Default, "1.0.0", "v1.0.0", "1.0.1")]
        [InlineData(VersionSource.GitTag, "1.0.0", "v1.0.0", "1.0.1")]
        [InlineData(VersionSource.Csproj, "1.0.0", "v1.0.0", "1.0.1")]
        [InlineData(VersionSource.Default, "1.0.0", "v0.0.9", "1.0.0")]
        [InlineData(VersionSource.GitTag, "1.0.0", "v0.0.9", "1.0.0")]
        [InlineData(VersionSource.Csproj, "1.0.0", "v0.0.9", "1.0.1")]
        [InlineData(VersionSource.Default, "1.0.0", "", "1.0.0")]
        [InlineData(VersionSource.GitTag, "1.0.0", "", "1.0.0")]
        [InlineData(VersionSource.Csproj, "1.0.0", "", "1.0.1")]
        public void ShouldGetNextVersionBasedOnCsproj(
            VersionSource versionSource,
            string initialCsprojVersion,
            string gitTag,
            string expectedCsprojVersion)
        {
            TempCsProject.Create(_testSetup.WorkingDirectory, initialCsprojVersion);
            CommitAll(_testSetup.Repository, "fix: commit some fix");
            if (!string.IsNullOrWhiteSpace(gitTag))
            {
                _testSetup.Repository.ApplyTag(gitTag);
            }

            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            workingCopy.Versionize(versionSource: versionSource, skipDirtyCheck: true);
            var projects = Projects.Discover(_testSetup.WorkingDirectory);
            Assert.Equal(expectedCsprojVersion, projects.Version.ToString());
        }

        public static TheoryData<VersionSource, string, string, string> NextVersionInvalidData => new TheoryData<VersionSource, string, string, string> {
            {
                VersionSource.Default,
                "1.0.0",
                "v0.0.9",
                $"Version was not affected by commits since last release (1.0.0), since you specified to ignore insignificant changes, no action will be performed."
            },
            {
                VersionSource.GitTag,
                "1.0.0",
                "v0.0.9",
                $"Version was not affected by commits since last release (1.0.0), since you specified to ignore insignificant changes, no action will be performed."
            },
            {
                VersionSource.Csproj,
                "1.0.0",
                "v1.0.1",
                $"The next version 1.0.1 has been tagged already."
            }
        };

        [Theory]
        [MemberData(nameof(NextVersionInvalidData))]
        public void ShouldExistWhenNextVersionInvalid(
            VersionSource versionSource,
            string initialCsprojVersion,
            string gitTag,
            string errorMessage)
        {
            TempCsProject.Create(_testSetup.WorkingDirectory, initialCsprojVersion);
            CommitAll(_testSetup.Repository, "fix: commit some fix");
            _testSetup.Repository.ApplyTag(gitTag);

            var workingCopy = WorkingCopy.Discover(_testSetup.WorkingDirectory);
            Should.Throw<CommandLineExitException>(() => workingCopy.Versionize(versionSource: versionSource, skipDirtyCheck: true, ignoreInsignificant: true));
            _testPlatformAbstractions.Messages[2].ShouldBe(errorMessage);
        }

        public void Dispose()
        {
            _testSetup.Dispose();
        }

        private static void CommitAll(IRepository repository, string message = "feat: Initial commit")
        {
            var author = new Signature("Gitty McGitface", "noreply@git.com", DateTime.Now);
            Commands.Stage(repository, "*");
            repository.Commit(message, author, author);
        }
    }
}
