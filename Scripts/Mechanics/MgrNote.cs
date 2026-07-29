namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Runtime representation of one MGR note. It deliberately remains independent of
/// Defect OrbModel so MGR does not inherit orb passives, evocation, Focus, or slot rules.
/// </summary>
public abstract class MgrNote
{
    public abstract NoteKind Kind { get; }
    public abstract int BaseEffectAmount { get; }
    public abstract int ForteRate { get; }
    public virtual bool IsAffectedByForte => true;
    public string Name => Kind.ToString();
    public virtual string TexturePath => $"{Entry.ResPath}/images/notes/{Name}.png";

    /// <summary>
    /// Reproduces STS1's positive/negative Forte scaling. Integer division intentionally
    /// truncates toward zero, so a rate-3 note changes once per three Forte.
    /// </summary>
    public virtual int GetEffectAmount(int forte)
    {
        if (!IsAffectedByForte)
            return BaseEffectAmount;

        return Math.Max(0, BaseEffectAmount + forte / ForteRate);
    }

    public override string ToString() => Name;
}

public sealed class AttackNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Attack;
    public override int BaseEffectAmount => 2;
    public override int ForteRate => 2;

    // STS1 Attack notes are the exception: each Forte adds two damage.
    public override int GetEffectAmount(int forte)
    {
        long amount = BaseEffectAmount + (long)forte * ForteRate;
        return (int)Math.Clamp(amount, 0L, int.MaxValue);
    }
}

public sealed class SkillNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Skill;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => 1;
}

public sealed class PowerNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Power;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => 2;
}

public sealed class StatusNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Status;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;
}

public sealed class CurseNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Curse;
    public override int BaseEffectAmount => 2;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;
}

public sealed class StarryNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Starry;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;
}

public sealed class GhostNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Ghost;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;

    public override string TexturePath =>
        $"{Entry.ResPath}/images/notes/Ghost.png";
}

/// <summary>
/// A single rack slot whose resolution contains all five basic Note effects
/// plus the Starry Note effect. Its animated presentation cycles through those
/// six component shapes rather than requiring a dedicated static texture.
/// </summary>
public sealed class OmniaNote : MgrNote
{
    public override NoteKind Kind => NoteKind.OmniaNote;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;

    // Used only as a safe initial texture before the animated visual begins.
    public override string TexturePath =>
        $"{Entry.ResPath}/images/notes/Attack.png";
}
