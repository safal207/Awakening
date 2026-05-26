using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Цвета и константы внешнего вида главного героя.
/// Референс: парень в голубой рубашке (арт-концепт).
/// </summary>
public static class HeroStyle
{
    public const string Name = "Герой";
    public const float Height = 1.78f;

    // ── Рубашка ────────────────────────────────────────────────────────────
    /// <summary>Основной синий цвет рубашки (#1A6FC4 → OpenGL linear).</summary>
    public static readonly Vector3 ShirtBlue  = new(0.18f, 0.55f, 0.95f);
    public static readonly Vector3 ShirtLight = new(0.65f, 0.88f, 1.00f);  // блик
    public static readonly Vector3 ShirtDark  = new(0.08f, 0.28f, 0.55f);  // тень
    public static readonly Vector3 Collar     = new(0.22f, 0.62f, 1.00f);  // воротник
    public static readonly Vector3 Button     = new(0.90f, 0.92f, 0.95f);  // пуговицы

    // ── Кожа ───────────────────────────────────────────────────────────────
    public static readonly Vector3 Skin         = new(0.92f, 0.70f, 0.50f);
    public static readonly Vector3 SkinShadow   = new(0.78f, 0.58f, 0.40f);
    public static readonly Vector3 SkinHighlight = new(0.98f, 0.82f, 0.64f);

    // ── Лицо ───────────────────────────────────────────────────────────────
    public static readonly Vector3 Eye   = new(0.15f, 0.38f, 0.72f);  // голубые глаза
    public static readonly Vector3 Iris  = new(0.08f, 0.20f, 0.48f);  // зрачок/радужка
    public static readonly Vector3 Brow  = new(0.55f, 0.38f, 0.18f);  // брови
    public static readonly Vector3 Lip   = new(0.80f, 0.50f, 0.38f);  // губы

    // ── Волосы ─────────────────────────────────────────────────────────────
    /// <summary>Светло-русые волосы (референс: блондин).</summary>
    public static readonly Vector3 HairBlonde  = new(0.72f, 0.55f, 0.22f);
    public static readonly Vector3 HairShadow  = new(0.52f, 0.38f, 0.12f);
    // Оставляем старое поле для совместимости
    public static readonly Vector3 Hair = HairBlonde;

    // ── Штаны / обувь / пояс ───────────────────────────────────────────────
    public static readonly Vector3 Pants     = new(0.10f, 0.12f, 0.18f);  // тёмно-серые
    public static readonly Vector3 PantsDark = new(0.06f, 0.07f, 0.11f);
    public static readonly Vector3 Belt      = new(0.22f, 0.14f, 0.07f);  // коричневый кожаный пояс
    public static readonly Vector3 BeltMetal = new(0.65f, 0.60f, 0.50f);  // пряжка
    public static readonly Vector3 Shoe      = new(0.08f, 0.07f, 0.06f);  // тёмная обувь
    public static readonly Vector3 ShoeSole  = new(0.18f, 0.16f, 0.14f);  // подошва

    // ── Акцент (часы, значок) ──────────────────────────────────────────────
    public static readonly Vector3 Accent     = new(0.25f, 0.85f, 1.00f);
    public static readonly Vector3 WatchFace  = new(0.05f, 0.05f, 0.08f);
    public static readonly Vector3 WatchBezel = new(0.30f, 0.30f, 0.32f);

    public static bool IsHero(NpcCharacter npc) => npc.Id == 0 || npc.Name == Name;

    public static void ApplyTo(NpcCharacter hero)
    {
        hero.Name       = Name;
        hero.Height     = Height;
        hero.Color      = ShirtBlue;
        hero.HeadColor  = Skin;
        hero.PantsColor = Pants;
        hero.HairColor  = HairBlonde;
    }
}
