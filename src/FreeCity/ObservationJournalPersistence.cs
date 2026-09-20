using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public sealed class ObservationJournalSaveData
{
    public List<ObservationJournalEntrySaveData> Entries { get; set; } = new();
}

public sealed class ObservationJournalEntrySaveData
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public int Day { get; set; }
    public string Status { get; set; } = "";
}

public static class ObservationJournalPersistence
{
    public static ObservationJournalSaveData Capture(HeroProgress progress)
    {
        var data = new ObservationJournalSaveData();
        foreach (ObservationEntry entry in ObservationJournalState.For(progress).Entries)
        {
            data.Entries.Add(new ObservationJournalEntrySaveData
            {
                Id = entry.Id,
                Kind = entry.Kind.ToString(),
                Text = entry.Text,
                Day = entry.Day,
                Status = entry.Status.ToString(),
            });
        }
        return data;
    }

    public static bool TryRestore(HeroProgress progress, ObservationJournalSaveData? data)
    {
        var journal = new ObservationJournal();
        if (data == null)
        {
            ObservationJournalState.Replace(progress, journal);
            return true;
        }

        bool clean = true;
        foreach (ObservationJournalEntrySaveData row in data.Entries ?? new())
        {
            if (!System.Enum.TryParse(row.Kind, ignoreCase: true, out ObservationKind kind) ||
                !System.Enum.TryParse(row.Status, ignoreCase: true, out ObservationStatus status) ||
                !journal.TryRestore(new ObservationEntry(row.Id ?? "", kind, row.Text ?? "", row.Day, status)))
            {
                clean = false;
            }
        }

        ObservationJournalState.Replace(progress, journal);
        return clean;
    }
}
