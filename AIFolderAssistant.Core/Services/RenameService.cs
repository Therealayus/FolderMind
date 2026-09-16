using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.Core.Services;

/// <summary>
/// Safe rename workflow: verify source, validate destination, never overwrite
/// (auto-suffixes <c>Name (2)</c>), verify success, record history.
/// Never renames automatically — only via explicit user action.
/// </summary>
public sealed class RenameService : IRenameService
{
    private readonly IRenameHistoryRecorder _history;
    private readonly ILogger<RenameService> _logger;

    public RenameService(IRenameHistoryRecorder history, ILogger<RenameService> logger)
    {
        _history = history;
        _logger = logger;
    }

    public Task<RenameResult> RenameAsync(string folderPath, string newName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var (pathOk, pathError) = PathValidator.ValidateFolderPath(folderPath, mustExist: true);
        if (!pathOk)
            return Task.FromResult(Fail(folderPath, string.Empty, pathError ?? "Invalid folder."));

        // Explicit blank input is rejected (sanitizer fallback is for AI output, not user input).
        if (string.IsNullOrWhiteSpace(newName))
            return Task.FromResult(Fail(folderPath, string.Empty, "Folder name must not be empty."));

        var sanitized = NameSanitizer.Sanitize(newName);
        var (nameOk, nameError) = PathValidator.ValidateNewFolderName(sanitized);
        if (!nameOk)
            return Task.FromResult(Fail(folderPath, sanitized, nameError ?? "Invalid name."));

        try
        {
            var fullSource = Path.GetFullPath(folderPath);
            var parent = Path.GetDirectoryName(fullSource);
            if (parent is null)
                return Task.FromResult(Fail(folderPath, sanitized, "Cannot determine parent directory."));

            var currentName = Path.GetFileName(fullSource.TrimEnd(Path.DirectorySeparatorChar));
            if (currentName.Equals(sanitized, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Fail(folderPath, sanitized, "The folder already has this name."));

            // Resolve collisions without overwriting: "Name (2)", "Name (3)", ...
            var dest = Path.Combine(parent, sanitized);
            var attempt = 1;
            var finalName = sanitized;
            while (Directory.Exists(dest) || File.Exists(dest))
            {
                attempt++;
                finalName = $"{sanitized} ({attempt})";
                dest = Path.Combine(parent, finalName);
                if (attempt > 99)
                    return Task.FromResult(Fail(folderPath, sanitized, "Too many colliding folder names."));
            }

            // Re-verify source still exists (race-condition guard) just before the move.
            if (!Directory.Exists(fullSource))
                return Task.FromResult(Fail(folderPath, finalName, "Source folder no longer exists."));

            Directory.Move(fullSource, dest);

            if (!Directory.Exists(dest) || Directory.Exists(fullSource))
                return Task.FromResult(Fail(folderPath, finalName, "Rename verification failed."));

            _history.Record(fullSource, currentName, finalName, success: true, errorMessage: null);
            _logger.LogInformation("Renamed {From} to {To}.", fullSource, dest);
            return Task.FromResult(new RenameResult { Success = true, NewPath = dest });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed for {Folder}.", folderPath);
            _history.Record(folderPath, Path.GetFileName(folderPath) ?? folderPath, sanitized, success: false, ex.Message);
            return Task.FromResult(Fail(folderPath, sanitized, ex.Message));
        }
    }

    public Task<UndoResult> UndoAsync(string folderPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var last = _history.GetLastForFolder(folderPath);
            if (last is null || !last.Success)
                return Task.FromResult(new UndoResult { Success = false, ErrorMessage = "No successful rename to undo." });

            // folderPath is the post-rename path (or its parent+NewName); original location:
            var currentDir = Directory.Exists(folderPath) ? Path.GetFullPath(folderPath) : null;
            if (currentDir is null)
                return Task.FromResult(new UndoResult { Success = false, ErrorMessage = "Current folder no longer exists." });

            var parent = Path.GetDirectoryName(currentDir);
            if (parent is null)
                return Task.FromResult(new UndoResult { Success = false, ErrorMessage = "Cannot determine parent directory." });

            var restorePath = Path.Combine(parent, last.OriginalName);
            // Safety: never rename back if the original path was reused.
            if (Directory.Exists(restorePath) || File.Exists(restorePath))
                return Task.FromResult(new UndoResult { Success = false, ErrorMessage = "Original path has been reused; undo is unsafe." });

            Directory.Move(currentDir, restorePath);
            _history.Record(currentDir, last.NewName, last.OriginalName, success: true, errorMessage: null);
            return Task.FromResult(new UndoResult { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Undo failed for {Folder}.", folderPath);
            return Task.FromResult(new UndoResult { Success = false, ErrorMessage = ex.Message });
        }
    }

    private static RenameResult Fail(string folderPath, string newName, string error)
        => new() { Success = false, ErrorMessage = error };
}
