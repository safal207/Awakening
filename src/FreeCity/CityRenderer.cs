using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public readonly record struct CityRenderContext(
    int Shader,
    int ModelLocation,
    int ViewLocation,
    int ProjectionLocation,
    int ColorLocation,
    int FogColorLocation,
    int FogDensityLocation = -1,
    int AmbientLocation = -1,
    int MaterialLocation = -1,
    int WorldPassLocation = -1,
    int LightMatrixLocation = -1,
    int ShadowStrengthLocation = -1,
    int EyeLocation = -1,
    int DaylightLocation = -1);

public class CityRenderer : IDisposable
{
    public const float DefaultMorningHour = 8f;
    public static readonly Vector3 MorningSpawn = new(11.5f, 0.12f, 5f);
    private const int FloatsPerVertex = 9;
    private const int VertexStrideBytes = FloatsPerVertex * sizeof(float);

    private int _roadVao, _roadVbo, _roadCount;
    private int _sidewalkVao, _sidewalkVbo, _sidewalkCount;
    private int _buildingVao, _buildingVbo, _buildingCount;
    private int _windowVao, _windowVbo, _windowCount;
    private int _npcVao, _npcVbo, _npcCount;
    private int _highlightVao, _highlightVbo;
    private int _roadGpuBytes, _sidewalkGpuBytes, _buildingGpuBytes, _windowGpuBytes, _highlightGpuBytes;
    private int _sceneryVao, _sceneryVbo, _sceneryCount, _sceneryGpuBytes;
    private int _facadeVao, _facadeVbo, _facadeCount, _facadeGpuBytes;
    private int _glassVao, _glassVbo, _glassCount, _glassGpuBytes;
    private readonly List<SceneRange> _facadeRanges = new(), _buildingRanges = new(), _windowRanges = new(),
        _glassRanges = new(), _sceneryRanges = new();
    private readonly SceneMaterials _materials = new();
    private readonly SunShadowMap _shadows = new();
    private readonly CharacterMesh _characterMesh = new();
    public Vector3? CharacterViewPosition { get; set; }
    public bool ShadowsEnabled { get; set; } = true;
    private int _npcGpuCapacityBytes;

    private readonly List<NpcCharacter> _npcs = new();
    private readonly List<CityBlock> _blocks;
    private readonly List<InterestMarker> _markers = new();
    private readonly List<Box2> _buildingBounds = new();
    private readonly HashSet<string> _visitedMarkers = new();
    private readonly AwarenessSystem _awareness = new();
    private readonly int _seed;
    private HeroProgress _progress;
    private NpcCharacter? _player;
    private float _animationTime;
    private float _timeOfDay = 8f;
    private float _feedbackTimer;
    private string _feedbackMessage = "";
    private Vector3 _feedbackColor = new(0.9f, 0.8f, 0.25f);

    // Interiors
    private int _interiorVao, _interiorVbo, _interiorCount, _interiorGpuBytes;
    private CityBlock? _insideBlock;
    private bool _inside;

    // Easter egg tracking
    private float _standStillTimer;
    private int _talkCountForEmpathy;
    private const float CenterRadius = 5f;
    private const int TalkThresholdForEmpathy = 3;
    private const float MarkerVisitRadius = 7f;
    private bool _nightWalkAwarded;

    public void RegisterTalk()
    {
        _talkCountForEmpathy++;
        if (_talkCountForEmpathy >= TalkThresholdForEmpathy)
        {
            AwardEgg("talk_thrice", "Эмпатия: разговоры меняют людей", new Vector3(0.9f, 0.55f, 0.8f), empathyGain: 5f);
            _talkCountForEmpathy = 0;
        }
    }

    public void SaveGame() => SaveSystem.Save(_seed, _progress, _awareness, _timeOfDay, _npcs);

    public void RestoreNpcs(List<SaveSystem.NpcSaveData>? npcData)
    {
        if (npcData == null) return;
        int count = Math.Min(npcData.Count, _npcs.Count);
        for (int i = 0; i < count; i++)
        {
            var d = npcData[i];
            _npcs[i].Friendliness = d.Friendliness;
            _npcs[i].Trust = d.Trust;
            _npcs[i].TimesTalked = d.TimesTalked;
            _npcs[i].LastTalkDay = d.LastTalkDay;
            _npcs[i].Awareness = d.Awareness;
            if (Enum.TryParse<NpcState>(d.State, out var state))
                _npcs[i].State = state;
        }
    }

    public bool IsInside => _inside;
    public CityBlock? InsideBlock => _insideBlock;

    public Vector3? TryEnterInterior(Vector3 playerPos)
    {
        foreach (var block in _blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp) continue;
            float doorZ = block.Z + block.Depth;
            float doorX = block.X + block.Width * 0.5f;
            float dist = Vector3.Distance(new Vector3(doorX, 0, doorZ), new Vector3(playerPos.X, 0, playerPos.Z));
            if (dist > 3f) continue;

            _insideBlock = block;
            _inside = true;
            BuildInteriorGeometry(block);
            return new Vector3(doorX, 0, block.Z + block.Depth - 1.2f);
        }
        return null;
    }

    public Vector3 ExitInterior()
    {
        float doorX = _insideBlock!.Value.X + _insideBlock.Value.Width * 0.5f;
        float doorZ = _insideBlock.Value.Z + _insideBlock.Value.Depth;
        _insideBlock = null;
        _inside = false;
        DeleteMesh(ref _interiorVao, ref _interiorVbo, ref _interiorCount, ref _interiorGpuBytes);
        return new Vector3(doorX, CityGenerator.GroundHeight(doorX, doorZ + 2f), doorZ + 2f);
    }

    public bool IsNearDoor(Vector3 pos, float maxDist)
    {
        foreach (var block in _blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp) continue;
            float doorX = block.X + block.Width * 0.5f;
            float doorZ = block.Z + block.Depth;
            float dist = Vector3.Distance(new Vector3(doorX, 0, doorZ), new Vector3(pos.X, 0, pos.Z));
            if (dist <= maxDist) return true;
        }
        return false;
    }

    public string InteriorName()
    {
        if (_insideBlock == null) return "";
        return _insideBlock.Value.Type switch
        {
            BuildingType.Cafe => "КАФЕ",
            BuildingType.Office => "ОФИС",
            BuildingType.Store => "МАГАЗИН",
            BuildingType.Apartment => "КВАРТИРА",
            BuildingType.Bank => "БАНК",
            BuildingType.House => "ДОМ",
            BuildingType.Police => "ПОЛИЦИЯ",
            BuildingType.GasStation => "ЗАПРАВКА",
            _ => "ЗДАНИЕ",
        };
    }

    private void BuildInteriorGeometry(CityBlock block)
    {
        var v = new List<float>();
        float x = block.X, z = block.Z;
        float w = block.Width, d = block.Depth;
        float h = 3f;
        float inset = 0.3f;
        float ix = x + inset, iz = z + inset;
        float iw = w - inset * 2, id = d - inset * 2;

        Vector3 wallCol = new(0.7f, 0.65f, 0.6f);
        Vector3 floorCol = new(0.3f, 0.25f, 0.2f);
        Vector3 ceilCol = new(0.85f, 0.82f, 0.8f);

        // Floor
        SceneGeometry.Ground(v,ix,iz,iw,id,0,floorCol);
        // Ceiling
        Quad(ref v, ix, h, iz, ix + iw, h, iz, ix + iw, h, iz + id, ix, h, iz + id, ceilCol.X, ceilCol.Y, ceilCol.Z);

        // Room surfaces face inward; the door marker sits on the visible side.
        FaceRect(v,new(ix+iw,0,iz+id),-Vector3.UnitX,iw,h,wallCol);
        FaceRect(v,new(ix,0,iz),Vector3.UnitX,iw,h,wallCol*0.94f);
        FaceRect(v,new(ix+iw,0,iz),Vector3.UnitZ,id,h,wallCol*0.97f);
        FaceRect(v,new(ix,0,iz+id),-Vector3.UnitZ,id,h,wallCol);
        FaceRect(v,new(ix,0,iz+0.015f),Vector3.UnitX,iw,0.15f,wallCol*0.5f);
        float doorCx = x+w*0.5f;
        FaceRect(v,new(doorCx+0.6f,0,iz+id-0.02f),-Vector3.UnitX,1.2f,2.2f,new(0.31f,0.44f,0.48f));

        // Furniture by type
        Vector3 wood = new(0.55f, 0.35f, 0.18f);
        Vector3 metal = new(0.5f, 0.5f, 0.55f);
        Vector3 fabric = new(0.3f, 0.5f, 0.7f);
        Vector3 bright = new(0.9f, 0.6f, 0.1f);

        switch (block.Type)
        {
            case BuildingType.Cafe:
                // Counter along back wall
                Box(ref v, x + 1, 0, z + 1, 3, 1.2f, 1, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 1, 1.2f, z + 1, 3, 0.1f, 1.2f, metal.X, metal.Y, metal.Z);
                // Tables
                Box(ref v, x + 6, 0.8f, z + 2.5f, 1.5f, 0.1f, 1.0f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 6, 0.8f, z + 5f, 1.5f, 0.1f, 1.0f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 3.5f, 0.8f, z + 5f, 1.5f, 0.1f, 1.0f, wood.X, wood.Y, wood.Z);
                break;
            case BuildingType.Office:
                // Desks
                Box(ref v, x + 2, 0.8f, z + 2, 2, 0.1f, 1.2f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 6, 0.8f, z + 2, 2, 0.1f, 1.2f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 2, 0.8f, z + 6, 2, 0.1f, 1.2f, wood.X, wood.Y, wood.Z);
                // Chairs
                Box(ref v, x + 2.5f, 0.5f, z + 3.8f, 0.8f, 0.5f, 0.8f, fabric.X, fabric.Y, fabric.Z);
                Box(ref v, x + 6.5f, 0.5f, z + 3.8f, 0.8f, 0.5f, 0.8f, fabric.X, fabric.Y, fabric.Z);
                break;
            case BuildingType.Store:
                // Shelves along walls
                Box(ref v, x + 1, 0.5f, z + 2, 2.5f, 2f, 0.8f, metal.X, metal.Y, metal.Z);
                Box(ref v, x + 1, 0.5f, z + 4.5f, 2.5f, 2f, 0.8f, metal.X, metal.Y, metal.Z);
                Box(ref v, x + 6.5f, 0.5f, z + 2, 2.5f, 2f, 0.8f, metal.X, metal.Y, metal.Z);
                // Counter
                Box(ref v, x + 4, 0, z + 1, 2, 1.2f, 1.2f, wood.X, wood.Y, wood.Z);
                break;
            case BuildingType.Apartment:
                // Bed
                Box(ref v, x + 6, 0.3f, z + 5, 2.5f, 0.3f, 2f, fabric.X, fabric.Y, fabric.Z);
                Box(ref v, x + 6, 0.6f, z + 5, 2.5f, 0.1f, 2f, 0.9f, 0.9f, 0.9f);
                // Table
                Box(ref v, x + 2, 0.8f, z + 2, 1.5f, 0.1f, 0.8f, wood.X, wood.Y, wood.Z);
                // Chair
                Box(ref v, x + 2.5f, 0.5f, z + 3.3f, 0.6f, 0.5f, 0.6f, metal.X, metal.Y, metal.Z);
                break;
            case BuildingType.Bank:
                // Counter
                Box(ref v, x + 3, 0, z + 1, 4, 1.3f, 1.5f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 3, 1.3f, z + 1, 4, 0.1f, 1.7f, metal.X, metal.Y, metal.Z);
                // Vault (back corner)
                Box(ref v, x + 7, 0, z + 7, 2, 2.5f, 2, metal.X, metal.Y, metal.Z);
                Box(ref v, x + 7.2f, 1.0f, z + 7.2f, 1.6f, 0.8f, 0.3f, bright.X, bright.Y, bright.Z);
                break;
            case BuildingType.House:
                // Table
                Box(ref v, x + 5, 0.8f, z + 5, 1.5f, 0.1f, 0.8f, wood.X, wood.Y, wood.Z);
                // Chair
                Box(ref v, x + 5.5f, 0.5f, z + 6.3f, 0.6f, 0.5f, 0.6f, wood.X, wood.Y, wood.Z);
                // Bed
                Box(ref v, x + 1.5f, 0.3f, z + 1.5f, 2f, 0.3f, 1.8f, fabric.X, fabric.Y, fabric.Z);
                break;
            case BuildingType.Police:
                Box(ref v, x + 2, 0.8f, z + 2, 2, 0.1f, 1.2f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 6, 0.8f, z + 2, 2, 0.1f, 1.2f, wood.X, wood.Y, wood.Z);
                Box(ref v, x + 2, 0.5f, z + 3.8f, 0.8f, 0.5f, 0.8f, fabric.X, fabric.Y, fabric.Z);
                Box(ref v, x + 6, 0.5f, z + 3.8f, 0.8f, 0.5f, 0.8f, fabric.X, fabric.Y, fabric.Z);
                // Cell bars (back)
                Box(ref v, x + 3, 0, z + 8, 4, 2.5f, 0.1f, metal.X, metal.Y, metal.Z);
                break;
            case BuildingType.GasStation:
                Box(ref v, x + 4, 0, z + 1, 2, 1.2f, 1.5f, metal.X, metal.Y, metal.Z);
                Box(ref v, x + 1, 0.5f, z + 5, 2, 1.5f, 0.8f, metal.X, metal.Y, metal.Z);
                Box(ref v, x + 7, 0.5f, z + 5, 2, 1.5f, 0.8f, metal.X, metal.Y, metal.Z);
                break;
        }

        Upload(ref _interiorVao, ref _interiorVbo, ref _interiorCount, ref _interiorGpuBytes, v);
    }

    public Vector3 ClampPlayerToWalkable(Vector3 pos, float radius)
    {
        if (!_inside || _insideBlock is not CityBlock block)
            return ClampToWalkable(pos, radius);
        float inset = 0.3f + radius;
        pos.X = Math.Clamp(pos.X, block.X + inset, block.X + block.Width - inset);
        pos.Z = Math.Clamp(pos.Z, block.Z + inset, block.Z + block.Depth - inset);
        return pos;
    }

    public Vector3 ClampToWalkable(Vector3 pos, float radius)
    {
        foreach (var bounds in _buildingBounds)
        {
            float bx1 = bounds.Min.X - radius;
            float bx2 = bounds.Max.X + radius;
            float bz1 = bounds.Min.Y - radius;
            float bz2 = bounds.Max.Y + radius;

            if (pos.X < bx1 || pos.X > bx2 || pos.Z < bz1 || pos.Z > bz2) continue;

            float dLeft = pos.X - bx1;
            float dRight = bx2 - pos.X;
            float dTop = pos.Z - bz1;
            float dBottom = bz2 - pos.Z;

            float min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
            if (min == dLeft) pos.X = bx1;
            else if (min == dRight) pos.X = bx2;
            else if (min == dTop) pos.Z = bz1;
            else pos.Z = bz2;
        }
        pos.Y = CityGenerator.GroundHeight(pos.X, pos.Z);
        return pos;
    }

    public Vector3 AdjustForNpcCollision(Vector3 pos, float radius, NpcCharacter? self = null)
    {
        if (_inside && self == _player) return pos;
        for (int i = 0; i < _npcs.Count; i++)
        {
            if (_npcs[i] == self || (_inside && _npcs[i] == _player)) continue;
            if (_npcs[i].State == NpcState.Sleeping) continue;
            Vector3 diff = pos - _npcs[i].Position;
            float distSq = diff.LengthSquared;
            float minDist = radius + 0.35f;
            if (distSq >= minDist * minDist) continue;
            float dist = MathF.Sqrt(Math.Max(distSq, 0.0001f));
            float overlap = (minDist - dist) / dist;
            pos += diff * overlap;
        }
        return pos;
    }

    public bool IsPositionWalkable(Vector3 pos, float radius)
    {
        foreach (var bounds in _buildingBounds)
        {
            float bx1 = bounds.Min.X - radius;
            float bx2 = bounds.Max.X + radius;
            float bz1 = bounds.Min.Y - radius;
            float bz2 = bounds.Max.Y + radius;

            if (pos.X >= bx1 && pos.X <= bx2 && pos.Z >= bz1 && pos.Z <= bz2)
                return false;
        }
        return true;
    }

    public Vector3 ResolveCameraPosition(Vector3 focus, Vector3 desired, float radius = 0.3f)
    {
        if (_inside && _insideBlock is CityBlock interior)
        {
            const float wallPadding = 0.45f;
            desired.X = Math.Clamp(desired.X, interior.X + wallPadding, interior.X + interior.Width - wallPadding);
            desired.Y = Math.Clamp(desired.Y, 0.45f, 2.65f);
            desired.Z = Math.Clamp(desired.Z, interior.Z + wallPadding, interior.Z + interior.Depth - wallPadding);
            return desired;
        }

        Vector3 delta = desired - focus;
        float distance = delta.Length;
        if (distance < 0.001f)
            return desired;

        Vector3 resolved = ResolveCameraRay(focus, desired, radius);
        const float minimumUsefulDistance = 2.4f;
        if (Vector3.DistanceSquared(focus, resolved) >= minimumUsefulDistance * minimumUsefulDistance)
            return resolved;

        Vector3 best = resolved;
        float bestDistanceSquared = Vector3.DistanceSquared(focus, resolved);
        float[] alternativeAngles = { 35f, -35f, 70f, -70f, 180f };
        foreach (float angle in alternativeAngles)
        {
            float radians = MathHelper.DegreesToRadians(angle);
            float sin = MathF.Sin(radians);
            float cos = MathF.Cos(radians);
            Vector3 alternativeOffset = new(
                delta.X * cos - delta.Z * sin,
                delta.Y,
                delta.X * sin + delta.Z * cos);
            Vector3 candidate = ResolveCameraRay(focus, focus + alternativeOffset, radius);
            float candidateDistanceSquared = Vector3.DistanceSquared(focus, candidate);
            if (candidateDistanceSquared <= bestDistanceSquared)
                continue;

            best = candidate;
            bestDistanceSquared = candidateDistanceSquared;
            if (bestDistanceSquared >= distance * distance * 0.9f)
                break;
        }

        return best;
    }

    private Vector3 ResolveCameraRay(Vector3 focus, Vector3 desired, float radius)
    {
        Vector3 delta = desired - focus;
        float distance = delta.Length;
        if (distance < 0.001f)
            return desired;

        float nearestHit = 1f;
        foreach (var block in _blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp)
                continue;

            Vector3 min = new(
                block.X + 0.5f - radius,
                -radius,
                block.Z + 0.5f - radius);
            Vector3 max = new(
                block.X + block.Width - 0.5f + radius,
                block.Height * 2.5f + 0.35f + radius,
                block.Z + block.Depth - 0.5f + radius);

            if (SegmentIntersectsBox(focus, desired, min, max, out float hit) && hit < nearestHit)
                nearestHit = hit;
        }

        if (nearestHit >= 1f)
            return desired;

        const float surfaceGap = 0.18f;
        float safeHit = Math.Max(0.04f, nearestHit - surfaceGap / distance);
        return focus + delta * safeHit;
    }

    private static bool SegmentIntersectsBox(Vector3 start, Vector3 end, Vector3 min, Vector3 max, out float entry)
    {
        Vector3 delta = end - start;
        float first = 0f;
        float last = 1f;

        if (!ClipAxis(start.X, delta.X, min.X, max.X, ref first, ref last) ||
            !ClipAxis(start.Y, delta.Y, min.Y, max.Y, ref first, ref last) ||
            !ClipAxis(start.Z, delta.Z, min.Z, max.Z, ref first, ref last))
        {
            entry = 1f;
            return false;
        }

        entry = first;
        return first <= last && last >= 0f && first <= 1f;
    }

    private static bool ClipAxis(float origin, float direction, float min, float max, ref float first, ref float last)
    {
        if (MathF.Abs(direction) < 0.00001f)
            return origin >= min && origin <= max;

        float inverse = 1f / direction;
        float near = (min - origin) * inverse;
        float far = (max - origin) * inverse;
        if (near > far)
            (near, far) = (far, near);

        first = Math.Max(first, near);
        last = Math.Min(last, far);
        return first <= last;
    }

    public HeroProgress Progress => _progress;
    public int Seed => _seed;
    public IReadOnlyList<CityBlock> Blocks => _blocks;
    public IReadOnlyList<InterestMarker> InterestMarkers => _markers;
    public string FeedbackMessage => _feedbackMessage;
    public float FeedbackTimer => _feedbackTimer;
    public Vector3 FeedbackColor => _feedbackColor;
    public long EstimatedGpuBufferBytes => (long)_roadGpuBytes +
        _sidewalkGpuBytes +
        _buildingGpuBytes +
        _windowGpuBytes +
        _npcGpuCapacityBytes +
        _highlightGpuBytes +
        _interiorGpuBytes +
        _sceneryGpuBytes + _facadeGpuBytes + _glassGpuBytes;

    public long EstimatedGpuTextureBytes => _materials.TextureBytes + _shadows.TextureBytes;

    public IReadOnlyList<NpcCharacter> Npcs => _npcs;
    public int NpcCount => _npcs.Count;
    public NpcCharacter? Player => _player;
    public bool HidePlayerForCamera { get; set; }
    public AwarenessSystem Awareness => _awareness;
    public float TimeOfDay { get => _timeOfDay; set => _timeOfDay = value; }

    public CityRenderer(int seed, HeroProgress? progress = null)
    {
        _seed = seed;
        _progress = progress ?? new HeroProgress();
        _blocks = CityGenerator.Generate(seed);
        float buildingInset = 0.5f;
        foreach (var block in _blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp) continue;
            _buildingBounds.Add(new Box2(
                block.X + buildingInset,
                block.Z + buildingInset,
                block.X + block.Width - buildingInset,
                block.Z + block.Depth - buildingInset));
            _buildingBounds.Add(CityStreetProps.ParkedCarBounds(block));
        }
        BuildInterestMarkers();
        SpawnNpcs(seed);
    }

    private void SpawnNpcs(int seed)
    {
        var rng = new Random(seed);
        for (int i = 0; i < 50; i++)
        {
            float hx = (float)(rng.NextDouble() - 0.5) * 180f;
            float hz = (float)(rng.NextDouble() - 0.5) * 180f;
            float wx = (float)(rng.NextDouble() - 0.5) * 180f;
            float wz = (float)(rng.NextDouble() - 0.5) * 180f;
            _npcs.Add(new NpcCharacter(ClampToWalkable(new Vector3(hx, 0, hz),0.3f),
                ClampToWalkable(new Vector3(wx, 0, wz),0.3f), seed + i * 397));
        }

        _player = _npcs[0];
        HeroStyle.ApplyTo(_player);
        _player.Position = MorningSpawn;
        _player.Rotation = _player.TargetRotation = 0;
    }

    private void PushCharactersApart()
    {
        const float minDist = 0.7f;
        for (int i = 0; i < _npcs.Count; i++)
        {
            if (_npcs[i].State == NpcState.Sleeping) continue;
            if (_inside && _npcs[i] == _player) continue;
            for (int j = i + 1; j < _npcs.Count; j++)
            {
                if (_npcs[j].State == NpcState.Sleeping) continue;
                if (_inside && _npcs[j] == _player) continue;

                Vector3 diff = _npcs[i].Position - _npcs[j].Position;
                float distSq = diff.LengthSquared;
                if (distSq >= minDist * minDist) continue;

                float dist = MathF.Sqrt(distSq);
                if (dist < 0.001f)
                {
                    diff = new Vector3(0.01f, 0, 0);
                    dist = 0.01f;
                }

                float overlap = (minDist - dist) / dist * 0.5f;
                Vector3 push = diff * overlap;
                _npcs[i].Position += push;
                _npcs[i].Position = ClampToWalkable(_npcs[i].Position, 0.3f);
                _npcs[j].Position -= push;
                _npcs[j].Position = ClampToWalkable(_npcs[j].Position, 0.3f);
            }
        }
    }

    private void BuildInterestMarkers()
    {
        _markers.Add(new InterestMarker(
            "center",
            "Центр петли",
            "Место, где день сходится в одну точку.",
            Vector3.Zero,
            CenterRadius,
            new Vector3(0.95f, 0.85f, 0.25f),
            memoryGain: 6f,
            curiosityGain: 2f));

        AddFirstMarker(BuildingType.Bank, "bank", "Банк", new Vector3(0.7f, 0.85f, 0.95f), memory: 4f, agency: 5f);
        AddFirstMarker(BuildingType.Cafe, "cafe", "Кафе", new Vector3(0.95f, 0.55f, 0.35f), empathy: 5f, curiosity: 2f);
        AddFirstMarker(BuildingType.Police, "police", "Участок", new Vector3(0.35f, 0.55f, 0.95f), courage: 6f, agency: 2f);
        AddFirstMarker(BuildingType.GasStation, "gas", "Заправка", new Vector3(0.8f, 0.8f, 0.8f), curiosity: 4f, courage: 2f);

        CityBlock tallest = default;
        bool hasTallest = false;
        foreach (var block in _blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp) continue;
            if (!hasTallest || block.Height > tallest.Height)
            {
                tallest = block;
                hasTallest = true;
            }
        }

        if (hasTallest)
        {
            _markers.Add(new InterestMarker(
                "tower",
                "Самая высокая крыша",
                "Сверху видно, что город слишком аккуратно повторяется.",
                BlockCenter(tallest),
                MarkerVisitRadius,
                new Vector3(0.65f, 0.45f, 0.95f),
                memoryGain: 2f,
                curiosityGain: 7f,
                courageGain: 3f));
        }
    }

    private void AddFirstMarker(BuildingType type, string id, string name, Vector3 color, float memory = 0f, float curiosity = 0f, float empathy = 0f, float agency = 0f, float courage = 0f)
    {
        foreach (var block in _blocks)
        {
            if (block.Type != type) continue;
            _markers.Add(new InterestMarker(id, name, $"Точка интереса: {name}", BlockCenter(block), MarkerVisitRadius, color, memory, curiosity, empathy, agency, courage));
            return;
        }
    }

    private static Vector3 BlockCenter(CityBlock block) => new(block.X + block.Width * 0.5f, 0f, block.Z + block.Depth * 0.5f);

    public void BuildGeometry()
    {
        BuildRoads();
        BuildSidewalks();
        BuildBuildings();
        BuildTrees();
    }

    private void BuildRoads()
    {
        var vertices = CityStreets.BuildRoads();
        Upload(ref _roadVao, ref _roadVbo, ref _roadCount, ref _roadGpuBytes, vertices);
    }

    private void BuildSidewalks()
    {
        var vertices = CityStreets.BuildPaving(_blocks);
        Upload(ref _sidewalkVao, ref _sidewalkVbo, ref _sidewalkCount, ref _sidewalkGpuBytes, vertices);
    }

    private void BuildBuildings()
    {
        var v = new List<float>();
        var wv = new List<float>();
        var fv = new List<float>();
        var gv = new List<float>();
        _facadeRanges.Clear(); _buildingRanges.Clear(); _windowRanges.Clear(); _glassRanges.Clear();
        foreach (var b in _blocks)
        {
            if (b.Type is BuildingType.Tree or BuildingType.Lamp) continue;
            float x = b.X + 0.5f, z = b.Z + 0.5f;
            float w = b.Width - 1, d = b.Depth - 1, h = b.Height * 2.5f;
            int first = v.Count/9, windowFirst = wv.Count/9, facadeFirst = fv.Count/9, glassFirst = gv.Count/9;
            bool stone = b.Type is BuildingType.Office or BuildingType.Bank or BuildingType.Police;
            Vector3 wall = stone ? b.Color*0.5f+new Vector3(0.36f,0.37f,0.36f) :
                Vector3.Lerp(new(0.86f,0.79f,0.73f),new(1.15f,1.11f,1.03f),b.Color.X);
            SceneGeometry.Box(fv, new(x,0,z),new(w,h,d),wall);
            SceneGeometry.Box(v, new(x-0.012f,0,z-0.012f),new(w+0.024f,0.28f,d+0.024f),b.Color*0.67f);
            SceneGeometry.Box(v, new(x-0.08f,h-0.18f,z-0.08f),new(w+0.16f,0.20f,d+0.16f),b.Accent*0.75f);
            SceneGeometry.Ground(v,x-0.1f,z-0.1f,w+0.2f,d+0.2f,h+0.03f,new(0.29f,0.33f,0.34f));
            Vector3 trim = new(0.74f,0.77f,0.75f);
            Vector3 frame = new(0.18f,0.24f,0.26f);
            var rng = new Random(b.X * 31 + b.Z * 17);
            for (int face = 0; face < 4; face++)
            {
                Vector3 origin = face switch {
                    0 => new(x,0,z+d), 1 => new(x+w,0,z), 2 => new(x+w,0,z+d), _ => new(x,0,z) };
                Vector3 horizontal = face switch { 0 => Vector3.UnitX, 1 => -Vector3.UnitX, 2 => -Vector3.UnitZ, _ => Vector3.UnitZ };
                Vector3 normal = Vector3.Cross(horizontal,Vector3.UnitY);
                float span = face < 2 ? w : d;
                int columns = Math.Max(1,(int)(span/2.6f));
                float spacing = span/columns;
                for (int floor = 0; floor < b.Height; floor++)
                    for (int column = 0; column < columns; column++)
                    {
                        float width = Math.Min(1.3f,spacing-0.6f);
                        Vector3 at = origin + horizontal*(column*spacing+(spacing-width)*0.5f)
                            + Vector3.UnitY*(0.75f+floor*2.5f);
                        bool lit = rng.NextDouble() < 0.42;
                        FaceRect(v,at-horizontal*0.08f-Vector3.UnitY*0.08f+normal*0.014f,horizontal,width+0.16f,1.51f,frame);
                        Vector3 glass = Vector3.Lerp(new(0.27f,0.43f,0.51f),new(0.46f,0.65f,0.69f),(floor%3)/2f);
                        FaceRect(gv,at+normal*0.021f,horizontal,width,1.35f,glass);
                        FaceRect(v,at+normal*0.034f+horizontal*width*0.48f,horizontal,0.045f,1.35f,trim*0.8f);
                        FaceRect(v,at+normal*0.035f+Vector3.UnitY*0.64f,horizontal,width,0.045f,trim*0.8f);
                        FaceRect(v,at-horizontal*0.12f+normal*0.045f-Vector3.UnitY*0.13f,horizontal,width+0.24f,0.10f,trim);
                        if (lit) FaceRect(wv,at+normal*0.025f,horizontal,width,1.35f,new(0.94f,0.77f,0.46f));
                    }
            }
            float doorX = x+w*0.5f;
            FaceRect(v,new(doorX-0.7f,0.025f,z+d+0.055f),Vector3.UnitX,1.4f,2.15f,frame);
            FaceRect(v,new(doorX-0.56f,0.13f,z+d+0.060f),Vector3.UnitX,1.12f,1.85f,new(0.32f,0.44f,0.47f));
            FaceRect(v,new(doorX+0.37f,0.85f,z+d+0.065f),Vector3.UnitX,0.06f,0.30f,new(0.81f,0.76f,0.57f));
            SceneGeometry.Box(v,new(doorX-1.05f,2.25f,z+d-0.02f),new(2.1f,0.13f,0.46f),b.Accent);
            if (b.Type is BuildingType.Cafe or BuildingType.Store)
            {
                for (int stripe = 0; stripe < 6; stripe++)
                    SceneGeometry.Box(v,new(doorX-1.5f+stripe*0.5f,2.4f,z+d),new(0.5f,0.16f,0.8f),
                        stripe%2==0?b.Accent:new Vector3(0.85f,0.84f,0.74f));
            }
            CityFacadeDetails.Append(v,b);
            Vector3 center = new(x+w/2,h/2,z+d/2);
            float radius = MathF.Sqrt(w*w+d*d+h*h)*0.5f+3;
            _facadeRanges.Add(new(facadeFirst,fv.Count/9-facadeFirst,center,radius,stone?6:1));
            _buildingRanges.Add(new(first,v.Count/9-first,center,radius));
            _windowRanges.Add(new(windowFirst,wv.Count/9-windowFirst,center,radius));
            _glassRanges.Add(new(glassFirst,gv.Count/9-glassFirst,center,radius));
        }
        Upload(ref _facadeVao,ref _facadeVbo,ref _facadeCount,ref _facadeGpuBytes,fv);
        Upload(ref _glassVao,ref _glassVbo,ref _glassCount,ref _glassGpuBytes,gv);
        Upload(ref _buildingVao,ref _buildingVbo,ref _buildingCount,ref _buildingGpuBytes,v);
        Upload(ref _windowVao,ref _windowVbo,ref _windowCount,ref _windowGpuBytes,wv);
    }

    private static void FaceRect(List<float> v, Vector3 origin, Vector3 horizontal, float width, float height, Vector3 color)
    {
        Vector3 side = horizontal * width, up = Vector3.UnitY * height;
        SceneGeometry.Quad(v,origin,origin+side,origin+side+up,origin+up,color);
    }

    private void BuildTrees()
    {
        var v = new List<float>();
        _sceneryRanges.Clear();
        foreach (var block in _blocks)
        {
            int first = v.Count/9;
            CityStreetProps.Append(v,block);
            if (block.Type is not (BuildingType.Tree or BuildingType.Lamp))
            {
                AddTree(block.X-1.6f,block.Z+4,0.85f);
                _sceneryRanges.Add(new(first,v.Count/9-first,new(block.X+8,3,block.Z+5),17));
                continue;
            }
            float x = block.X, z = block.Z;
            SceneGeometry.Ground(v,x+1,z+1,block.Width-2,block.Depth-2,0.126f,new(0.26f,0.40f,0.27f));
            SceneGeometry.Ground(v,x+4.1f,z,1.8f,block.Depth,0.13f,new(0.61f,0.63f,0.61f));
            if (block.Type == BuildingType.Tree)
            {
                AddTree(x + 2.5f, z + 3, 1f);
                AddTree(x + 7.4f, z + 6.5f, 0.85f);
            }
            else
            {
                Vector3 metal = new(0.18f, 0.24f, 0.25f);
                SceneGeometry.Box(v, new(x+2,0,z+2), new(0.16f,3.5f,0.16f), metal);
                SceneGeometry.Box(v, new(x+1.7f,3.4f,z+1.7f), new(0.76f,0.16f,0.76f), metal);
                SceneGeometry.Box(v, new(x+1.83f,3.22f,z+1.83f), new(0.50f,0.2f,0.50f), new(0.94f,0.84f,0.56f));
                AddTree(x+7, z+6, 0.9f);
            }
            Vector3 bench = new(0.39f,0.30f,0.25f);
            SceneGeometry.Box(v,new(x+5.5f,0.42f,z+1.4f),new(2,0.12f,0.6f),bench);
            SceneGeometry.Box(v,new(x+5.5f,0.7f,z+1.9f),new(2,0.45f,0.1f),bench);
            SceneGeometry.Box(v,new(x+5.65f,0,z+1.5f),new(0.12f,0.5f,0.35f),bench*0.6f);
            SceneGeometry.Box(v,new(x+7.2f,0,z+1.5f),new(0.12f,0.5f,0.35f),bench*0.6f);
            _sceneryRanges.Add(new(first,v.Count/9-first,new(x+8,3,z+5),17));
        }
        Upload(ref _sceneryVao, ref _sceneryVbo, ref _sceneryCount, ref _sceneryGpuBytes, v);

        void AddTree(float x, float z, float scale)
        {
            Vector3 root=new(x,0.12f,z), bark=new(0.29f,0.25f,0.21f);
            SceneGeometry.Cylinder(v,root,root+new Vector3(0.12f,3.8f,0)*scale,0.14f*scale,bark);
            for(int crown=0;crown<7;crown++)
            {
                float angle=crown*2.4f, spread=crown==0?0:1.12f;
                Vector3 at=root+new Vector3(MathF.Cos(angle)*spread,4.15f+(crown%3)*0.46f,MathF.Sin(angle)*spread)*scale;
                SceneGeometry.Cylinder(v,root+Vector3.UnitY*2.3f*scale,at,0.06f*scale,bark,6);
                SceneGeometry.Foliage(v,at,new Vector3(1.10f,1.25f,1.08f)*scale,
                    crown%2==0?new(0.26f,0.40f,0.23f):new(0.35f,0.49f,0.27f));
            }
        }
    }

    public void UpdateNpcs(float dt)
    {
        _animationTime += dt;
        if (_feedbackTimer > 0f)
            _feedbackTimer = Math.Max(0f, _feedbackTimer - dt);

        _timeOfDay += dt * 0.03f;
        if (_timeOfDay > 24f)
            ApplyDayBoundary(0f, resetPlayerPosition: false, closeInterior: false);

        foreach (var npc in _npcs)
        {
            if (npc == _player) continue;
            npc.Update(_timeOfDay, dt);
            if (npc.State != NpcState.Sleeping)
                npc.Position = ClampToWalkable(npc.Position, 0.25f);
        }

        // --- Easter egg detection (per-frame) ---
        if (_player != null)
        {
            if (_player.State == NpcState.Relaxing || _player.State == NpcState.Walking && _player.Velocity.LengthSquared < 0.01f)
            {
                _standStillTimer += dt;
                if (_standStillTimer >= 5f)
                {
                    AwardEgg("stand_still", "Любопытство: ты услышал повтор дня", new Vector3(0.35f, 0.85f, 0.95f), curiosityGain: 5f);
                    _standStillTimer = 0f;
                }
            }
            else
            {
                _standStillTimer = 0f;
            }

            foreach (var marker in _markers)
            {
                if (_visitedMarkers.Contains(marker.Id)) continue;
                if (Vector3.DistanceSquared(_player.Position, marker.Position) > MarkerVisitRadius * MarkerVisitRadius) continue;

                _visitedMarkers.Add(marker.Id);
                AwardEgg(
                    $"visit_{marker.Id}",
                    $"{marker.Name}: место вспомнило тебя",
                    marker.Color,
                    marker.MemoryGain,
                    marker.CuriosityGain,
                    marker.EmpathyGain,
                    marker.AgencyGain,
                    marker.CourageGain);
            }

            if (!_nightWalkAwarded && (_timeOfDay >= 23f || _timeOfDay < 5f) && _player.Position.LengthSquared > CenterRadius * CenterRadius)
            {
                _nightWalkAwarded = true;
                AwardEgg("night_walk", "Мужество: город ночью не тот же самый", new Vector3(0.55f, 0.5f, 0.95f), courageGain: 6f, memoryGain: 2f);
            }
        }

        if (_player != null && _player.State != NpcState.Aware)
            _awareness.Update(_player, _timeOfDay, dt);

        PushCharactersApart();

        BuildNpcMesh();

        // If day just changed, we could also reset per-day eggs if desired.
        // For now, eggs are cumulative.
    }

    /// <summary>
    /// Explicit player-driven end-of-day boundary. It uses the same core reset
    /// path as natural midnight, but starts the next cycle at a readable morning
    /// hour and returns the hero to the stable morning spawn.
    /// </summary>
    public int AdvanceToNextMorning()
    {
        int before = _progress.Day;
        ApplyDayBoundary(DefaultMorningHour, resetPlayerPosition: true, closeInterior: true);
        return _progress.Day - before;
    }

    private void ApplyDayBoundary(float nextTimeOfDay, bool resetPlayerPosition, bool closeInterior)
    {
        _progress.NewDay(); // exactly one Sverka for this boundary
        _timeOfDay = Math.Clamp(nextTimeOfDay, 0f, 23.99f);
        _nightWalkAwarded = false;
        _standStillTimer = 0f;

        foreach (NpcCharacter npc in _npcs)
        {
            if (npc == _player) continue;
            npc.Reset();
        }

        if (_player != null)
        {
            if (resetPlayerPosition)
            {
                _player.Position = MorningSpawn;
                _player.Rotation = _player.TargetRotation = 0f;
            }

            _player.Velocity = Vector3.Zero;
            _player.AnimBlend = 0f;
            _player.State = NpcState.Walking;
        }

        if (closeInterior && _inside)
        {
            _inside = false;
            _insideBlock = null;
            DeleteMesh(ref _interiorVao, ref _interiorVbo, ref _interiorCount, ref _interiorGpuBytes);
        }

        // Spatial chapter consequences (for example Mark staying near Lida after
        // an anchored meeting) become visible immediately on the new morning.
        FirstMemorySpatial.SyncDay(_progress);
    }

    public string GetPlayerDialogue()
    {
        return _player?.GetDialogue(_awareness.Level, _progress) ?? "";
    }

    public NpcCharacter? FindClosestNpc(Vector3 pos, float maxDist)
    {
        NpcCharacter? closest = null;
        float closestDistSq = maxDist * maxDist;
        foreach (var npc in _npcs)
        {
            if (npc == _player) continue;
            float dsq = (npc.Position - pos).LengthSquared;
            if (dsq < closestDistSq)
            {
                closestDistSq = dsq;
                closest = npc;
            }
        }
        return closest;
    }

    private void AwardEgg(
        string id,
        string message,
        Vector3 color,
        float memoryGain = 0f,
        float curiosityGain = 0f,
        float empathyGain = 0f,
        float agencyGain = 0f,
        float courageGain = 0f)
    {
        if (!_progress.DiscoverEgg(id, memoryGain, curiosityGain, empathyGain, agencyGain, courageGain))
            return;

        if (_progress.LastLeveledQuality is HeroQuality quality)
            message = $"{QualityName(quality)} {ToRoman(_progress.LastLeveledQualityLevel)}";

        TriggerFeedback(message, color);
        _awareness.Add(4f);
    }

    private void TriggerFeedback(string message, Vector3 color)
    {
        _feedbackMessage = message;
        _feedbackColor = color;
        _feedbackTimer = 4f;
    }

    private static string QualityName(HeroQuality quality) => quality switch
    {
        HeroQuality.Memory => "Память",
        HeroQuality.Curiosity => "Любопытство",
        HeroQuality.Empathy => "Эмпатия",
        HeroQuality.Agency => "Воля",
        HeroQuality.Courage => "Мужество",
        _ => "Качество",
    };

    private static string ToRoman(int value) => value switch
    {
        1 => "1",
        2 => "2",
        3 => "3",
        4 => "4",
        _ => "",
    };

    private void BuildNpcMesh()
    {
        _characterMesh.Clear();
        foreach (var npc in _npcs)
        {
            if (npc == _player && HidePlayerForCamera) continue;
            if (_inside && npc != _player) continue;
            CharacterDetail detail = npc == _player || _player == null ? CharacterDetail.Full :
                CharacterMesh.DetailForDistance(Vector3.DistanceSquared(npc.Position, CharacterViewPosition ?? _player.Position));
            _characterMesh.Append(npc, _animationTime, detail: detail);
        }
        _npcCount = _characterMesh.VertexCount;
        UploadDynamic(_characterMesh.Data, _characterMesh.FloatCount);
    }

    private unsafe void UploadDynamic(float[] data, int len)
    {
        if (len == 0) return;
        if (_npcVao == 0)
        {
            _npcVao = GL.GenVertexArray();
            _npcVbo = GL.GenBuffer();
            GL.BindVertexArray(_npcVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _npcVbo);
            ConfigureVertexAttributes();
        }

        GL.BindVertexArray(_npcVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _npcVbo);
        int byteCount = len * sizeof(float);
        EnsureNpcBufferCapacity(byteCount);

        fixed (float* p = data)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, byteCount, (nint)p);
    }

    private void EnsureNpcBufferCapacity(int byteCount)
    {
        if (byteCount <= _npcGpuCapacityBytes) return;

        _npcGpuCapacityBytes = Math.Max(byteCount, Math.Max(4096, _npcGpuCapacityBytes * 2));
        GL.BufferData(BufferTarget.ArrayBuffer, _npcGpuCapacityBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
    }

    public void Render(CityRenderContext context, ref Matrix4 view, ref Matrix4 proj, Vector3 fogCol)
    {
        SceneLighting lighting = SceneLighting.At(_timeOfDay);
        float shadowStrength = _inside || !ShadowsEnabled ? 0 : 1-lighting.WindowGlow;
        Vector3 focus = _player?.Position ?? Vector3.Zero;
        if (!_inside)
        {
            _materials.Load();
            if (shadowStrength > 0.01f)
            {
                _shadows.Begin(focus,lighting.Sun);
                try
                {
                    DrawShadow(_facadeVao,_facadeRanges,focus);
                    DrawShadow(_buildingVao,_buildingRanges,focus);
                    DrawShadow(_sceneryVao,_sceneryRanges,focus);
                    if (_npcCount > 0) { GL.BindVertexArray(_npcVao); GL.DrawArrays(PrimitiveType.Triangles,0,_npcCount); }
                }
                finally { _shadows.End(); }
            }
            _materials.Bind();
            _shadows.Bind();
        }
        GL.UseProgram(context.Shader);
        var id = Matrix4.Identity;
        var lightMatrix = _shadows.LightMatrix;
        GL.UniformMatrix4(context.ViewLocation,false,ref view);
        GL.UniformMatrix4(context.ProjectionLocation,false,ref proj);
        GL.UniformMatrix4(context.ModelLocation,false,ref id);
        GL.UniformMatrix4(context.LightMatrixLocation,false,ref lightMatrix);
        GL.Uniform3(context.ColorLocation,-1f,-1f,-1f);
        GL.Uniform3(context.FogColorLocation,fogCol);
        GL.Uniform3(context.EyeLocation,view.Inverted().ExtractTranslation());
        GL.Uniform1(context.FogDensityLocation,_inside?0f:0.010f);
        GL.Uniform1(context.ShadowStrengthLocation,shadowStrength);
        GL.Uniform1(context.DaylightLocation,_inside?1f:1-lighting.WindowGlow);
        GL.Uniform1(context.WorldPassLocation,1);
        GL.Uniform3(context.AmbientLocation,_inside?new Vector3(0.74f,0.72f,0.67f):lighting.Ambient);
        try
        {
            if (_inside)
            {
                GL.Uniform1(context.MaterialLocation,0);
                if (_interiorCount > 0) { GL.BindVertexArray(_interiorVao); GL.DrawArrays(PrimitiveType.Triangles,0,_interiorCount); }
            }
            else
            {
                GL.Uniform1(context.MaterialLocation,2);
                GL.BindVertexArray(_roadVao); GL.DrawArrays(PrimitiveType.Triangles,0,_roadCount);
                GL.Uniform1(context.MaterialLocation,3);
                GL.BindVertexArray(_sidewalkVao); GL.DrawArrays(PrimitiveType.Triangles,0,_sidewalkCount);
                DrawVisible(_facadeVao,_facadeRanges,view,proj,context.MaterialLocation);
                GL.Uniform1(context.MaterialLocation,0);
                DrawVisible(_buildingVao,_buildingRanges,view,proj);
                GL.Uniform1(context.MaterialLocation,4);
                DrawVisible(_glassVao,_glassRanges,view,proj);
                if (lighting.WindowGlow > 0.01f)
                {
                    GL.Uniform1(context.MaterialLocation,7);
                    DrawVisible(_windowVao,_windowRanges,view,proj);
                }
                GL.Uniform1(context.MaterialLocation,0);
                DrawVisible(_sceneryVao,_sceneryRanges,view,proj);
            }
            GL.Uniform1(context.MaterialLocation,0);
            if (_npcCount > 0) { GL.BindVertexArray(_npcVao); GL.DrawArrays(PrimitiveType.Triangles,0,_npcCount); }
        }
        finally
        {
            GL.Uniform1(context.WorldPassLocation,0);
            GL.Uniform1(context.ShadowStrengthLocation,0f);
        }
    }

    private static void DrawVisible(int vao, List<SceneRange> ranges, in Matrix4 view, in Matrix4 projection, int materialLocation = -1)
    {
        GL.BindVertexArray(vao);
        foreach (var range in ranges)
        {
            if (range.Count == 0 || !range.Visible(view,projection)) continue;
            if (materialLocation >= 0) GL.Uniform1(materialLocation,range.Material);
            GL.DrawArrays(PrimitiveType.Triangles,range.First,range.Count);
        }
    }

    private static void DrawShadow(int vao, List<SceneRange> ranges, Vector3 focus)
    {
        GL.BindVertexArray(vao);
        foreach (var range in ranges)
            if (range.Count > 0 && range.Near(focus,75))
                GL.DrawArrays(PrimitiveType.Triangles,range.First,range.Count);
    }

    public void RenderHighlight(int shader, int modelL, Vector3 pos)
    {
        GL.UseProgram(shader);
        var m = Matrix4.CreateTranslation(pos);
        GL.UniformMatrix4(modelL, false, ref m);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
        if (_highlightVao == 0)
        {
            _highlightVao = GL.GenVertexArray();
            _highlightVbo = GL.GenBuffer();
            float[] vtx = {
                0,0,0,1,0,0,1,1,0,0,0,0,1,1,0,0,1,0, 0,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,1,1,
                0,0,0,0,1,0,0,1,1,0,0,0,0,1,1,0,0,1, 1,0,0,1,1,0,1,1,1,1,0,0,1,1,1,1,0,1,
                0,0,0,0,0,1,1,0,1,0,0,0,1,0,1,1,0,0, 0,1,0,1,1,0,1,1,1,0,1,0,1,1,1,0,1,1,
            };
            GL.BindVertexArray(_highlightVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _highlightVbo);
            _highlightGpuBytes = vtx.Length * sizeof(float);
            GL.BufferData(BufferTarget.ArrayBuffer, _highlightGpuBytes, vtx, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, 0);
            GL.EnableVertexAttribArray(0);
        }
        GL.BindVertexArray(_highlightVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
    }

    private static void Quad(ref List<float> v,
        float x1, float y1, float z1, float x2, float y2, float z2,
        float x3, float y3, float z3, float x4, float y4, float z4,
        float r, float g, float b) =>
        SceneGeometry.Quad(v, new(x1,y1,z1), new(x2,y2,z2), new(x3,y3,z3), new(x4,y4,z4), new(r,g,b));

    private static void Box(ref List<float> v, float x, float y, float z, float w, float h, float d,
        float r, float g, float b) =>
        SceneGeometry.Box(v, new(x,y,z), new(w,h,d), new(r,g,b));

    private static unsafe void Upload(ref int vao, ref int vbo, ref int count, ref int gpuBytes, List<float> verts)
    {
        if (verts.Count == 0) { count = 0; gpuBytes = 0; return; }
        count = verts.Count / FloatsPerVertex;
        if (vao == 0) vao = GL.GenVertexArray();
        if (vbo == 0) vbo = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        ReadOnlySpan<float> source = CollectionsMarshal.AsSpan(verts);
        gpuBytes = count * PackedSceneVertex.Stride;
        GL.BufferData(BufferTarget.ArrayBuffer, gpuBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
        // Keep the temporary packing buffer bounded even for a city-sized mesh.
        const int batchSize = 16384;
        var packed = ArrayPool<PackedSceneVertex>.Shared.Rent(batchSize);
        try
        {
            for (int first = 0; first < count; first += batchSize)
            {
                int length = Math.Min(batchSize, count - first);
                for (int i = 0; i < length; i++)
                    packed[i] = new PackedSceneVertex(source.Slice((first + i) * FloatsPerVertex, FloatsPerVertex));
                fixed (PackedSceneVertex* p = packed)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)(first * PackedSceneVertex.Stride),
                        length * PackedSceneVertex.Stride, (nint)p);
            }
        }
        finally { ArrayPool<PackedSceneVertex>.Shared.Return(packed); }
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, PackedSceneVertex.Stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.HalfFloat, false, PackedSceneVertex.Stride, 12);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Short, true, PackedSceneVertex.Stride, 20);
        GL.EnableVertexAttribArray(2);
    }


    private static void ConfigureVertexAttributes()
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
    }

    public void Dispose()
    {
        _materials.Dispose();
        _shadows.Dispose();
        DeleteMesh(ref _facadeVao, ref _facadeVbo, ref _facadeCount, ref _facadeGpuBytes);
        DeleteMesh(ref _glassVao, ref _glassVbo, ref _glassCount, ref _glassGpuBytes);
        DeleteMesh(ref _roadVao, ref _roadVbo, ref _roadCount, ref _roadGpuBytes);
        DeleteMesh(ref _sidewalkVao, ref _sidewalkVbo, ref _sidewalkCount, ref _sidewalkGpuBytes);
        DeleteMesh(ref _buildingVao, ref _buildingVbo, ref _buildingCount, ref _buildingGpuBytes);
        DeleteMesh(ref _windowVao, ref _windowVbo, ref _windowCount, ref _windowGpuBytes);
        DeleteMesh(ref _sceneryVao, ref _sceneryVbo, ref _sceneryCount, ref _sceneryGpuBytes);
        DeleteMesh(ref _npcVao, ref _npcVbo, ref _npcCount);
        _npcGpuCapacityBytes = 0;
        DeleteMesh(ref _interiorVao, ref _interiorVbo, ref _interiorCount, ref _interiorGpuBytes);
        DeleteBuffer(ref _highlightVao, ref _highlightVbo);
        _highlightGpuBytes = 0;
    }

    private static void DeleteMesh(ref int vao, ref int vbo, ref int count, ref int gpuBytes)
    {
        if (vbo != 0) GL.DeleteBuffer(vbo);
        if (vao != 0) GL.DeleteVertexArray(vao);
        vao = 0;
        vbo = 0;
        count = 0;
        gpuBytes = 0;
    }

    private static void DeleteMesh(ref int vao, ref int vbo, ref int count)
    {
        if (vbo != 0) GL.DeleteBuffer(vbo);
        if (vao != 0) GL.DeleteVertexArray(vao);
        vao = 0;
        vbo = 0;
        count = 0;
    }

    private static void DeleteBuffer(ref int vao, ref int vbo)
    {
        if (vbo != 0) GL.DeleteBuffer(vbo);
        if (vao != 0) GL.DeleteVertexArray(vao);
        vao = 0;
        vbo = 0;
    }
}
