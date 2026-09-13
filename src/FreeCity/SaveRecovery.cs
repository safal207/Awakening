using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public sealed class SaveLoadException : IOException
{
    public SaveLoadException(string message, Exception? inner = null) : base(message, inner) { }
}

public static partial class SaveSystem
{
    private sealed class FutureSaveException : Exception { }
    private const int MaxSaveBytes = 8 * 1024 * 1024;

    private static (SaveData data, string json)? ReadCandidate(string path)
    {
        string json;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > MaxSaveBytes) throw new InvalidDataException("Save exceeds the size limit.");
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
            json = reader.ReadToEnd();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Save must be an object.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate save property.");
            if (property.Name.Equals("Version",StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int version) &&
                version > CurrentSaveVersion) throw new FutureSaveException();
        }
        var data = JsonSerializer.Deserialize<SaveData>(json, LoadOptions) ?? throw new InvalidDataException("Empty save.");
        if (data.Version > CurrentSaveVersion) throw new FutureSaveException();
        if (!names.Contains("Seed") || !names.Contains("Day")) throw new InvalidDataException("Missing save identity.");
        ValidateData(data);
        return (data, json);
    }

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes,
        List<NpcSaveData>? npcs, MemoryLedger memoryLedger, PlayerSaveData? player, string notice) LoadFromPath(string path)
    {
        try
        {
            bool corrupt = false;
            try
            {
                var primary = ReadCandidate(path);
                if (primary.HasValue) return ConvertSave(primary.Value.data, "");
            }
            catch (Exception e) when (e is JsonException or InvalidDataException or DecoderFallbackException)
            {
                corrupt = true;
            }

            var backup = ReadCandidate(path + ".bak");
            if (backup.HasValue)
            {
                if (corrupt)
                    File.Copy(path, path + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json", overwrite: false);
                WriteAllTextAtomically(path, backup.Value.json);
                return ConvertSave(backup.Value.data, "Прогресс восстановлен из резервной копии.");
            }
            if (corrupt) throw new InvalidDataException("No usable backup.");
            return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null, new MemoryLedger(), null, "");
        }
        catch (FutureSaveException e)
        {
            throw new SaveLoadException("Нужна более новая версия игры. Сохранение не изменено.", e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or DecoderFallbackException)
        {
            Console.WriteLine("Save recovery failed: " + e);
            throw new SaveLoadException("Не удалось загрузить прогресс. Исходные файлы сохранены.", e);
        }
    }

    private static void ValidateData(SaveData data)
    {
        static void Require(bool valid, string field)
        {
            if (!valid) throw new InvalidDataException("Invalid save field: " + field);
        }
        static bool Number(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
        Require(data.Version >= 0 && data.Version <= CurrentSaveVersion, "Version");
        Require(data.Day >= 1 && data.Day <= 1_000_000, "Day");
        Require(Number(data.TimeOfDay, 0, 24), "TimeOfDay");
        Require(Number(data.Memory,0,100) && Number(data.Curiosity,0,100) && Number(data.Empathy,0,100) &&
            Number(data.Agency,0,100) && Number(data.Courage,0,100) && Number(data.Awareness,0,100), "qualities");
        Require(data.DailyObjectiveDay >= 1 && data.DailyObjectiveDay <= data.Day &&
            data.DailyTalkProgress >= 0 && data.DailyTalkProgress <= HeroProgress.DailyTalkGoal, "daily objective");
        Require(data.LastSavedUtc.Year >= 2000, "LastSavedUtc");
        Require(data.DiscoveredEggs != null && data.RewardedDialogueChoices != null &&
            data.MemoryEvents != null && data.MemoryAnchors != null, "collections");
        Require(data.DiscoveredEggs!.All(id => !string.IsNullOrWhiteSpace(id)) &&
            data.RewardedDialogueChoices!.All(id => !string.IsNullOrWhiteSpace(id)), "reward ids");
        if (data.Npcs != null)
        {
            Require(data.Npcs.Count <= 50, "npc count");
            foreach (var npc in data.Npcs)
                Require(npc != null && Number(npc.Friendliness,0,100) && Number(npc.Trust,0,100) &&
                    Number(npc.Awareness,0,100) && Number(npc.LastTalkDay,-1,data.Day) && npc.TimesTalked >= 0 &&
                    (npc.State == "" || Enum.TryParse<NpcState>(npc.State,out var state) && Enum.IsDefined(state)), "npc");
        }
        var ledger = new MemoryLedger();
        foreach (var memoryEvent in data.MemoryEvents!)
            Require(ledger.RegisterEvent(memoryEvent) && memoryEvent.Day <= data.Day, "event");
        foreach (var anchor in data.MemoryAnchors!)
            Require(anchor != null && Enum.IsDefined(anchor.Status) && anchor.Witnesses != null &&
                anchor.Witnesses.All(w => w != null && w.NpcId >= 0) && ledger.RegisterAnchor(anchor.ToAnchor()), "anchor");
        new FirstDistrictEpisode().Restore(data.DistrictEpisode, ledger);
        data.Player?.Validate(data.Seed);
    }
}
