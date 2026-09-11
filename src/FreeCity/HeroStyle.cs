using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class HeroStyle
{
    public const string Name = "Герой";
    public const float Height = 1.78f;

    public static readonly Vector3 ShirtBlue = new(0.27f, 0.51f, 0.64f);
    public static readonly Vector3 Skin = new(0.83f, 0.62f, 0.46f);
    public static readonly Vector3 Pants = new(0.22f, 0.26f, 0.27f);
    public static readonly Vector3 Hair = new(0.20f, 0.13f, 0.095f);
    public static readonly Vector3 HairLight = new(0.78f, 0.62f, 0.38f);
    public static readonly Vector3 ShirtLight = new(0.65f, 0.88f, 1.0f);
    public static readonly Vector3 ShirtDark = new(0.08f, 0.28f, 0.55f);
    public static readonly Vector3 Belt = new(0.08f, 0.055f, 0.035f);
    public static readonly Vector3 Accent = new(0.25f, 0.85f, 1.0f);
    public static readonly Vector3 Strap = new(0.03f, 0.035f, 0.04f);
    public static readonly Vector3 Shoe = new(0.07f, 0.07f, 0.075f);
    public static readonly Vector3 Eye = new(0.03f, 0.05f, 0.09f);
    public static readonly Vector3 WorkLeather = new(0.23f, 0.16f, 0.12f);
    public static readonly Vector3 Stitch = new(0.65f, 0.52f, 0.36f);
    public static readonly Vector3 Iris = new(0.22f, 0.34f, 0.32f);
    public static readonly Vector3 Metal = new(0.53f, 0.56f, 0.54f);

    public static bool IsHero(NpcCharacter npc) => npc.Id == 0 || npc.Name == Name;

    public static void ApplyTo(NpcCharacter hero)
    {
        MemoryRuntime.HeroId = hero.Id;
        hero.Name = Name;
        hero.Height = Height;
        hero.Color = ShirtBlue;
        hero.HeadColor = Skin;
        hero.PantsColor = Pants;
        hero.HairColor = Hair;
    }
}
