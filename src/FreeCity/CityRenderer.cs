using System;
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
    int FogColorLocation);

public class CityRenderer : IDisposable
{
    private const int FloatsPerVertex = 9;
    private const int VertexStrideBytes = FloatsPerVertex * sizeof(float);

    private int _roadVao, _roadVbo, _roadCount;
    private int _sidewalkVao, _sidewalkVbo, _sidewalkCount;
    private int _buildingVao, _buildingVbo, _buildingCount;
    private int _windowVao, _windowVbo, _windowCount;
    private int _npcVao, _npcVbo, _npcCount;
    private int _highlightVao, _highlightVbo;
    private int _roadGpuBytes, _sidewalkGpuBytes, _buildingGpuBytes, _windowGpuBytes, _highlightGpuBytes;
    private SpriteRenderer? _spriteRenderer;
    private Texture? _treeTexture;
    private readonly List<(Vector3 pos, float scale)> _treePositions = new();
    private float[] _npcBuf = new float[131072];
    private int _npcBufLen;
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
        return new Vector3(doorX, 0, doorZ + 2f);
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
        Quad(ref v, ix, 0, iz, ix + iw, 0, iz, ix + iw, 0, iz + id, ix, 0, iz + id, floorCol.X, floorCol.Y, floorCol.Z);
        // Ceiling
        Quad(ref v, ix, h, iz, ix + iw, h, iz, ix + iw, h, iz + id, ix, h, iz + id, ceilCol.X, ceilCol.Y, ceilCol.Z);

        // Walls (inward-facing, thick enough to block outside view)
        // Front (positive Z) — door side, darker
        Quad(ref v, ix, 0, iz + id, ix + iw, 0, iz + id, ix + iw, h, iz + id, ix, h, iz + id, wallCol.X * 0.65f, wallCol.Y * 0.65f, wallCol.Z * 0.65f);
        // Back (negative Z) — solid wall, darkest
        Quad(ref v, ix + iw, 0, iz, ix, 0, iz, ix, h, iz, ix + iw, h, iz, wallCol.X * 0.45f, wallCol.Y * 0.45f, wallCol.Z * 0.45f);
        // Right (positive X)
        Quad(ref v, ix + iw, 0, iz, ix + iw, 0, iz + id, ix + iw, h, iz + id, ix + iw, h, iz, wallCol.X * 0.55f, wallCol.Y * 0.55f, wallCol.Z * 0.55f);
        // Left (negative X)
        Quad(ref v, ix, 0, iz, ix, 0, iz + id, ix, h, iz + id, ix, h, iz, wallCol.X * 0.75f, wallCol.Y * 0.75f, wallCol.Z * 0.75f);

        // Back wall accent — darker strip to emphasize solidity
        float stripH = 0.15f;
        Quad(ref v, ix + iw, 0, iz, ix, 0, iz, ix, stripH, iz, ix + iw, stripH, iz,
             wallCol.X * 0.3f, wallCol.Y * 0.3f, wallCol.Z * 0.3f);

        // Door marker (lighter rectangle on front wall)
        float doorW = 1.2f, doorH = 2.2f;
        float doorCx = x + w * 0.5f;
        Quad(ref v, doorCx - doorW * 0.5f, 0, iz + id + 0.01f, doorCx + doorW * 0.5f, 0, iz + id + 0.01f,
                     doorCx + doorW * 0.5f, doorH, iz + id + 0.01f, doorCx - doorW * 0.5f, doorH, iz + id + 0.01f,
                     0.4f, 0.6f, 0.8f);

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
        return pos;
    }

    public Vector3 AdjustForNpcCollision(Vector3 pos, float radius, NpcCharacter? self = null)
    {
        for (int i = 0; i < _npcs.Count; i++)
        {
            if (_npcs[i] == self) continue;
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
        _interiorGpuBytes;

    public IReadOnlyList<NpcCharacter> Npcs => _npcs;
    public int NpcCount => _npcs.Count;
    public NpcCharacter? Player => _player;
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
            _npcs.Add(new NpcCharacter(new Vector3(hx, 0, hz), new Vector3(wx, 0, wz), seed + i * 397));
        }

        _player = _npcs[0];
        HeroStyle.ApplyTo(_player);
    }

    private void PushCharactersApart()
    {
        const float minDist = 0.7f;
        for (int i = 0; i < _npcs.Count; i++)
        {
            if (_npcs[i].State == NpcState.Sleeping) continue;
            for (int j = i + 1; j < _npcs.Count; j++)
            {
                if (_npcs[j].State == NpcState.Sleeping) continue;

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
        var v = new List<float>();
        int half = CityGenerator.CityRadius * (CityGenerator.BlockSize + CityGenerator.RoadWidth) + CityGenerator.BlockSize;

        for (int dx = -CityGenerator.CityRadius; dx <= CityGenerator.CityRadius; dx++)
        {
            float rx = dx * (CityGenerator.BlockSize + CityGenerator.RoadWidth) - CityGenerator.RoadWidth / 2f;
            Quad(ref v, rx, 0.01f, -half, rx + CityGenerator.RoadWidth, 0.01f, -half,
                         rx + CityGenerator.RoadWidth, 0.01f, half, rx, 0.01f, half,
                         0.15f, 0.15f, 0.15f);
        }

        for (int dz = -CityGenerator.CityRadius; dz <= CityGenerator.CityRadius; dz++)
        {
            float rz = dz * (CityGenerator.BlockSize + CityGenerator.RoadWidth) - CityGenerator.RoadWidth / 2f;
            Quad(ref v, -half, 0.01f, rz, half, 0.01f, rz,
                         half, 0.01f, rz + CityGenerator.RoadWidth, -half, 0.01f, rz + CityGenerator.RoadWidth,
                         0.15f, 0.15f, 0.15f);
        }

        Upload(ref _roadVao, ref _roadVbo, ref _roadCount, ref _roadGpuBytes, v);
    }

    private void BuildSidewalks()
    {
        var v = new List<float>();
        int half = CityGenerator.CityRadius * (CityGenerator.BlockSize + CityGenerator.RoadWidth) + CityGenerator.BlockSize;

        for (int dx = -CityGenerator.CityRadius; dx <= CityGenerator.CityRadius; dx++)
        {
            float rx = dx * (CityGenerator.BlockSize + CityGenerator.RoadWidth);
            // тротуар слева от дороги
            Quad(ref v, rx - CityGenerator.RoadWidth / 2f - CityGenerator.SidewalkW, 0.02f, -half,
                         rx - CityGenerator.RoadWidth / 2f, 0.02f, -half,
                         rx - CityGenerator.RoadWidth / 2f, 0.02f, half,
                         rx - CityGenerator.RoadWidth / 2f - CityGenerator.SidewalkW, 0.02f, half,
                         0.55f, 0.55f, 0.55f);
            // тротуар справа от дороги
            Quad(ref v, rx + CityGenerator.RoadWidth / 2f, 0.02f, -half,
                         rx + CityGenerator.RoadWidth / 2f + CityGenerator.SidewalkW, 0.02f, -half,
                         rx + CityGenerator.RoadWidth / 2f + CityGenerator.SidewalkW, 0.02f, half,
                         rx + CityGenerator.RoadWidth / 2f, 0.02f, half,
                         0.55f, 0.55f, 0.55f);
        }

        for (int dz = -CityGenerator.CityRadius; dz <= CityGenerator.CityRadius; dz++)
        {
            float rz = dz * (CityGenerator.BlockSize + CityGenerator.RoadWidth);
            Quad(ref v, -half, 0.02f, rz - CityGenerator.RoadWidth / 2f - CityGenerator.SidewalkW,
                         half, 0.02f, rz - CityGenerator.RoadWidth / 2f - CityGenerator.SidewalkW,
                         half, 0.02f, rz - CityGenerator.RoadWidth / 2f,
                         -half, 0.02f, rz - CityGenerator.RoadWidth / 2f,
                         0.55f, 0.55f, 0.55f);
            Quad(ref v, -half, 0.02f, rz + CityGenerator.RoadWidth / 2f,
                         half, 0.02f, rz + CityGenerator.RoadWidth / 2f,
                         half, 0.02f, rz + CityGenerator.RoadWidth / 2f + CityGenerator.SidewalkW,
                         -half, 0.02f, rz + CityGenerator.RoadWidth / 2f + CityGenerator.SidewalkW,
                         0.55f, 0.55f, 0.55f);
        }

        Upload(ref _sidewalkVao, ref _sidewalkVbo, ref _sidewalkCount, ref _sidewalkGpuBytes, v);
    }

    private void BuildBuildings()
    {
        var v = new List<float>();
        var wv = new List<float>(); // ночные окна

        float inset = 0.5f; // отступ от края блока для визуального разделения

        foreach (var b in _blocks)
        {
            if (b.Type == BuildingType.Tree || b.Type == BuildingType.Lamp) continue;

            float x = b.X + inset, z = b.Z + inset;
            float w = b.Width - inset * 2, d = b.Depth - inset * 2;
            float h = b.Height * 2.5f;
            var col = b.Color;
            var acc = b.Accent;

            Box(ref v, x, 0, z, w, h, d, col.X, col.Y, col.Z);

            // Крыша
            Quad(ref v, x - 0.3f, h, z - 0.3f, x + w + 0.3f, h, z - 0.3f,
                         x + w + 0.3f, h, z + d + 0.3f, x - 0.3f, h, z + d + 0.3f,
                         acc.X * 1.2f, acc.Y * 1.2f, acc.Z * 1.2f);

            // Окна (квадратики на фасаде)
            var rng = new Random(b.X * 31 + b.Z * 17);
            int maxWindows = Math.Max(1, (int)(w / 3f));
            for (int f = 0; f < b.Height; f++)
            {
                float fy = 1f + f * 2.5f;
                for (int wi = 0; wi < maxWindows; wi++)
                {
                    bool lit = rng.NextDouble() < 0.6f;
                    float wx = x + 1f + wi * (w - 2f) / maxWindows;
                    float winW = Math.Min(1f, (w - 2f) / maxWindows - 0.3f);
                    // Передняя сторона — дневные окна
                    Quad(ref v, wx, fy, z + d + 0.01f, wx + winW, fy, z + d + 0.01f,
                                 wx + winW, fy + 1.5f, z + d + 0.01f, wx, fy + 1.5f, z + d + 0.01f,
                                 0.5f, 0.6f, 0.8f);
                    if (lit)
                        Quad(ref wv, wx, fy, z + d + 0.02f, wx + winW, fy, z + d + 0.02f,
                                     wx + winW, fy + 1.5f, z + d + 0.02f, wx, fy + 1.5f, z + d + 0.02f,
                                     0.95f, 0.75f, 0.2f);
                    // Задняя сторона — дневные окна
                    Quad(ref v, wx + winW, fy, z - 0.01f, wx, fy, z - 0.01f,
                                 wx, fy + 1.5f, z - 0.01f, wx + winW, fy + 1.5f, z - 0.01f,
                                 0.5f, 0.6f, 0.8f);
                    if (lit)
                        Quad(ref wv, wx + winW, fy, z - 0.02f, wx, fy, z - 0.02f,
                                     wx, fy + 1.5f, z - 0.02f, wx + winW, fy + 1.5f, z - 0.02f,
                                     0.95f, 0.75f, 0.2f);
                }
            }
        }

        Upload(ref _buildingVao, ref _buildingVbo, ref _buildingCount, ref _buildingGpuBytes, v);
        Upload(ref _windowVao, ref _windowVbo, ref _windowCount, ref _windowGpuBytes, wv);
    }

    private void BuildTrees()
    {
        _treePositions.Clear();
        _treeTexture = Texture.CreateTree();
        var rng = new Random(42);

        for (int dx = -CityGenerator.CityRadius; dx <= CityGenerator.CityRadius; dx++)
        {
            float rx = dx * (CityGenerator.BlockSize + CityGenerator.RoadWidth) - CityGenerator.RoadWidth / 2f - 1f;
            for (int dz = -CityGenerator.CityRadius; dz <= CityGenerator.CityRadius; dz++)
            {
                float rz = dz * (CityGenerator.BlockSize + CityGenerator.RoadWidth) - CityGenerator.RoadWidth / 2f - 1f;
                if (rng.NextDouble() < 0.4f)
                {
                    float tx = rx + (float)rng.NextDouble() * 2f - 1f;
                    float tz = rz + (float)rng.NextDouble() * 2f - 1f;
                    float scale = 0.9f + (float)rng.NextDouble() * 0.3f;
                    _treePositions.Add((new Vector3(tx, 0f, tz), scale));
                }
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
        {
            _timeOfDay = 0f;
            _progress.NewDay();
            _nightWalkAwarded = false;
            // Reset all NPCs except player to initial state
            foreach (var npc in _npcs)
            {
                if (npc == _player) continue;
                npc.Reset();
            }
        }

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
        _npcBufLen = 0;

        void Emit(float v0, float v1, float v2, float v3, float v4, float v5, float v6, float v7, float v8)
        {
            if (_npcBufLen + FloatsPerVertex > _npcBuf.Length) Array.Resize(ref _npcBuf, _npcBuf.Length * 2);
            _npcBuf[_npcBufLen++] = v0; _npcBuf[_npcBufLen++] = v1; _npcBuf[_npcBufLen++] = v2;
            _npcBuf[_npcBufLen++] = v3; _npcBuf[_npcBufLen++] = v4; _npcBuf[_npcBufLen++] = v5;
            _npcBuf[_npcBufLen++] = v6; _npcBuf[_npcBufLen++] = v7; _npcBuf[_npcBufLen++] = v8;
        }

        void EmitBox(float ax, float ay, float az, float bx, float by, float bz,
            float cx, float cy, float cz, float dx, float dy, float dz,
            float r, float g, float b, float shade, float nx, float ny, float nz)
        {
            float sr = r * shade, sg = g * shade, sb = b * shade;
            Emit(ax, ay, az, sr, sg, sb, nx, ny, nz);
            Emit(bx, by, bz, sr, sg, sb, nx, ny, nz);
            Emit(cx, cy, cz, sr, sg, sb, nx, ny, nz);
            Emit(ax, ay, az, sr, sg, sb, nx, ny, nz);
            Emit(cx, cy, cz, sr, sg, sb, nx, ny, nz);
            Emit(dx, dy, dz, sr, sg, sb, nx, ny, nz);
        }

        void AddEllipsoid(float cx, float cy, float cz, float rx, float ry, float rz, float cr, float cg, float cb, int segs, int rings)
        {
            for (int i = 0; i < rings; i++)
            {
                float theta1 = i * MathF.PI / rings;
                float theta2 = (i + 1) * MathF.PI / rings;
                for (int j = 0; j < segs; j++)
                {
                    float phi1 = j * 2f * MathF.PI / segs;
                    float phi2 = (j + 1) * 2f * MathF.PI / segs;
                    float x1 = cx + rx * MathF.Sin(theta1) * MathF.Cos(phi1);
                    float y1 = cy + ry * MathF.Cos(theta1);
                    float z1 = cz + rz * MathF.Sin(theta1) * MathF.Sin(phi1);
                    Vector3 n1 = Vector3.Normalize(new((x1 - cx) / rx, (y1 - cy) / ry, (z1 - cz) / rz));
                    float x2 = cx + rx * MathF.Sin(theta1) * MathF.Cos(phi2);
                    float y2 = cy + ry * MathF.Cos(theta1);
                    float z2 = cz + rz * MathF.Sin(theta1) * MathF.Sin(phi2);
                    Vector3 n2 = Vector3.Normalize(new((x2 - cx) / rx, (y2 - cy) / ry, (z2 - cz) / rz));
                    float x3 = cx + rx * MathF.Sin(theta2) * MathF.Cos(phi2);
                    float y3 = cy + ry * MathF.Cos(theta2);
                    float z3 = cz + rz * MathF.Sin(theta2) * MathF.Sin(phi2);
                    Vector3 n3 = Vector3.Normalize(new((x3 - cx) / rx, (y3 - cy) / ry, (z3 - cz) / rz));
                    float x4 = cx + rx * MathF.Sin(theta2) * MathF.Cos(phi1);
                    float y4 = cy + ry * MathF.Cos(theta2);
                    float z4 = cz + rz * MathF.Sin(theta2) * MathF.Sin(phi1);
                    Vector3 n4 = Vector3.Normalize(new((x4 - cx) / rx, (y4 - cy) / ry, (z4 - cz) / rz));
                    Emit(x1, y1, z1, cr, cg, cb, n1.X, n1.Y, n1.Z);
                    Emit(x2, y2, z2, cr, cg, cb, n2.X, n2.Y, n2.Z);
                    Emit(x3, y3, z3, cr, cg, cb, n3.X, n3.Y, n3.Z);
                    Emit(x1, y1, z1, cr, cg, cb, n1.X, n1.Y, n1.Z);
                    Emit(x3, y3, z3, cr, cg, cb, n3.X, n3.Y, n3.Z);
                    Emit(x4, y4, z4, cr, cg, cb, n4.X, n4.Y, n4.Z);
                }
            }
        }

        void AddSphere(float cx, float cy, float cz, float r, float cr, float cg, float cb, int segs, int rings)
        {
            for (int i = 0; i < rings; i++)
            {
                float theta1 = i * MathF.PI / rings;
                float theta2 = (i + 1) * MathF.PI / rings;
                for (int j = 0; j < segs; j++)
                {
                    float phi1 = j * 2f * MathF.PI / segs;
                    float phi2 = (j + 1) * 2f * MathF.PI / segs;
                    float x1 = cx + r * MathF.Sin(theta1) * MathF.Cos(phi1);
                    float y1 = cy + r * MathF.Cos(theta1);
                    float z1 = cz + r * MathF.Sin(theta1) * MathF.Sin(phi1);
                    Vector3 n1 = Vector3.Normalize(new(x1 - cx, y1 - cy, z1 - cz));
                    float x2 = cx + r * MathF.Sin(theta1) * MathF.Cos(phi2);
                    float y2 = cy + r * MathF.Cos(theta1);
                    float z2 = cz + r * MathF.Sin(theta1) * MathF.Sin(phi2);
                    Vector3 n2 = Vector3.Normalize(new(x2 - cx, y2 - cy, z2 - cz));
                    float x3 = cx + r * MathF.Sin(theta2) * MathF.Cos(phi2);
                    float y3 = cy + r * MathF.Cos(theta2);
                    float z3 = cz + r * MathF.Sin(theta2) * MathF.Sin(phi2);
                    Vector3 n3 = Vector3.Normalize(new(x3 - cx, y3 - cy, z3 - cz));
                    float x4 = cx + r * MathF.Sin(theta2) * MathF.Cos(phi1);
                    float y4 = cy + r * MathF.Cos(theta2);
                    float z4 = cz + r * MathF.Sin(theta2) * MathF.Sin(phi1);
                    Vector3 n4 = Vector3.Normalize(new(x4 - cx, y4 - cy, z4 - cz));
                    Emit(x1, y1, z1, cr, cg, cb, n1.X, n1.Y, n1.Z);
                    Emit(x2, y2, z2, cr, cg, cb, n2.X, n2.Y, n2.Z);
                    Emit(x3, y3, z3, cr, cg, cb, n3.X, n3.Y, n3.Z);
                    Emit(x1, y1, z1, cr, cg, cb, n1.X, n1.Y, n1.Z);
                    Emit(x3, y3, z3, cr, cg, cb, n3.X, n3.Y, n3.Z);
                    Emit(x4, y4, z4, cr, cg, cb, n4.X, n4.Y, n4.Z);
                }
            }
        }

        void AddTube(float cx, float cy, float cz, float rTop, float rBot, float h,
            float cr, float cg, float cb, int segs,
            float topOffX = 0f, float topOffZ = 0f,
            float botOffX = 0f, float botOffZ = 0f)
        {
            if (h < 0.0001f) return;
            float dr = rBot - rTop;
            float ny = dr / h * 0.3f;
            for (int i = 0; i < segs; i++)
            {
                float a1 = i * 2f * MathF.PI / segs;
                float a2 = (i + 1) * 2f * MathF.PI / segs;
                float nx1 = MathF.Cos(a1), nz1 = MathF.Sin(a1);
                float nx2 = MathF.Cos(a2), nz2 = MathF.Sin(a2);
                float ncx = (nx1 + nx2) * 0.5f;
                float ncz = (nz1 + nz2) * 0.5f;
                float nl = MathF.Sqrt(ncx * ncx + ncz * ncz + ny * ny);
                float x1 = cx + rBot * nx1 + botOffX, z1 = cz + rBot * nz1 + botOffZ;
                float x2 = cx + rBot * nx2 + botOffX, z2 = cz + rBot * nz2 + botOffZ;
                float x3 = cx + rTop * nx2 + topOffX, z3 = cz + rTop * nz2 + topOffZ;
                float x4 = cx + rTop * nx1 + topOffX, z4 = cz + rTop * nz1 + topOffZ;
                Emit(x1, cy, z1, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
                Emit(x2, cy, z2, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
                Emit(x3, cy + h, z3, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
                Emit(x1, cy, z1, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
                Emit(x3, cy + h, z3, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
                Emit(x4, cy + h, z4, cr, cg, cb, ncx / nl, ny / nl, ncz / nl);
            }
        }

        void AddHeroOutfitDetails(NpcCharacter npc, Vector3 center, Vector3 fwd, Vector3 rgt,
            float h, float chestR, float waistR, float shoulderY, float hipY, float torsoH, float neckR, float bob,
            float shoulderR, float hipR)
        {
            if (npc != _player) return;

            float px = center.X, py = center.Y, pz = center.Z;
            float hipToWaist = torsoH * 0.30f;

            // Collar at neck base
            float collarY = shoulderY + bob;
            float collarH = h * 0.02f;
            AddTube(px, collarY, pz, neckR * 0.9f, neckR * 1.3f, collarH,
                HeroStyle.ShirtLight.X * 0.8f, HeroStyle.ShirtLight.Y * 0.8f, HeroStyle.ShirtLight.Z * 0.8f, 8);

            // Belt at waist
            float beltY = hipY + hipToWaist + bob;
            float beltH = h * 0.015f;
            AddTube(px, beltY, pz, waistR * 1.05f, waistR * 1.05f, beltH,
                HeroStyle.Belt.X, HeroStyle.Belt.Y, HeroStyle.Belt.Z, 12);

            // Belt buckle — local-space forward
            float buckleW = h * 0.018f;
            Vector3 bc = center + fwd * (waistR * 1.02f);
            bc.Y = beltY;
            EmitBox(bc.X - rgt.X * buckleW, bc.Y, bc.Z - rgt.Z * buckleW,
                    bc.X + rgt.X * buckleW, bc.Y, bc.Z + rgt.Z * buckleW,
                    bc.X + rgt.X * buckleW, bc.Y + beltH, bc.Z + rgt.Z * buckleW,
                    bc.X - rgt.X * buckleW, bc.Y + beltH, bc.Z - rgt.Z * buckleW,
                    HeroStyle.Accent.X, HeroStyle.Accent.Y, HeroStyle.Accent.Z, 1.0f, 0, 0, 1);

            // Shirt buttons — local-space forward
            float btnR = h * 0.006f;
            int btnCount = 3;
            float btnStartY = shoulderY - torsoH * 0.1f + bob;
            float btnEndY = hipY + hipToWaist + beltH + bob;
            for (int i = 0; i < btnCount; i++)
            {
                float t = (i + 1f) / (btnCount + 1);
                float btnY = btnStartY + (btnEndY - btnStartY) * t;
                Vector3 btnPos = center + fwd * (chestR * 1.02f);
                btnPos.Y = btnY;
                AddSphere(btnPos.X, btnPos.Y, btnPos.Z, btnR,
                    HeroStyle.ShirtLight.X * 0.7f, HeroStyle.ShirtLight.Y * 0.7f, HeroStyle.ShirtLight.Z * 0.7f, 6, 3);
            }

            // Cyan badge on left chest — local-space
            float badgeS = h * 0.012f;
            Vector3 badgeCenter = center + fwd * (chestR * 1.04f) - rgt * (chestR * 0.28f);
            badgeCenter.Y = shoulderY - torsoH * 0.25f + bob;
            EmitBox(badgeCenter.X - rgt.X * badgeS, badgeCenter.Y, badgeCenter.Z - rgt.Z * badgeS,
                    badgeCenter.X + rgt.X * badgeS, badgeCenter.Y, badgeCenter.Z + rgt.Z * badgeS,
                    badgeCenter.X + rgt.X * badgeS, badgeCenter.Y + badgeS * 1.5f, badgeCenter.Z + rgt.Z * badgeS,
                    badgeCenter.X - rgt.X * badgeS, badgeCenter.Y + badgeS * 1.5f, badgeCenter.Z - rgt.Z * badgeS,
                    HeroStyle.Accent.X, HeroStyle.Accent.Y, HeroStyle.Accent.Z, 1.0f, 0, 0, 1);

            // Diagonal strap (left shoulder → right hip) — local-space
            float strapW = h * 0.006f;
            float strapThick = h * 0.0045f;
            float strapStartY = shoulderY + bob;
            float strapEndY = hipY + hipToWaist + bob;
            Vector3 strapStart = center - rgt * (shoulderR * 0.6f) + fwd * (chestR * 0.25f);
            Vector3 strapEnd = center + rgt * (hipR * 0.45f) + fwd * (chestR * 0.1f);
            strapStart.Y = strapStartY;
            strapEnd.Y = strapEndY;
            Vector3 strapDir = strapEnd - strapStart;
            float strapLen = strapDir.Length;
            if (strapLen > 0.01f)
            {
                strapDir /= strapLen;
                Vector3 perp = Vector3.Cross(strapDir, fwd);
                perp.Normalize();
                Vector3 perp2 = Vector3.Cross(strapDir, perp);
                perp2.Normalize();

                void Svert(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    Emit(a.X, a.Y, a.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                    Emit(b.X, b.Y, b.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                    Emit(c.X, c.Y, c.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                    Emit(a.X, a.Y, a.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                    Emit(c.X, c.Y, c.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                    Emit(d.X, d.Y, d.Z, HeroStyle.Strap.X, HeroStyle.Strap.Y, HeroStyle.Strap.Z, perp.X, perp.Y, perp.Z);
                }

                Vector3 hw = perp * (strapW * 0.5f);
                Vector3 ht = perp2 * (strapThick * 0.5f);
                for (int seg = 0; seg < 4; seg++)
                {
                    float t0 = seg / 4f;
                    float t1 = (seg + 1f) / 4f;
                    Vector3 p0 = strapStart + strapDir * (strapLen * t0);
                    Vector3 p1 = strapStart + strapDir * (strapLen * t1);
                    Svert(p0 - hw - ht, p0 + hw - ht, p1 + hw - ht, p1 - hw - ht);
                    Svert(p0 - hw + ht, p0 + hw + ht, p1 + hw + ht, p1 - hw + ht);
                    Svert(p0 - hw - ht, p0 - hw + ht, p1 - hw + ht, p1 - hw - ht);
                    Svert(p0 + hw - ht, p0 + hw + ht, p1 + hw + ht, p1 + hw - ht);
                }
            }
        }

        foreach (var npc in _npcs)
        {
            float px = npc.Position.X, py = npc.Position.Y, pz = npc.Position.Z;
            float h = npc.Height;
            var c = npc.Color;
            var hc = npc.HeadColor;
            var pants = npc.PantsColor;

            // --- Animation system ---
            float phase = npc.AnimPhase;
            float blend = npc.AnimBlend;
            float idleFactor = 1f - blend;
            bool isHero = HeroStyle.IsHero(npc);

            // Sprint detection (hero-only): phase speed relative to walk baseline
            float sprintFactor = 0f;
            if (isHero && blend > 0.9f)
            {
                float phaseRate = 3.5f * MathF.Abs(npc.Velocity.LengthFast);
                float walkBaseline = 3.5f * 8f;
                sprintFactor = MathF.Max(0f, phaseRate / walkBaseline - 1f);
                sprintFactor = MathF.Min(sprintFactor, 1.5f);
            }

            // Hero idle: layered breathing with multiple frequencies
            float breatheHero = 0f;
            if (isHero && idleFactor > 0.01f)
            {
                float t = _animationTime;
                float heroPhase = npc.Id * 1.7f;
                float breatheA = MathF.Sin(t * 1.8f + heroPhase) * 0.005f;
                float breatheB = MathF.Sin(t * 2.6f + heroPhase * 0.7f) * 0.003f;
                float breatheC = MathF.Sin(t * 1.1f + heroPhase * 1.3f) * 0.002f;
                breatheHero = (breatheA + breatheB + breatheC) * idleFactor;
            }
            float breathe = isHero ? breatheHero : MathF.Sin(_animationTime * 2f + npc.Id * 1.7f) * 0.004f;

            // Walk cycle
            float legPhase = MathF.Sin(phase);
            float cosPhase = MathF.Cos(phase);
            float bodyBobRaw = MathF.Abs(legPhase);
            float bodyBobWalk = (bodyBobRaw * bodyBobRaw * (3f - 2f * bodyBobRaw)) * 0.03f;
            float bodyBob = bodyBobWalk * blend + breathe;

            // Walk amplitude with sprint boost
            float walkAmp = 0.14f * blend * (1f + sprintFactor * 0.3f);
            float swingAmt = legPhase * walkAmp;

            // Slight lateral lean during walk
            float torsoLean = cosPhase * 0.012f * blend;

            // Direction-aware offsets
            float fwdX = MathF.Sin(npc.Rotation);
            float fwdZ = MathF.Cos(npc.Rotation);
            float rightX = MathF.Cos(npc.Rotation);
            float rightZ = -MathF.Sin(npc.Rotation);

            float legOffX = fwdX * swingAmt;
            float legOffZ = fwdZ * swingAmt;

            // Arm swing: wider arc, slight lateral component
            float armSwingFwd = MathF.Sin(phase + MathF.PI) * 0.12f * blend * (1f + sprintFactor * 0.4f);
            float armSwingSide = cosPhase * 0.015f * blend;
            float armOffX = fwdX * armSwingFwd + rightX * armSwingSide;
            float armOffZ = fwdZ * armSwingFwd + rightZ * armSwingSide;

            // Hero sprint: slight forward lean
            float sprintLean = sprintFactor * 0.008f;

            // === PROPORTIONS (ground-up, exact) ===
            float ankleY = h * 0.018f;
            float shinH = h * 0.176f;
            float kneeY = ankleY + shinH;
            float thighH = h * 0.294f;
            float hipY = kneeY + thighH;
            float torsoH = h * 0.229f;
            float shoulderY = hipY + torsoH;
            float neckH = h * 0.035f;
            float headY = shoulderY + neckH + h * 0.095f;

            float shoulderR = h * 0.205f;
            float waistR = h * 0.14f;
            float hipR = h * 0.18f;
            float headR = h * 0.11f;
            float neckR = h * 0.045f;
            float armRTop = h * 0.036f;
            float armRBot = h * 0.024f;
            float armH = h * 0.20f;
            float foreH = h * 0.20f;
            float handR = h * 0.032f;
            float legRTop = h * 0.06f;
            float legRBot = h * 0.038f;
            float footW = h * 0.07f;
            float footH = h * 0.03f;
            float footD = h * 0.11f;

            int segs = 10;

            // Apply body bob to all Y
            float bob = bodyBob;

            // === LEGS === two-segment (thigh + shin) with knee joint
            float leftPhase = MathF.Sin(phase);
            float rightPhase = MathF.Sin(phase + MathF.PI);

            // Hip sway (subtle rotation)
            float hipSway = leftPhase * 0.03f * blend;
            float hipOffX = fwdX * hipSway;
            float hipOffZ = fwdZ * hipSway;

            // Per-leg helpers
            void BuildLeg(float side, float legPhaseVal)
            {
                float cx = px + side * hipR * 0.6f;

                // Foot swing forward/back
                float footSw = legPhaseVal * 0.12f * blend;
                float footOffX = fwdX * footSw;
                float footOffZ = fwdZ * footSw;

                // Foot lift (rises during swing)
                float footLift = MathF.Max(0f, -legPhaseVal) * 0.10f * blend;

                // Knee bend (moves back when foot lifts)
                float kneeBend = MathF.Max(0f, -legPhaseVal) * 0.07f * blend;
                float kneeOffX = -fwdX * kneeBend;
                float kneeOffZ = -fwdZ * kneeBend;

                // Opposite hip sway
                float oppHipSway = legPhaseVal * 0.03f * blend;
                float oppHipOffX = fwdX * oppHipSway;
                float oppHipOffZ = fwdZ * oppHipSway;

                // Shin (ankle → knee)
                float shH = Math.Max(0.01f, shinH - footLift);
                float kneeR = legRBot * 1.35f;
                AddTube(cx, ankleY + bob + footLift, pz, kneeR, legRBot, shH,
                    pants.X, pants.Y, pants.Z, segs,
                    kneeOffX, kneeOffZ,   // top (knee) offset
                    footOffX, footOffZ);  // bottom (foot) offset

                // Thigh (knee → hip)
                AddTube(cx, kneeY + bob, pz, legRTop, kneeR, thighH,
                    pants.X * 0.92f, pants.Y * 0.92f, pants.Z * 0.92f, segs,
                    oppHipOffX, oppHipOffZ,  // top (hip) offset
                    kneeOffX, kneeOffZ);     // bottom (knee) offset

                // Feet follow foot offset + lift
                float fy = ankleY + bob + footLift;
                EmitBox(cx - footW * 0.5f + footOffX, fy, pz - footD * 0.5f + footOffZ,
                        cx + footW * 0.5f + footOffX, fy, pz - footD * 0.5f + footOffZ,
                        cx + footW * 0.5f + footOffX, fy + footH, pz - footD * 0.5f + footOffZ,
                        cx - footW * 0.5f + footOffX, fy + footH, pz - footD * 0.5f + footOffZ,
                        pants.X * 0.6f, pants.Y * 0.6f, pants.Z * 0.6f, 1.0f, 0, 0, 1);
                AddEllipsoid(cx + footOffX, fy + footH * 0.5f, pz + footD * 0.08f + footOffZ,
                    footW * 0.62f, footH * 0.78f, footD * 0.64f,
                    pants.X * 0.52f, pants.Y * 0.52f, pants.Z * 0.52f, 10, 4);
            }

            BuildLeg(-1f, leftPhase);
            BuildLeg(1f, rightPhase);

            // === TORSO === 4 segments for natural S-curve (lordosis)
            float hipToWaist = torsoH * 0.30f;
            float waistToChest = torsoH * 0.35f;
            float chestToShoulders = torsoH * 0.35f;

            // Natural spine curve: chest forward (+Z), waist neutral, hips back (-Z)
            float spineCurve = h * 0.03f;
            float chestZOff = pz + spineCurve * 0.8f;
            float hipZOff = pz - spineCurve * 0.3f;
            float waistZOff = pz;

            AddTube(px, hipY + bob, hipZOff, waistR * 1.1f, hipR, hipToWaist,
                pants.X * 0.85f, pants.Y * 0.85f, pants.Z * 0.85f, segs,
                0, 0, 0, 0);

            float waistMidY = hipY + hipToWaist + bob;
            AddTube(px, waistMidY, waistZOff, waistR, waistR * 1.1f, waistToChest,
                c.X * 0.85f, c.Y * 0.8f, c.Z * 0.8f, segs,
                0, 0, 0, 0);

            float chestMidY = waistMidY + waistToChest;
            float chestW = shoulderR * 0.85f;
            float waistW = waistR;
            AddTube(px, chestMidY, chestZOff, chestW, waistW, chestToShoulders,
                c.X * 0.95f, c.Y * 0.88f, c.Z * 0.85f, segs,
                0, 0, 0, 0);

            // === SHOULDERS === (deltoid)
            float shoulderS = shoulderR * 0.42f;
            float shoulderZ = shoulderY + bob;
            float shrCol = c.X * 0.9f, shgCol = c.Y * 0.82f, shbCol = c.Z * 0.78f;
            float upperLeanX = rightX * torsoLean - fwdX * sprintLean;
            float upperLeanZ = rightZ * torsoLean - fwdZ * sprintLean;
            AddEllipsoid(px - shoulderR + upperLeanX, shoulderZ, pz + spineCurve * 0.4f + upperLeanZ, shoulderS, shoulderS * 0.7f, shoulderS * 0.6f,
                shrCol, shgCol, shbCol, 8, 4);
            AddEllipsoid(px + shoulderR + upperLeanX, shoulderZ, pz + spineCurve * 0.4f + upperLeanZ, shoulderS, shoulderS * 0.7f, shoulderS * 0.6f,
                shrCol, shgCol, shbCol, 8, 4);

            // === NECK === (trapezius taper: wider at base)
            AddTube(px + upperLeanX * 0.6f, shoulderY + bob, pz + spineCurve * 0.5f + upperLeanZ * 0.6f, neckR, neckR * 1.5f, neckH,
                hc.X * 0.85f, hc.Y * 0.8f, hc.Z * 0.78f, 8);

            // === HEAD === ellipsoid (slightly tilted forward)
            float headTilt = h * 0.01f;
            float headLeanX = upperLeanX * 0.8f;
            float headLeanZ = upperLeanZ * 0.8f;
            float headCX = px + headLeanX, headCY = headY + bob, headCZ = pz + spineCurve * 0.6f - headTilt + headLeanZ;
            float headRX = headR * 0.82f;
            float headRY = headR * 1.15f;
            float headRZ = headR * 0.95f;
            AddEllipsoid(headCX, headCY, headCZ, headRX, headRY, headRZ, hc.X, hc.Y, hc.Z, 12, 8);

            // === EYES ===
            float eyeY = headCY + headRY * 0.2f;
            float eyeZ = headCZ + headRZ * 0.75f;
            float eyeR = HeroStyle.IsHero(npc) ? headR * 0.22f : headR * 0.18f;
            float eyeOff = headRX * 0.4f;
            Vector3 eyeCol = HeroStyle.IsHero(npc) ? HeroStyle.Eye : new Vector3(0.03f, 0.03f, 0.07f);
            AddSphere(headCX - eyeOff, eyeY, eyeZ, eyeR, eyeCol.X, eyeCol.Y, eyeCol.Z, 8, 4);
            AddSphere(headCX + eyeOff, eyeY, eyeZ, eyeR, eyeCol.X, eyeCol.Y, eyeCol.Z, 8, 4);

            // === EYEBROWS ===
            float browY = eyeY + headRY * 0.22f;
            float browW = headRX * 0.18f;
            float browH = headRY * 0.03f;
            float browD = headRZ * 0.04f;
            float browCol = hc.X * 0.15f;
            EmitBox(headCX - eyeOff - browW * 0.5f, browY, eyeZ - browD,
                    headCX - eyeOff + browW * 0.5f, browY, eyeZ - browD,
                    headCX - eyeOff + browW * 0.5f, browY + browH, eyeZ - browD,
                    headCX - eyeOff - browW * 0.5f, browY + browH, eyeZ - browD,
                    browCol, browCol * 0.7f, browCol * 0.3f, 1.0f, 0, 0, 1);
            EmitBox(headCX + eyeOff - browW * 0.5f, browY, eyeZ - browD,
                    headCX + eyeOff + browW * 0.5f, browY, eyeZ - browD,
                    headCX + eyeOff + browW * 0.5f, browY + browH, eyeZ - browD,
                    headCX + eyeOff - browW * 0.5f, browY + browH, eyeZ - browD,
                    browCol, browCol * 0.7f, browCol * 0.3f, 1.0f, 0, 0, 1);

            // === MOUTH ===
            float mouthY = headCY - headRY * 0.07f;
            float mouthW = headRX * 0.2f;
            float mouthH = headRY * 0.025f;
            float mouthCol = hc.X * 0.5f;
            EmitBox(headCX - mouthW, mouthY, eyeZ - headRZ * 0.05f,
                    headCX + mouthW, mouthY, eyeZ - headRZ * 0.05f,
                    headCX + mouthW, mouthY + mouthH, eyeZ - headRZ * 0.05f,
                    headCX - mouthW, mouthY + mouthH, eyeZ - headRZ * 0.05f,
                    mouthCol, mouthCol * 0.4f, mouthCol * 0.4f, 1.0f, 0, 0, 1);

            // === HAIR ===
            float hairY = headCY + headRY * 0.5f;
            var hairCol = HeroStyle.IsHero(npc) ? HeroStyle.HairLight : npc.HairColor;
            AddEllipsoid(headCX, hairY, headCZ, headRX * 1.05f, headRY * 0.25f, headRZ * 0.9f,
                hairCol.X, hairCol.Y, hairCol.Z, 12, 4);

            // === ARMS === two-segment (upper arm + forearm) with elbow joint
            float armSkin = 0.92f;
            float armCr = hc.X * armSkin, armCg = hc.Y * armSkin, armCb = hc.Z * armSkin;

            void BuildArm(float side, float swingPhase)
            {
                float shoulderX = px + side * (shoulderR + armRTop * 0.15f);
                float shoulderYPos = shoulderY + bob - shoulderS * 0.12f;

                // Smoothed swing with sprint amplification
                float swing = swingPhase * walkAmp * 0.85f;
                float swingX = fwdX * swing + rightX * torsoLean * side;
                float swingZ = fwdZ * swing + rightZ * torsoLean * side;

                // Elbow bend: peaks when arm swings back, relaxes forward
                float swingT = (swingPhase + 1f) * 0.5f;
                float elbowBend = swingT * 0.14f * blend * (1f + sprintFactor * 0.3f);
                float elbowOffX = -fwdX * elbowBend * side;
                float elbowOffZ = -fwdZ * elbowBend;

                // Upper arm (shoulder → elbow)
                float elbowY = shoulderYPos - armH;
                float uaTopR = armRTop, uaBotR = armRTop * 0.85f;
                AddTube(shoulderX, elbowY, pz, uaBotR, uaTopR, armH,
                    armCr * 0.95f, armCg * 0.95f, armCb * 0.95f, 8,
                    elbowOffX, elbowOffZ,
                    swingX, swingZ);

                // Forearm (elbow → wrist) with follow-through
                float wristYPos = elbowY - foreH;
                float faTopR = armRTop * 0.85f, faBotR = armRBot;
                float followPhase = swingPhase * 0.7f;
                float fwdElbow = followPhase * 0.08f * blend * (1f + sprintFactor * 0.25f);
                float fwdElbowX = fwdX * fwdElbow;
                float fwdElbowZ = fwdZ * fwdElbow;
                AddTube(shoulderX, wristYPos, pz, faBotR, faTopR, foreH,
                    armCr, armCg, armCb, 8,
                    fwdElbowX * 0.4f, fwdElbowZ * 0.4f,
                    elbowOffX, elbowOffZ);

                // Hand at wrist
                float handX = shoulderX + fwdElbowX * 0.5f;
                float handZ = pz + fwdElbowZ * 0.5f;
                AddEllipsoid(handX, wristYPos - handR * 0.3f, handZ,
                    handR * 0.8f, handR * 1.1f, handR * 0.7f,
                    armCr, armCg, armCb, 6, 3);
            }

            BuildArm(-1f, MathF.Sin(phase + MathF.PI));
            BuildArm(1f, MathF.Sin(phase));

            AddHeroOutfitDetails(npc, new Vector3(px, py, pz),
                new Vector3(fwdX, 0, fwdZ), new Vector3(rightX, 0, rightZ),
                h, chestW, waistR, shoulderY, hipY, torsoH, neckR, bob, shoulderR, hipR);

            if (npc == _player && npc.State == NpcState.Aware)
            {
                float ringY = py + h * 0.018f;
                float r = h * 0.34f;
                float t = h * 0.018f;
                Vector3 aura = new(0.18f, 0.58f, 1.0f);
                EmitBox(px - r, ringY, pz - r, px + r, ringY, pz - r, px + r, ringY, pz - r + t, px - r, ringY, pz - r + t, aura.X, aura.Y, aura.Z, 0.95f, 0, 1, 0);
                EmitBox(px - r, ringY, pz + r - t, px + r, ringY, pz + r - t, px + r, ringY, pz + r, px - r, ringY, pz + r, aura.X, aura.Y, aura.Z, 0.85f, 0, 1, 0);
                EmitBox(px - r, ringY, pz - r, px - r + t, ringY, pz - r, px - r + t, ringY, pz + r, px - r, ringY, pz + r, aura.X, aura.Y, aura.Z, 0.75f, 0, 1, 0);
                EmitBox(px + r - t, ringY, pz - r, px + r, ringY, pz - r, px + r, ringY, pz + r, px + r - t, ringY, pz + r, aura.X, aura.Y, aura.Z, 0.75f, 0, 1, 0);
                AddSphere(px, headY + headRY * 1.45f + bob, pz, h * 0.028f, aura.X, aura.Y, aura.Z, 8, 4);
            }
        }

        _npcCount = _npcBufLen / FloatsPerVertex;
        UploadDynamic(_npcBuf, _npcBufLen);
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
        GL.UseProgram(context.Shader);
        var id = Matrix4.Identity;
        GL.UniformMatrix4(context.ViewLocation, false, ref view);
        GL.UniformMatrix4(context.ProjectionLocation, false, ref proj);
        GL.UniformMatrix4(context.ModelLocation, false, ref id);
        GL.Uniform3(context.ColorLocation, -1f, -1f, -1f);
        GL.Uniform3(context.FogColorLocation, fogCol.X, fogCol.Y, fogCol.Z);

        // Ensure depth state for opaque 3D world
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);

        if (_inside)
        {
            GL.Enable(EnableCap.CullFace);
            if (_interiorCount > 0)
            {
                GL.BindVertexArray(_interiorVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, _interiorCount);
            }
            return;
        }

        GL.BindVertexArray(_roadVao); GL.DrawArrays(PrimitiveType.Triangles, 0, _roadCount);
        GL.BindVertexArray(_sidewalkVao); GL.DrawArrays(PrimitiveType.Triangles, 0, _sidewalkCount);
        GL.BindVertexArray(_buildingVao); GL.DrawArrays(PrimitiveType.Triangles, 0, _buildingCount);
        if (_windowCount > 0) { GL.BindVertexArray(_windowVao); GL.DrawArrays(PrimitiveType.Triangles, 0, _windowCount); }
        bool cullFaceEnabled = GL.IsEnabled(EnableCap.CullFace);
        GL.Disable(EnableCap.CullFace);
        if (_spriteRenderer == null) _spriteRenderer = new SpriteRenderer();
        if (_treeTexture != null)
        {
            _treeTexture.Bind(0);
            _spriteRenderer.Begin();
            float h = 2.6f, w = 1.4f;
            foreach (var (pos, scale) in _treePositions)
            {
                float sh = h * scale, sw = w * scale;
                Vector3 c = pos with { Y = pos.Y + sh * 0.5f };
                _spriteRenderer.Add(c, sw, sh, Vector3.One, 1f);
            }
            _spriteRenderer.Flush(ref view, ref proj);
        }
        if (cullFaceEnabled) GL.Enable(EnableCap.CullFace);
        GL.UseProgram(context.Shader);
        GL.UniformMatrix4(context.ViewLocation, false, ref view);
        GL.UniformMatrix4(context.ProjectionLocation, false, ref proj);
        GL.UniformMatrix4(context.ModelLocation, false, ref id);
        GL.Uniform3(context.ColorLocation, -1f, -1f, -1f);
        GL.BindVertexArray(_npcVao); GL.DrawArrays(PrimitiveType.Triangles, 0, _npcCount);
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
        float r, float g, float b)
    {
        // Нормаль из первых трёх точек (левая система координат)
        Vector3 a = new(x2 - x1, y2 - y1, z2 - z1);
        Vector3 bV = new(x3 - x1, y3 - y1, z3 - z1);
        Vector3 n = Vector3.Cross(a, bV);
        n.Normalize();
        float nx = n.X, ny = n.Y, nz = n.Z;
        AddVertex(v, x1, y1, z1, r, g, b, nx, ny, nz);
        AddVertex(v, x2, y2, z2, r, g, b, nx, ny, nz);
        AddVertex(v, x3, y3, z3, r, g, b, nx, ny, nz);
        AddVertex(v, x1, y1, z1, r, g, b, nx, ny, nz);
        AddVertex(v, x3, y3, z3, r, g, b, nx, ny, nz);
        AddVertex(v, x4, y4, z4, r, g, b, nx, ny, nz);
    }

    private static void Box(ref List<float> v, float x, float y, float z, float w, float h, float d,
        float r, float g, float b)
    {
        float x2 = x + w, y2 = y + h, z2 = z + d;
        Quad(ref v, x, y, z2, x2, y, z2, x2, y2, z2, x, y2, z2, r * 0.7f, g * 0.7f, b * 0.7f);
        Quad(ref v, x2, y, z, x, y, z, x, y2, z, x2, y2, z, r * 0.5f, g * 0.5f, b * 0.5f);
        Quad(ref v, x2, y, z, x2, y, z2, x2, y2, z2, x2, y2, z, r * 0.8f, g * 0.8f, b * 0.8f);
        Quad(ref v, x, y, z, x, y, z2, x, y2, z2, x, y2, z, r * 0.6f, g * 0.6f, b * 0.6f);
        Quad(ref v, x, y2, z, x2, y2, z, x2, y2, z2, x, y2, z2, r * 1.1f, g * 1.1f, b * 1.1f);
        Quad(ref v, x, y, z, x, y, z2, x2, y, z2, x2, y, z, r * 0.9f, g * 0.9f, b * 0.9f);
    }

    private static unsafe void Upload(ref int vao, ref int vbo, ref int count, ref int gpuBytes, List<float> verts)
    {
        if (verts.Count == 0) { count = 0; gpuBytes = 0; return; }
        count = verts.Count / FloatsPerVertex;
        if (vao == 0) vao = GL.GenVertexArray();
        if (vbo == 0) vbo = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        Span<float> span = CollectionsMarshal.AsSpan(verts);
        gpuBytes = verts.Count * sizeof(float);
        fixed (float* p = span)
            GL.BufferData(BufferTarget.ArrayBuffer, gpuBytes, (nint)p, BufferUsageHint.StaticDraw);
        ConfigureVertexAttributes();
    }

    private static void AddVertex(List<float> v, float x, float y, float z, float r, float g, float b, float nx, float ny, float nz)
    {
        v.Add(x);
        v.Add(y);
        v.Add(z);
        v.Add(r);
        v.Add(g);
        v.Add(b);
        v.Add(nx);
        v.Add(ny);
        v.Add(nz);
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
        DeleteMesh(ref _roadVao, ref _roadVbo, ref _roadCount, ref _roadGpuBytes);
        DeleteMesh(ref _sidewalkVao, ref _sidewalkVbo, ref _sidewalkCount, ref _sidewalkGpuBytes);
        DeleteMesh(ref _buildingVao, ref _buildingVbo, ref _buildingCount, ref _buildingGpuBytes);
        DeleteMesh(ref _windowVao, ref _windowVbo, ref _windowCount, ref _windowGpuBytes);
        _spriteRenderer?.Dispose();
        _treeTexture?.Dispose();
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
