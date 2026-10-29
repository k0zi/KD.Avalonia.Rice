using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class PropertyGridTests
{
    [Theory]
    [InlineData("FontSize", "Font size")]
    [InlineData("HTTPProxy", "HTTP proxy")]
    [InlineData("Ipv6Address", "Ipv6 address")]
    [InlineData("snake_case_name", "snake case name")]
    [InlineData("Name", "Name")]
    [InlineData("UseHTTP", "Use HTTP")]
    public void Names_are_humanized(string name, string expected) =>
        Assert.Equal(expected, RicePropertyEntry.Humanize(name));

    [Fact]
    public void Attributes_set_name_category_description_and_read_only()
    {
        var entries = RicePropertyEntry.FromType(typeof(Sample)).ToDictionary(e => e.Name);

        var size = entries[nameof(Sample.FontSize)];
        Assert.Equal("Size", size.DisplayName);
        Assert.Equal("Text", size.Category);
        Assert.Equal("Height of the letters.", size.Description);
        Assert.Equal(6, size.Minimum);
        Assert.Equal(72, size.Maximum);
        Assert.False(size.IsReadOnly);

        Assert.Equal("Shown name", entries[nameof(Sample.Title)].DisplayName);
        Assert.Equal("Look", entries[nameof(Sample.Title)].Category);
        Assert.Equal(RiceStrings.PropertyGridMisc, entries[nameof(Sample.IsEnabled)].Category);
        Assert.Equal("Is enabled", entries[nameof(Sample.IsEnabled)].DisplayName);
        Assert.True(entries[nameof(Sample.Id)].IsReadOnly);
        Assert.True(entries[nameof(Sample.Locked)].IsReadOnly);
    }

    [Fact]
    public void Hidden_indexer_and_non_public_properties_are_left_out()
    {
        var names = RicePropertyEntry.FromType(typeof(Sample)).Select(e => e.Name).ToList();

        Assert.DoesNotContain(nameof(Sample.Hidden), names);
        Assert.DoesNotContain(nameof(Sample.NotGenerated), names);
        Assert.DoesNotContain("Item", names);
        Assert.DoesNotContain("Secret", names);
    }

    [Fact]
    public void Base_class_properties_come_first()
    {
        var names = RicePropertyEntry.FromType(typeof(Derived)).Select(e => e.Name).ToList();

        Assert.True(names.IndexOf(nameof(Sample.FontSize)) < names.IndexOf(nameof(Derived.Extra)));
    }

    [Fact]
    public void Values_are_read_and_written()
    {
        var sample = new Sample();
        var entry = RicePropertyEntry.FromType(typeof(Sample)).Single(e => e.Name == nameof(Sample.FontSize));

        entry.SetValue(sample, 20.0);

        Assert.Equal(20.0, entry.GetValue(sample));
    }

    private class Sample
    {
        [Category("Text"), DisplayName("Size"), Description("Height of the letters."), Range(6, 72)]
        public double FontSize { get; set; } = 12;

        [Display(Name = "Shown name", GroupName = "Look")]
        public string Title { get; set; } = "";

        public bool IsEnabled { get; set; }

        public Guid Id { get; } = Guid.NewGuid();

        [ReadOnly(true)]
        public string Locked { get; set; } = "";

        [Browsable(false)]
        public int Hidden { get; set; }

        [Display(AutoGenerateField = false)]
        public int NotGenerated { get; set; }

        public int this[int index] => index;

        private string Secret { get; set; } = "";
    }

    private class Derived : Sample
    {
        public int Extra { get; set; }
    }
}
