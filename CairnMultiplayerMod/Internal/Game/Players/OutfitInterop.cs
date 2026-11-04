using System;
using System.Collections.Generic;
using CairnMultiplayerMod.Internal.Diagnostics;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTheGameBakers.Cairn;
using Il2CppTheGameBakers.Cairn.Netplay;
using UnityEngine;

namespace CairnMultiplayerMod.Internal.Game.Players;

/// <summary>
/// Debug aid for the outfit experiment: lists the game's body-and-outfit skins and puts one
/// on the local climber through the game's own call. The mod already copies which outfit
/// meshes the local climber shows, so whatever sticks here is what other players would see.
/// </summary>
internal static class OutfitInterop
{
    internal static string Run(string argument)
    {
        argument = (argument ?? "").Trim();
        var handler = FindHandler();
        if (handler == null) return "Start the climb first - there is no climber to dress.";

        var names = Enum.GetNames(typeof(PawnBodyAndOutfitSkinType));
        if (argument.Length == 0 || argument.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var current = SafeCurrent(handler);
            ModLog.Info($"[Outfit] types: {string.Join(", ", names)}; current: {current}");
            LogDatabase(handler);
            LogLiveRenderers(handler);
            return $"Outfits: {string.Join(", ", names)}. Now wearing: {current}. Use /outfit <name>.";
        }

        if (!Enum.TryParse<PawnBodyAndOutfitSkinType>(argument, ignoreCase: true, out var type)
            || !Enum.IsDefined(typeof(PawnBodyAndOutfitSkinType), type))
            return $"No outfit called '{argument}'. Try /outfit list.";

        try
        {
            var changed = handler.TryChangeBodyAndOutfitSkin(type);
            var now = SafeCurrent(handler);
            ModLog.Info($"[Outfit] TryChangeBodyAndOutfitSkin({type}) returned {changed}; current now {now}");
            return changed
                ? $"Now wearing: {now}."
                : $"The game refused {type} here. Still wearing: {now}.";
        }
        catch (Exception exception)
        {
            ModLog.SuppressedException("outfit.try-change", exception);
            return $"Changing to {type} failed: {exception.GetType().Name}.";
        }
    }

    private static PawnSkinHandler _localHandler;

    /// <summary>The number of the outfit skin the local climber wears, for the cosmetics byte.</summary>
    internal static bool TryGetLocalSkinValue(out int value)
    {
        value = -1;
        var handler = ResolveLocalHandler();
        if (handler == null) return false;
        try
        {
            value = (int)handler.CurrentBodyAndOutfitSkinType;
            return true;
        }
        catch (Exception exception)
        {
            ModLog.SuppressedException("outfit.read-local-skin", exception);
            return false;
        }
    }

    private static PawnSkinHandler ResolveLocalHandler()
    {
        if (_localHandler != null && _localHandler.Pointer != IntPtr.Zero) return _localHandler;
        _localHandler = FindHandler();
        return _localHandler;
    }

    // What each ghost renderer held before an outfit was put on it, so the copy's own colourable
    // clothes can come back with the Default outfit. Keyed by renderer, dropped with the ghost.
    private static readonly Dictionary<IntPtr, (Mesh Mesh, Il2CppReferenceArray<Material> Materials,
        Il2CppReferenceArray<Transform> Bones, Transform RootBone)> Originals = new();

    /// <summary>
    /// Dresses a remote climber's copy in an outfit skin. A skin entry points at a reference
    /// renderer rather than carrying a mesh, and the game copies that reference's mesh and
    /// materials onto the climber's own outfit and body renderers; the same goes onto the
    /// copy's renderers of those names. The materials have to travel with the mesh, since a
    /// mesh in several parts draws only the parts that have a material. The Default outfit
    /// puts the copy's own clothes back, which are the colourable ones the colour pick uses.
    /// </summary>
    internal static bool ApplyToGhost(NetplayRemotePlayer ghost, int skinValue)
    {
        if (ghost == null || ghost.Pointer == IntPtr.Zero) return false;
        var handler = ResolveLocalHandler();
        if (handler == null) return false;

        try
        {
            var type = (PawnBodyAndOutfitSkinType)skinValue;
            if (!Enum.IsDefined(typeof(PawnBodyAndOutfitSkinType), type)) return false;
            if (!handler.skinDatabase.TryGetValue(type, out var data) || data == null) return false;

            var restore = type == PawnBodyAndOutfitSkinType.Default || type == PawnBodyAndOutfitSkinType.None;
            var renderers = ghost.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var applied = ApplyPart(renderers, data.outfit, handler.outfitSkinnedMeshRenderer, restore);
            applied |= ApplyPart(renderers, data.body, handler.bodySkinnedMeshRenderer, restore);
            return applied;
        }
        catch (Exception exception)
        {
            ModLog.SuppressedException("outfit.apply-to-ghost", exception);
            return false;
        }
    }

    private static bool ApplyPart(Il2CppArrayBase<SkinnedMeshRenderer> renderers, PawnSkinData.MeshData part,
        SkinnedMeshRenderer target, bool restoreOriginals)
    {
        if (part == null || target == null) return false;

        var name = target.name;
        var reference = part.skinnedMeshRenderer;
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || renderer.name != name) continue;

            Originals.TryAdd(renderer.Pointer,
                (renderer.sharedMesh, renderer.sharedMaterials, renderer.bones, renderer.rootBone));
            var original = Originals[renderer.Pointer];

            var mesh = restoreOriginals ? original.Mesh : part.meshOverride && part.mesh != null ? part.mesh : reference?.sharedMesh;
            var materials = restoreOriginals ? original.Materials
                : part.materialsOverride && part.materials != null ? part.materials : reference?.sharedMaterials;
            if (mesh == null || materials == null || materials.Count == 0)
            {
                ModLog.Info($"[Outfit] nothing to put on ghost '{name}' (mesh {mesh?.name}, materials {materials?.Count})");
                return false;
            }

            var before = $"{renderer.sharedMesh?.name}/{renderer.sharedMesh?.vertexCount}v [{MaterialNames(renderer.sharedMaterials)}]";
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;

            // A skinned mesh is only right on the bones it was built for. The reference's bones
            // are looked up by name on the copy's own skeleton.
            string bones;
            if (restoreOriginals)
            {
                renderer.bones = original.Bones;
                renderer.rootBone = original.RootBone;
                bones = $"original {original.Bones?.Length}";
            }
            else
            {
                bones = Rebind(renderer, reference, original.Bones);
            }

            ModLog.Info($"[Outfit] ghost '{name}' {(restoreOriginals ? "restored" : "dressed")}: {before} -> " +
                        $"{mesh.name}/{mesh.vertexCount}v [{MaterialNames(materials)}]; " +
                        $"mesh bind poses {mesh.bindposeCount}, submeshes {mesh.subMeshCount}, bones {bones}");
            return true;
        }

        ModLog.Info($"[Outfit] ghost has no renderer called '{name}'");
        return false;
    }

    /// <summary>
    /// The copy carries more than one skeleton with the same bone names, so a lookup over the
    /// whole copy can pick a bone from the wrong one. The right skeleton is the one the
    /// renderer already sits on: its bones' lowest common ancestor and everything below it,
    /// plus the chain of ancestors above it.
    /// </summary>
    private static Dictionary<string, Transform> SkeletonMap(Il2CppReferenceArray<Transform> ownBones, out string rootName)
    {
        rootName = "?";
        var map = new Dictionary<string, Transform>();
        var root = CommonAncestor(ownBones);
        if (root == null) return map;

        rootName = root.name;
        var below = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < below.Count; i++)
            if (below[i] != null) map.TryAdd(below[i].name, below[i]);
        for (var up = root.parent; up != null; up = up.parent) map.TryAdd(up.name, up);
        return map;
    }

    private static Transform CommonAncestor(Il2CppReferenceArray<Transform> bones)
    {
        List<Transform> chain = null;
        for (int i = 0; i < bones.Length; i++)
        {
            var bone = bones[i];
            if (bone == null) continue;
            if (chain == null)
            {
                chain = new List<Transform>();
                for (var t = bone; t != null; t = t.parent) chain.Add(t);
                continue;
            }

            var index = -1;
            for (var t = bone; t != null && index < 0; t = t.parent)
                index = chain.FindIndex(c => c.Pointer == t.Pointer);
            if (index > 0) chain.RemoveRange(0, index);
        }
        return chain?[0];
    }

    /// <summary>Points the renderer at the copy's bones that carry the same names as the reference's.</summary>
    private static string Rebind(SkinnedMeshRenderer renderer, SkinnedMeshRenderer reference,
        Il2CppReferenceArray<Transform> ownBones)
    {
        var wanted = reference?.bones;
        if (wanted == null || ownBones == null) return "no bones to go on";

        var ghostBones = SkeletonMap(ownBones, out var rootName);
        var bones = new Il2CppReferenceArray<Transform>(wanted.Length);
        var missing = new List<string>();
        for (int i = 0; i < wanted.Length; i++)
        {
            var bone = wanted[i];
            if (bone != null && ghostBones.TryGetValue(bone.name, out var found)) bones[i] = found;
            else missing.Add(bone?.name ?? $"#{i}");
        }

        renderer.bones = bones;
        var root = reference.rootBone;
        if (root != null && ghostBones.TryGetValue(root.name, out var rootFound)) renderer.rootBone = rootFound;

        return missing.Count == 0
            ? $"{wanted.Length} rebound by name on skeleton '{rootName}' ({ghostBones.Count} bones)"
            : $"{wanted.Length} rebound on skeleton '{rootName}' ({ghostBones.Count} bones), {missing.Count} missing ({string.Join(", ", missing.GetRange(0, Math.Min(8, missing.Count)))})";
    }

    /// <summary>Forgets what was remembered for a copy that is going away.</summary>
    internal static void ForgetGhost(NetplayRemotePlayer ghost)
    {
        if (ghost == null || ghost.Pointer == IntPtr.Zero) return;
        try
        {
            var renderers = ghost.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) Originals.Remove(renderers[i].Pointer);
        }
        catch (Exception exception) { ModLog.SuppressedException("outfit.forget-ghost", exception); }
    }

    private static string MaterialNames(Il2CppReferenceArray<Material> materials)
    {
        if (materials == null) return "none";
        var names = new List<string>();
        for (int i = 0; i < materials.Count; i++) names.Add(materials[i]?.name ?? "null");
        return string.Join(", ", names);
    }

    /// <summary>Logs what the climber's own outfit and body renderers hold right now.</summary>
    private static void LogLiveRenderers(PawnSkinHandler handler)
    {
        try
        {
            foreach (var renderer in new[] { handler.outfitSkinnedMeshRenderer, handler.bodySkinnedMeshRenderer })
            {
                if (renderer == null) continue;
                ModLog.Info($"[Outfit] live '{renderer.name}': mesh='{renderer.sharedMesh?.name}' " +
                            $"verts={renderer.sharedMesh?.vertexCount} materials=[{MaterialNames(renderer.sharedMaterials)}]");
            }
        }
        catch (Exception exception) { ModLog.SuppressedException("outfit.log-live", exception); }
    }

    /// <summary>Logs, for every outfit skin, what each part would replace.</summary>
    private static void LogDatabase(PawnSkinHandler handler)
    {
        try
        {
            foreach (var type in (PawnBodyAndOutfitSkinType[])Enum.GetValues(typeof(PawnBodyAndOutfitSkinType)))
            {
                if (!handler.skinDatabase.TryGetValue(type, out var data) || data == null)
                {
                    ModLog.Info($"[Outfit] database: {type} has no entry");
                    continue;
                }
                foreach (var (label, part) in new[]
                         { ("outfit", data.outfit), ("body", data.body), ("shoeLeft", data.shoeLeft), ("shoeRight", data.shoeRight) })
                {
                    if (part == null) { ModLog.Info($"[Outfit] database: {type}.{label} empty"); continue; }
                    var reference = part.skinnedMeshRenderer;
                    ModLog.Info($"[Outfit] database: {type}.{label} -> reference '{reference?.name}' " +
                                $"mesh='{reference?.sharedMesh?.name}' verts={reference?.sharedMesh?.vertexCount} " +
                                $"materials=[{MaterialNames(reference?.sharedMaterials)}] " +
                                $"(overrides: mesh={part.meshOverride} materials={part.materialsOverride})");
                }
            }
        }
        catch (Exception exception) { ModLog.SuppressedException("outfit.log-database", exception); }
    }

    private static PawnSkinHandler FindHandler()
    {
        var mc = LocalPlayerInterop.TryGetMCGameObject();
        if (mc == null) return null;
        try
        {
            return mc.GetComponentInChildren<PawnSkinHandler>(true);
        }
        catch (Exception exception)
        {
            ModLog.SuppressedException("outfit.find-handler", exception);
            return null;
        }
    }

    private static string SafeCurrent(PawnSkinHandler handler)
    {
        try { return handler.CurrentBodyAndOutfitSkinType.ToString(); }
        catch (Exception exception)
        {
            ModLog.SuppressedException("outfit.read-current", exception);
            return "unknown";
        }
    }
}
