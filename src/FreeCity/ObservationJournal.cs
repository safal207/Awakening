using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Probuzhdenie.FreeCity;

public enum ObservationKind
{
    Fact,
    Question,
    Promise,
    OpenMeeting,
}

public enum ObservationStatus
{
    Open,
    Completed,
    NotCompleted,
}

public sealed record ObservationEntry(
    string Id,
    ObservationKind Kind,
    string Text,
    int Day,
    ObservationStatus Status);

public sealed class ObservationJournal
{
    private readonly List<ObservationEntry> _entries = new();
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public IReadOnlyList<ObservationEntry> Entries => _entries;

    public bool TryAdd(string id, ObservationKind kind, string text, int day, ObservationStatus status = ObservationStatus.Open)
    {
        if (!Valid(id, kind, text, day, status) || _index.ContainsKey(id))
            return false;

        _index[id] = _entries.Count;
        _entries.Add(new ObservationEntry(id, kind, text.Trim(), day, status));
        return true;
    }

    public bool TrySetStatus(string id, ObservationStatus status, string? replacementText = null)
    {
        if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(status) || !_index.TryGetValue(id, out int index))
            return false;

        ObservationEntry current = _entries[index];
        string text = string.IsNullOrWhiteSpace(replacementText) ? current.Text : replacementText.Trim();
        if (current.Status == status && string.Equals(current.Text, text, StringComparison.Ordinal))
            return false;

        _entries[index] = current with { Text = text, Status = status };
        return true;
    }

    public bool TryGet(string id, out ObservationEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(id) || !_index.TryGetValue(id, out int index))
            return false;
        entry = _entries[index];
        return true;
    }

    internal bool TryRestore(ObservationEntry entry)
    {
        if (entry == null || !Valid(entry.Id, entry.Kind, entry.Text, entry.Day, entry.Status))
            return false;
        if (_index.TryGetValue(entry.Id, out int index))
            return _entries[index] == entry;

        _index[entry.Id] = _entries.Count;
        _entries.Add(entry with { Text = entry.Text.Trim() });
        return true;
    }

    private static bool Valid(string id, ObservationKind kind, string text, int day, ObservationStatus status) =>
        !string.IsNullOrWhiteSpace(id) &&
        !string.IsNullOrWhiteSpace(text) &&
        day >= 1 &&
        Enum.IsDefined(kind) &&
        Enum.IsDefined(status);
}

public static class ObservationJournalState
{
    private sealed class Box
    {
        public ObservationJournal Journal = new();
    }

    private static readonly ConditionalWeakTable<HeroProgress, Box> States = new();

    public static ObservationJournal For(HeroProgress progress)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        return States.GetValue(progress, _ => new Box()).Journal;
    }

    internal static void Replace(HeroProgress progress, ObservationJournal journal)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        if (journal == null) throw new ArgumentNullException(nameof(journal));
        States.Remove(progress);
        States.Add(progress, new Box { Journal = journal });
    }
}
