using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class RibbonKeyTipTests
{
    [Fact]
    public void Key_tips_come_from_the_word_initials_then_the_other_letters()
    {
        var tips = RiceRibbonKeyTips.Assign([(null, "Cut"), (null, "Copy"), (null, "Format painter"), (null, "Paste special")]);

        Assert.Equal(["C", "O", "F", "P"], tips);
    }

    [Fact]
    public void Explicit_key_tips_win_and_no_tip_is_the_start_of_another()
    {
        var tips = RiceRibbonKeyTips.Assign([(null, "Font"), ("fn", null), (null, "Find")]);

        // "F" would be the start of "FN": Font gets its next letter, Find the next free one.
        Assert.Equal(["O", "FN", "I"], tips);
    }

    [Fact]
    public void Items_without_a_usable_label_get_digits()
    {
        var tips = RiceRibbonKeyTips.Assign([(null, null), (null, ""), (null, "A"), (null, "A")]);

        Assert.Equal(["1", "2", "A", "3"], tips);
    }

    [Fact]
    public void Shortcut_text_in_tooltips_does_not_get_in_the_way()
    {
        var tips = RiceRibbonKeyTips.Assign([(null, "Bold (Ctrl+B)"), (null, "Italic (Ctrl+I)")]);

        Assert.Equal(["B", "I"], tips);
    }
}
