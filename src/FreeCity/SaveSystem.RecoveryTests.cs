using System;
using System.Collections.Generic;
using System.IO;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private static bool RunSaveRecoverySelfTest(out string message)
    {
        var paths = new List<string>();
        try
        {
            CheckValidPrimaryLoadsNormally(paths);
            CheckCorruptPrimaryRecoversBackup(paths);
            CheckCorruptPrimaryAndBackupFailExplicitly(paths);
            CheckFuturePrimaryFallsBackToBackup(paths);
            CheckStaleTempIsIgnored(paths);
            message = "Save recovery self-test passed (5 cases).";
            return true;
        }
        catch (Exception e)
        {
            message = "Save recovery self-test failed: " + e.Message;
            return false;
        }
        finally
        {
            foreach (string path in paths)
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
                try
                {
                    string backup = BackupPathFor(path);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                catch { }
            }
        }
    }

    private static void CheckValidPrimaryLoadsNormally(List<string> paths)
    {
        string path = TempSavePath(paths, "recovery-valid-primary");
        SaveToPath(path, 71001, new HeroProgress(), new AwarenessSystem(), 9f, DateTime.UtcNow, null);

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.seed == 71001, "valid primary must be loaded directly");
        RequireMemory(loaded.progress.RecoveryState == SaveRecoveryState.None,
            "valid primary must not enter recovery state");
        RequireMemory(!loaded.progress.SaveWritesBlocked,
            "valid primary must remain writable");
    }

    private static void CheckCorruptPrimaryRecoversBackup(List<string> paths)
    {
        string path = TempSavePath(paths, "recovery-backup");
        var progress = new HeroProgress();
        var awareness = new AwarenessSystem();

        SaveToPath(path, 72001, progress, awareness, 8f, DateTime.UtcNow.AddMinutes(-5), null);
        SaveToPath(path, 72002, progress, awareness, 10f, DateTime.UtcNow, null);
        string backupPath = BackupPathFor(path);
        RequireMemory(File.Exists(backupPath), "second valid save must preserve previous primary as backup");

        File.WriteAllText(path, "{ definitely-not-json ");
        string corruptPrimary = File.ReadAllText(path);

        var recovered = LoadFromPath(path);
        RequireMemory(recovered.seed == 72001 && Math.Abs(recovered.timeOfDay - 8f) < 0.001f,
            "corrupt primary must recover the previous verified backup");
        RequireMemory(recovered.progress.RecoveryState == SaveRecoveryState.RecoveredFromBackup,
            "backup recovery must be explicit in HeroProgress");
        RequireMemory(recovered.progress.SaveWritesBlocked,
            "recovered session must block automatic overwrite before acknowledgement");

        SaveToPath(path, 72003, recovered.progress, awareness, 12f, DateTime.UtcNow, null);
        RequireMemory(File.ReadAllText(path) == corruptPrimary,
            "blocked recovered session must not silently overwrite corrupt primary");

        RequireMemory(recovered.progress.AcknowledgeRecoveryForOverwrite(),
            "recovered backup must support explicit overwrite acknowledgement");
        RequireMemory(!recovered.progress.SaveWritesBlocked &&
                      recovered.progress.RecoveryState == SaveRecoveryState.Acknowledged,
            "acknowledgement must be the explicit write-unblock boundary");

        SaveToPath(path, 72003, recovered.progress, awareness, 12f, DateTime.UtcNow, null);
        var afterAcknowledgement = LoadFromPath(path);
        RequireMemory(afterAcknowledgement.seed == 72003,
            "explicit acknowledgement must allow a new valid primary save");
        RequireMemory(afterAcknowledgement.progress.RecoveryState == SaveRecoveryState.None,
            "new valid primary must load normally after acknowledged recovery save");
    }

    private static void CheckCorruptPrimaryAndBackupFailExplicitly(List<string> paths)
    {
        string path = TempSavePath(paths, "recovery-both-corrupt");
        string backupPath = BackupPathFor(path);
        File.WriteAllText(path, "not-json-primary");
        File.WriteAllText(backupPath, "not-json-backup");

        var failed = LoadFromPath(path);
        RequireMemory(failed.progress.RecoveryState == SaveRecoveryState.LoadFailed,
            "corrupt primary and backup must produce explicit LoadFailed state");
        RequireMemory(failed.progress.SaveWritesBlocked,
            "LoadFailed world must never overwrite old files automatically");
        RequireMemory(!failed.progress.AcknowledgeRecoveryForOverwrite(),
            "LoadFailed must not be unlockable as if it were a trusted recovered world");

        string before = File.ReadAllText(path);
        SaveToPath(path, 73001, failed.progress, new AwarenessSystem(), 8f, DateTime.UtcNow, null);
        RequireMemory(File.ReadAllText(path) == before,
            "failed load must not turn into a silent new-game overwrite");
    }

    private static void CheckFuturePrimaryFallsBackToBackup(List<string> paths)
    {
        string path = TempSavePath(paths, "recovery-future-primary");
        var progress = new HeroProgress();
        var awareness = new AwarenessSystem();
        SaveToPath(path, 74001, progress, awareness, 7f, DateTime.UtcNow.AddMinutes(-2), null);
        SaveToPath(path, 74002, progress, awareness, 9f, DateTime.UtcNow, null);

        var future = BaseSaveData();
        future.SchemaVersion = CurrentSchemaVersion + 1;
        future.Seed = 74999;
        WriteSaveData(path, future);

        var recovered = LoadFromPath(path);
        RequireMemory(recovered.seed == 74001,
            "unsupported future primary must fall back to last verified compatible backup");
        RequireMemory(recovered.progress.RecoveryState == SaveRecoveryState.RecoveredFromBackup,
            "future-schema fallback must be explicit recovery");
        RequireMemory(recovered.progress.SaveWritesBlocked,
            "future-schema recovery must not overwrite source automatically");
    }

    private static void CheckStaleTempIsIgnored(List<string> paths)
    {
        string path = TempSavePath(paths, "recovery-stale-temp");
        SaveToPath(path, 75001, new HeroProgress(), new AwarenessSystem(), 11f, DateTime.UtcNow, null);

        string directory = Path.GetDirectoryName(path) ?? ".";
        string stale = Path.Combine(directory, $".{Path.GetFileName(path)}.stale.tmp");
        paths.Add(stale);
        File.WriteAllText(stale, "partial interrupted write");

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.seed == 75001,
            "stale temp file must never outrank the valid primary");
        RequireMemory(loaded.progress.RecoveryState == SaveRecoveryState.None,
            "stale temp beside valid primary must not trigger recovery");
    }
}
