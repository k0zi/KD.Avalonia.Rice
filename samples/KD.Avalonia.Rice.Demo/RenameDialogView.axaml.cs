using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KD.Avalonia.Rice.Demo;

public partial class RenameDialogView : UserControl
{
    public RenameDialogView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        NameBox.Focus();
        NameBox.SelectAll();
    }
}
