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
    /// truncates toward zero, so a rate-4 note changes once per four Forte.
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
    public override int ForteRate => 4;
}

public sealed class StatusNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Status;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => 2;
}

public sealed class CurseNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Curse;
    public override int BaseEffectAmount => 2;
    public override int ForteRate => int.MaxValue;
    public override bool IsAffectedByForte => false;
}

public sealed class QuestNote : MgrNote
{
    public override NoteKind Kind => NoteKind.Quest;
    public override int BaseEffectAmount => 1;
    public override int ForteRate => 6;
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

    // Quest.png already contains the STS1 Ghost note artwork. Reuse it so the
    // new semantic distinction does not require another duplicate asset.
    public override string TexturePath =>
        $"{Entry.ResPath}/images/notes/Quest.png";
}
