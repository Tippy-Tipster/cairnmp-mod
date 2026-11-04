using CairnMultiplayer.Shared;
using Xunit;

namespace CairnMultiplayerMod.Tests;

public sealed class CosmeticColorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(Protocol.CosmeticColorCount)]
    public void AColorChoiceSurvivesPacking(int choice)
        => Assert.Equal(choice, Protocol.ColorChoiceOf(Protocol.WithColorChoice(0, choice)));

    [Fact]
    public void PackingAColorLeavesTheGlovesBitAlone()
    {
        var withGloves = Protocol.WithColorChoice(Protocol.CosmeticFlagGlowingGloves, 4);
        Assert.True((withGloves & Protocol.CosmeticFlagGlowingGloves) != 0);
        Assert.Equal(4, Protocol.ColorChoiceOf(withGloves));

        var withoutGloves = Protocol.WithColorChoice(0, 4);
        Assert.True((withoutGloves & Protocol.CosmeticFlagGlowingGloves) == 0);
    }

    [Fact]
    public void ChangingTheColorReplacesThePreviousOne()
        => Assert.Equal(2, Protocol.ColorChoiceOf(Protocol.WithColorChoice(Protocol.WithColorChoice(0, 5), 2)));

    [Theory]
    [InlineData(-1)]
    [InlineData(Protocol.CosmeticColorCount + 1)]
    [InlineData(99)]
    public void AnOutOfRangeChoiceBecomesAutomatic(int choice)
        => Assert.Equal(0, Protocol.ColorChoiceOf(Protocol.WithColorChoice(0xFF, choice)));

    [Fact]
    public void AReceivedValueBeyondTheColorCountReadsAsAutomatic()
    {
        // Three bits could hold 7, but only 1..CosmeticColorCount exist.
        byte flags = 0b111 << 1;
        Assert.Equal(0, Protocol.ColorChoiceOf(flags));
    }

    [Fact]
    public void FlagsFromPeersWithoutAColorChoiceReadAsAutomatic()
        => Assert.Equal(0, Protocol.ColorChoiceOf(Protocol.CosmeticFlagGlowingGloves));
}
