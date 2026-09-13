using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal sealed class NpcRoute
{
    private readonly List<Vector3> _points = new(64);
    private int _index;
    private Vector3 _target, _previous;
    private bool _hasTarget;
    private float _retry, _stalled;

    internal void Reset()
    {
        _points.Clear();
        _index = 0;
        _hasTarget = false;
        _retry = _stalled = 0;
    }

    internal bool Waypoint(StreetNavigation navigation, Vector3 position, Vector3 target, float dt,
        Box2? obstacle, out Vector3 waypoint)
    {
        waypoint = position;
        if (!_hasTarget || Vector2.DistanceSquared(_target.Xz, target.Xz) > 0.001f)
        {
            Reset();
            _target = target;
            _previous = position;
            _hasTarget = true;
        }
        _retry -= dt;
        if (_index < _points.Count)
        {
            _stalled = Vector2.DistanceSquared(position.Xz, _previous.Xz) < 0.00001f ? _stalled + dt : 0;
            if (_stalled > 1.5f || !navigation.IsSegmentClear(position, _points[_index], obstacle))
            {
                _points.Clear();
                _index = 0;
                _stalled = 0;
                _retry = System.Math.Max(_retry, 0.25f);
            }
        }
        _previous = position;
        if (_points.Count == 0 && _retry <= 0)
        {
            RouteResult result = navigation.FindRoute(position, target, _points, obstacle);
            if (result == RouteResult.Unreachable) _retry = 2f;
        }
        while (_index < _points.Count && Vector2.DistanceSquared(position.Xz, _points[_index].Xz) < 0.01f) _index++;
        // An occupied corner need not be touched exactly. Advance only when
        // the next segment is still clear for the full pedestrian radius.
        while (_index + 1 < _points.Count && Vector2.DistanceSquared(position.Xz, _points[_index].Xz) < 1f &&
            navigation.IsSegmentClear(position, _points[_index + 1], obstacle)) _index++;
        if (_index >= _points.Count)
        {
            if (Vector2.DistanceSquared(position.Xz, target.Xz) > 0.0225f) { _points.Clear(); _index = 0; }
            return false;
        }
        waypoint = _points[_index];
        return true;
    }
}
