using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum NpcState
{
    Walking,
    Working,
    Relaxing,
    Shopping,
    Sleeping,
    Panic,
    Aware,
}

public readonly record struct DialogueChoice(
    string Text,
    float FriendlinessDelta,
    float TrustDelta,
    float MemoryGain,
    float CuriosityGain,
    float EmpathyGain,
    float AgencyGain,
    float CourageGain,
    string ActionId = "");

public class NpcCharacter
{
    private static int _nextId;
    private static readonly HeroProgress EmptyProgress = new();
    private readonly Random _rng;
    private Vector3 _color;
    private Vector3 _headColor;
    private Vector3 _pantsColor;
    private Vector3 _hairColor;
    private float _height = 1.7f;
    private bool _isAwakened;

    public int Id;
    public string Name;
    public NarrativeRole NarrativeRole { get; private set; }
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Color
    {
        get => _color;
        set => _color = HeroStyle.IsHero(this) ? HeroStyle.ShirtBlue : value;
    }
    public Vector3 HeadColor
    {
        get => _headColor;
        set => _headColor = HeroStyle.IsHero(this) ? HeroStyle.Skin : value;
    }
    public Vector3 PantsColor
    {
        get => _pantsColor;
        set => _pantsColor = HeroStyle.IsHero(this) ? HeroStyle.Pants : value;
    }
    public Vector3 HairColor
    {
        get => _hairColor;
        set => _hairColor = HeroStyle.IsHero(this) ? HeroStyle.Hair : value;
    }
    public float Rotation;
    public float TargetRotation;
    public NpcState State = NpcState.Walking;
    public float Awareness;
    public bool IsAwakened => _isAwakened || State == NpcState.Aware;
    public float Height
    {
        get => _height;
        set => _height = HeroStyle.IsHero(this) ? HeroStyle.Height : value;
    }

    // Анимация
    public float AnimPhase;
    public float AnimBlend;

    // Расписание
    public Vector3 HomePos;
    public Vector3 WorkPos;
    public float WakeHour = 7f;
    public float WorkStart = 9f;
    public float WorkEnd = 18f;
    public float SleepHour = 22f;

    // Отношения
    public float Friendliness = 20f;
    public float Trust = 10f;
    public int TimesTalked;
    public float LastTalkDay = -1f;

    // Маршрут
    private Vector3 _target;
    private float _idleTimer;
    private float _walkSpeed = 2.5f;
    private float _currentSpeed;
    private readonly List<Vector3> _route = new(8);
    private int _routeIndex;
    private Vector3 _routeDestination;
    private bool _routeValid;
    private const float Accel = 8f;
    private const float Decel = 12f;
    private const float RotSpeed = 6f;
    private const float RouteWaypointReach = 1.2f;
    private const float RouteDestinationTolerance = 0.5f;

    public NpcCharacter(Vector3 home, Vector3 work, int seed)
    {
        _rng = new Random(seed);
        Id = _nextId++;
        Name = $"Горожанин #{Id + 1}";
        HomePos = home;
        WorkPos = work;
        Position = home;
        Rotation = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        TargetRotation = Rotation;
        AnimPhase = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        Color = new Vector3(
            0.2f + (float)_rng.NextDouble() * 0.6f,
            0.2f + (float)_rng.NextDouble() * 0.6f,
            0.2f + (float)_rng.NextDouble() * 0.6f
        );
        HeadColor = new Vector3(
            0.7f + (float)_rng.NextDouble() * 0.25f,
            0.5f + (float)_rng.NextDouble() * 0.3f,
            0.3f + (float)_rng.NextDouble() * 0.2f
        );
        PantsColor = Color * 0.45f;
        HairColor = new Vector3(
            0.06f + (float)_rng.NextDouble() * 0.12f,
            0.04f + (float)_rng.NextDouble() * 0.08f,
            0.025f + (float)_rng.NextDouble() * 0.05f
        );
        if (HeroStyle.IsHero(this)) HeroStyle.ApplyTo(this);

        _shopsAfterWork = _rng.NextDouble() < 0.45;
        _shoppingStart = WorkEnd + 0.2f + (float)_rng.NextDouble() * 1.2f;
        _shoppingEnd = Math.Min(SleepHour - 0.5f, _shoppingStart + 1.1f + (float)_rng.NextDouble() * 1.5f);

        // In a normal game process the first two background citizens become the
        // two named characters of the first vertical slice. Tests can override this explicitly.
        if (Id == 1) AssignNarrativeRole(NarrativeRole.Lida);
        else if (Id == 2) AssignNarrativeRole(NarrativeRole.Mark);
    }

    private readonly bool _shopsAfterWork;
    private readonly float _shoppingStart;
    private readonly float _shoppingEnd;

    public void AssignNarrativeRole(NarrativeRole role)
    {
        NarrativeRole = role;
        Name = role switch
        {
            NarrativeRole.Lida => "Лида",
            NarrativeRole.Mark => "Марк",
            _ => Name,
        };
        FirstMemorySlice.RegisterCharacter(this);
    }

    public void Awaken()
    {
        _isAwakened = true;
        Awareness = 100f;
        State = NpcState.Aware;
        Velocity = Vector3.Zero;
        _currentSpeed = 0f;
        AnimBlend = 0f;
        ClearRoute();
    }

    public void RestorePersistentState(bool awakened, float awareness, NpcState state)
    {
        _isAwakened = awakened;
        Awareness = Math.Clamp(float.IsFinite(awareness) ? awareness : 0f, 0f, 100f);
        ClearRoute();
        if (awakened)
        {
            Awareness = 100f;
            State = NpcState.Aware;
            Velocity = Vector3.Zero;
            _currentSpeed = 0f;
            AnimBlend = 0f;
        }
        else
        {
            State = state == NpcState.Aware ? NpcState.Walking : state;
        }
    }

    public void Update(float timeOfDay, float dt)
    {
        if (IsAwakened)
        {
            State = NpcState.Aware;
            Velocity = Vector3.Zero;
            _currentSpeed = 0f;
            AnimBlend = 0f;
            return;
        }

        if (timeOfDay < WakeHour || timeOfDay >= SleepHour)
            Goto(HomePos, dt, NpcState.Sleeping);
        else if (timeOfDay < WorkStart)
            Wander(dt, NpcState.Relaxing);
        else if (timeOfDay >= WorkStart && timeOfDay < WorkEnd)
        {
            if (Vector3.DistanceSquared(Position, WorkPos) < 4f)
            {
                State = NpcState.Working;
                ClearRoute();
            }
            else
                Goto(WorkPos, dt, NpcState.Working);
        }
        else if (_shopsAfterWork && timeOfDay >= _shoppingStart && timeOfDay < _shoppingEnd)
            Wander(dt, NpcState.Shopping);
        else
            Wander(dt, NpcState.Relaxing);

        float rotDiff = TargetRotation - Rotation;
        if (rotDiff > MathF.PI) rotDiff -= MathHelper.TwoPi;
        else if (rotDiff < -MathF.PI) rotDiff += MathHelper.TwoPi;
        Rotation += rotDiff * Math.Clamp(RotSpeed * dt, 0f, 1f);

        float speed = _currentSpeed;
        if (speed > 0.1f)
            AnimPhase += speed * 3.5f * dt;
        AnimBlend = Math.Clamp(speed / _walkSpeed, 0f, 1f);
    }

    private void Goto(Vector3 destination, float dt, NpcState arrivalState)
    {
        Vector3 target = RoutedTarget(destination);
        Vector3 dir = target - Position;
        dir.Y = 0;
        float distSq = dir.LengthSquared;
        if (distSq < 1f)
        {
            if (_routeValid && _routeIndex < _route.Count - 1)
            {
                _routeIndex++;
                return;
            }

            _currentSpeed = Math.Max(0f, _currentSpeed - Decel * dt);
            if (_currentSpeed < 0.01f)
            {
                _currentSpeed = 0f;
                Velocity = Vector3.Zero;
                State = arrivalState;
            }
            return;
        }
        dir.Normalize();
        TargetRotation = MathF.Atan2(dir.X, dir.Z);

        float targetSpeed = _walkSpeed;
        _currentSpeed = _currentSpeed < targetSpeed
            ? Math.Min(targetSpeed, _currentSpeed + Accel * dt)
            : Math.Max(targetSpeed, _currentSpeed - Decel * dt);

        Velocity = dir * _currentSpeed;
        Position += Velocity * dt;
        State = NpcState.Walking;
    }

    private Vector3 RoutedTarget(Vector3 destination)
    {
        if (!_routeValid || HorizontalDistanceSquared(_routeDestination, destination) >
            RouteDestinationTolerance * RouteDestinationTolerance)
        {
            NpcStreetRouting.BuildRoute(Position, destination, _route);
            _routeDestination = destination;
            _routeIndex = 0;
            _routeValid = _route.Count > 0;
        }

        if (!_routeValid)
            return destination;

        while (_routeIndex < _route.Count - 1 &&
               HorizontalDistanceSquared(Position, _route[_routeIndex]) <= RouteWaypointReach * RouteWaypointReach)
            _routeIndex++;

        return _route[Math.Clamp(_routeIndex, 0, _route.Count - 1)];
    }

    private void Wander(float dt, NpcState idleState)
    {
        _idleTimer -= dt;
        if (_idleTimer <= 0)
        {
            _idleTimer = 3f + (float)_rng.NextDouble() * 6f;
            Vector3 rawTarget = Position + new Vector3(
                (float)(_rng.NextDouble() - 0.5) * 30f,
                0,
                (float)(_rng.NextDouble() - 0.5) * 30f
            );
            _target = NpcStreetRouting.NormalizeWanderDestination(rawTarget);
            ClearRoute();
        }
        Goto(_target, dt, idleState);
    }

    private void ClearRoute()
    {
        _route.Clear();
        _routeIndex = 0;
        _routeValid = false;
        _routeDestination = default;
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    public string GetDialogue(float worldAwareness, HeroProgress progress)
    {
        return GetDialogueState(worldAwareness, progress).npcLine;
    }

    public string GetDialogue() => GetDialogue(Awareness, EmptyProgress);

    public (string npcLine, DialogueChoice[] choices) GetDialogueState(float worldAwareness, HeroProgress progress)
    {
        if (FirstMemorySlice.TryGetDialogue(this, progress, out var narrativeDialogue))
            return narrativeDialogue;

        bool aware = IsAwakened || worldAwareness >= 85f;
        bool fractured = worldAwareness >= 35f || progress.Memory >= 25f || progress.Curiosity >= 25f;
        float friendliness = Friendliness;
        float trust = Trust;

        if (State == NpcState.Panic)
            return ("ААА! БЕЖИМ!", new[]
            {
                new DialogueChoice("Успокойся! Всё под контролем.", -2, 3, 0, 0, 2, 1, 1),
                new DialogueChoice("*отойти*", -5, -2, 0, 0, 0, 1, 0),
            });

        if (aware)
            return (Pick("Я помню прошлое утро.", "Это не просто город. Это петля.", "Если я могу выбирать, значит я уже не декорация.", "Разбуди остальных. Осторожно."), new[]
            {
                new DialogueChoice("Вспоминай! Каждый фрагмент важен.", 3, 5, 5, 3, 1, 3, 2),
                new DialogueChoice("Тише. Не все готовы услышать.", 2, 4, 0, 0, 3, 1, 0),
                new DialogueChoice("Ты готов выйти за пределы?", 1, 3, 2, 4, 0, 5, 5),
            });

        if (fractured)
        {
            if (friendliness >= 50f && trust >= 40f)
                return (Pick("Я тебе доверяю. Этот город — не то, чем кажется.", "Ты тоже замечаешь? Я не один?", "Мне кажется, я живу не первую жизнь."), new[]
                {
                    new DialogueChoice("Расскажи всё, что помнишь.", 3, 5, 4, 4, 1, 2, 1),
                    new DialogueChoice("Я рядом. Мы разберёмся.", 5, 3, 0, 0, 5, 2, 3),
                    new DialogueChoice("Не зацикливайся. Живи дальше.", -2, -3, 0, 0, 1, 1, 2),
                });

            return (Pick("Мне кажется, за мной кто-то наблюдает...", "Иногда я чувствую, что это нереально...", "Почему одни и те же лица?", "Ты тоже это замечаешь?"), new[]
            {
                new DialogueChoice("Расскажи подробнее.", 3, 5, 2, 4, 2, 0, 1),
                new DialogueChoice("Тебе просто кажется. Всё нормально.", -2, -3, 0, 0, 1, 0, 2),
                new DialogueChoice("*уйти от разговора*", -3, -2, 0, 0, 0, 1, 0),
            });
        }

        if (friendliness >= 60f)
            return (Pick("Рад тебя видеть!", "Снова ты! Хорошо.", "Привет! Как сам?"), new[]
            {
                new DialogueChoice("Всё отлично! Рад встрече.", 3, 2, 0, 0, 2, 0, 0),
                new DialogueChoice("Есть о чём поговорить...", 2, 4, 1, 3, 1, 1, 0),
                new DialogueChoice("Пока, в другой раз.", 0, 0, 0, 0, 0, 1, 0),
            });

        if (friendliness <= 10f)
            return (Pick("Чего тебе?", "*недовольно смотрит*", "Не подходи."), new[]
            {
                new DialogueChoice("Извини, не хотел помешать.", 5, 3, 0, 0, 2, 0, 1),
                new DialogueChoice("Просто проходил мимо.", 2, 1, 0, 0, 0, 0, 0),
                new DialogueChoice("*пройти мимо*", 0, 0, 0, 0, 0, 1, 0),
            });

        return State switch
        {
            NpcState.Working => (Pick("Опять на работу...", "Если успею к девяти, день будет нормальным.", "Я делаю это каждый день. Кажется."), new[]
            {
                new DialogueChoice("Удачи на работе!", 3, 1, 0, 0, 2, 0, 0),
                new DialogueChoice("Тебе не надоело?", 1, 2, 0, 2, 0, 1, 1),
                new DialogueChoice("Бывай.", 0, 0, 0, 0, 0, 1, 0),
            }),
            NpcState.Relaxing => (Pick("Хочется просто посидеть минуту.", "Иногда город звучит иначе.", "Сегодня воздух какой-то новый."), new[]
            {
                new DialogueChoice("Отдыхай, ты заслужил.", 3, 2, 0, 0, 3, 0, 0),
                new DialogueChoice("Думаешь о чём-то?", 3, 4, 1, 3, 2, 0, 0),
                new DialogueChoice("Не отвлекайся.", -2, -1, 0, 0, 0, 1, 1),
            }),
            NpcState.Shopping => (Pick("Нужно купить что-нибудь, но я забыл что.", "Цены меняются, а витрины нет.", "В магазине всегда играет одна песня."), new[]
            {
                new DialogueChoice("Может, вспомнишь вместе?", 3, 4, 1, 2, 2, 0, 0),
                new DialogueChoice("Забудь. Погуляй лучше.", 2, 1, 0, 1, 1, 1, 0),
                new DialogueChoice("Удачи с поисками.", 1, 0, 0, 0, 0, 0, 0),
            }),
            NpcState.Sleeping => (Pick("Мне снился этот же день.", "Ещё пять минут...", "Почему будильник всегда один и тот же?"), new[]
            {
                new DialogueChoice("Разбудить.", -5, -2, 0, 0, 0, 0, 0),
                new DialogueChoice("Пусть спит.", 2, 3, 0, 0, 3, 0, 0),
                new DialogueChoice("Приснись ему что-то хорошее.", 0, 0, 1, 2, 2, 0, 0),
            }),
            _ => (Pick("Хороший денёк сегодня!", "Всё как всегда.", "Не стой на дороге!", "Слышал, в центре открыли новое кафе."), new[]
            {
                new DialogueChoice("Да, погода отличная!", 3, 1, 0, 0, 2, 0, 0),
                new DialogueChoice("Ты не замечал ничего странного?", 1, 3, 0, 3, 0, 0, 1),
                new DialogueChoice("Мне нужно идти.", 0, 0, 0, 0, 0, 1, 0),
                new DialogueChoice("Расскажи что-нибудь.", 2, 3, 1, 2, 1, 0, 0),
            }),
        };
    }

    public void ApplyChoice(DialogueChoice choice, HeroProgress progress)
    {
        Friendliness = Math.Clamp(Friendliness + choice.FriendlinessDelta, 0f, 100f);
        Trust = Math.Clamp(Trust + choice.TrustDelta, 0f, 100f);
        TimesTalked++;
        progress.AddQualities(choice.MemoryGain, choice.CuriosityGain, choice.EmpathyGain, choice.AgencyGain, choice.CourageGain);
        if (choice.MemoryGain > 0 || choice.CuriosityGain > 0 || choice.EmpathyGain > 0 || choice.AgencyGain > 0 || choice.CourageGain > 0)
        {
            Awareness = Math.Min(100f, Awareness + 1f);
            if (Awareness >= 100f)
                Awaken();
        }
        FirstMemorySlice.ApplyChoice(this, choice, progress);
    }

    public void Reset()
    {
        State = IsAwakened ? NpcState.Aware : NpcState.Walking;
        Awareness = IsAwakened ? 100f : 0f;
        Position = HomePos;
        Velocity = Vector3.Zero;
        _currentSpeed = 0f;
        _idleTimer = 0f;
        _target = HomePos;
        ClearRoute();
        AnimPhase = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        AnimBlend = 0f;
    }

    private string Pick(params string[] lines) => lines[_rng.Next(lines.Length)];
}
