namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Game-independent state for MGR's planned Phrase mechanic.
/// It deliberately does not inherit or replace another character's resource model.
/// </summary>
public sealed class PhraseState
{
    private readonly List<MgrNote> _notes = [];

    public PhraseState(int capacity = 4)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
    }

    public int Capacity { get; private set; }
    public IReadOnlyList<MgrNote> Notes => _notes;
    public int EmptySlotCount => Math.Max(0, Capacity - _notes.Count);
    public bool IsStarting => _notes.Count == 0;
    public bool IsEnding => _notes.Count == Capacity - 1;
    public bool IsComplete => _notes.Count >= Capacity;

    public void SetCapacity(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
    }

    public void Add(MgrNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (IsComplete)
            throw new InvalidOperationException("Resolve the completed phrase before adding another note.");

        _notes.Add(note);
    }

    public IReadOnlyList<MgrNote> RemoveRightmost(int count)
    {
        if (count <= 0 || _notes.Count == 0)
            return [];

        int removedCount = Math.Min(count, _notes.Count);
        int startIndex = _notes.Count - removedCount;
        MgrNote[] removed = _notes.GetRange(startIndex, removedCount).ToArray();
        _notes.RemoveRange(startIndex, removedCount);
        return removed;
    }

    public PhraseResolution Resolve()
    {
        if (!IsComplete)
            throw new InvalidOperationException("Only a complete phrase can resolve.");

        MgrNote[] notes = _notes.Take(Capacity).ToArray();
        _notes.RemoveRange(0, Capacity);

        return new PhraseResolution(notes);
    }

    public void Clear() => _notes.Clear();
}

public sealed record PhraseResolution(
    IReadOnlyList<MgrNote> Notes);
