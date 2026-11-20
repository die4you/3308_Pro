using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("2.6", "v2.6.0", false)]
    [InlineData("2.6.0", "v2.6.0", false)]
    [InlineData("2.6.0", "2.6", false)]
    [InlineData("2.6.0", "2.6.0.0", false)]
    [InlineData("2.6.0", "v2.5.0", false)]
    [InlineData("2.6.0", "v2.5.9", false)]
    [InlineData("2.6.0", "v2.6.1", true)]
    [InlineData("2.6", "v2.6.1", true)]
    [InlineData("2.6.0", "v2.7", true)]
    [InlineData("2.6.0", "v2.7.0", true)]
    [InlineData("2.6.0", "v3.0.0", true)]
    public void VersionComparison_ShouldCorrectlyDetectUpdate(string current, string remote, bool expectedUpdateAvailable)
    {
        var currentVer = GitHubUpdateService.ParseVersion(current);
        var remoteVer = GitHubUpdateService.ParseVersion(remote);

        bool isUpdateAvailable = remoteVer > currentVer;

        Assert.Equal(expectedUpdateAvailable, isUpdateAvailable);
    }

    [Fact]
    public void ParseVersion_ShouldNormalizeMissingPartsToZero()
    {
        var vTwoParts = GitHubUpdateService.ParseVersion("2.6");
        var vThreeParts = GitHubUpdateService.ParseVersion("v2.6.0");
        var vFourParts = GitHubUpdateService.ParseVersion("2.6.0.0");

        Assert.Equal(vTwoParts, vThreeParts);
        Assert.Equal(vThreeParts, vFourParts);
    }
}
