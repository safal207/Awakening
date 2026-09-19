using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class CityStreetProps
{
    internal static Box2 ParkedCarBounds(CityBlock b)
    {
        float x=b.X+b.Width+CityGenerator.SidewalkW+1.2f, z=b.Z+5;
        return new Box2(x-0.92f,z-2.2f,x+0.92f,z+2.2f);
    }

    internal static void Append(List<float> v, CityBlock b)
    {
        Vector3 metal = new(0.16f,0.18f,0.18f);
        float x = b.X + b.Width + 2.1f, z = b.Z + 1.2f;
        SceneGeometry.Cylinder(v,new(x,0.12f,z),new(x,5.3f,z),0.055f,metal);
        SceneGeometry.Cylinder(v,new(x,5.25f,z),new(x+1.8f,5.25f,z),0.045f,metal);
        SceneGeometry.Box(v,new(x+1.4f,5.13f,z-0.18f),new(0.7f,0.16f,0.36f),metal);
        SceneGeometry.Box(v,new(x+1.46f,5.10f,z-0.13f),new(0.58f,0.04f,0.26f),new(0.91f,0.88f,0.69f));
        SceneGeometry.Box(v,new(x-0.7f,3.45f,z-0.04f),new(1.5f,0.3f,0.08f),new(0.12f,0.33f,0.24f));
        SceneGeometry.Box(v,new(x-0.57f,3.57f,z+0.005f),new(1.2f,0.035f,0.07f),new(0.8f,0.85f,0.76f));
        if (Math.Abs(b.X+b.Z) % (CityGenerator.CellSize*3) == 0)
        {
            SceneGeometry.Box(v,new(x-0.16f,2.5f,z-0.3f),new(0.36f,0.9f,0.38f),new(0.68f,0.47f,0.09f));
            for (int i=0;i<3;i++)
                SceneGeometry.Foliage(v,new(x+0.03f,2.68f+i*0.25f,z-0.31f),new(0.09f,0.09f,0.025f),
                    i==2 ? new(0.68f,0.10f,0.07f) : new(0.06f,0.12f,0.09f));
        }

        if (b.X == 0 && b.Z == 0)
        {
            AppendFirstMemorySignal(v);
            FirstMemoryFindings.AppendProps(v);
        }

        if (b.Type is not (BuildingType.Tree or BuildingType.Lamp))
        {
            SceneGeometry.Cylinder(v,new(b.X-1.9f,0.12f,b.Z+7),new(b.X-1.9f,0.7f,b.Z+7),0.16f,new(0.61f,0.14f,0.10f));
            SceneGeometry.Foliage(v,new(b.X-1.9f,0.74f,b.Z+7),new(0.19f,0.11f,0.19f),new(0.65f,0.18f,0.12f));
            SceneGeometry.Cylinder(v,new(x,0.12f,b.Z+8),new(x,0.86f,b.Z+8),0.27f,metal);
            Car(v,new(b.X+b.Width+CityGenerator.SidewalkW+1.2f,0,b.Z+5),
                ((b.X/CityGenerator.CellSize+b.Z/CityGenerator.CellSize)%3) switch {
                    0 => new(0.60f,0.16f,0.12f), 1 => new(0.29f,0.38f,0.40f), _ => new(0.68f,0.69f,0.66f) });
        }
    }

    private static void AppendFirstMemorySignal(List<float> v)
    {
        Vector3 p = FirstMemorySpatial.SignalPosition;
        Vector3 pole = new(0.12f,0.15f,0.16f);
        Vector3 housing = new(0.075f,0.085f,0.09f);
        Vector3 deadRed = new(0.22f,0.055f,0.045f);
        Vector3 deadGreen = new(0.035f,0.10f,0.065f);

        SceneGeometry.Cylinder(v, p, p + new Vector3(0f, 2.35f, 0f), 0.075f, pole, 8);
        SceneGeometry.Box(v, p + new Vector3(-0.23f, 1.65f, -0.16f), new Vector3(0.46f, 0.78f, 0.32f), housing);
        SceneGeometry.Box(v, p + new Vector3(-0.14f, 2.19f, -0.175f), new Vector3(0.28f, 0.13f, 0.035f), deadRed);
        SceneGeometry.Box(v, p + new Vector3(-0.14f, 1.84f, -0.175f), new Vector3(0.28f, 0.13f, 0.035f), deadGreen);
        SceneGeometry.Box(v, p + new Vector3(-0.32f, 1.56f, -0.22f), new Vector3(0.64f, 0.08f, 0.44f), pole * 1.2f);
    }

    private static void Car(List<float> v, Vector3 p, Vector3 paint)
    {
        Vector3 rubber = new(0.035f,0.04f,0.044f), glass = new(0.20f,0.32f,0.39f);
        SceneGeometry.Box(v,p+new Vector3(-0.84f,0.30f,-2.15f),new(1.68f,0.48f,4.3f),paint);
        SceneGeometry.Box(v,p+new Vector3(-0.78f,0.71f,-1.9f),new(1.56f,0.13f,3.8f),paint*1.08f);
        Vector3 a=p+new Vector3(-0.75f,0.81f,-1.1f), b=p+new Vector3(0.75f,0.81f,-1.1f);
        Vector3 c=p+new Vector3(0.64f,1.38f,-0.66f), d=p+new Vector3(-0.64f,1.38f,-0.66f);
        Vector3 e=p+new Vector3(-0.75f,0.81f,1.25f), f=p+new Vector3(0.75f,0.81f,1.25f);
        Vector3 g=p+new Vector3(0.64f,1.38f,0.75f), h=p+new Vector3(-0.64f,1.38f,0.75f);
        SceneGeometry.Quad(v,b,a,d,c,glass);
        SceneGeometry.Quad(v,a,e,h,d,glass);
        SceneGeometry.Quad(v,f,b,c,g,glass);
        SceneGeometry.Quad(v,e,f,g,h,glass);
        SceneGeometry.Quad(v,d,h,g,c,paint);
        foreach (float side in new[] {-1f,1f})
        {
            foreach (float wheel in new[] {-1.4f,1.35f})
            {
                Vector3 start=p+new Vector3(side*0.79f,0.32f,wheel), end=p+new Vector3(side*0.91f,0.32f,wheel);
                SceneGeometry.Cylinder(v,start,end,0.33f,rubber,12);
                SceneGeometry.Cylinder(v,end,end+new Vector3(side*0.015f,0,0),0.18f,new(0.48f,0.51f,0.53f),12);
            }
            SceneGeometry.Box(v,p+new Vector3(side*0.52f-0.16f,0.56f,-2.17f),new(0.32f,0.14f,0.04f),new(0.95f,0.93f,0.74f));
            SceneGeometry.Box(v,p+new Vector3(side*0.52f-0.16f,0.55f,2.14f),new(0.32f,0.12f,0.04f),new(0.65f,0.08f,0.05f));
        }
        SceneGeometry.Box(v,p+new Vector3(-0.8f,0.37f,-2.2f),new(1.6f,0.12f,0.06f),rubber);
        SceneGeometry.Box(v,p+new Vector3(-0.8f,0.37f,2.14f),new(1.6f,0.12f,0.06f),rubber);
    }
}
