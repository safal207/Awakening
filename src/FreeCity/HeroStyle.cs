using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class HeroStyle
{
    public const string Name = "Герой";
    public const float Height = 1.78f;

    public static readonly Vector3 ShirtBlue = new(0.18f, 0.55f, 0.95f);
    public static readonly Vector3 Skin = new(0.92f, 0.70f, 0.50f);
    public static readonly Vector3 Pants = new(0.04f, 0.06f, 0.11f);
    public static readonly Vector3 Hair = new(0.07f, 0.045f, 0.025f);
    public static readonly Vector3 ShirtLight = new(0.65f, 0.88f, 1.0f);
    public static readonly Vector3 ShirtDark = new(0.08f, 0.28f, 0.55f);
    public static readonly Vector3 Belt = new(0.08f, 0.055f, 0.035f);
    public static readonly Vector3 Accent = new(0.25f, 0.85f, 1.0f);

    public static bool IsHero(NpcCharacter npc) => npc.Id == 0 || npc.Name == Name;

    public static void ApplyTo(NpcCharacter hero)
    {
        hero.Name = Name;
        hero.Height = Height;
        hero.Color = ShirtBlue;
        hero.HeadColor = Skin;
        hero.PantsColor = Pants;
        hero.HairColor = Hair;
    }
}
