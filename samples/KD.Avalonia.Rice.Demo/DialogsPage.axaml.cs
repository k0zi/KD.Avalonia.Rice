using Avalonia.Controls;
using KD.Avalonia.Rice.Dialogs;

namespace KD.Avalonia.Rice.Demo;

public partial class DialogsPage : UserControl
{
    private string _fileName = "report.md";

    public DialogsPage()
    {
        InitializeComponent();

        foreach (var icon in Enum.GetValues<DialogIcon>())
        {
            AddButton(IconButtons, icon.ToString(), async () =>
                await RiceDialog.ShowAsync($"A dialog with the {icon} icon. The message wraps when it is longer than the width of the dialog.",
                    icon, icon == DialogIcon.Question ? DialogButtons.YesNo : DialogButtons.Ok, owner: Owner));
        }
        foreach (var buttons in Enum.GetValues<DialogButtons>())
        {
            AddButton(ButtonSetButtons, buttons.ToString(), async () =>
                await RiceDialog.ShowAsync($"The {buttons} button set. Enter clicks the accented button, Esc the one that backs out.",
                    DialogIcon.Information, buttons, owner: Owner));
        }

        CustomButtonsButton.Click += async (_, _) => await ShowCustomButtons();
        CustomViewButton.Click += async (_, _) => await ShowCustomView();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void AddButton(Panel panel, string text, Func<Task<DialogResult>> show)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => ShowResult(await show());
        panel.Children.Add(button);
    }

    private async Task ShowCustomButtons()
    {
        var saved = 0;
        var result = await RiceDialog.ShowAsync(new DialogOptions
        {
            Icon = DialogIcon.Warning,
            Message = $"Save the changes of \"{_fileName}\" before closing?",
            Buttons =
            [
                new DialogButton("Save", "saved") { IsAccent = true, IsDefault = true, Command = new RelayCommand(() => saved++) },
                new DialogButton("Don't save", "discarded"),
                new DialogButton(RiceStrings.Cancel) { IsCancel = true },
            ],
        }, Owner);
        ShowResult(result is null ? "cancelled" : $"{result} (Save command ran {saved}×)");
    }

    private async Task ShowCustomView()
    {
        var viewModel = new RenameDialogViewModel(_fileName);
        var result = await RiceDialog.ShowAsync<RenameDialogView>(viewModel,
        [
            new DialogButton("Rename") { IsAccent = true, IsDefault = true, Command = viewModel.RenameCommand, ClosesDialog = false },
            DialogButton.For(DialogResult.Cancel),
        ], DialogIcon.Question, owner: Owner);
        if (result is string name)
            _fileName = name;
        ShowResult(result ?? "–");
    }

    private void ShowResult(object? result) => ResultText.Text = $"Result: {result}";
}
