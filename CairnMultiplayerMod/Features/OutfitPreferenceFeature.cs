using CairnMultiplayerMod.Framework;

namespace CairnMultiplayerMod.Features;

/// <summary>
/// Puts the outfit a player picked in the multiplayer menu on their climber. The pick is
/// applied once for each climber the game creates, and the existing appearance sync then
/// shows it to everyone else.
/// </summary>
internal sealed class OutfitPreferenceFeature : MultiplayerFeature
{
    public override string Id => "outfit-preference";

    protected internal override void OnRegister(FeatureBuilder feature)
        => feature.EveryFrame(Tick, FeaturePhase.Gameplay);

    private void Tick()
    {
        if (!Game.State.IsLocalPlayerInGame) return;
        Game.Players.ApplyPreferredOutfit();
    }
}
