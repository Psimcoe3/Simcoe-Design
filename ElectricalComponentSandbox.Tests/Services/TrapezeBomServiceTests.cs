using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class TrapezeBomServiceTests
{
    private static HangerComponent MakeHanger(TrapezeAssembly trapeze)
    {
        return new HangerComponent { Trapeze = trapeze };
    }

    [Fact]
    public void GenerateBom_NullHanger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TrapezeBomService.GenerateBom((HangerComponent)null!));
    }

    [Fact]
    public void GenerateBom_NullEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TrapezeBomService.GenerateBom((IEnumerable<HangerComponent>)null!));
    }

    [Fact]
    public void GenerateBom_HangerWithoutTrapeze_ReturnsEmptyBom()
    {
        var hanger = new HangerComponent();

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.Empty(bom.Lines);
    }

    [Fact]
    public void GenerateBom_SingleTierDefault_HasStrutRodAndHardware()
    {
        var hanger = MakeHanger(TrapezeAssembly.CreateSingleTierDefault());

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.NotEmpty(bom.Struts);
        Assert.NotEmpty(bom.Rods);
        Assert.NotEmpty(bom.Hardware);
        // Two rods on the default
        Assert.Equal(2, bom.Rods.Sum(r => r.Quantity));
    }

    [Fact]
    public void GenerateBom_SingleTier_RodLengthAccumulatesAcrossBothRods()
    {
        var hanger = MakeHanger(TrapezeAssembly.CreateSingleTierDefault());

        var bom = TrapezeBomService.GenerateBom(hanger);

        // Default rods are 24" each → total 48"
        Assert.Equal(48.0, bom.Rods.Sum(r => r.TotalLengthInches), 3);
    }

    [Fact]
    public void GenerateBom_MultiTier_StrutCountMatchesTiers()
    {
        var trapeze = TrapezeAssembly.CreateMultiTier(tierCount: 3);
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.Equal(3, bom.Struts.Sum(s => s.Quantity));
    }

    [Fact]
    public void GenerateBom_MultiTier_HexNutsScaleWithTierCount()
    {
        // 2 rods × 2 nuts per tier × 4 tiers = 16 hex nuts
        var trapeze = TrapezeAssembly.CreateMultiTier(tierCount: 4);
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        var hexNutLine = bom.Lines.Single(l => l.ItemCode.StartsWith("HW-HXN-"));
        Assert.Equal(16, hexNutLine.Quantity);
    }

    [Fact]
    public void GenerateBom_BackToBackStrut_DoublesStrutCount()
    {
        var trapeze = TrapezeAssembly.CreateSingleTierDefault();
        trapeze.Tiers[0].BackToBack = true;
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.Equal(2, bom.Struts.Sum(s => s.Quantity));
    }

    [Fact]
    public void GenerateBom_AcrossMultipleHangers_AggregatesQuantities()
    {
        var h1 = MakeHanger(TrapezeAssembly.CreateMultiTier(tierCount: 2));
        var h2 = MakeHanger(TrapezeAssembly.CreateMultiTier(tierCount: 2));

        var bom = TrapezeBomService.GenerateBom(new[] { h1, h2 });

        // 2 tiers × 2 hangers = 4 struts
        Assert.Equal(4, bom.Struts.Sum(s => s.Quantity));
        // 2 rods × 2 hangers = 4 rods
        Assert.Equal(4, bom.Rods.Sum(r => r.Quantity));
    }

    [Fact]
    public void GenerateBom_ChannelNutsCountTwoPerConduitPerTier()
    {
        var trapeze = TrapezeAssembly.CreateSingleTierDefault();
        trapeze.Tiers[0].ConduitCount = 5;
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        var channelNuts = bom.Lines.Single(l => l.ItemCode.StartsWith("HW-CHN-"));
        Assert.Equal(10, channelNuts.Quantity); // 5 conduits × 2 nuts
    }

    [Fact]
    public void GenerateBom_RodCouplingAddsCouplingLine()
    {
        var trapeze = TrapezeAssembly.CreateSingleTierDefault();
        trapeze.Rods[0].HasCoupling = true;
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.Contains(bom.Lines, l => l.ItemCode.StartsWith("HW-CPL-"));
    }

    [Fact]
    public void GenerateBom_FinishCodeAppearsInItemCodes()
    {
        var trapeze = TrapezeAssembly.CreateSingleTierDefault();
        trapeze.Finish = TrapezeFinish.HotDipGalvanized;
        var hanger = MakeHanger(trapeze);

        var bom = TrapezeBomService.GenerateBom(hanger);

        Assert.Contains(bom.Lines, l => l.ItemCode.EndsWith("-HDG"));
    }

    [Fact]
    public void CreateMultiTier_TierCountOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapezeAssembly.CreateMultiTier(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapezeAssembly.CreateMultiTier(5));
    }

    [Fact]
    public void EnsureTrapeze_LazyInitializesSingleTierDefault()
    {
        var hanger = new HangerComponent();

        Assert.Null(hanger.Trapeze);
        var trapeze = hanger.EnsureTrapeze();

        Assert.NotNull(trapeze);
        Assert.Same(trapeze, hanger.Trapeze);
        Assert.Equal(1, trapeze.TierCount);
    }
}
