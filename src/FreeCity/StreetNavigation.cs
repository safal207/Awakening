using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;

namespace Probuzhdenie.FreeCity;

internal enum RouteResult { Pending, Complete, Unreachable }

internal sealed class StreetNavigation
{
    internal const float Radius = 0.3f;
    private const float RouteClearance = 0.85f;
    private const int AxisCount = (CityGenerator.CityRadius * 2 + 1) * 2;
    private static readonly Roy_T.AStar.Primitives.Velocity GraphSpeed = Roy_T.AStar.Primitives.Velocity.FromMetersPerSecond(1);
    private readonly Node[] _nodes = new Node[AxisCount * AxisCount];
    private readonly List<IEdge> _edges = new(), _disabled = new();
    private readonly Box2[] _obstacles;
    private readonly PathFinder _finder = new();
    private int _budget = int.MaxValue;
    internal int NodeCount => _nodes.Length;
    internal int EdgeCount
    {
        get
        {
            int count = 0;
            foreach (var node in _nodes) count += node.Outgoing.Count;
            return count;
        }
    }
    internal int PlansComputed { get; private set; }

    internal StreetNavigation(IReadOnlyList<Box2> obstacles)
    {
        _obstacles = new Box2[obstacles.Count];
        for (int i = 0; i < obstacles.Count; i++) _obstacles[i] = obstacles[i];
        for (int x = 0; x < AxisCount; x++)
            for (int z = 0; z < AxisCount; z++)
                _nodes[x * AxisCount + z] = new Node(new Position(Axis(x), Axis(z)));
        for (int x = 0; x < AxisCount; x++)
            for (int z = 0; z < AxisCount; z++)
            {
                int index = x * AxisCount + z;
                if (x + 1 < AxisCount) Connect(_nodes[index], _nodes[index + AxisCount]);
                if (z + 1 < AxisCount) Connect(_nodes[index], _nodes[index + 1]);
            }
    }

    // Corners of the two sidewalks around every block, with street crossings
    // between neighboring blocks. All edges use the game's collision bounds.
    private static float Axis(int index) => (index / 2 - CityGenerator.CityRadius) * CityGenerator.CellSize +
        (index % 2 == 0 ? -1.5f : CityGenerator.BlockSize + 1.5f);
    private static Vector3 Point(INode node) => Ground(new Vector3(node.Position.X, 0, node.Position.Y));
    private static Vector3 Ground(Vector3 p) => new(p.X, CityGenerator.GroundHeight(p.X, p.Z), p.Z);

    private void Connect(Node a, Node b)
    {
        if (!IsSegmentClear(Point(a), Point(b), clearance: RouteClearance)) return;
        a.Connect(b, GraphSpeed);
        b.Connect(a, GraphSpeed);
        _edges.Add(a.Outgoing[a.Outgoing.Count - 1]);
        _edges.Add(b.Outgoing[b.Outgoing.Count - 1]);
    }

    internal void BeginStep(int searchBudget = 2) => _budget = searchBudget;

    internal Vector3 ClosestPoint(Vector3 position, HashSet<Vector3>? occupied = null)
    {
        Node? best = Nearest(position, null, visible: false, occupied);
        return best == null ? position : Point(best);
    }

    private Node? Nearest(Vector3 position, Box2? dynamicObstacle, bool visible, HashSet<Vector3>? occupied = null)
    {
        Node? best = null;
        float distance = float.MaxValue;
        foreach (var node in _nodes)
        {
            if (node.Outgoing.Count == 0) continue;
            Vector3 point = Point(node);
            if (occupied?.Contains(point) == true) continue;
            float candidate = Vector2.DistanceSquared(point.Xz, position.Xz);
            if (candidate >= distance || visible && !IsSegmentClear(position, point, dynamicObstacle)) continue;
            best = node;
            distance = candidate;
        }
        return best;
    }

    internal RouteResult FindRoute(Vector3 from, Vector3 to, List<Vector3> result, Box2? dynamicObstacle = null)
    {
        if (_budget <= 0) return RouteResult.Pending;
        _budget--;
        PlansComputed++;
        result.Clear();
        if (!IsSegmentClear(from, from, dynamicObstacle) || !IsSegmentClear(to, to, dynamicObstacle))
            return RouteResult.Unreachable;
        try
        {
            if (dynamicObstacle is Box2 bounds)
                foreach (var edge in _edges)
                    if (Intersects(Point(edge.Start), Point(edge.End), bounds, RouteClearance))
                    {
                        edge.Start.Outgoing.Remove(edge);
                        edge.End.Incoming.Remove(edge);
                        _disabled.Add(edge);
                    }
            var start = Nearest(from, dynamicObstacle, visible: true);
            var end = Nearest(to, dynamicObstacle, visible: true);
            if (start == null || end == null) return RouteResult.Unreachable;
            var path = _finder.FindPath(start, end, GraphSpeed);
            if (path.Type != PathType.Complete) return RouteResult.Unreachable;
            result.Add(Point(start));
            foreach (var edge in path.Edges) result.Add(Point(edge.End));
            if (Vector2.DistanceSquared(result[^1].Xz, to.Xz) > 0.0001f) result.Add(Ground(to));
            return RouteResult.Complete;
        }
        finally
        {
            foreach (var edge in _disabled)
            {
                edge.Start.Outgoing.Add(edge);
                edge.End.Incoming.Add(edge);
            }
            _disabled.Clear();
        }
    }

    internal bool IsSegmentClear(Vector3 from, Vector3 to, Box2? dynamicObstacle = null, float clearance = Radius)
    {
        if (!float.IsFinite(from.X) || !float.IsFinite(from.Z) || !float.IsFinite(to.X) || !float.IsFinite(to.Z)) return false;
        foreach (var bounds in _obstacles)
            if (Intersects(from, to, bounds, clearance)) return false;
        return dynamicObstacle is not Box2 moving || !Intersects(from, to, moving, clearance);
    }

    private static bool Intersects(Vector3 from, Vector3 to, Box2 bounds, float clearance)
    {
        float padding = clearance - 0.0001f;
        return CityRenderer.SegmentIntersectsBox(new(from.X, 0, from.Z), new(to.X, 0, to.Z),
            new(bounds.Min.X - padding, -1, bounds.Min.Y - padding),
            new(bounds.Max.X + padding, 1, bounds.Max.Y + padding), out _);
    }
}
