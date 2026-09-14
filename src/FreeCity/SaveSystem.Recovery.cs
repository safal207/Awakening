using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private enum SaveCandidateStatus
    {
        Invalid,
        Recoverable,
        Valid,
    }

    private readonly record struct LoadedCandidate(
        int Seed,
        HeroProgress Progress,
        float TimeOfDay,
        float Awareness,
        double OfflineMinutes,
        List<NpcSaveData>? Npcs);

    private static string BackupPathFor(string path) => path + ".bak";

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<NpcSaveData>? npcs)
        LoadFromPathWithRecovery(string path)
    {
        string backupPath = BackupPathFor(path);
        bool primaryExists = File.Exists(path);
        bool backupExists = File.Exists(backupPath);

        if (!primaryExists && !backupExists)
            return NewWorldResult();

        SaveCandidateStatus primaryStatus = SaveCandidateStatus.Invalid;
        LoadedCandidate primary = default;
        string primaryReason = primaryExists
            ? TryLoadCandidate(path, applyOfflineGrowth: true, out primary, out primaryStatus)
            : "primary save missing";

        if (primaryStatus == SaveCandidateStatus.Valid)
            return ToTuple(primary);

        SaveCandidateStatus backupStatus = SaveCandidateStatus.Invalid;
        LoadedCandidate backup = default;
        string backupReason = backupExists
            ? TryLoadCandidate(backupPath, applyOfflineGrowth: true, out backup, out backupStatus)
            : "backup save missing";

        if (backupStatus == SaveCandidateStatus.Valid)
        {
            backup.Progress.MarkRecovery(
                SaveRecoveryState.RecoveredFromBackup,
                $"Основное сохранение не прошло проверку ({primaryReason}). Загружена проверенная резервная копия; запись заблокирована до подтверждения.");
            Console.WriteLine(backup.Progress.RecoveryMessage);
            return ToTuple(backup);
        }

        if (primaryStatus == SaveCandidateStatus.Recoverable)
        {
            primary.Progress.MarkRecovery(
                SaveRecoveryState.PartialPrimary,
                $"Сохранение восстановлено частично ({primaryReason}); резервная копия недоступна или невалидна ({backupReason}). Автоматическая запись заблокирована.");
            Console.WriteLine(primary.Progress.RecoveryMessage);
            return ToTuple(primary);
        }

        var failed = new HeroProgress();
        failed.MarkRecovery(
            SaveRecoveryState.LoadFailed,
            $"Не удалось безопасно загрузить сохранение. Primary: {primaryReason}. Backup: {backupReason}. Новый мир не считается успешной загрузкой.");
        Console.WriteLine(failed.RecoveryMessage);
        return (Environment.TickCount, failed, 8f, 0f, 0d, null);
    }

    private static string TryLoadCandidate(
        string path,
        bool applyOfflineGrowth,
        out LoadedCandidate candidate,
        out SaveCandidateStatus status)
    {
        candidate = default;
        status = SaveCandidateStatus.Invalid;

        try
        {
            string json = File.ReadAllText(path);
            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, LoadOptions);
            if (data == null) return "empty save payload";

            if (data.SchemaVersion < 0 || data.SchemaVersion > CurrentSchemaVersion)
                return $"unsupported schema {data.SchemaVersion}";

            if (!ValidateCoreNumbers(data, out string numberReason))
                return numberReason;

            var progress = new HeroProgress();
            progress.Restore(data.Day, data.Memory, data.Curiosity, data.Empathy, data.Agency, data.Courage);
            progress.LoadDiscoveredEggs(data.DiscoveredEggs ?? Enumerable.Empty<string>());
            progress.LoadDailyObjective(data.DailyObjectiveDay, data.DailyTalkProgress, data.DailyObjectiveCompleted, data.DailyTalkedNpcs);

            bool memoryClean = MemoryPersistence.TryRestore(data.MemoryLedger, out MemoryLedger ledger);
            progress.Ledger = ledger;

            bool spatialClean = ValidateSpatialRow(data.Seed, data.PlayerSpatial, out string spatialReason);
            PlayerSpatialPersistence.SetPending(progress, data.PlayerSpatial);
            NpcAwakeningPersistence.SetPending(progress, data.Npcs);

            double minutesAway = Math.Max(0d, (DateTime.UtcNow - data.LastSavedUtc.ToUniversalTime()).TotalMinutes);
            double offlineMinutes = 0d;
            if (applyOfflineGrowth && data.Awareness >= HeroProgress.OfflineGrowthAwarenessThreshold)
                offlineMinutes = progress.ApplyOfflineGrowth(minutesAway);

            int seed = data.Seed == 0 ? Environment.TickCount : data.Seed;
            candidate = new LoadedCandidate(seed, progress, data.TimeOfDay, data.Awareness, offlineMinutes, data.Npcs);

            if (!memoryClean || !spatialClean)
            {
                status = SaveCandidateStatus.Recoverable;
                if (!memoryClean && !spatialClean)
                    return "memory ledger and player spatial state require recovery";
                return !memoryClean ? "memory ledger requires recovery" : spatialReason;
            }

            status = SaveCandidateStatus.Valid;
            return "valid";
        }
        catch (JsonException e)
        {
            return "invalid JSON: " + e.Message;
        }
        catch (IOException e)
        {
            return "I/O failure: " + e.Message;
        }
        catch (UnauthorizedAccessException e)
        {
            return "access failure: " + e.Message;
        }
        catch (Exception e)
        {
            return "validation failure: " + e.Message;
        }
    }

    private static bool ValidateCoreNumbers(SaveData data, out string reason)
    {
        reason = "valid";
        if (data.Day < 1)
        {
            reason = "day must be at least 1";
            return false;
        }

        foreach ((string name, float value) in new[]
        {
            (nameof(data.Memory), data.Memory),
            (nameof(data.Curiosity), data.Curiosity),
            (nameof(data.Empathy), data.Empathy),
            (nameof(data.Agency), data.Agency),
            (nameof(data.Courage), data.Courage),
            (nameof(data.Awareness), data.Awareness),
        })
        {
            if (!float.IsFinite(value) || value < 0f || value > 100f)
            {
                reason = $"{name} is outside 0..100 or non-finite";
                return false;
            }
        }

        if (!float.IsFinite(data.TimeOfDay) || data.TimeOfDay < 0f || data.TimeOfDay > 24f)
        {
            reason = "time of day is outside 0..24 or non-finite";
            return false;
        }

        if (data.Npcs != null)
        {
            var seenPersistentIds = new HashSet<int>();
            for (int i = 0; i < data.Npcs.Count; i++)
            {
                NpcSaveData npc = data.Npcs[i];
                if (!float.IsFinite(npc.Friendliness) || !float.IsFinite(npc.Trust) ||
                    !float.IsFinite(npc.LastTalkDay) || !float.IsFinite(npc.Awareness))
                {
                    reason = $"NPC row {i} contains non-finite numeric data";
                    return false;
                }

                if (npc.PersistentId is int persistentId)
                {
                    if (persistentId < 0 || !seenPersistentIds.Add(persistentId))
                    {
                        reason = $"NPC row {i} has invalid or duplicate PersistentId";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool ValidateSpatialRow(int seed, PlayerSpatialSaveData? data, out string reason)
    {
        reason = "valid";
        if (data == null) return true;

        if (!float.IsFinite(data.X) || !float.IsFinite(data.Y) ||
            !float.IsFinite(data.Z) || !float.IsFinite(data.Rotation))
        {
            reason = "player spatial row contains non-finite values";
            return false;
        }

        float min = -CityGenerator.CityRadius * CityGenerator.CellSize - CityGenerator.CellSize;
        float max = CityGenerator.CityRadius * CityGenerator.CellSize + CityGenerator.BlockSize + CityGenerator.CellSize;
        if (data.X < min || data.X > max || data.Z < min || data.Z > max)
        {
            reason = "player spatial row is outside the generated world envelope";
            return false;
        }

        if (!data.IsInside) return true;
        if (!data.InteriorBlockX.HasValue || !data.InteriorBlockZ.HasValue)
        {
            reason = "interior spatial row is missing block identity";
            return false;
        }

        if (seed == 0)
        {
            reason = "interior spatial row cannot use an unspecified world seed";
            return false;
        }

        foreach (CityBlock block in CityGenerator.Generate(seed))
        {
            if (block.X != data.InteriorBlockX.Value || block.Z != data.InteriorBlockZ.Value) continue;
            if (block.Type is BuildingType.Tree or BuildingType.Lamp)
            {
                reason = "interior block is not enterable";
                return false;
            }
            return true;
        }

        reason = "interior block does not exist in this world seed";
        return false;
    }

    private static bool IsFullyValidSaveFile(string path)
    {
        if (!File.Exists(path)) return false;
        _ = TryLoadCandidate(path, applyOfflineGrowth: false, out _, out SaveCandidateStatus status);
        return status == SaveCandidateStatus.Valid;
    }

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<NpcSaveData>? npcs)
        ToTuple(LoadedCandidate candidate) =>
        (candidate.Seed, candidate.Progress, candidate.TimeOfDay, candidate.Awareness, candidate.OfflineMinutes, candidate.Npcs);

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<NpcSaveData>? npcs)
        NewWorldResult() =>
        (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null);
}
