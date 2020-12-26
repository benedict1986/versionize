﻿using Versionize.CommandLine;
using Versionize.Tests.TestSupport;
using Xunit;

namespace Versionize.Tests
{
    public class ProgramTests
    {
        public ProgramTests()
        {
            CommandLineUI.Platform = new TestPlatformAbstractions();
        }

        [Fact]
        public void ShouldRunVersionizeWithDryRunOption()
        {
            var exitCode = Program.Main(new[] { "--dry-run", "--skip-dirty" });

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ShouldVersionizeDesiredReleaseVersion()
        {
            var exitCode = Program.Main(new[] { "--dry-run", "--skip-dirty", "--release-as", "2.0.0" });

            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("", VersionSource.Default)]
        [InlineData("Random", VersionSource.Default)]
        [InlineData("Default", VersionSource.Default)]
        [InlineData("GitTag", VersionSource.GitTag)]
        [InlineData("Csproj", VersionSource.Csproj)]
        [InlineData("default", VersionSource.Default)]
        [InlineData("gitTag", VersionSource.GitTag)]
        [InlineData("csproj", VersionSource.Csproj)]
        public void ShouldConvertVersionSource(string versionSource, VersionSource expectedVersionSource)
        {
            var source = Program.ConvertVersionSource(versionSource);

            Assert.Equal(expectedVersionSource, source);
        }
    }
}
