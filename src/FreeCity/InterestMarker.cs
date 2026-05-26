using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public struct InterestMarker
{
    public string Id;
    public string Name;
    public string Description;
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    public float MemoryGain;
    public float CuriosityGain;
    public float EmpathyGain;
    public float AgencyGain;
    public float CourageGain;

    public InterestMarker(
        string id,
        string name,
        string description,
        Vector3 position,
        float radius = 5f,
        Vector3? color = null,
        float memoryGain = 0f,
        float curiosityGain = 0f,
        float empathyGain = 0f,
        float agencyGain = 0f,
        float courageGain = 0f)
    {
        Id = id;
        Name = name;
        Description = description;
        Position = position;
        Radius = radius;
        Color = color ?? new Vector3(0.9f, 0.5f, 0.2f);
        MemoryGain = memoryGain;
        CuriosityGain = curiosityGain;
        EmpathyGain = empathyGain;
        AgencyGain = agencyGain;
        CourageGain = courageGain;
    }
}
