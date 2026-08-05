using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Cards;

public abstract class TheCrowdChoice(int noteCount) : MgrCard(
    0,
    CardType.Skill,
    CardRarity.Token,
    TargetType.None,
    showInCardLibrary: false)
{
    public int NoteCount { get; } = noteCount;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/TheCrowd.png");

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade()
    {
    }
}

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "the_crowd_choice_0")]
public sealed class TheCrowdChoice0() : TheCrowdChoice(0);

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "the_crowd_choice_1")]
public sealed class TheCrowdChoice1() : TheCrowdChoice(1);

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "the_crowd_choice_2")]
public sealed class TheCrowdChoice2() : TheCrowdChoice(2);

[RegisterCard(typeof(MgrTokenCardPool), StableEntryStem = "the_crowd_choice_3")]
public sealed class TheCrowdChoice3() : TheCrowdChoice(3);
