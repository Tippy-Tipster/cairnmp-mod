namespace CairnMultiplayer.Shared;

/// <summary>
/// Protocol version. Bumped every time the packet layout changes in an incompatible
/// way. The handshake rejects clients that do not match.
/// </summary>
public static class Protocol
{
    public const int Version = 16;
    public const string ConnectionKey = "cairnmp";
    public const int DefaultPort = 14000;

    /// <summary>
    /// CairnMP client version sent to the API when creating a lobby.
    /// Matches the deployed launcher/mod version.
    /// </summary>
    public const string GameVersion = "2.4.1";

    public const float PlayerStateUpdateIntervalSeconds = 1f / 30f;

    public const float BoneStateUpdateIntervalSeconds = 1f / 30f;

    /// <summary>
    /// Hard cap on the number of Vector3 entries a NetFrame may carry (root + one per bone).
    /// Serialization throws above it and validation rejects the packet, so the capture side
    /// checks against this value rather than truncating: a truncated frame breaks the native
    /// `positions.Length == anchors.relatives.Length + 1` invariant on the receiving end.
    /// </summary>
    public const int MaxFrameVectorCount = 512;

    public const float WeatherStateUpdateIntervalSeconds = 0.5f;

    public const float WeatherStateTransitionSeconds = 0.5f;

    public const float LampStatePollIntervalSeconds = 0.2f;

    public const float CosmeticStatePollIntervalSeconds = 0.5f;

    public const byte CosmeticFlagGlowingGloves = 1 << 0;

    /// <summary>Climber colours a player can pick; choice 0 is "automatic".</summary>
    public const int CosmeticColorCount = 6;

    private const int CosmeticColorShift = 1;
    private const byte CosmeticColorMask = 0b111 << CosmeticColorShift;

    /// <summary>
    /// The colour choice rides in bits 1-3 of the cosmetic flags: 0 lets the game pick one per
    /// player, 1..<see cref="CosmeticColorCount"/> picks that colour. Peers that predate the
    /// choice only read the gloves bit, so they ignore it.
    /// </summary>
    public static byte WithColorChoice(byte flags, int choice)
    {
        if (choice < 0 || choice > CosmeticColorCount) choice = 0;
        return (byte)((flags & ~CosmeticColorMask) | (choice << CosmeticColorShift));
    }

    /// <summary>The colour choice in <paramref name="flags"/>; anything out of range reads as automatic.</summary>
    public static int ColorChoiceOf(byte flags)
    {
        var choice = (flags & CosmeticColorMask) >> CosmeticColorShift;
        return choice <= CosmeticColorCount ? choice : 0;
    }

    public const float PingLifetimeSeconds = 15f;

    public const float PingCooldownSeconds = 1f;

    public const float DefaultPingDistance = 50f;

    /// <summary>Number of synchronized finger bones. Aava skeleton (resolved by name):
    /// per hand, Thumb 00-02 (3) + Index/Middle/Ring/Pinky 00-03 (4 each) = 19; x2 hands = 38.</summary>
    public const int FingerBoneCount = 38;

    public const int HandPosePackedSize = FingerBoneCount * QuaternionCodec.PackedSize;

    public const float HandPosePollIntervalSeconds = 1f / 12f;

    public const float TimeStateUpdateIntervalSeconds = 0.5f;

    public const float TimeStateFastForwardIntervalSeconds = 1f / 15f;
}

/// <summary>
/// Lifecycle state of a player as seen by the server. Ghosts are only rendered when
/// the local player AND the remote player are both InGame -- this avoids spawning
/// prefabs during scene transitions (which crashes Cairn's Addressables pipeline)
/// and hides zombie ghosts during loading.
///
/// Keep the values stable across protocol versions.
/// </summary>
public enum PlayerState : byte
{
    Unknown = 0, // default, no state received yet
    Connecting = 1, // socket connected, handshake not finished yet
    InMenu = 2, // main menu, not in a game
    Loading = 3, // scene transitions in progress, intro cinematic
    InGame = 4, // MC spawned, scenes stable -- ok to render ghosts
    Bivouac = 5, // active participant, gameplay animation suspended; still votes on sleep
}

/// <summary>
/// Packet type identifiers. Sent as the first byte of every packet.
/// Keep the values stable across versions -- deprecated packets go at the bottom,
/// never renumbered.
/// </summary>
public enum PacketId : byte
{
    // Retired ids remain gaps so older clients cannot reinterpret them as new packets.
    ClientHandshake = 1,
    ClientDisconnect = 2,
    ClientPlayerState = 3,
    ClientBoneState = 5,
    ClientPitonPlaced = 6,
    ClientPitonRemoved = 7,
    ClientPlayerFrame = 8,
    ClientClimbotFrame = 9,
    ClientRopeClip = 15,
    ClientExtensionManifest = 17,
    ClientExtensionCommand = 18,
    /// <summary>Any feature's real-time stream, client to host. The channel is identified
    /// inside the payload, so a new stream never needs a new packet id.</summary>
    ClientFeatureStream = 19,
    /// <summary>The sender took up a piton another climber placed.</summary>
    ClientPitonRecovered = 20,

    ServerHandshakeAck = 64,
    ServerHandshakeReject = 65,
    ServerPlayerJoined = 66,
    ServerPlayerLeft = 67,
    ServerPlayerState = 68,
    ServerStartGame = 70,
    ServerBoneState = 71,
    ServerPitonPlaced = 72,
    ServerPitonRemoved = 73,
    ServerPlayerFrame = 74,
    ServerClimbotFrame = 75,
    ServerTeleport = 81,
    ServerRopeClip = 82,
    ServerExtensionManifestResult = 84,
    ServerExtensionCommandResult = 85,
    ServerExtensionEvent = 86,
    ServerExtensionState = 87,
    ServerExtensionPeerStatus = 88,
    /// <summary>Host relay of a feature stream, carrying the sender's id.</summary>
    ServerFeatureStream = 89,
    /// <summary>Tells a piton's owner that another climber took it up.</summary>
    ServerPitonRecovered = 90,
}

/// <summary>
/// The difficulty values match Cairn's `DifficultyTweakables.SelectedDifficulty`
/// enum -- same names, same int values. We control which one is active so the client
/// can hook directly into the game's launch options.
/// </summary>
public enum GameDifficulty : int
{
    Invalid = 0,
    Alpinist = 1769420573,
    Explorer = 766328718,
    FreeSolo = -1944667143,
    FreeRoam = 418187680,
}

/// <summary>Outcome of a managed extension command handled by the host.</summary>
public enum ExtensionCommandStatus : byte
{
    Committed = 0,
    Rejected = 1,
    Failed = 2,
    TimedOut = 3,
    Unavailable = 4,
}
