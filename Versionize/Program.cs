using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Versionize.CommandLine;

namespace Versionize
{
    [Command(
        Name = "Versionize",
        Description = "Automatic versioning and CHANGELOG generation, using conventional commit messages")]
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "versionize";
            app.HelpOption();
            app.VersionOption("-v|--version", GetVersion());

            var optionWorkingDirectory = app.Option("-w|--workingDir <WORKING_DIRECTORY>", "Directory containing projects to version", CommandOptionType.SingleValue);
            var optionDryRun = app.Option("-d|--dry-run", "Skip changing versions in projects, changelog generation and git commit", CommandOptionType.NoValue);
            var optionSkipDirty = app.Option("--skip-dirty", "Skip git dirty check", CommandOptionType.NoValue);
            var optionReleaseAs = app.Option("-r|--release-as <VERSION>", "Specify the release version manually", CommandOptionType.SingleValue);
            var optionSilent = app.Option("--silent", "Suppress output to console", CommandOptionType.NoValue);
            var optionVersionSource = app.Option("--version-source", "Set the source of the version. Currently it supports Default, GitTag and Csproj (case insensitive)", CommandOptionType.SingleValue);

            var optionSkipCommit = app.Option("--skip-commit", "Skip commit and git tag after updating changelog and incrementing the version", CommandOptionType.NoValue);
            var optionIgnoreInsignificant = app.Option("-i|--ignore-insignificant-commits", "Do not bump the version if no significant commits (fix, feat or BREAKING) are found", CommandOptionType.NoValue);
            var optionIncludeAllCommitsInChangelog = app.Option("--changelog-all", "Include all commits in the changelog not just fix, feat and breaking changes", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                CommandLineUI.Verbosity = optionSilent.HasValue() ? LogLevel.Silent : LogLevel.All;
                var versionSource = ConvertVersionSource(optionVersionSource.Value());

                WorkingCopy
                    .Discover(optionWorkingDirectory.Value() ?? Directory.GetCurrentDirectory())
                    .Versionize(
                        dryrun: optionDryRun.HasValue(),
                        skipDirtyCheck: optionSkipDirty.HasValue(),
                        skipCommit: optionSkipCommit.HasValue(),
                        releaseVersion: optionReleaseAs.Value(),
                        ignoreInsignificant: optionIgnoreInsignificant.HasValue(),
                        includeAllCommitsInChangelog: optionIncludeAllCommitsInChangelog.HasValue(),
                        versionSource: versionSource
                    );

                return 0;
            });

            return app.Execute(args);
        }

        static string GetVersion() => typeof(Program).Assembly.GetName().Version.ToString();

        public static VersionSource ConvertVersionSource(string inputVersionSource)
        {
            if (string.IsNullOrWhiteSpace(inputVersionSource))
            {
                return VersionSource.Default;
            }
            else if (Enum.TryParse<VersionSource>(inputVersionSource, true, out VersionSource versionSource))
            {
                return (VersionSource)versionSource;
            }
            else
            {
                CommandLineUI.Platform.WriteLine("Selected version source is not supported. Default value will be used", Color.Orange);
                return VersionSource.Default;
            }
        }
    }
}
