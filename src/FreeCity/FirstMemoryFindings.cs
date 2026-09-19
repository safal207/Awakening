using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public sealed record StoryFinding(
    string Id,
    string Title,
    ObservationKind Kind,
    string JournalText,
    Vector3 Position,
    int UnlockMorning);

/// <summary>
/// Six bounded environmental observations for the First Memory chapter.
/// They grant knowledge only: no qualities, Awareness, achievements or counters.
/// </summary>
public static class FirstMemoryFindings
{
    public const string JournalPrefix = "first_memory.finding.";

    private static readonly StoryFinding[] AllFindings =
    {
        new(
            "relay_service_marks",
            "Следы на реле",
            ObservationKind.Fact,
            "Винты корпуса реле исцарапаны так, будто его вскрывали и собирали много раз.",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(1.4f, 0f, -1.7f)),
            1),
        new(
            "repeated_timetable_correction",
            "Исправление расписания",
            ObservationKind.Question,
            "На старом расписании одна и та же минута исправлена несколько раз. Почему правка каждый раз оказывается здесь?",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(4.7f, 0f, 4.2f)),
            1),
        new(
            "mark_seed_tags",
            "Бирки рассады",
            ObservationKind.Fact,
            "На бирках Марка разные растения, но дата высадки повторяется как сегодняшняя.",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(6.0f, 0f, -2.5f)),
            2),
        new(
            "dispatcher_carbon_copy",
            "Копирка диспетчера",
            ObservationKind.Question,
            "На чистой копирке остались продавленные строки от текста, которого в сегодняшнем журнале нет.",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(-3.2f, 0f, 2.6f)),
            2),
        new(
            "archive_number_gap",
            "Пропуск в архиве",
            ObservationKind.Fact,
            "В нумерации архивных карточек Ники есть последовательный пропуск, хотя соседние записи оформлены без ошибок.",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(-9.0f, 0f, 6.5f)),
            3),
        new(
            "plaza_bench_wear",
            "Износ пустой скамьи",
            ObservationKind.Question,
            "Два места на скамье заметно истёрты, хотя расписание почти не приводит людей на эту площадь одновременно.",
            Ground(FirstMemorySpatial.SignalPosition + new Vector3(3.5f, 0f, 7.0f)),
            3),
    };

    public static IReadOnlyList<StoryFinding> All => AllFindings;

    public static bool IsDiscovered(HeroProgress progress, string findingId) =>
        ObservationJournalState.For(progress).TryGet(JournalId(findingId), out _);

    public static bool IsAvailable(HeroProgress progress, StoryFinding finding)
    {
        if (progress == null || finding == null) return false;
        FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
        int morning = chapter.Morning(progress.Day);

        if (finding.UnlockMorning > morning || IsDiscovered(progress, finding.Id))
            return false;

        // Morning-one clues become actionable only after the player has engaged
        // with the broken signal. Old unlocked clues remain available later.
        if (finding.UnlockMorning == 1 &&
            !chapter.InvestigationCompleted &&
            !chapter.RoutineRepairDayOne &&
            !FirstMemorySpatial.IsSignalObserved(progress))
            return false;

        return true;
    }

    public static bool TryFindNearby(
        HeroProgress progress,
        Vector3 playerPosition,
        float range,
        out StoryFinding? finding)
    {
        finding = null;
        float best = range * range;
        foreach (StoryFinding candidate in AllFindings)
        {
            if (!IsAvailable(progress, candidate)) continue;
            float distance = Vector3.DistanceSquared(playerPosition, candidate.Position);
            if (distance > best) continue;
            best = distance;
            finding = candidate;
        }
        return finding != null;
    }

    public static bool TryDiscover(HeroProgress progress, string findingId, out StoryFinding? finding)
    {
        finding = Find(findingId);
        if (finding == null || !IsAvailable(progress, finding))
            return false;

        return ObservationJournalState.For(progress).TryAdd(
            JournalId(finding.Id),
            finding.Kind,
            finding.JournalText,
            progress.Day,
            ObservationStatus.Completed);
    }

    public static StoryFinding? Find(string findingId)
    {
        if (string.IsNullOrWhiteSpace(findingId)) return null;
        foreach (StoryFinding finding in AllFindings)
            if (string.Equals(finding.Id, findingId, StringComparison.Ordinal))
                return finding;
        return null;
    }

    internal static void AppendProps(List<float> vertices)
    {
        Vector3 metal = new(0.18f, 0.21f, 0.22f);
        Vector3 paper = new(0.80f, 0.79f, 0.68f);
        Vector3 wood = new(0.42f, 0.28f, 0.17f);
        Vector3 archive = new(0.32f, 0.46f, 0.50f);
        Vector3 plant = new(0.28f, 0.45f, 0.24f);

        // 1: opened relay cover / service marks.
        Vector3 p = AllFindings[0].Position;
        SceneGeometry.Box(vertices, p + new Vector3(-0.26f, 0.02f, -0.19f), new(0.52f, 0.22f, 0.38f), metal);
        SceneGeometry.Box(vertices, p + new Vector3(-0.22f, 0.25f, -0.03f), new(0.44f, 0.035f, 0.28f), metal * 1.25f);

        // 2: old timetable board.
        p = AllFindings[1].Position;
        SceneGeometry.Cylinder(vertices, p, p + new Vector3(0f, 1.65f, 0f), 0.045f, metal, 8);
        SceneGeometry.Box(vertices, p + new Vector3(-0.48f, 0.82f, -0.05f), new(0.96f, 0.68f, 0.10f), wood);
        SceneGeometry.Box(vertices, p + new Vector3(-0.41f, 0.90f, -0.061f), new(0.82f, 0.52f, 0.025f), paper);

        // 3: Mark's seed-tag crate.
        p = AllFindings[2].Position;
        SceneGeometry.Box(vertices, p + new Vector3(-0.52f, 0.02f, -0.35f), new(1.04f, 0.34f, 0.70f), wood);
        for (int i = 0; i < 3; i++)
        {
            float x = -0.28f + i * 0.28f;
            SceneGeometry.Cylinder(vertices, p + new Vector3(x, 0.34f, 0f), p + new Vector3(x, 0.72f, 0f), 0.025f, plant, 6);
            SceneGeometry.Box(vertices, p + new Vector3(x - 0.08f, 0.53f, -0.025f), new(0.16f, 0.12f, 0.05f), paper);
        }

        // 4: dispatcher carbon-copy board.
        p = AllFindings[3].Position;
        SceneGeometry.Box(vertices, p + new Vector3(-0.38f, 0.64f, -0.06f), new(0.76f, 0.52f, 0.12f), metal);
        SceneGeometry.Box(vertices, p + new Vector3(-0.32f, 0.70f, -0.071f), new(0.64f, 0.40f, 0.025f), new(0.35f, 0.29f, 0.45f));
        SceneGeometry.Cylinder(vertices, p, p + new Vector3(0f, 0.70f, 0f), 0.04f, metal, 8);

        // 5: archive card box with a visible gap.
        p = AllFindings[4].Position;
        SceneGeometry.Box(vertices, p + new Vector3(-0.44f, 0.02f, -0.28f), new(0.88f, 0.38f, 0.56f), archive);
        for (int i = 0; i < 4; i++)
        {
            if (i == 2) continue;
            float x = -0.30f + i * 0.20f;
            SceneGeometry.Box(vertices, p + new Vector3(x, 0.40f, -0.18f), new(0.12f, 0.28f, 0.36f), paper);
        }

        // 6: two-seat worn bench.
        p = AllFindings[5].Position;
        SceneGeometry.Box(vertices, p + new Vector3(-1.05f, 0.48f, -0.28f), new(2.10f, 0.16f, 0.56f), wood);
        SceneGeometry.Box(vertices, p + new Vector3(-1.05f, 0.84f, 0.17f), new(2.10f, 0.48f, 0.12f), wood * 0.92f);
        foreach (float x in new[] { -0.78f, 0.68f })
            SceneGeometry.Box(vertices, p + new Vector3(x, 0.02f, -0.20f), new(0.10f, 0.52f, 0.10f), metal);
        SceneGeometry.Box(vertices, p + new Vector3(-0.82f, 0.63f, -0.285f), new(0.54f, 0.018f, 0.57f), wood * 1.35f);
        SceneGeometry.Box(vertices, p + new Vector3(0.28f, 0.63f, -0.285f), new(0.54f, 0.018f, 0.57f), wood * 1.35f);
    }

    private static string JournalId(string findingId) => JournalPrefix + findingId;

    private static Vector3 Ground(Vector3 point)
    {
        point.Y = CityGenerator.GroundHeight(point.X, point.Z);
        return point;
    }
}
