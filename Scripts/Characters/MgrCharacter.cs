using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using MGRMod.Settings;

namespace MGRMod.Characters;

[RegisterCharacter]
public sealed class MgrCharacter : ModCharacterTemplate<MgrCardPool, MgrRelicPool, MgrPotionPool>
{
    public static readonly Color ThemeColor = new(1f, 0.43f, 0f);

    internal const string SceneRoot = $"{Entry.ResPath}/scenes/characters";
    internal const string ImageRoot = $"{Entry.ResPath}/images/characters";
    private const string CharacterScenePath = $"{SceneRoot}/Mgr_character.tscn";

    public override Color NameColor => ThemeColor;
    public override Color EnergyLabelOutlineColor => new(0.32f, 0.08f, 0.02f);
    public override Color MapDrawingColor => ThemeColor;
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 67;
    public override int StartingGold => 114;

    public override CharacterAssetProfile AssetProfile => MgrCharacterAssets.Profile;

    // Multiplayer/remote-targeting hand poses. These use Tower 2's native
    // character-arm UI and therefore need no custom scene nodes.
    public override string CustomArmPointingTexturePath =>
        $"{ImageRoot}/hand/MGR_hand_point.png";
    public override string CustomArmRockTexturePath =>
        $"{ImageRoot}/hand/MGR_hand_rock.png";
    public override string CustomArmPaperTexturePath =>
        $"{ImageRoot}/hand/MGR_hand_paper.png";
    public override string CustomArmScissorsTexturePath =>
        $"{ImageRoot}/hand/MGR_hand_scissors.png";

    // Development fallback only. It prevents missing non-MGR assets from blocking the first load.
    public override string? PlaceholderCharacterId => "ironclad";
    // MGR is available from a fresh profile and has no prerequisite character run.
    protected override Type UnlocksAfterRunAsType => null!;
    public override bool RequiresEpochAndTimeline => false;
    public override float AttackAnimDelay => 0f;
    public override float CastAnimDelay => 0f;

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return CreateStaticCreatureVisuals();
    }

    /// <summary>
    /// Creates the same non-Spine visual root without relying on a caller's
    /// character fallback path. The Fake Merchant event needs this because its
    /// base-game setup assumes every temporary character visual is Spine-backed.
    /// </summary>
    internal static NCreatureVisuals? CreateStaticCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(CharacterScenePath);
    }

    protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(
        Node visualsRoot,
        CharacterModel character)
    {
        // The scene already contains idle_001 as its static texture. Returning
        // before the cue state machine is created prevents all per-frame image
        // swaps when the local performance option is disabled.
        if (!MgrVisualSettings.ShouldPlayCharacterAnimation)
            return null;

        // Only an idle loop is available for now. StandardCue deliberately maps
        // missing combat cues back to idle, so attacks, casts, hits and death cannot
        // leave the character on a missing texture or a stalled animation state.
        return ModAnimStateMachines.StandardCue(
            visualsRoot,
            character,
            idleName: MgrCharacterAnimation.IdleCue,
            deadName: null,
            hitName: null,
            attackName: null,
            castName: null,
            relaxedName: MgrCharacterAnimation.IdleCue,
            cueSet: MgrCharacterAnimation.CombatCues);
    }

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_slash"
        ];
    }
}
