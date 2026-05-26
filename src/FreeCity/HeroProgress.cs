using System;
using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public enum HeroQuality
{
    Memory,
    Curiosity,
    Empathy,
    Agency,
    Courage,
}

public class HeroProgress
{
    public const float OfflineGrowthAwarenessThreshold = 70f;
    private const double MaxOfflineGrowthMinutes = 12 * 60;

    public int Day { get; private set; } = 1;
    public float Memory { get; private set; } = 0f; // 0-100
    public float Curiosity { get; private set; } = 0f; // 0-100
    public float Empathy { get; private set; } = 0f; // 0-100
    public float Agency { get; private set; } = 0f; // 0-100
    public float Courage { get; private set; } = 0f; // 0-100
    public double LastOfflineGrowthMinutes { get; private set; }
    public HeroQuality? LastLeveledQuality { get; private set; }
    public int LastLeveledQualityLevel { get; private set; }

    // Track which Easter eggs have been discovered to prevent repeated gains
    private readonly HashSet<string> _discoveredEggs = new();
    public IReadOnlyCollection<string> DiscoveredEggs => _discoveredEggs;

    public void NewDay()
    {
        Day++;
        // Slight decay of qualities overnight to encourage active play
        Memory = Math.Max(0f, Memory - 0.5f);
        Curiosity = Math.Max(0f, Curiosity - 0.5f);
        Empathy = Math.Max(0f, Empathy - 0.5f);
        Agency = Math.Max(0f, Agency - 0.5f);
        Courage = Math.Max(0f, Courage - 0.5f);
    }

    public bool DiscoverEgg(string eggId, float memoryGain = 5f, float curiosityGain = 5f, float empathyGain = 0f, float agencyGain = 0f, float courageGain = 0f)
    {
        if (_discoveredEggs.Contains(eggId)) return false;
        _discoveredEggs.Add(eggId);

        AddQualities(memoryGain, curiosityGain, empathyGain, agencyGain, courageGain);
        return true;
    }

    /// <summary>
    /// Load discovered eggs without applying any quality gains (used when loading a save).
    /// </summary>
    public void LoadDiscoveredEggs(IEnumerable<string> eggs)
    {
        if (eggs == null) return;
        foreach (var egg in eggs)
        {
            _discoveredEggs.Add(egg);
        }
    }

    public void AddQualities(float memory = 0f, float curiosity = 0f, float empathy = 0f, float agency = 0f, float courage = 0f)
    {
        LastLeveledQuality = null;
        LastLeveledQualityLevel = 0;

        Memory = ApplyQuality(HeroQuality.Memory, Memory, memory);
        Curiosity = ApplyQuality(HeroQuality.Curiosity, Curiosity, curiosity);
        Empathy = ApplyQuality(HeroQuality.Empathy, Empathy, empathy);
        Agency = ApplyQuality(HeroQuality.Agency, Agency, agency);
        Courage = ApplyQuality(HeroQuality.Courage, Courage, courage);
    }

    public void Restore(int day, float memory, float curiosity, float empathy, float agency, float courage)
    {
        Day = Math.Max(1, day);
        Memory = ClampQuality(memory);
        Curiosity = ClampQuality(curiosity);
        Empathy = ClampQuality(empathy);
        Agency = ClampQuality(agency);
        Courage = ClampQuality(courage);
    }

    public double ApplyOfflineGrowth(double minutesAway)
    {
        LastOfflineGrowthMinutes = Math.Clamp(minutesAway, 0, MaxOfflineGrowthMinutes);
        if (LastOfflineGrowthMinutes <= 0) return 0;

        float hours = (float)(LastOfflineGrowthMinutes / 60.0);
        AddQualities(
            memory: hours * 0.7f,
            curiosity: hours * 0.8f,
            empathy: hours * 0.45f,
            agency: hours * 0.65f,
            courage: hours * 0.35f);

        return LastOfflineGrowthMinutes;
    }

    public override string ToString()
    {
        return $"День {Day}: Память {Memory:F1}, Любопытство {Curiosity:F1}, Эмпатия {Empathy:F1}, Агентность {Agency:F1}, Мужество {Courage:F1}";
    }

    private static float ClampQuality(float value) => Math.Clamp(value, 0f, 100f);

    private static int QualityLevel(float value) => Math.Clamp((int)(value / 25f), 0, 4);

    private float ApplyQuality(HeroQuality quality, float value, float amount)
    {
        if (Math.Abs(amount) < 0.001f) return value;

        int before = QualityLevel(value);
        value = ClampQuality(value + amount);
        int after = QualityLevel(value);

        if (after > before)
        {
            LastLeveledQuality = quality;
            LastLeveledQualityLevel = after;
        }

        return value;
    }
}
