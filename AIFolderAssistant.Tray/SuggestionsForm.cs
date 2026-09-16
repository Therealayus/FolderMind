using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace AIFolderAssistant.Tray;

/// <summary>
/// Review window for pending suggestions: rename (explicit + safe),
/// ignore, refresh. Closing returns to tray — it never exits the app.
/// Built in code (no designer/resx) so it compiles with the .NET SDK alone.
/// </summary>
internal sealed class SuggestionsForm : Form
{
    private readonly IServiceProvider _services;
    private readonly ListBox _list;
    private readonly Label _status;
    private readonly Button _renameButton;
    private readonly Button _ignoreButton;
    private List<Suggestion> _items = new();

    internal SuggestionsForm(IServiceProvider services)
    {
        _services = services;
        Text = "FolderMind AI — Suggestions";
        Size = new Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        var hint = new Label
        {
            Text = "FolderMind never renames without your confirmation.",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0)
        };
        _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _status = new Label { Dock = DockStyle.Bottom, Height = 26, Padding = new Padding(12, 4, 0, 0) };

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 6, 12, 6)
        };
        var close = new Button { Text = "Close", Width = 90, DialogResult = DialogResult.Cancel };
        _ignoreButton = new Button { Text = "Ignore", Width = 90 };
        _ignoreButton.Click += async (_, _) => await IgnoreSelectedAsync();
        _renameButton = new Button { Text = "Rename Folder", Width = 120 };
        _renameButton.Click += async (_, _) => await RenameSelectedAsync();
        var refresh = new Button { Text = "Refresh", Width = 90 };
        refresh.Click += (_, _) => RefreshList();
        bar.Controls.AddRange(new Control[] { close, _ignoreButton, _renameButton, refresh });

        Controls.AddRange(new Control[] { _list, bar, _status, hint });
        CancelButton = close;
        Load += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        try
        {
            var repo = _services.GetRequiredService<ISuggestionRepository>();
            _items = repo.GetByStatus(SuggestionStatus.Pending).OrderByDescending(s => s.CreatedAt).ToList();
            _list.Items.Clear();
            foreach (var s in _items)
                _list.Items.Add($"{s.OriginalName}  →  {s.SuggestedName}   ({s.Confidence:P0})");
            _status.Text = _items.Count == 0 ? "No suggestions yet." : $"{_items.Count} pending suggestion(s).";
            UpdateButtons();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not load suggestions: {ex.Message}";
        }
    }

    private void UpdateButtons()
    {
        var has = _list.SelectedIndex >= 0;
        _renameButton.Enabled = has;
        _ignoreButton.Enabled = has;
    }

    private async Task RenameSelectedAsync()
    {
        if (_list.SelectedIndex < 0)
            return;
        var item = _items[_list.SelectedIndex];
        var confirm = MessageBox.Show(this,
            $"Rename folder?\n\nFrom: {item.OriginalName}\nTo:   {item.SuggestedName}\n\nIn: {item.FolderPath}",
            "FolderMind AI — Confirm rename", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (confirm != DialogResult.OK)
            return;
        var rename = _services.GetRequiredService<IRenameService>();
        var result = await rename.RenameAsync(item.FolderPath, item.SuggestedName, CancellationToken.None);
        var suggestions = _services.GetRequiredService<ISuggestionRepository>();
        if (result.Success)
        {
            suggestions.UpdateStatus(item.Id, SuggestionStatus.Accepted, DateTime.UtcNow);
            _status.Text = $"Renamed to “{item.SuggestedName}”.";
        }
        else
        {
            _status.Text = $"Rename failed: {result.ErrorMessage}";
        }
        RefreshList();
    }

    private async Task IgnoreSelectedAsync()
    {
        if (_list.SelectedIndex < 0)
            return;
        await Task.Yield();
        var item = _items[_list.SelectedIndex];
        _services.GetRequiredService<ISuggestionRepository>()
            .UpdateStatus(item.Id, SuggestionStatus.Ignored, DateTime.UtcNow);
        RefreshList();
    }
}
