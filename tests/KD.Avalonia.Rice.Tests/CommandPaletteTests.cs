using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class CommandPaletteTests
{
    [Theory]
    [InlineData("theme", "Toggle theme")]
    [InlineData("TT", "Toggle theme")]
    [InlineData("tgth", "Toggle theme")]
    [InlineData("", "Anything")]
    public void Pattern_matches_when_its_characters_are_in_order(string pattern, string text) =>
        Assert.NotNull(FuzzyMatcher.Match(pattern, text));

    [Theory]
    [InlineData("tx", "Toggle theme")]
    [InlineData("eht", "Theme")]
    [InlineData("themes", "Theme")]
    public void Pattern_does_not_match_otherwise(string pattern, string text) =>
        Assert.Null(FuzzyMatcher.Match(pattern, text));

    [Fact]
    public void Word_starts_are_preferred()
    {
        var match = FuzzyMatcher.Match("tt", "Toggle theme");

        Assert.Equal([0, 7], match!.Value.Positions);
    }

    [Fact]
    public void Falls_back_when_word_starts_skip_needed_characters()
    {
        // Jumping to the word start "T" of "Tab" leaves no "a" after it; the plain pass finds T-a-b in "Table".
        var match = FuzzyMatcher.Match("tab", "Tablet Tb");

        Assert.Equal([0, 1, 2], match!.Value.Positions);
    }

    [Theory]
    [InlineData("theme", "Theme", "Toggle theme")]
    [InlineData("tt", "Toggle theme", "Bottom toolbar")]
    [InlineData("set", "Settings", "Reset layout")]
    public void Better_matches_score_higher(string pattern, string better, string worse) =>
        Assert.True(FuzzyMatcher.Match(pattern, better)!.Value.Score > FuzzyMatcher.Match(pattern, worse)!.Value.Score);

    [Theory]
    [InlineData("camelCase", 5, true)]
    [InlineData("snake_case", 6, true)]
    [InlineData("word", 2, false)]
    [InlineData("Ipv6", 3, true)]
    public void Word_starts_include_camel_case_and_separators(string text, int index, bool expected) =>
        Assert.Equal(expected, FuzzyMatcher.IsWordStart(text, index));

    [Fact]
    public void Search_ranks_and_highlights_the_title()
    {
        var toggle = new RiceCommandItem("Toggle theme", () => { });
        var settings = new RiceCommandItem("Theme settings", () => { });
        var about = new RiceCommandItem("About", () => { });

        var matches = CommandSearch.Search([toggle, settings, about], "theme", [], 50);

        Assert.Equal([settings, toggle], matches.Select(m => m.Command));
        Assert.Equal([0, 1, 2, 3, 4], matches[0].Highlights);
    }

    [Fact]
    public void Every_word_of_the_query_must_match()
    {
        var toggle = new RiceCommandItem("Toggle theme", () => { });
        var settings = new RiceCommandItem("Theme settings", () => { });

        var matches = CommandSearch.Search([toggle, settings], "theme set", [], 50);

        Assert.Equal([settings], matches.Select(m => m.Command));
    }

    [Fact]
    public void Category_keywords_and_description_are_searched_without_highlights()
    {
        var toggle = new RiceCommandItem("Toggle theme", () => { }) { Keywords = "dark light mode" };

        var match = Assert.Single(CommandSearch.Search([toggle], "dark", [], 50));

        Assert.Empty(match.Highlights);
    }

    [Fact]
    public void Empty_query_lists_recent_commands_first()
    {
        var a = new RiceCommandItem("A", () => { });
        var b = new RiceCommandItem("B", () => { });
        var c = new RiceCommandItem("C", () => { });
        var gone = new RiceCommandItem("Gone", () => { });

        var matches = CommandSearch.Search([a, b, c], "  ", [c, gone], 50);

        Assert.Equal([c, a, b], matches.Select(m => m.Command));
    }

    [Fact]
    public void Recent_commands_win_among_equal_matches()
    {
        var first = new RiceCommandItem("Open file", () => { });
        var second = new RiceCommandItem("Open fold", () => { });

        var matches = CommandSearch.Search([first, second], "open", [second], 50);

        Assert.Equal(second, matches[0].Command);
    }

    [Fact]
    public void Results_are_limited()
    {
        var commands = Enumerable.Range(0, 10).Select(i => new RiceCommandItem($"Item {i}", () => { }));

        Assert.Equal(3, CommandSearch.Search(commands, "item", [], 3).Count);
    }

    [Fact]
    public void Prefix_searches_only_the_entries_of_its_provider()
    {
        var command = new RiceCommandItem("Charts settings", () => { });
        var page = new RiceCommandItem("Charts", () => { });
        var pages = new RiceCommandProvider("@", "Pages", [page]);

        var (matches, mode) = CommandSearch.Run([command], [pages], "@ chr", [], 50, _ => { });

        Assert.Same(pages, mode);
        Assert.Equal([page], matches.Select(m => m.Command));
        Assert.Equal([0, 1, 3], matches[0].Highlights);
    }

    [Fact]
    public void Without_a_prefix_the_commands_are_searched()
    {
        var command = new RiceCommandItem("Charts settings", () => { });
        var pages = new RiceCommandProvider("@", "Pages", [new RiceCommandItem("Charts", () => { })]);

        var (matches, mode) = CommandSearch.Run([command], [pages], "chr", [], 50, _ => { });

        Assert.Null(mode);
        Assert.Equal([command], matches.Select(m => m.Command));
    }

    [Fact]
    public void Longest_prefix_wins()
    {
        var short1 = new RiceCommandProvider("#", "Lines", [new RiceCommandItem("Line", () => { })]);
        var long2 = new RiceCommandProvider("##", "Symbols", [new RiceCommandItem("Symbol", () => { })]);

        Assert.Same(long2, CommandSearch.Run([], [short1, long2], "##sy", [], 50, _ => { }).Mode);
        Assert.Same(short1, CommandSearch.Run([], [short1, long2], "#li", [], 50, _ => { }).Mode);
    }

    [Fact]
    public void Dynamic_providers_get_the_text_after_the_prefix()
    {
        string? received = null;
        var lines = new RiceCommandProvider
        {
            Prefix = "#",
            Title = "Go to line",
            FilterResults = false,
            GetCommands = text =>
            {
                received = text;
                return [new RiceCommandItem($"Go to line {text}", () => { })];
            },
        };

        var (matches, _) = CommandSearch.Run([], [lines], "#  42 ", [], 50, _ => { });

        Assert.Equal("42", received);
        Assert.Equal("Go to line 42", Assert.Single(matches).Command.Title);
    }

    [Fact]
    public void Question_mark_lists_the_modes_and_choosing_one_types_its_prefix()
    {
        string? typed = null;
        var pages = new RiceCommandProvider("@", "Go to a page", []);
        var commands = new RiceCommandProvider(">", "Commands", []);

        var (matches, mode) = CommandSearch.Run([], [pages, commands], "?", [], 50, q => typed = q);

        Assert.Null(mode);
        Assert.Equal(["Go to a page", "Commands"], matches.Select(m => m.Command.Title));
        Assert.All(matches, m => Assert.True(m.Command.KeepOpen));
        matches[0].Command.Invoke();
        Assert.Equal("@ ", typed);
    }

    [Fact]
    public void Question_mark_is_a_plain_query_without_modes()
    {
        var help = new RiceCommandItem("Help?", () => { });

        var (matches, _) = CommandSearch.Run([help], [], "?", [], 50, _ => { });

        Assert.Equal([help], matches.Select(m => m.Command));
    }

    [Fact]
    public void Invoke_runs_the_action_and_the_command()
    {
        var calls = new List<string>();
        var command = new RiceCommandItem("Run", () => calls.Add("action"))
        {
            Command = new TestCommand(() => calls.Add("command")),
        };

        command.Invoke();

        Assert.Equal(["action", "command"], calls);
    }

    [Fact]
    public void Disabled_commands_do_not_run()
    {
        var ran = false;
        var command = new RiceCommandItem("Run", () => ran = true) { Command = new TestCommand(() => { }, canExecute: false) };

        command.Invoke();

        Assert.False(command.CanInvoke());
        Assert.False(ran);
    }

    private sealed class TestCommand(Action execute, bool canExecute = true) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter) => execute();
    }
}
