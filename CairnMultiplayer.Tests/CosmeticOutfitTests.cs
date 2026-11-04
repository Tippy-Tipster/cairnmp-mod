using CairnMultiplayer.Shared;
using Xunit;

namespace CairnMultiplayerMod.Tests;

public sealed class CosmeticOutfitTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void AnOutfitSkinSurvivesPacking(int skin)
        => Assert.Equal(skin, Protocol.OutfitSkinOf(Protocol.WithOutfitSkin(0, skin)));

    [Fact]
    public void PeersWithoutAnOutfitSkinReadAsNotSent()
    {
        Assert.Equal(-1, Protocol.OutfitSkinOf(0));
        Assert.Equal(-1, Protocol.OutfitSkinOf(Protocol.CosmeticFlagGlowingGloves));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void AnOutOfRangeSkinIsSentAsNotSent(int skin)
        => Assert.Equal(-1, Protocol.OutfitSkinOf(Protocol.WithOutfitSkin(0xFF, skin)));

    [Fact]
    public void PackingAnOutfitSkinLeavesTheGlovesBitAlone()
    {
        var withGloves = Protocol.WithOutfitSkin(Protocol.CosmeticFlagGlowingGloves, 3);
        Assert.True((withGloves & Protocol.CosmeticFlagGlowingGloves) != 0);
        Assert.Equal(3, Protocol.OutfitSkinOf(withGloves));

        var withoutGloves = Protocol.WithOutfitSkin(0, 3);
        Assert.True((withoutGloves & Protocol.CosmeticFlagGlowingGloves) == 0);
    }

    [Fact]
    public void ChangingTheOutfitSkinReplacesThePreviousOne()
        => Assert.Equal(2, Protocol.OutfitSkinOf(Protocol.WithOutfitSkin(Protocol.WithOutfitSkin(0, 5), 2)));
}
