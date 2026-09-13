using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class NavigationTests
{
    public static bool Run(out string message)
    {
        var ledger = MemoryRuntime.Current;
        int hero = MemoryRuntime.HeroId;
        var timer = Stopwatch.StartNew();
        static void Require(bool valid, string detail) { if (!valid) throw new InvalidOperationException(detail); }
        static void CheckSegments(StreetNavigation navigation, Vector3 from, List<Vector3> path, Box2? obstacle = null)
        {
            foreach (Vector3 point in path)
            {
                Require(navigation.IsSegmentClear(from, point, obstacle), $"route cuts an obstacle: {from} -> {point}");
                from = point;
            }
        }
        try
        {
            MemoryRuntime.Reset();
            var path = new List<Vector3>();
            var city = new CityRenderer(424242);
            var navigation = city.Navigation;
            var from = new Vector3(-1.5f,.12f,5);
            var to = new Vector3(11.5f,.12f,5);
            Require(!navigation.IsSegmentClear(from,to), "wall regression fixture must cross a building");
            var direct = new NpcCharacter(from,to,9,id:49);
            for (int i=0;i<400;i++) { direct.Update(9,.1f); direct.Position=city.ClampToWalkable(direct.Position,.3f); }
            Require(Vector2.Distance(direct.Position.Xz,to.Xz)>5,"unrouted movement must reproduce wall sticking");
            Require(navigation.FindRoute(from,to,path)==RouteResult.Complete && path.Count>=3,"route around wall");
            CheckSegments(navigation,from,path);

            int edges=navigation.EdgeCount;
            var crossingFrom=new Vector3(39.5f,.12f,11.5f);
            var crossingTo=new Vector3(39.5f,.12f,26.5f);
            var blocked=new Box2(38,16,41,23);
            Require(navigation.FindRoute(crossingFrom,crossingTo,path,blocked)==RouteResult.Complete,"detour around temporary blocked crossing");
            CheckSegments(navigation,crossingFrom,path,blocked);
            Require(path.Count>2,"temporary obstacle must change route");
            Require(navigation.FindRoute(crossingFrom,crossingTo,path)==RouteResult.Complete && path.Count==2,
                "temporary edges restored after route query");
            Require(navigation.EdgeCount==edges,"graph does not accumulate edges");
            Require(navigation.FindRoute(from,new Vector3(5,0,5),path)==RouteResult.Unreachable && path.Count==0,"blocked destination is not a successful partial route");
            Require(navigation.FindRoute(from,new Vector3(float.NaN,0,5),path)==RouteResult.Unreachable,"nonfinite destination rejected");
            var disconnected=new StreetNavigation(new[]{new Box2(-.5f,-1000,.5f,1000)});
            Require(disconnected.FindRoute(new Vector3(-16.5f,0,-16.5f),new Vector3(26.5f,0,-16.5f),path)==RouteResult.Unreachable && path.Count==0,
                "disconnected components must not accept the library's closest-approach path");
            navigation.BeginStep(1);
            int plans=navigation.PlansComputed;
            Require(navigation.FindRoute(from,to,path)==RouteResult.Complete &&
                navigation.FindRoute(from,to,path)==RouteResult.Pending && navigation.PlansComputed==plans+1,"per-step search budget");

            int routes=0, arrivals=0;
            foreach (int seed in new[]{424242,515151,0,42})
            {
                city=new CityRenderer(seed);
                navigation=city.Navigation;
                Require(city.Npcs.Select(n=>n.HomePos).Distinct().Count()==50 &&
                    city.Npcs.Select(n=>n.WorkPos).Distinct().Count()==50,"destinations are not shared standing spots");
                foreach(var npc in city.Npcs)
                {
                    Require(navigation.FindRoute(npc.HomePos,npc.WorkPos,path)==RouteResult.Complete,"commute not connected: "+npc.Id);
                    CheckSegments(navigation,npc.HomePos,path);
                    Require(navigation.FindRoute(npc.WorkPos,npc.HomePos,path)==RouteResult.Complete,"return not connected: "+npc.Id);
                    CheckSegments(navigation,npc.WorkPos,path);
                    routes+=2;
                }
                foreach (float hour in new[]{9f,23f})
                {
                    city.TimeOfDay=hour;
                    var arrived=new HashSet<int>();
                    for(int step=0;step<3000 && arrived.Count<47;step++)
                    {
                        int before=navigation.PlansComputed;
                        city.UpdateCitizens(.1f);
                        Require(navigation.PlansComputed-before<=2,"search burst exceeded budget");
                        for(int i=3;i<50;i++)
                        {
                            var npc=city.Npcs[i];
                            Require(navigation.IsSegmentClear(npc.Position,npc.Position),"resident entered static obstacle: "+i);
                            Vector3 target=hour==9 ? npc.WorkPos : npc.HomePos;
                            if(Vector2.DistanceSquared(npc.Position.Xz,target.Xz)<.09f &&
                                npc.State==(hour==9?NpcState.Working:NpcState.Sleeping)) arrived.Add(i);
                        }
                    }
                    Require(arrived.Count==47,$"commute stalled seed={seed} hour={hour}: "+
                        string.Join(",",city.Npcs.Skip(3).Where(n=>!arrived.Contains(n.Id)).Select(n=>$"{n.Id}@{n.Position}->{(hour==9?n.WorkPos:n.HomePos)} {n.State}")));
                    arrivals+=arrived.Count;
                }
            }

            city=new CityRenderer(424242);
            foreach(var npc in city.Npcs) npc.State=NpcState.Aware;
            Vector3 heroPosition=city.Player!.Position;
            var left=city.Npcs[3];
            var right=city.Npcs[4];
            left.Reset(); right.Reset();
            left.State=right.State=NpcState.Walking;
            left.Position=right.WorkPos=new Vector3(-1.5f,.12f,-16.5f);
            right.Position=left.WorkPos=new Vector3(26.5f,.12f,-16.5f);
            foreach(var npc in city.Npcs.Skip(5))
                if(Vector3.Distance(npc.Position,left.WorkPos)<2 || Vector3.Distance(npc.Position,right.WorkPos)<2)
                    npc.Position+=new Vector3(0,0,112);
            city.TimeOfDay=9;
            for(int i=0;i<600;i++)
            {
                city.UpdateCitizens(.1f);
                Require(Vector2.DistanceSquared(left.Position.Xz,right.Position.Xz)>=.65f*.65f,"opposing pedestrians overlap");
                Require(city.Player.Position==heroPosition,"crowd moved the player without input");
            }
            Require(left.State==NpcState.Working && right.State==NpcState.Working,$"opposing pedestrians did not pass each other: {left.Position} {left.State}, {right.Position} {right.State}; targets occupied by "+
                string.Join(",",city.Npcs.Where(n=>n!=left && n!=right && (Vector3.Distance(n.Position,left.WorkPos)<2 || Vector3.Distance(n.Position,right.WorkPos)<2)).Select(n=>n.Id)));
            left.Reset(); left.WorkPos=new Vector3(5,0,5);
            int failedPlans=city.Navigation.PlansComputed;
            for(int i=0;i<60;i++)city.UpdateCitizens(.1f);
            Require(left.State!=NpcState.Working && left.AnimBlend==0 &&
                city.Navigation.PlansComputed-failedPlans<=4,"unreachable route spins searches or reports arrival");

            city=new CityRenderer(424242);
            foreach(var npc in city.Npcs) npc.State=NpcState.Aware;
            var walker=city.Npcs[3];
            walker.Reset();
            walker.State=NpcState.Walking;
            walker.Position=new Vector3(179.5f,.12f,179.5f);
            walker.WorkPos=new Vector3(179.5f,.12f,291.5f);
            city.TimeOfDay=9;
            for(int i=0;i<20;i++)city.UpdateCitizens(.1f);
            int searches=city.Navigation.PlansComputed;
            Vector3 movementStart=walker.Position;
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<50;i++)city.UpdateCitizens(.1f);
            long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Require(Vector3.Distance(movementStart,walker.Position)>10 && walker.AnimBlend>.5f,"allocation probe must actually walk");
            Require(city.Navigation.PlansComputed==searches && bytes==0,"steady route must not search or allocate: "+bytes);
            walker.Reset();
            Require(walker.Velocity==Vector3.Zero && walker.AnimBlend==0,"reset clears motion");
            walker.Position=new Vector3(179.5f,.12f,179.5f);
            walker.State=NpcState.Sleeping;
            Vector3 blockedPlayer=city.AdjustForNpcCollision(walker.Position+new Vector3(.1f,0,0),.3f,city.Player);
            Require(Vector3.Distance(blockedPlayer,walker.Position)>=.64f,"visible sleeping resident lost its collider");
            blockedPlayer=city.AdjustForNpcCollision(walker.Position,.3f,city.Player);
            Require(Vector3.Distance(blockedPlayer,walker.Position)>=.64f,"exact overlap has no escape direction");
            message=$"Street navigation tests passed ({routes} routes, {arrivals} commute arrivals, 0 B over 50 steady steps, {timer.Elapsed.TotalSeconds:F2}s).";
            return true;
        }
        catch(Exception e) { message="Street navigation tests failed: "+e; return false; }
        finally { MemoryRuntime.Replace(ledger); MemoryRuntime.HeroId=hero; }
    }
}
