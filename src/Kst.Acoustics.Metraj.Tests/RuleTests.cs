using Kst.Acoustics.Metraj.Rules;
using Kst.Acoustics.Metraj.Metrics;
using Kst.Acoustics.Metraj.Filters;

namespace Kst.Acoustics.Metraj.Tests;

public class RuleTests
{
    [Fact]
    public void RuleSet_Serialization_ShouldWork()
    {
        var rs = new RuleSet
        {
            TargetUnit = MetrajUnit.Meters,
            LengthTolerance = 1.5,
            TemplatePaths = new List<string> { "template1.dxf" }
        };
        rs.Rules.Add(new MetrajRule { Name = "Wall", LayerPattern = "WALL.*", Combinator = FilterLogic.Or });

        string json = rs.ToJson();
        var deserialized = RuleSet.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(MetrajUnit.Meters, deserialized.TargetUnit);
        Assert.Equal(1.5, deserialized.LengthTolerance);
        Assert.Single(deserialized.TemplatePaths);
        Assert.Equal("Wall", deserialized.Rules[0].Name);
        Assert.Equal(FilterLogic.Or, deserialized.Rules[0].Combinator);
    }

    [Fact]
    public void RuleSet_RGB_Serialization_ShouldWork()
    {
        var rs = new RuleSet();
        rs.Rules.Add(new MetrajRule { Name = "RGB", ColorR = 255, ColorG = 128, ColorB = 0 });

        string json = rs.ToJson();
        var deserialized = RuleSet.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal((byte?)255, deserialized.Rules[0].ColorR);
        Assert.Equal((byte?)128, deserialized.Rules[0].ColorG);
        Assert.Equal((byte?)0, deserialized.Rules[0].ColorB);
    }

    [Fact]
    public void RuleSetStore_SaveAndLoad_ShouldWork()
    {
        string dwgPath = "test_project.dwg";
        string configPath = "test_project.metraj-rules.json";

        var rs = new RuleSet { LengthTolerance = 3.3 };
        RuleSetStore.Save(dwgPath, rs);

        Assert.True(File.Exists(configPath));

        var loaded = RuleSetStore.Load(dwgPath);
        Assert.Equal(3.3, loaded.LengthTolerance);

        if (File.Exists(configPath)) File.Delete(configPath);
    }
}
