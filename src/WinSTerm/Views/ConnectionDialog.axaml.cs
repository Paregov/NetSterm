using Avalonia.Controls;
using WinSTerm.Models;

namespace WinSTerm.Views;

public partial class ConnectionDialog : Window
{
    /// <summary>
    /// The connection info result set when the user saves the dialog.
    /// Null if the dialog was cancelled.
    /// </summary>
    public ConnectionInfo? Result { get; set; }

    public ConnectionDialog()
    {
        InitializeComponent();
    }

    public ConnectionDialog(ConnectionInfo existing) : this()
    {
        // TODO: Populate dialog fields from existing connection info
        // when dialog UI is fully ported
    }
}
