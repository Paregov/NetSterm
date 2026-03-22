using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NetSterm.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        base.OnKeyDown(e);
    }
}
