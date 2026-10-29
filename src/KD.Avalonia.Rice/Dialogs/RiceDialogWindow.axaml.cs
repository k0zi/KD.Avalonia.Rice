using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using KD.Avalonia.Rice.Controls;
using Material.Icons;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>
/// Dialog window without a title bar: an optional icon, the message, optional custom content and the buttons.
/// Usually opened through <see cref="RiceDialog"/>; drag it by its background.
/// </summary>
public partial class RiceDialogWindow : RiceWindow
{
    private readonly IDialogContent? _dialogContent;

    // Required by the XAML loader / designer.
    public RiceDialogWindow() : this(new DialogOptions())
    {
    }

    public RiceDialogWindow(DialogOptions options, double renderScaling = 1)
    {
        InitializeComponent();
        if (options.Title is { } title)
            Title = title;
        Width = options.Width;
        MaxWidth = options.MaxWidth;

        ShowIcon(options.Icon, renderScaling);
        MessageText.Text = options.Message;
        MessageText.IsVisible = !string.IsNullOrEmpty(options.Message);
        ContentHost.Content = options.Content;
        ContentHost.ContentTemplate = options.ContentTemplate;
        ContentHost.IsVisible = options.Content is not null;

        foreach (var button in options.Buttons)
        {
            ButtonsPanel.Children.Add(CreateButton(button));
            if (button.IsCancel)
                Result = button.Result;
        }

        _dialogContent = options.Content as IDialogContent ?? (options.Content as Control)?.DataContext as IDialogContent;
        if (_dialogContent is not null)
            _dialogContent.CloseRequested += OnCloseRequested;
    }

    /// <summary>
    /// Result of the button that closed the dialog, or of the Esc button (else <see cref="DialogResult.None"/>)
    /// when it was closed otherwise.
    /// </summary>
    public object? Result { get; private set; } = DialogResult.None;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_dialogContent is not null)
            _dialogContent.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, object? result) => CloseWith(result);

    private void CloseWith(object? result)
    {
        Result = result;
        Close(result);
    }

    private void ShowIcon(DialogIcon icon, double renderScaling)
    {
        IconPresenter.IsVisible = icon != DialogIcon.None;
        if (icon == DialogIcon.None)
            return;
        var image = SystemDialogIcons.Get(icon, (int)IconPresenter.Width, renderScaling);
        if (image is not null)
        {
            IconPresenter.Content = image;
            return;
        }
        (IconPresenter.Content, var colorClass) = icon switch
        {
            DialogIcon.Question => (MaterialIconKind.HelpCircle, "accent"),
            DialogIcon.Information => (MaterialIconKind.Information, "accent"),
            DialogIcon.Warning => (MaterialIconKind.Alert, "warning"),
            _ => (MaterialIconKind.CloseCircle, "danger"),
        };
        IconPresenter.Classes.Add(colorClass);
    }

    private Button CreateButton(DialogButton button)
    {
        var control = new Button
        {
            Content = button.Text,
            MinWidth = 88,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = button.IsDefault,
            IsCancel = button.IsCancel,
            Command = new ButtonCommand(this, button),
            CommandParameter = button.CommandParameter,
        };
        control.Classes.Set("accent", button.IsAccent);
        return control;
    }

    // Runs the button's own command first, then closes the dialog; CanExecute comes from the button's command.
    private sealed class ButtonCommand(RiceDialogWindow window, DialogButton button) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add
            {
                if (button.Command is not null)
                    button.Command.CanExecuteChanged += value;
            }
            remove
            {
                if (button.Command is not null)
                    button.Command.CanExecuteChanged -= value;
            }
        }

        public bool CanExecute(object? parameter) => button.Command?.CanExecute(parameter) ?? true;

        public void Execute(object? parameter)
        {
            button.Command?.Execute(parameter);
            if (button.ClosesDialog)
                window.CloseWith(button.Result);
        }
    }
}
