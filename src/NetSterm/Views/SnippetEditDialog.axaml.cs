using Avalonia.Controls;
using Avalonia.Interactivity;
using NetSterm.Models;

namespace NetSterm.Views;

public partial class SnippetEditDialog : Window
{
    public CommandSnippet? Result { get; private set; }

    private readonly CommandSnippet? _existing;

    public SnippetEditDialog() : this(null)
    {
    }

    public SnippetEditDialog(CommandSnippet? existing)
    {
        InitializeComponent();
        _existing = existing;

        if (existing != null)
        {
            Title = "Edit Snippet";
            NameBox.Text = existing.Name;
            CommandBox.Text = existing.Command;
            DescriptionBox.Text = existing.Description ?? "";
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        NameBox.Focus();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ValidationMessage.Text = "Name is required.";
            ValidationMessage.IsVisible = true;
            NameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandBox.Text))
        {
            ValidationMessage.Text = "Command is required.";
            ValidationMessage.IsVisible = true;
            CommandBox.Focus();
            return;
        }

        Result = new CommandSnippet
        {
            Id = _existing?.Id ?? Guid.NewGuid().ToString(),
            Name = NameBox.Text.Trim(),
            Command = CommandBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text)
                ? null
                : DescriptionBox.Text.Trim(),
            FolderId = _existing?.FolderId,
            CreatedAt = _existing?.CreatedAt ?? DateTime.UtcNow
        };

        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
