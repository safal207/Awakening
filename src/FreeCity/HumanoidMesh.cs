using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Строит список 3D-боксов (каждый бокс — ориентированный параллелепипед),
/// описывающих реалистичную гуманоидную фигуру главного героя.
/// Меш собирается один раз при создании и переиспользуется при рендере.
///
/// Референс: парень со светлыми волосами, голубая рубашка, тёмные брюки,
/// коричневый кожаный ремень, тёмные туфли, синие глаза.
/// </summary>
public static class HumanoidMesh
{
    // ── Внутренние структуры ───────────────────────────────────────────────

    /// <summary>Один сегмент тела: позиция центра, размер, поворот, цвет.</summary>
    public readonly record struct BodyBox(
        Vector3 Center,
        Vector3 Size,
        float   RotX,   // дополнительный поворот вокруг X (для суставов)
        float   RotY,   // поворот вокруг Y (плечи/бёдра)
        float   RotZ,   // поворот вокруг Z (качание при ходьбе)
        Vector3 Color);

    // ── Пропорции (в условных единицах, масштабируются на Height) ──────────
    private const float HeadH   = 0.32f;
    private const float HeadW   = 0.28f;
    private const float HeadD   = 0.26f;
    private const float NeckH   = 0.08f;
    private const float NeckW   = 0.11f;
    private const float TorsoH  = 0.42f;
    private const float TorsoW  = 0.38f;
    private const float TorsoD  = 0.20f;
    private const float ShoulderH = 0.10f;
    private const float UpperArmH = 0.24f;
    private const float UpperArmW = 0.11f;
    private const float ForeArmH  = 0.22f;
    private const float ForeArmW  = 0.09f;
    private const float HandH     = 0.10f;
    private const float HandW     = 0.08f;
    private const float BeltH     = 0.05f;
    private const float UpperLegH = 0.26f;
    private const float UpperLegW = 0.14f;
    private const float LowerLegH = 0.24f;
    private const float LowerLegW = 0.11f;
    private const float FootH     = 0.07f;
    private const float FootL     = 0.18f;

    // ── Построение меша ────────────────────────────────────────────────────

    /// <summary>
    /// Генерирует список боксов для Т-позы.
    /// <paramref name="scale"/> = hero.Height / 1.78f.
    /// </summary>
    public static List<BodyBox> BuildTPose(float scale = 1f)
    {
        var boxes = new List<BodyBox>(64);
        float s = scale;

        // Координатная ось: Y — вверх. Ноги начинаются у Y=0, голова сверху.
        float floorY   = 0f;
        float legTopY  = floorY + (LowerLegH + FootH) * s;
        float beltY    = legTopY + UpperLegH * s;
        float torsoBot = beltY + BeltH * s;
        float torsoMid = torsoBot + TorsoH * s * 0.5f;
        float torsoTop = torsoBot + TorsoH * s;
        float neckBot  = torsoTop;
        float headBot  = neckBot + NeckH * s;
        float headMid  = headBot + HeadH * s * 0.5f;
        float headTop  = headBot + HeadH * s;

        float armTopY  = torsoTop - ShoulderH * s * 0.5f;
        float armMidY  = armTopY  - UpperArmH * s * 0.5f;
        float armBotY  = armTopY  - UpperArmH * s;
        float foreMidY = armBotY  - ForeArmH  * s * 0.5f;
        float foreBotY = armBotY  - ForeArmH  * s;
        float handMidY = foreBotY - HandH     * s * 0.5f;

        float lx      = TorsoW * s * 0.5f + UpperArmW * s * 0.5f + 0.01f * s;
        float legOff  = UpperLegW * s * 0.45f;

        // ── НОГИ ──────────────────────────────────────────────────────────
        // index 0: левое бедро
        boxes.Add(B(new(-legOff, legTopY + UpperLegH * s * 0.5f, 0),
                     new(UpperLegW * s, UpperLegH * s, UpperLegW * s), HeroStyle.Pants));
        // index 1: правое бедро
        boxes.Add(B(new(+legOff, legTopY + UpperLegH * s * 0.5f, 0),
                     new(UpperLegW * s, UpperLegH * s, UpperLegW * s), HeroStyle.Pants));
        // index 2: левое голень
        boxes.Add(B(new(-legOff, legTopY - LowerLegH * s * 0.5f + FootH * s, 0),
                     new(LowerLegW * s, LowerLegH * s, LowerLegW * s), HeroStyle.PantsDark));
        // index 3: правая голень
        boxes.Add(B(new(+legOff, legTopY - LowerLegH * s * 0.5f + FootH * s, 0),
                     new(LowerLegW * s, LowerLegH * s, LowerLegW * s), HeroStyle.PantsDark));
        // index 4,5: ступни
        boxes.Add(B(new(-legOff, floorY + FootH * s * 0.5f, FootL * s * 0.2f),
                     new(LowerLegW * s * 1.05f, FootH * s, FootL * s), HeroStyle.Shoe));
        boxes.Add(B(new(+legOff, floorY + FootH * s * 0.5f, FootL * s * 0.2f),
                     new(LowerLegW * s * 1.05f, FootH * s, FootL * s), HeroStyle.Shoe));
        // index 6,7: подошвы
        boxes.Add(B(new(-legOff, floorY + 0.012f * s, FootL * s * 0.2f),
                     new(LowerLegW * s * 1.10f, 0.022f * s, FootL * s * 1.05f), HeroStyle.ShoeSole));
        boxes.Add(B(new(+legOff, floorY + 0.012f * s, FootL * s * 0.2f),
                     new(LowerLegW * s * 1.10f, 0.022f * s, FootL * s * 1.05f), HeroStyle.ShoeSole));

        // ── ПОЯС ──────────────────────────────────────────────────────────
        // index 8: ремень
        boxes.Add(B(new(0, beltY + BeltH * s * 0.5f, 0),
                     new(TorsoW * s * 1.05f, BeltH * s, TorsoD * s * 1.05f), HeroStyle.Belt));
        // index 9: пряжка
        boxes.Add(B(new(0, beltY + BeltH * s * 0.5f, TorsoD * s * 0.5f + 0.005f * s),
                     new(0.07f * s, BeltH * s * 0.8f, 0.008f * s), HeroStyle.BeltMetal));

        // ── ТОРС ──────────────────────────────────────────────────────────
        // index 10: основной торс
        boxes.Add(B(new(0, torsoMid, 0),
                     new(TorsoW * s, TorsoH * s, TorsoD * s), HeroStyle.ShirtBlue));
        // index 11: полоса пуговиц (блик по центру)
        boxes.Add(B(new(0, torsoMid, TorsoD * s * 0.5f + 0.002f * s),
                     new(0.04f * s, TorsoH * s * 0.82f, 0.004f * s), HeroStyle.ShirtLight));
        // index 12,13: плечевые вставки
        boxes.Add(B(new(-TorsoW * s * 0.42f, torsoTop - 0.05f * s, 0),
                     new(0.07f * s, 0.08f * s, TorsoD * s), HeroStyle.ShirtDark));
        boxes.Add(B(new(+TorsoW * s * 0.42f, torsoTop - 0.05f * s, 0),
                     new(0.07f * s, 0.08f * s, TorsoD * s), HeroStyle.ShirtDark));
        // index 14,15: воротник
        boxes.Add(B(new(-0.04f * s, torsoTop + 0.01f * s, TorsoD * s * 0.35f),
                     new(0.08f * s, 0.05f * s, TorsoD * s * 0.22f), HeroStyle.Collar));
        boxes.Add(B(new(+0.04f * s, torsoTop + 0.01f * s, TorsoD * s * 0.35f),
                     new(0.08f * s, 0.05f * s, TorsoD * s * 0.22f), HeroStyle.Collar));
        // index 16-19: пуговицы (4 шт)
        for (int i = 0; i < 4; i++)
        {
            float by = torsoBot + TorsoH * s * (0.18f + i * 0.20f);
            boxes.Add(B(new(0, by, TorsoD * s * 0.5f + 0.004f * s),
                         new(0.018f * s, 0.018f * s, 0.006f * s), HeroStyle.Button));
        }
        // index 20: нагрудный карман (левый)
        boxes.Add(B(new(-TorsoW * s * 0.22f, torsoTop - TorsoH * s * 0.25f, TorsoD * s * 0.5f + 0.003f * s),
                     new(0.09f * s, 0.06f * s, 0.005f * s), HeroStyle.ShirtDark));

        // ── ПЛЕЧИ ─────────────────────────────────────────────────────────
        // index 21,22
        float shoulderSize = UpperArmW * s * 1.15f;
        boxes.Add(B(new(-lx, armTopY, 0), new(shoulderSize, ShoulderH * s, shoulderSize), HeroStyle.ShirtBlue));
        boxes.Add(B(new(+lx, armTopY, 0), new(shoulderSize, ShoulderH * s, shoulderSize), HeroStyle.ShirtBlue));

        // ── РУКИ ──────────────────────────────────────────────────────────
        // index 23,24: плечо (верхняя часть)
        boxes.Add(B(new(-lx, armMidY, 0), new(UpperArmW * s, UpperArmH * s, UpperArmW * s), HeroStyle.ShirtBlue));
        boxes.Add(B(new(+lx, armMidY, 0), new(UpperArmW * s, UpperArmH * s, UpperArmW * s), HeroStyle.ShirtBlue));
        // index 25,26: предплечье (кожа — рукав закатан)
        boxes.Add(B(new(-lx, foreMidY, 0), new(ForeArmW * s, ForeArmH * s, ForeArmW * s), HeroStyle.Skin));
        boxes.Add(B(new(+lx, foreMidY, 0), new(ForeArmW * s, ForeArmH * s, ForeArmW * s), HeroStyle.Skin));
        // index 27,28: кисти
        boxes.Add(B(new(-lx, handMidY, 0), new(HandW * s, HandH * s, HandW * s * 0.7f), HeroStyle.Skin));
        boxes.Add(B(new(+lx, handMidY, 0), new(HandW * s, HandH * s, HandW * s * 0.7f), HeroStyle.Skin));
        // index 29,30: часы на левом запястье
        boxes.Add(B(new(-lx, foreBotY + 0.02f * s, 0),
                     new(ForeArmW * s * 1.2f, 0.025f * s, ForeArmW * s * 1.2f), HeroStyle.WatchBezel));
        boxes.Add(B(new(-lx, foreBotY + 0.021f * s, ForeArmW * s * 0.55f),
                     new(0.055f * s, 0.022f * s, 0.010f * s), HeroStyle.WatchFace));

        // ── ШЕЯ ───────────────────────────────────────────────────────────
        // index 31
        boxes.Add(B(new(0, neckBot + NeckH * s * 0.5f, 0),
                     new(NeckW * s, NeckH * s, NeckW * s), HeroStyle.Skin));

        // ── ГОЛОВА ────────────────────────────────────────────────────────
        // index 32: основной череп
        boxes.Add(B(new(0, headMid, 0), new(HeadW * s, HeadH * s, HeadD * s), HeroStyle.Skin));
        // index 33: затылочная тень
        boxes.Add(B(new(0, headMid, -HeadD * s * 0.44f),
                     new(HeadW * s * 0.96f, HeadH * s * 0.9f, 0.01f * s), HeroStyle.SkinShadow));
        // index 34: лоб (светлее)
        boxes.Add(B(new(0, headTop - HeadH * s * 0.22f, HeadD * s * 0.44f),
                     new(HeadW * s * 0.88f, HeadH * s * 0.28f, 0.01f * s), HeroStyle.SkinHighlight));
        // index 35,36: скулы/щёки
        boxes.Add(B(new(-HeadW * s * 0.42f, headMid - HeadH * s * 0.05f, HeadD * s * 0.3f),
                     new(0.012f * s, HeadH * s * 0.38f, HeadD * s * 0.5f), HeroStyle.SkinShadow));
        boxes.Add(B(new(+HeadW * s * 0.42f, headMid - HeadH * s * 0.05f, HeadD * s * 0.3f),
                     new(0.012f * s, HeadH * s * 0.38f, HeadD * s * 0.5f), HeroStyle.SkinShadow));

        // ── ГЛАЗА ─────────────────────────────────────────────────────────
        float eyeY = headMid + HeadH * s * 0.10f;
        float eyeZ = HeadD * s * 0.50f;
        float eyeW = 0.062f * s;
        float eyeH = 0.040f * s;
        float eyeD = 0.005f * s;
        // белки
        boxes.Add(B(new(-HeadW * s * 0.20f, eyeY, eyeZ), new(eyeW, eyeH, eyeD), new Vector3(0.97f, 0.97f, 0.97f)));
        boxes.Add(B(new(+HeadW * s * 0.20f, eyeY, eyeZ), new(eyeW, eyeH, eyeD), new Vector3(0.97f, 0.97f, 0.97f)));
        // радужки (голубые)
        boxes.Add(B(new(-HeadW * s * 0.20f, eyeY, eyeZ + 0.001f * s), new(eyeW * 0.65f, eyeH * 0.85f, eyeD), HeroStyle.Eye));
        boxes.Add(B(new(+HeadW * s * 0.20f, eyeY, eyeZ + 0.001f * s), new(eyeW * 0.65f, eyeH * 0.85f, eyeD), HeroStyle.Eye));
        // зрачки
        boxes.Add(B(new(-HeadW * s * 0.20f, eyeY, eyeZ + 0.002f * s), new(eyeW * 0.30f, eyeH * 0.55f, eyeD), HeroStyle.Iris));
        boxes.Add(B(new(+HeadW * s * 0.20f, eyeY, eyeZ + 0.002f * s), new(eyeW * 0.30f, eyeH * 0.55f, eyeD), HeroStyle.Iris));
        // верхние веки
        boxes.Add(B(new(-HeadW * s * 0.20f, eyeY + eyeH * 0.45f, eyeZ + 0.001f * s),
                     new(eyeW * 1.05f, eyeH * 0.15f, eyeD), new Vector3(0.15f, 0.10f, 0.08f)));
        boxes.Add(B(new(+HeadW * s * 0.20f, eyeY + eyeH * 0.45f, eyeZ + 0.001f * s),
                     new(eyeW * 1.05f, eyeH * 0.15f, eyeD), new Vector3(0.15f, 0.10f, 0.08f)));

        // ── БРОВИ ─────────────────────────────────────────────────────────
        float browY = eyeY + eyeH + 0.018f * s;
        boxes.Add(B(new(-HeadW * s * 0.20f, browY, eyeZ), new(eyeW * 1.15f, 0.018f * s, eyeD), HeroStyle.Brow));
        boxes.Add(B(new(+HeadW * s * 0.20f, browY, eyeZ), new(eyeW * 1.15f, 0.018f * s, eyeD), HeroStyle.Brow));

        // ── НОС ───────────────────────────────────────────────────────────
        boxes.Add(B(new(0, headMid - HeadH * s * 0.06f, HeadD * s * 0.52f),
                     new(0.04f * s, 0.06f * s, 0.04f * s), HeroStyle.SkinShadow));

        // ── РОТ / ГУБЫ ────────────────────────────────────────────────────
        float mouthY = headMid - HeadH * s * 0.24f;
        boxes.Add(B(new(0, mouthY, HeadD * s * 0.50f), new(0.08f * s, 0.016f * s, eyeD), HeroStyle.Lip));

        // ── ВОЛОСЫ ────────────────────────────────────────────────────────
        // верхняя шапка волос
        boxes.Add(B(new(0, headTop + 0.005f * s, -0.01f * s),
                     new(HeadW * s * 1.05f, 0.06f * s, HeadD * s * 1.0f), HeroStyle.HairBlonde));
        // боковые пряди
        boxes.Add(B(new(-HeadW * s * 0.51f, headMid + HeadH * s * 0.28f, 0),
                     new(0.022f * s, HeadH * s * 0.48f, HeadD * s * 0.95f), HeroStyle.HairBlonde));
        boxes.Add(B(new(+HeadW * s * 0.51f, headMid + HeadH * s * 0.28f, 0),
                     new(0.022f * s, HeadH * s * 0.48f, HeadD * s * 0.95f), HeroStyle.HairBlonde));
        // чёлка (немного вперёд)
        boxes.Add(B(new(0, headTop - HeadH * s * 0.12f, HeadD * s * 0.50f),
                     new(HeadW * s * 0.90f, 0.045f * s, 0.022f * s), HeroStyle.HairBlonde));
        // затылок
        boxes.Add(B(new(0, headMid + HeadH * s * 0.18f, -HeadD * s * 0.50f),
                     new(HeadW * s * 1.00f, HeadH * s * 0.45f, 0.022f * s), HeroStyle.HairShadow));
        // тень в волосах сверху
        boxes.Add(B(new(0, headTop - HeadH * s * 0.02f, -0.015f * s),
                     new(HeadW * s * 0.80f, 0.025f * s, HeadD * s * 0.70f), HeroStyle.HairShadow));

        return boxes;
    }

    // ── Анимации ───────────────────────────────────────────────────────────

    /// <summary>
    /// Применяет Walking-анимацию: качание рук и ног, лёгкое покачивание торса.
    /// phase = AnimPhase персонажа, blend = 0..1 (0=стоит, 1=полный шаг).
    /// </summary>
    public static void ApplyWalkAnim(List<BodyBox> boxes, float phase, float blend)
    {
        float swing    = blend * MathF.Sin(phase) * 0.38f;
        float armSwing = -swing * 0.75f;

        // бёдра (0, 1), голени (2, 3)
        ReplaceRotX(boxes, 0,  swing);
        ReplaceRotX(boxes, 1, -swing);
        ReplaceRotX(boxes, 2,  swing * 0.5f);
        ReplaceRotX(boxes, 3, -swing * 0.5f);
        // плечи (23, 24), предплечья (25, 26)
        ReplaceRotX(boxes, 23,  armSwing);
        ReplaceRotX(boxes, 24, -armSwing);
        ReplaceRotX(boxes, 25,  armSwing * 0.6f);
        ReplaceRotX(boxes, 26, -armSwing * 0.6f);
    }

    /// <summary>
    /// Idle-анимация: лёгкое дыхание (покачивание торса по Y), моргание.
    /// </summary>
    public static void ApplyIdleAnim(List<BodyBox> boxes, float time)
    {
        float breath = MathF.Sin(time * 1.1f) * 0.008f;
        // index 10 — основной торс
        if (boxes.Count > 10)
        {
            var b = boxes[10];
            boxes[10] = b with { Center = b.Center + new Vector3(0, breath, 0) };
        }
    }

    // ── Хелперы ────────────────────────────────────────────────────────────

    private static BodyBox B(Vector3 center, Vector3 size, Vector3 color)
        => new(center, size, 0f, 0f, 0f, color);

    private static void ReplaceRotX(List<BodyBox> boxes, int i, float rotX)
    {
        if (i < 0 || i >= boxes.Count) return;
        var b = boxes[i];
        boxes[i] = b with { RotX = rotX };
    }
}
