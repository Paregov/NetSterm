using Avalonia.Controls;
using WinSTerm.Models;

namespace WinSTerm.Views;

public partial class ExportDialog : Window
{
    /// <summary>
    /// The export options result set when the user confirms export.
    /// Null if the dialog was cancelled.
    /// </summary>
    public ExportOptions? Result { get; set; }

    public ExportDialog()
    {
        InitializeComponent();
    }
}
