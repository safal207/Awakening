using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class CityFacadeDetails
{
    internal static void Append(List<float> v, CityBlock b)
    {
        float x=b.X+0.5f,z=b.Z+0.5f,w=b.Width-1,d=b.Depth-1,h=b.Height*2.5f;
        Vector3 stone = new(0.62f,0.64f,0.60f), iron = new(0.12f,0.15f,0.16f);
        SceneGeometry.Box(v,new(x-0.16f,h-0.38f,z-0.16f),new(w+0.32f,0.13f,d+0.32f),stone);
        SceneGeometry.Box(v,new(x+0.5f,h+0.05f,z+0.5f),new(w-1,0.3f,d-1),new(0.29f,0.32f,0.32f));
        SceneGeometry.Box(v,new(x+1,h+0.35f,z+1),new(1.3f,0.72f,1.6f),new(0.47f,0.50f,0.48f));
        DepotDistrict.AppendFacade(v, b);
        if (b.Type is BuildingType.House or BuildingType.Apartment or BuildingType.Cafe)
        {
            for(int floor=1;floor<Math.Min(b.Height,6);floor++)
            {
                float y=floor*2.5f+0.65f;
                SceneGeometry.Box(v,new(x+0.9f,y,z+d+0.08f),new(2.5f,0.09f,0.7f),iron);
                SceneGeometry.Cylinder(v,new(x+0.9f,y+0.85f,z+d+0.76f),new(x+3.4f,y+0.85f,z+d+0.76f),0.025f,iron);
                for(int bar=0;bar<=10;bar++)
                    SceneGeometry.Cylinder(v,new(x+0.9f+bar*0.25f,y,z+d+0.76f),new(x+0.9f+bar*0.25f,y+0.85f,z+d+0.76f),0.016f,iron,6);
                for(int rung=0;rung<10;rung++)
                    SceneGeometry.Cylinder(v,new(x+2.8f,y-rung*0.25f,z+d+0.48f),new(x+3.28f,y-rung*0.25f,z+d+0.48f),0.019f,iron,6);
                foreach(float rail in new[] {2.8f,3.28f})
                    SceneGeometry.Cylinder(v,new(x+rail,y-2.5f,z+d+0.48f),new(x+rail,y+0.7f,z+d+0.48f),0.025f,iron);
            }
        }
        if (b.Height >= 5 && Math.Abs(b.X+b.Z)%3==0)
        {
            Vector3 tank = new(x+w*0.65f,h+2,z+d*0.5f);
            SceneGeometry.Cylinder(v,tank,tank+Vector3.UnitY*2.3f,1.1f,new(0.35f,0.27f,0.19f),12);
            SceneGeometry.Foliage(v,tank+Vector3.UnitY*2.25f,new(1.15f,0.5f,1.15f),iron);
            for(int i=0;i<4;i++)
            {
                float a=i*MathHelper.PiOver2;
                Vector3 foot=tank+new Vector3(MathF.Cos(a)*0.8f,-1.7f,MathF.Sin(a)*0.8f);
                SceneGeometry.Cylinder(v,foot,foot+Vector3.UnitY*1.8f,0.055f,iron);
            }
        }
    }
}
