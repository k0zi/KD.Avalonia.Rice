namespace KD.Avalonia.Rice.Controls;

/// <summary>Modal dialog with a <see cref="ThemeSelector"/>; the title bar's theme settings button opens it.</summary>
public partial class ThemeDialog : RiceWindow
{
    public ThemeDialog()
    {
        InitializeComponent();
        Title = RiceStrings.Theme;
        CloseButton.Content = RiceStrings.Close;
        CloseButton.Click += (_, _) => Close();
    }
}
