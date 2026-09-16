using AIFolderAssistant.Core.BusinessRules;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class NameSanitizerTests
{
    [Theory]
    [InlineData("Financial <Documents>", "Financial Documents")]
    [InlineData("Invoices: Receipts?", "Invoices Receipts")]
    [InlineData("  Spaced   Out  ", "Spaced Out")]
    [InlineData("Trailing...", "Trailing")]
    public void StripsIllegalContent(string raw, string expected)
    {
        Assert.Equal(expected, NameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_ReturnsFallback(string? raw)
    {
        Assert.Equal("Untitled Folder", NameSanitizer.Sanitize(raw));
    }

    [Fact]
    public void ReservedName_GetsSuffixed()
    {
        Assert.Equal("CON Folder", NameSanitizer.Sanitize("CON"));
    }

    [Fact]
    public void LongName_IsTruncated()
    {
        var long_ = new string('a', 500);
        Assert.True(NameSanitizer.Sanitize(long_).Length <= NameSanitizer.MaxNameLength);
    }

    [Fact]
    public void ValidName_Unchanged()
    {
        Assert.Equal("Financial Documents", NameSanitizer.Sanitize("Financial Documents"));
    }
}
