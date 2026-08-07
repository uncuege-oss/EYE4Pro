using Kst.Acoustics.Metraj.Filters;
using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Tests;

public class FilterTests
{
    [Fact]
    public void LayerFilter_ExactMatch_ShouldWork()
    {
        var filter = new LayerFilter("WALLS");
        var entity = new MetrajLine("WALLS", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatch = new MetrajLine("WINDOWS", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void LayerFilter_RegexMatch_ShouldWork()
    {
        var filter = new LayerFilter("^A-.*", useRegex: true);
        var entity = new MetrajLine("A-WALL", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatch = new MetrajLine("B-WALL", new MetrajColor(256), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void ColorFilter_AciMatch_ShouldWork()
    {
        var filter = new ColorFilter(1); // Red
        var entity = new MetrajLine("0", new MetrajColor(1), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatch = new MetrajLine("0", new MetrajColor(2), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void ColorFilter_RgbMatch_ShouldWork()
    {
        var filter = new ColorFilter(255, 0, 0);
        var entity = new MetrajLine("0", new MetrajColor(1, 255, 0, 0), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatch = new MetrajLine("0", new MetrajColor(1, 0, 255, 0), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void TextFilter_ExactMatch_ShouldWork()
    {
        var filter = new TextFilter("ROOM 101");
        var entity = new MetrajText("0", new MetrajColor(256), "ROOM 101", new MetrajPoint(0,0,0));
        var nonMatch = new MetrajText("0", new MetrajColor(256), "ROOM 102", new MetrajPoint(0,0,0));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void TextFilter_RegexMatch_ShouldWork()
    {
        var filter = new TextFilter(@"\d+", useRegex: true);
        var entity = new MetrajText("0", new MetrajColor(256), "123", new MetrajPoint(0,0,0));
        var nonMatch = new MetrajText("0", new MetrajColor(256), "ABC", new MetrajPoint(0,0,0));

        Assert.True(filter.IsMatch(entity));
        Assert.False(filter.IsMatch(nonMatch));
    }

    [Fact]
    public void FilterCombinator_AndLogic_ShouldWork()
    {
        var combinator = new FilterCombinator(FilterLogic.And);
        combinator.Add(new LayerFilter("WALLS"));
        combinator.Add(new ColorFilter(1)); // Red

        var match = new MetrajLine("WALLS", new MetrajColor(1), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatchLayer = new MetrajLine("DOORS", new MetrajColor(1), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatchColor = new MetrajLine("WALLS", new MetrajColor(2), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(combinator.IsMatch(match));
        Assert.False(combinator.IsMatch(nonMatchLayer));
        Assert.False(combinator.IsMatch(nonMatchColor));
    }

    [Fact]
    public void FilterCombinator_OrLogic_ShouldWork()
    {
        var combinator = new FilterCombinator(FilterLogic.Or);
        combinator.Add(new LayerFilter("WALLS"));
        combinator.Add(new ColorFilter(1)); // Red

        var matchLayer = new MetrajLine("WALLS", new MetrajColor(2), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var matchColor = new MetrajLine("DOORS", new MetrajColor(1), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));
        var nonMatch = new MetrajLine("DOORS", new MetrajColor(2), new MetrajPoint(0,0,0), new MetrajPoint(1,1,1));

        Assert.True(combinator.IsMatch(matchLayer));
        Assert.True(combinator.IsMatch(matchColor));
        Assert.False(combinator.IsMatch(nonMatch));
    }
}
