using SR.ModRimWorld.FactionalWar;
using Xunit;

namespace FactionalWar.Tests;

public class BattleScaleTests
{
    [Theory]
    [InlineData(1000f, 1f, 1000f)]
    [InlineData(1000f, 0.1f, 100f)]
    [InlineData(1000f, 2f, 2000f)]
    [InlineData(450f, 0.5f, 225f)]
    public void The_threat_point_factor_scales_the_points_of_a_battle(float points, float factor, float expected) {
        Assert.Equal(expected, BattleScale.Points(points, factor), 3);
    }

    [Theory]
    [InlineData(0.09f)]
    [InlineData(2.01f)]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    public void A_factor_outside_the_slider_range_is_refused(float factor) {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleScale.Points(1000f, factor));
    }

    [Fact]
    public void The_defaults_are_the_originals() {
        Assert.Equal(1f, BattleScale.DefaultThreatPointFactor);
        Assert.True(BattleScale.DefaultNeedOptimization);
        Assert.Equal(0.1f, BattleScale.MinThreatPointFactor);
        Assert.Equal(2f, BattleScale.MaxThreatPointFactor);
    }
}
