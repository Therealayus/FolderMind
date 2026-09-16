using AIFolderAssistant.Core.BusinessRules;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class PathValidatorTests
{
    [Fact]
    public void EmptyPath_Rejected()
    {
        var (ok, _) = PathValidator.ValidateFolderPath("");
        Assert.False(ok);
    }

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files")]
    public void SystemDirectories_Rejected(string path)
    {
        var (ok, error) = PathValidator.ValidateFolderPath(path);
        Assert.False(ok);
        Assert.Contains("System", error ?? string.Empty);
    }

    [Fact]
    public void ExistingTempDir_Accepted()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var (ok, error) = PathValidator.ValidateFolderPath(dir, mustExist: true);
            Assert.True(ok, error);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void MissingDir_WithMustExist_Rejected()
    {
        var (ok, _) = PathValidator.ValidateFolderPath(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), mustExist: true);
        Assert.False(ok);
    }

    [Theory]
    [InlineData("ok", true)]
    [InlineData("", false)]
    [InlineData("a/b", false)]
    [InlineData("trailing.", false)]
    public void NewFolderNames_Validated(string name, bool expected)
    {
        Assert.Equal(expected, PathValidator.ValidateNewFolderName(name).Ok);
    }
}
