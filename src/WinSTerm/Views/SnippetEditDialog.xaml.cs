using System.Windows;
using MahApps.Metro.Controls;
using WinSTerm.Models;

namespace WinSTerm.Views;

public partial class SnippetEditDialog : MetroWindow
{
    public CommandSnippet? Result { get; private set; }

    private readonly CommandSnippet? _existing;

    public SnippetEditDialog(CommandSnippet? existing = null)
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

        NameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandBox.Text))
        {
            MessageBox.Show("Command is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            CommandBox.Focus();
            return;
        }

        Result = new CommandSnippet
        {
            Id = _existing?.Id ?? Guid.NewGuid().ToString(),
            Name = NameBox.Text.Trim(),
            Command = CommandBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            FolderId = _existing?.FolderId,
            CreatedAt = _existing?.CreatedAt ?? DateTime.UtcNow
        };

        DialogResult = true;
        Close();
    }
}
