using CairnMultiplayerMod.Framework;

namespace CairnMultiplayerMod.Features;

/// <summary>
/// Debug commands for the outfit sync. /outfit puts one of the game's outfits on the local
/// climber, and /ghost stands a copy of the climber in front of the player dressed the way
/// other players see it, since nobody can otherwise check that without a second player.
/// </summary>
internal sealed class OutfitPreviewFeature : MultiplayerFeature
{
    public override string Id => "outfit-preview";

    protected internal override void OnRegister(FeatureBuilder feature)
    {
        feature.Game.Chat.AddCommand("ghost", "/ghost [outfit|off|meshes|api]",
            "Preview how other players see your outfit on a copy of yourself", Run);

        feature.Game.Chat.AddCommand("outfit", "/outfit [list|<name>]",
            "Try one of the game's outfits on yourself", argument => Game.Chat.AddSystemLine(Game.Players.TryOutfit(argument)));

        feature.OnSceneReset(Remove);
        feature.OnSessionEnded(Remove);
    }

    private void Run(string argument)
        => Game.Chat.AddSystemLine(Game.Players.PreviewClimber(argument));

    private void Remove() => Game.Players.PreviewClimber("off");
}
