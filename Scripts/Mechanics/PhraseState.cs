namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Game-independent state for MGR's planned Phrase mechanic.
/// It deliberately does not inherit or replace another character's resource model.
/// </summary>
public sealed class PhraseState
{
    private readonly List<NoteKind> _notes = [];

    public PhraseState(int capacity = 4)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
    }

    public int Capacity { get; }
    public IReadOnlyList<NoteKind> Notes => _notes;
    public bool IsComplete => _notes.Count == Capacity;

    public void Add(NoteKind note)
    {
        if (IsComplete)
            throw new InvalidOperationException("Resolve the completed phrase before adding another note.");

        _notes.Add(note);
    }

    public PhraseResolution Resolve()
    {
        if (!IsComplete)
            throw new InvalidOperationException("Only a complete phrase can resolve.");

        NoteKind[] notes = _notes.ToArray();
        _notes.Clear();

        int distinctNotes = notes.Distinct().Count();
        return new PhraseResolution(
            notes,
            IsHarmony: distinctNotes == Capacity,
            IsEcho: distinctNotes == 1,
            Momentum: distinctNotes == Capacity ? 2 : 1);
    }

    public void Clear() => _notes.Clear();
}

public sealed record PhraseResolution(
    IReadOnlyList<NoteKind> Notes,
    bool IsHarmony,
    bool IsEcho,
    int Momentum);
