using Avalonia.Markup.Xaml;
using KD.Avalonia.Rice.Options;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Modal dialog showing the application name, author and version.</summary>
public partial class AboutDialog : RiceWindow
{
    // Required by the XAML loader / designer.
    public AboutDialog() : this(RiceAppInfo.Default)
    {
    }

    public AboutDialog(RiceAppInfo info)
    {
        InitializeComponent();
        AppInfo = info;
        Title = $"{RiceStrings.About} – {info.Name}";

        IconImage.Source = info.Icon;
        IconImage.IsVisible = info.Icon is not null;
        NameText.Text = info.Name;
        DescriptionText.Text = info.Description;
        DescriptionText.IsVisible = !string.IsNullOrWhiteSpace(info.Description);
        AuthorLabel.Text = RiceStrings.Author;
        AuthorText.Text = info.Author;
        AuthorLabel.IsVisible = AuthorText.IsVisible = !string.IsNullOrWhiteSpace(info.Author);
        VersionLabel.Text = RiceStrings.Version;
        VersionText.Text = info.Version;
        OkButton.Content = RiceStrings.Ok;
        OkButton.Click += (_, _) => Close();
    }
}
