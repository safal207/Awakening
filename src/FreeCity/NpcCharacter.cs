using System;
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
    float CourageGain);

public class NpcCharacter
{
    private static int _nextId;
    private static readonly HeroProgress EmptyProgress = new();
    private readonly Random _rng;

    public int Id;
    public string Name;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Color;
    public Vector3 HeadColor;
    public Vector3 PantsColor;
    public Vector3 HairColor;
    public float Rotation;
    public float TargetRotation;
    public NpcState State = NpcState.Walking;
    public float Awareness;
    public float Height = 1.7f;

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
    private const float Accel = 8f;
    private const float Decel = 12f;
    private const float RotSpeed = 6f;
    private readonly bool _shopsAfterWork;
    private readonly float _shoppingStart;
    private readonly float _shoppingEnd;

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

        _shopsAfterWork = _rng.NextDouble() < 0.45;
        _shoppingStart = WorkEnd + 0.2f + (float)_rng.NextDouble() * 1.2f;
        _shoppingEnd = Math.Min(SleepHour - 0.5f, _shoppingStart + 1.1f + (float)_rng.NextDouble() * 1.5f);
    }

    public void Update(float timeOfDay, float dt)
    {
        if (State == NpcState.Aware) return;

        if (timeOfDay < WakeHour || timeOfDay >= SleepHour)
            Goto(HomePos, dt, NpcState.Sleeping);
        else if (timeOfDay < WorkStart)
            Wander(dt, NpcState.Relaxing);
        else if (timeOfDay >= WorkStart && timeOfDay < WorkEnd)
        {
            if (Vector3.DistanceSquared(Position, WorkPos) < 4f)
                State = NpcState.Working;
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

    private void Goto(Vector3 target, float dt, NpcState state)
    {
        Vector3 dir = target - Position;
        dir.Y = 0;
        float distSq = dir.LengthSquared;
        if (distSq < 1f)
        {
            _currentSpeed = Math.Max(0f, _currentSpeed - Decel * dt);
            if (_currentSpeed < 0.01f)
            {
                _currentSpeed = 0f;
                Velocity = Vector3.Zero;
                State = state;
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

    private void Wander(float dt, NpcState idleState)
    {
        _idleTimer -= dt;
        if (_idleTimer <= 0)
        {
            _idleTimer = 3f + (float)_rng.NextDouble() * 6f;
            _target = Position + new Vector3(
                (float)(_rng.NextDouble() - 0.5) * 30f,
                0,
                (float)(_rng.NextDouble() - 0.5) * 30f
            );
        }
        Goto(_target, dt, idleState);
    }

    public string GetDialogue(float worldAwareness, HeroProgress progress)
    {
        return GetDialogueState(worldAwareness, progress).npcLine;
    }

    public string GetDialogue() => GetDialogue(Awareness, EmptyProgress);

    public (string npcLine, DialogueChoice[] choices) GetDialogueState(float worldAwareness, HeroProgress progress)
    {
        bool aware = State == NpcState.Aware || worldAwareness >= 85f;
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
            Awareness = Math.Min(100f, Awareness + 1f);
    }

    public void Reset()
    {
        State = NpcState.Walking;
        Awareness = 0f;
        Position = HomePos;
        Velocity = Vector3.Zero;
        _currentSpeed = 0f;
        _idleTimer = 0f;
        _target = HomePos;
        AnimPhase = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        AnimBlend = 0f;
    }

    private string Pick(params string[] lines) => lines[_rng.Next(lines.Length)];
}
