using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class DistrictScene
{
    internal static readonly Box2[] PropBounds = {
        new(11.2f, 12.2f, 11.8f, 12.8f), new(-3.5f,12.3f,-2.5f,12.7f),
        new(-11.2f,12f,-8.8f,12.7f), new(5.3f,12.5f,5.7f,12.9f),
    };
    internal static readonly Box2 TramBounds = new(-1f, 15.8f, 9f, 18.2f);
    internal static bool TramPresent(FirstDistrictEpisode episode, int day) => day == 1 &&
        episode.Phase is not (DistrictPhase.Repaired or DistrictPhase.Missed);

    internal static List<float> Build(FirstDistrictEpisode episode, int day)
    {
        var v = new List<float>(12000);
        Vector3 metal = new(0.19f,0.23f,0.22f), green = new(0.18f,0.75f,0.38f), amber = new(0.96f,0.63f,0.12f);
        SceneGeometry.Box(v,new(11.2f,0.12f,12.2f),new(0.6f,1.3f,0.6f),metal);
        SceneGeometry.Box(v,new(11.3f,0.95f,12.17f),new(0.4f,0.23f,0.04f),new(0.7f,0.75f,0.72f));
        SceneGeometry.Box(v,new(11.42f,1.42f,12.4f),new(0.16f,1.9f,0.16f),metal);
        SceneGeometry.Box(v,new(11.15f,2.75f,12.17f),new(0.7f,0.65f,0.4f),metal);
        SceneGeometry.Box(v,new(11.3f,2.9f,12.14f),new(0.4f,0.3f,0.04f),
            day > 1 || episode.Phase == DistrictPhase.Repaired ? green : amber);
        SceneGeometry.Box(v,new(5.3f,0.12f,12.5f),new(0.4f,3.1f,0.4f),metal);
        SceneGeometry.Box(v,new(4.7f,2.7f,12.45f),new(1.6f,0.7f,0.12f),new(0.16f,0.48f,0.39f));
        SceneGeometry.Box(v,new(5.2f,2.78f,12.42f),new(0.5f,0.5f,0.04f),new(0.9f,0.92f,0.87f));
        SceneGeometry.Box(v,new(-3.5f,0.12f,12.3f),new(1f,1.1f,0.4f),metal);
        SceneGeometry.Box(v,new(-3.45f,1.22f,12.25f),new(0.9f,0.04f,0.5f),new(0.84f,0.85f,0.81f));
        if (episode.Finished)
        {
            Vector3 ink = day > 1 && MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId) ? green : amber;
            SceneGeometry.Box(v,new(-3.3f,1.265f,12.32f),new(0.6f,0.01f,0.08f),ink);
        }
        SceneGeometry.Box(v,new(-11.2f,0.55f,12f),new(2.4f,0.16f,0.7f),new(0.37f,0.47f,0.39f));
        SceneGeometry.Box(v,new(-11.2f,0.65f,12.6f),new(2.4f,0.55f,0.1f),new(0.37f,0.47f,0.39f));
        SceneGeometry.Box(v,new(-11f,0.12f,12.1f),new(0.15f,0.5f,0.5f),metal);
        SceneGeometry.Box(v,new(-9.15f,0.12f,12.1f),new(0.15f,0.5f,0.5f),metal);
        if (TramPresent(episode, day))
        {
            SceneGeometry.Box(v,new(-1f,0.45f,15.8f),new(10f,2.3f,2.4f),new(0.16f,0.48f,0.4f));
            SceneGeometry.Box(v,new(-1f,2.75f,15.8f),new(10f,0.15f,2.4f),new(0.76f,0.78f,0.73f));
            for (int i = 0; i < 6; i++)
            {
                SceneGeometry.Box(v,new(-0.7f+i*1.55f,1.4f,15.77f),new(1.2f,1.1f,0.04f),new(0.34f,0.52f,0.56f));
                SceneGeometry.Box(v,new(-0.7f+i*1.55f,1.4f,18.2f),new(1.2f,1.1f,0.04f),new(0.34f,0.52f,0.56f));
            }
            SceneGeometry.Box(v,new(2.9f,0.5f,15.74f),new(1.2f,2.1f,0.04f),metal);
            SceneGeometry.Box(v,new(-1.03f,1.4f,16f),new(0.04f,1.1f,2f),new(0.34f,0.52f,0.56f));
            SceneGeometry.Box(v,new(9f,1.4f,16f),new(0.04f,1.1f,2f),new(0.34f,0.52f,0.56f));
            SceneGeometry.Box(v,new(-1.04f,0.85f,16.1f),new(0.04f,0.25f,0.35f),new(0.94f,0.89f,0.64f));
            SceneGeometry.Box(v,new(-1.04f,0.85f,17.55f),new(0.04f,0.25f,0.35f),new(0.94f,0.89f,0.64f));
            SceneGeometry.Cylinder(v,new(3,2.9f,17),new(4,3.7f,17),0.06f,metal);
            SceneGeometry.Cylinder(v,new(4,3.7f,17),new(5,2.9f,17),0.06f,metal);
            for (int i = 0; i < 4; i++)
                SceneGeometry.Box(v,new(i < 2 ? 0f : 7f,0.12f,i%2 == 0 ? 15.9f : 17.8f),new(0.8f,0.65f,0.3f),metal);
        }
        return v;
    }
}
