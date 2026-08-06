using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class RefrainFormPower : ModPowerTemplate
{
    private int[] _recordedNotes = [];

    [SavedProperty]
    public int[] RecordedNotes
    {
        get => _recordedNotes;
        set
        {
            AssertMutable();
            _recordedNotes = value ?? [];
        }
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/RefrainFormPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/RefrainFormPower.png");

    public void Record(IEnumerable<NoteKind> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        RecordedNotes = RecordedNotes
            .Concat(notes.Select(note => (int)note))
            .ToArray();
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || RecordedNotes.Length == 0)
            return;

        Flash();
        foreach (int rawKind in RecordedNotes)
        {
            if (Enum.IsDefined(typeof(NoteKind), rawKind))
            {
                await MgrNoteSystem.ChannelNote(
                    choiceContext,
                    player,
                    (NoteKind)rawKind);
            }
        }
    }
}
