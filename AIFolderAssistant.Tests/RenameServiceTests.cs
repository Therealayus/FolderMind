using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class RenameServiceTests : IDisposable
{
    private readonly string _root;
    private readonly InMemoryHistory _history = new();
    private readonly RenameService _service;

    public RenameServiceTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "fmtest-" + Guid.NewGuid().ToString("N"))).FullName;
        _service = new RenameService(_history, NullLogger<RenameService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Rename_WorksAndRecordsHistory()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;
        var result = await _service.RenameAsync(source, "Financial Documents", CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(Directory.Exists(result.NewPath));
        Assert.False(Directory.Exists(source));
        var last = _history.Last;
        Assert.NotNull(last);
        Assert.Equal("New Folder", last!.OriginalName);
        Assert.Equal("Financial Documents", last.NewName);
        Assert.True(last.Success);
    }

    [Fact]
    public async Task Rename_Collision_AutoSuffixesWithoutOverwrite()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Financial Documents"));
        var source = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;

        var result = await _service.RenameAsync(source, "Financial Documents", CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.EndsWith("Financial Documents (2)", result.NewPath);
        Assert.True(Directory.Exists(Path.Combine(_root, "Financial Documents")));
    }

    [Fact]
    public async Task Rename_MissingSource_FailsGracefully()
    {
        var result = await _service.RenameAsync(Path.Combine(_root, "Nope"), "Whatever", CancellationToken.None);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Rename_InvalidName_FailsGracefully()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;
        var result = await _service.RenameAsync(source, "   ", CancellationToken.None);
        Assert.False(result.Success);
        Assert.True(Directory.Exists(source));
    }

    [Fact]
    public async Task Undo_RestoresOriginalName()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;
        var renamed = await _service.RenameAsync(source, "Financial Documents", CancellationToken.None);
        Assert.True(renamed.Success);

        var undo = await _service.UndoAsync(renamed.NewPath!, CancellationToken.None);
        Assert.True(undo.Success, undo.ErrorMessage);
        Assert.True(Directory.Exists(source));
    }

    [Fact]
    public async Task Undo_RefusesWhenOriginalPathReused()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;
        var renamed = await _service.RenameAsync(source, "Financial Documents", CancellationToken.None);
        Assert.True(renamed.Success);
        Directory.CreateDirectory(source); // someone reused the original path

        var undo = await _service.UndoAsync(renamed.NewPath!, CancellationToken.None);
        Assert.False(undo.Success);
        Assert.Contains("reused", undo.ErrorMessage ?? string.Empty);
    }

    private sealed class InMemoryHistory : IRenameHistoryRecorder
    {
        public RenameHistoryEntry? Last { get; private set; }
        public void Record(string folderPath, string originalName, string newName, bool success, string? errorMessage) =>
            Last = new RenameHistoryEntry
            {
                FolderPath = folderPath,
                OriginalName = originalName,
                NewName = newName,
                Success = success,
                CreatedAt = DateTime.UtcNow
            };
        public RenameHistoryEntry? GetLastForFolder(string folderPath) => Last;
    }
}
