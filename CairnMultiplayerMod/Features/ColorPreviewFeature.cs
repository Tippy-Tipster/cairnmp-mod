using CairnMultiplayerMod.Framework;

namespace CairnMultiplayerMod.Features;

/// <summary>
/// /ghost stands a copy of the climber in front of the player in the picked colour. Nobody
/// sees their own colour, so this is the only way to check a pick without a second player.
/// </summary>
internal sealed class ColorPreviewFeature : MultiplayerFeature
{
    public override string Id => "color-preview";

    protected internal override void OnRegister(FeatureBuilder feature)
    {
        feature.Game.Chat.AddCommand("ghost", "/ghost [1-6|auto|off]",
            "Preview a climber colour on a copy of yourself", Run);

        feature.OnSceneReset(Remove);
        feature.OnSessionEnded(Remove);
    }

    private void Run(string argument)
        => Game.Chat.AddSystemLine(Game.Players.PreviewClimberColor(argument));

    private void Remove() => Game.Players.PreviewClimberColor("off");
}
