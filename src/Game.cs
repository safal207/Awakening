using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

namespace Probuzhdenie;

public class Game : GameWindow
{
    private enum GameScreen
    {
        MainMenu,
        Playing,
        PauseMenu,
        Dialogue,
    }

    private const string GameTitle = "Пробуждение";
    private const float MaxFrameDelta = 0.1f;
    private const float MouseYawSensitivity = 0.1f;
    private const float MousePitchSensitivity = 0.1f;
    private const float MinCameraPitchDegrees = 5f;
    private const float MaxCameraPitchDegrees = 65f;
    private const float DefaultCameraPitchDegrees = 18f;
    private const float ZoomSpeed = 18f;
    private const float WalkSpeed = 8f;
    private const float AutoSaveIntervalSeconds = 30f;

    private readonly Camera _cam = new();
    private readonly Input _input;
    private readonly UiRenderer _ui = new();
    private readonly RuntimeProfileOptions? _profileOptions;
    private readonly RuntimeProfiler? _runtimeProfiler;
    private readonly string[] _mainMenuItems = { "НАЧАТЬ", "ВЫХОД" };
    private readonly string[] _pauseMenuItems = { "ПРОДОЛЖИТЬ", "ВЫХОД" };
    private CityRenderer? _city;
    private CityRenderContext _cityRenderContext;
    private int _shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL;
    private GameScreen _screen = GameScreen.MainMenu;
    private bool _captured;
    private int _menuIndex;
    private float _dialogueTimer;
    private float _saveTimer;
    private double _offlineGrowthMinutes;
    private string _lastDialogue = "";
    private int _crossVao, _crossVbo;
    private float _crossAspect = -1f;
    private int _crossGpuBytes;
    private NpcCharacter? _dialogueNpc;
    private string _dialogueNpcLine = "";
    private DialogueChoice[] _dialogueChoices = Array.Empty<DialogueChoice>();
    private int _dialogueChoiceIndex;
    private double _profileElapsedSeconds;
    private PlayerController? _playerController;
    private float _dialogueCamTargetDist;
    private InteractionDetector? _interactionDetector;
    private InteractionResult _currentInteraction;
    private float _camDist = 14f, _camYaw, _camPitchDegrees = DefaultCameraPitchDegrees;
    private float _preDialogueCamDist = 14f;
    private SpriteRenderer? _spriteRenderer;
    private Texture? _logoSplash;

    public Game(RuntimeProfileOptions? profileOptions = null) : base(
        GameWindowSettings.Default,
        new NativeWindowSettings { Size = new Vector2i(1280, 720), Title = GameTitle })
    {
        _profileOptions = profileOptions;
        _runtimeProfiler = profileOptions != null ? new RuntimeProfiler(profileOptions) : null;
        _input = new Input(this);
        CursorState = CursorState.Normal;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = _profileOptions == null ? VSyncMode.On : VSyncMode.Off;
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);

        _shader = MakeShader();
        _modelL = GL.GetUniformLocation(_shader, "model");
        _viewL = GL.GetUniformLocation(_shader, "view");
        _projL = GL.GetUniformLocation(_shader, "proj");
        _colorL = GL.GetUniformLocation(_shader, "col");
        _ambL = GL.GetUniformLocation(_shader, "amb");
        _lightL = GL.GetUniformLocation(_shader, "light");
        _fogColL = GL.GetUniformLocation(_shader, "fogCol");
        _cityRenderContext = new CityRenderContext(_shader, _modelL, _viewL, _projL, _colorL, _fogColL);

        _spriteRenderer = new SpriteRenderer();

        int seed;
        HeroProgress progress;
        float timeOfDay;
        float awareness;
        List<SaveSystem.NpcSaveData>? npcData = null;

        if (_profileOptions != null)
        {
            seed = 424242;
            progress = new HeroProgress();
            timeOfDay = 8f;
            awareness = 0f;
            _offlineGrowthMinutes = 0;
        }
        else
        {
            var save = SaveSystem.Load();
            seed = save.seed;
            progress = save.progress;
            timeOfDay = save.timeOfDay;
            awareness = save.awareness;
            _offlineGrowthMinutes = save.offlineMinutes;
            npcData = save.npcs;
        }

        _city = new CityRenderer(seed, progress);
        _city.BuildGeometry();
        _city.TimeOfDay = timeOfDay;
        _city.Awareness.Restore(awareness);
        _city.UpdateNpcs(0f);
        if (npcData != null) _city.RestoreNpcs(npcData);

        // Камера от 3-го лица: сзади героя
        if (_city.Player != null)
        {
            _camYaw = _city.Player.Rotation - MathF.PI;
            UpdateThirdPerson(0);
            _cam.SnapToTarget();
        }

        _playerController = new PlayerController(_city, _input, _cam);
        _interactionDetector = new InteractionDetector(_city);

        if (_runtimeProfiler != null)
        {
            _screen = GameScreen.Playing;
            _captured = false;
            CursorState = CursorState.Normal;
            _runtimeProfiler.Start(ReadGlInfo());
            SnapCameraBehindHero();
            Console.WriteLine($"Runtime profile started for {_profileOptions!.DurationSeconds:F0}s. Report: {_profileOptions.ReportPath}");
        }

        _logoSplash = Texture.CreateLogo();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        _input.Update();
        float dt = Math.Min((float)args.Time, MaxFrameDelta);

        if (_screen != GameScreen.Playing)
        {
            UpdateMenu();
            return;
        }

        // Dialogue close by Escape (before global pause menu)
        if (_dialogueNpc != null && (_input.KeyPressed(Keys.Escape) || _input.GpBPressed))
        {
            _dialogueNpc = null;
            CaptureMouse();
        }

        if (_input.KeyPressed(Keys.Escape) || _input.GpStartPressed)
        {
            OpenPauseMenu();
            return;
        }

        if (_input.KeyPressed(Keys.C))
            SnapCameraBehindHero();

        if (_runtimeProfiler != null)
        {
            UpdateRuntimeProfileAutomation(dt);
        }
        else if (_captured && _city?.Player != null)
        {
            // Мышь + геймпад: поворот камеры вокруг игрока
            _camYaw -= _input.Dx * MouseYawSensitivity;
            _camPitchDegrees = Math.Clamp(_camPitchDegrees - _input.Dy * MousePitchSensitivity, MinCameraPitchDegrees, MaxCameraPitchDegrees);
            if (_input.GpConnected)
            {
                _camYaw -= _input.GpRightX * 4f * dt;
                _camPitchDegrees = Math.Clamp(_camPitchDegrees + _input.GpRightY * 3f * dt, MinCameraPitchDegrees, MaxCameraPitchDegrees);
            }

            if (_input.KeyDown(Keys.K)) _camDist += ZoomSpeed * dt;
            if (_input.KeyDown(Keys.J)) _camDist -= ZoomSpeed * dt;
            _camDist = Math.Clamp(_camDist, 4f, 30f);

            _playerController?.Update(dt);
        }

        // Camera always updates (including during dialogue for zoom)
        UpdateThirdPerson(dt);

        if (_city?.Player != null && _dialogueNpc == null)
            _currentInteraction = _interactionDetector?.Detect(_city.Player.Position) ?? default;
        else
            _currentInteraction = default;

        _city?.UpdateNpcs(dt);

        // Универсальная интеракция (E): поговорить, войти, выйти
        if (_city?.Player != null && _input.KeyPressed(Keys.E) && _dialogueNpc == null && _dialogueTimer <= 0)
        {
            switch (_currentInteraction.Type)
            {
                case InteractionType.Exit:
                    _city.Player.Position = _city.ExitInterior();
                    _playerController?.ResetMotion();
                    SnapCameraBehindHero();
                    break;
                case InteractionType.Enter:
                    var enterPos = _city.TryEnterInterior(_city.Player.Position);
                    if (enterPos.HasValue)
                    {
                        _city.Player.Position = enterPos.Value;
                        _playerController?.ResetMotion();
                        SnapCameraBehindHero();
                    }
                    break;
                case InteractionType.Talk:
                    StartDialogue(_currentInteraction.TargetNpc!);
                    break;
            }
        }

        _saveTimer += dt;
        if (_profileOptions == null && _saveTimer >= AutoSaveIntervalSeconds)
        {
            _city?.SaveGame();
            _saveTimer = 0f;
        }

        // Диалоги NPC
        if (_dialogueNpc != null)
        {
            if (_input.KeyPressed(Keys.Up) || _input.KeyPressed(Keys.W) || _input.GpLeftY < -0.5f)
                _dialogueChoiceIndex = (_dialogueChoiceIndex - 1 + _dialogueChoices.Length) % _dialogueChoices.Length;
            else if (_input.KeyPressed(Keys.Down) || _input.KeyPressed(Keys.S) || _input.GpLeftY > 0.5f)
                _dialogueChoiceIndex = (_dialogueChoiceIndex + 1) % _dialogueChoices.Length;
            else if (_input.KeyPressed(Keys.Enter) || _input.KeyPressed(Keys.Space) || _input.GpAPressed)
            {
                var choice = _dialogueChoices[_dialogueChoiceIndex];
                _dialogueNpc.ApplyChoice(choice, _city!.Progress);
                _city.Awareness.Add(2f);
                _city.RegisterTalk();
                _dialogueNpc = null;
                CaptureMouse();
                _dialogueTimer = 2f;
            }
        }
        else
        {
            _dialogueTimer -= dt;

            if (_city?.Player != null && (_input.LmbPressed || _input.GpAPressed) && _dialogueTimer <= 0)
            {
                _lastDialogue = _city.GetPlayerDialogue();
                Title = $"{GameTitle} - {_lastDialogue}";
                _dialogueTimer = 3f;
                _city.Awareness.Add(2f);
                _city.RegisterTalk();
            }
        }
    }

    private void UpdateMenu()
    {
        string[] items = CurrentMenuItems;

        if (_input.KeyPressed(Keys.Escape) || _input.GpStartPressed || _input.GpBPressed)
        {
            if (_screen == GameScreen.PauseMenu) ResumeGame();
            else Close();
            return;
        }

        if (_input.KeyPressed(Keys.Up) || _input.KeyPressed(Keys.W) || _input.GpLeftY < -0.5f)
            _menuIndex = (_menuIndex - 1 + items.Length) % items.Length;

        if (_input.KeyPressed(Keys.Down) || _input.KeyPressed(Keys.S) || _input.GpLeftY > 0.5f)
            _menuIndex = (_menuIndex + 1) % items.Length;

        if (_input.KeyPressed(Keys.Enter) || _input.KeyPressed(Keys.Space) || _input.GpAPressed)
            SelectMenuItem();
    }

    private string[] CurrentMenuItems => _screen == GameScreen.MainMenu ? _mainMenuItems : _pauseMenuItems;

    private void SelectMenuItem()
    {
        if (_screen == GameScreen.MainMenu)
        {
            if (_menuIndex == 0) StartGame();
            else Close();
            return;
        }

        if (_menuIndex == 0) ResumeGame();
        else Close();
    }

    private void StartGame()
    {
        _screen = GameScreen.Playing;
        _menuIndex = 0;
        CaptureMouse();
        SnapCameraBehindHero();
        Title = GameTitle;
    }

    private void OpenPauseMenu()
    {
        _city?.SaveGame();
        _screen = GameScreen.PauseMenu;
        _menuIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
        Title = $"{GameTitle} - Меню";
    }

    private void ResumeGame()
    {
        _screen = GameScreen.Playing;
        CaptureMouse();
        Title = GameTitle;
    }

    private void CaptureMouse()
    {
        _captured = true;
        CursorState = CursorState.Grabbed;
        _input.ResetMouse();
    }

    private void StartDialogue(NpcCharacter target)
    {
        var (npcLine, choices) = target.GetDialogueState(_city!.Awareness.Level, _city.Progress);
        _dialogueNpc = target;
        _dialogueNpcLine = npcLine;
        _dialogueChoices = choices;
        _dialogueChoiceIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
        _input.ResetMouse();
        _dialogueNpc.LastTalkDay = _city.Progress.Day;

        // Dialogue camera: zoom in, yaw to opposite side of NPC
        if (_city?.Player != null)
        {
            _preDialogueCamDist = _camDist;
            _dialogueCamTargetDist = 5f;

            Vector3 toNpc = target.Position - _city.Player.Position;
            if (toNpc.LengthSquared > 0.001f)
            {
                toNpc.Y = 0f;
                _camYaw = MathF.Atan2(toNpc.X, toNpc.Z) + MathF.PI;
            }
            _camPitchDegrees = MathHelper.Clamp(_camPitchDegrees, 12f, 30f);
        }
    }

    private void SnapCameraBehindHero()
    {
        if (_city?.Player == null) return;
        _camYaw = _city.Player.Rotation - MathF.PI;
        _camPitchDegrees = DefaultCameraPitchDegrees;
        _camDist = 11f;
        UpdateThirdPerson(0);
        _cam.SnapToTarget();
    }

    private void UpdateThirdPerson(float dt)
    {
        if (_city?.Player == null) return;
        Vector3 p = _city.Player.Position;
        float playerH = _city.Player.Height;

        // Always lerp camera distance (returns smoothly after dialogue)
        float targetDist = _dialogueNpc != null ? _dialogueCamTargetDist : _preDialogueCamDist;
        float lerpFactor = Math.Clamp(6f * dt, 0f, 1f);
        _camDist = MathHelper.Lerp(_camDist, targetDist, lerpFactor);

        float yr = _camYaw, pr = MathHelper.DegreesToRadians(_camPitchDegrees);
        _cam.TargetPos = p + new Vector3(
            _camDist * MathF.Cos(pr) * MathF.Sin(yr),
            _camDist * MathF.Sin(pr) + playerH * 1.4f,
            _camDist * MathF.Cos(pr) * MathF.Cos(yr));

        // During dialogue: look at midpoint between player and NPC
        Vector3 lookTarget;
        if (_dialogueNpc != null)
        {
            lookTarget = (p + _dialogueNpc.Position) * 0.5f + new Vector3(0, playerH * 0.5f, 0);
        }
        else
        {
            lookTarget = p + new Vector3(0, playerH * 0.7f, 0);
        }
        _cam.Front = Vector3.Normalize(lookTarget - _cam.TargetPos);
        _cam.Right = Vector3.Normalize(Vector3.Cross(_cam.Front, Vector3.UnitY));
        _cam.Up = Vector3.Normalize(Vector3.Cross(_cam.Right, _cam.Front));
        _cam.Yaw = MathHelper.RadiansToDegrees(_camYaw);
        _cam.Pitch = _camPitchDegrees;
        _cam.UpdateFollow(dt);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        float t = _city?.TimeOfDay ?? 8f;
        float day = Math.Clamp(MathF.Sin((t - 6f) / 12f * MathF.PI), 0.1f, 1f);
        GL.ClearColor(
            0.15f + 0.4f * day,
            0.2f + 0.5f * day,
            0.4f + 0.55f * day,
            1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_screen == GameScreen.Playing)
        {
            GL.UseProgram(_shader);
            var view = _cam.View;
            var proj = _cam.Proj(ClientSize.X / (float)Math.Max(1, ClientSize.Y));
            var id = Matrix4.Identity;
            GL.UniformMatrix4(_viewL, false, ref view);
            GL.UniformMatrix4(_projL, false, ref proj);
            GL.UniformMatrix4(_modelL, false, ref id);
            GL.Uniform3(_colorL, -1f, -1f, -1f);

            float amb = 0.2f + 0.4f * day;
            float sunX = MathF.Cos((t - 6f) / 12f * MathF.PI) * 0.6f;
            float sunY = MathF.Sin((t - 6f) / 12f * MathF.PI) * 0.8f + 0.1f;
            GL.Uniform3(_ambL, amb, amb, amb);
            GL.Uniform3(_lightL, sunX, sunY, 0.3f);

            Vector3 fogCol = new(0.15f + 0.4f * day, 0.2f + 0.5f * day, 0.4f + 0.55f * day);
            GL.Uniform3(_fogColL, fogCol.X, fogCol.Y, fogCol.Z);

            _city?.Render(_cityRenderContext, ref view, ref proj, fogCol);
            RenderHud();
        }
        else
        {
            RenderMenu();
        }

        SwapBuffers();
        UpdateRuntimeProfile(args.Time);
    }

    private void RenderMenu()
    {
        _ui.Begin(ClientSize.X, ClientSize.Y);

        Vector3 bg = new(0.024f, 0.031f, 0.037f);
        Vector3 panel = new(0.055f, 0.067f, 0.074f);
        Vector3 panel2 = new(0.085f, 0.101f, 0.11f);
        Vector3 accent = new(0.16f, 0.48f, 0.78f);
        Vector3 accentDark = new(0.08f, 0.25f, 0.43f);
        Vector3 warm = new(0.93f, 0.77f, 0.36f);
        Vector3 text = new(0.88f, 0.92f, 0.93f);
        Vector3 dim = new(0.55f, 0.63f, 0.66f);

        _ui.Rect(0f, 0f, 1f, 1f, bg);
        _ui.Rect(0f, 0f, 1f, 0.055f, new Vector3(0.035f, 0.045f, 0.052f));
        _ui.Rect(0.08f, 0.14f, 0.38f, 0.68f, panel);
        _ui.Rect(0.105f, 0.18f, 0.006f, 0.40f, accent);
        _ui.Rect(0.56f, 0.14f, 0.34f, 0.68f, new Vector3(0.035f, 0.043f, 0.048f));
        _ui.Rect(0.58f, 0.16f, 0.30f, 0.64f, new Vector3(0.045f, 0.055f, 0.061f));

        _ui.Render(_shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL);
        _ui.Begin(ClientSize.X, ClientSize.Y);
        if (_logoSplash != null && _spriteRenderer != null)
        {
            _logoSplash.Bind(0);
            var id2 = Matrix4.Identity;
            const float logouiW = 0.34f, logouiH = 0.085f;
            float logouix = 0.5f - logouiW * 0.5f, logouiy = 0.1f;
            float lndcX = (logouix + logouiW * 0.5f) * 2f - 1f;
            float lndcY = 1f - (logouiy + logouiH * 0.5f) * 2f;
            float lndcW = logouiW * 2f, lndcH = logouiH * 2f;
            _spriteRenderer.Begin();
            _spriteRenderer.Add(new Vector3(lndcX, lndcY, 0f), lndcW, lndcH, new Vector3(1f, 0.95f, 0.75f), 1f);
            _spriteRenderer.Flush(ref id2, ref id2);
        }

        string heading = _screen == GameScreen.PauseMenu ? "МЕНЮ ПАУЗЫ" : "НОВЫЙ ЦИКЛ";
        _ui.Text(heading, 0.13f, 0.285f, 0.0047f, dim);

        if (_offlineGrowthMinutes >= 1)
        {
            string offline = $"ГЕРОЙ РОС {Math.Ceiling(_offlineGrowthMinutes)} МИН";
            _ui.Text(offline, 0.13f, 0.325f, 0.0042f, warm);
        }

        string[] items = CurrentMenuItems;
        for (int i = 0; i < items.Length; i++)
        {
            float y = 0.43f + i * 0.105f;
            bool selected = i == _menuIndex;
            _ui.Rect(0.13f, y, 0.28f, 0.062f, selected ? accent : panel2);
            if (selected)
            {
                _ui.Rect(0.13f, y, 0.012f, 0.062f, warm);
            }

            float itemSize = 0.0054f;
            _ui.Text(items[i], 0.27f - _ui.MeasureText(items[i], itemSize) / 2f, y + 0.019f, itemSize, selected ? text : dim);
        }

        _ui.Text("ГЕРОЙ", 0.66f, 0.18f, 0.005f, dim);
        RenderHeroPreview(0.73f, 0.235f, 1.15f);
        _ui.Rect(0.62f, 0.735f, 0.22f, 0.004f, accentDark);
        _ui.Render(_shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL);
    }

    private void RenderHeroPreview(float cx, float y, float scale)
    {
        float s = 0.18f * scale;
        Vector3 skin = HeroStyle.Skin;
        Vector3 skinShadow = skin * 0.85f;
        Vector3 shirt = HeroStyle.ShirtBlue;
        Vector3 shirtDark = HeroStyle.ShirtDark;
        Vector3 shirtLight = HeroStyle.ShirtLight;
        Vector3 pants = HeroStyle.Pants;
        Vector3 hair = HeroStyle.Hair;
        Vector3 shoes = new(0.025f, 0.025f, 0.03f);
        Vector3 eye = new(0.03f, 0.03f, 0.06f);

        _ui.Rect(cx - s * 0.11f, y + s * 0.03f, s * 0.22f, s * 0.05f, hair);
        _ui.Rect(cx - s * 0.16f, y + s * 0.08f, s * 0.32f, s * 0.08f, hair);
        _ui.Rect(cx - s * 0.18f, y + s * 0.16f, s * 0.36f, s * 0.30f, skin);
        _ui.Rect(cx - s * 0.14f, y + s * 0.46f, s * 0.28f, s * 0.08f, skinShadow);
        _ui.Rect(cx - s * 0.075f, y + s * 0.29f, s * 0.035f, s * 0.035f, eye);
        _ui.Rect(cx + s * 0.04f, y + s * 0.29f, s * 0.035f, s * 0.035f, eye);
        _ui.Rect(cx - s * 0.045f, y + s * 0.42f, s * 0.09f, s * 0.026f, skinShadow);

        _ui.Rect(cx - s * 0.09f, y + s * 0.53f, s * 0.18f, s * 0.10f, skin);
        _ui.Rect(cx - s * 0.33f, y + s * 0.64f, s * 0.66f, s * 0.12f, shirtDark);
        _ui.Rect(cx - s * 0.25f, y + s * 0.70f, s * 0.50f, s * 0.52f, shirt);
        _ui.Rect(cx - s * 0.045f, y + s * 0.73f, s * 0.09f, s * 0.48f, shirtLight);

        _ui.Rect(cx - s * 0.46f, y + s * 0.72f, s * 0.13f, s * 0.46f, skin);
        _ui.Rect(cx + s * 0.33f, y + s * 0.72f, s * 0.13f, s * 0.46f, skin);
        _ui.Rect(cx - s * 0.49f, y + s * 1.16f, s * 0.16f, s * 0.10f, skinShadow);
        _ui.Rect(cx + s * 0.33f, y + s * 1.16f, s * 0.16f, s * 0.10f, skinShadow);

        _ui.Rect(cx - s * 0.25f, y + s * 1.22f, s * 0.20f, s * 0.58f, pants);
        _ui.Rect(cx + s * 0.05f, y + s * 1.22f, s * 0.20f, s * 0.58f, pants);
        _ui.Rect(cx - s * 0.30f, y + s * 1.79f, s * 0.26f, s * 0.10f, shoes);
        _ui.Rect(cx + s * 0.04f, y + s * 1.79f, s * 0.26f, s * 0.10f, shoes);
    }

    private void RenderHud()
    {
        if (_city == null) return;

        float a = (float)ClientSize.Y / Math.Max(1, ClientSize.X);
        float s = 0.008f;

        if (_crossVao == 0)
        {
            _crossVao = GL.GenVertexArray();
            _crossVbo = GL.GenBuffer();
            GL.BindVertexArray(_crossVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _crossVbo);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 36, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 36, 12);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 36, 24);
            GL.EnableVertexAttribArray(2);
        }

        if (Math.Abs(a - _crossAspect) > 0.001f)
        {
            _crossAspect = a;
            float[] cross = {
                -s*a, 0f, -1f, 0f,0f,0f, 0f,1f,0f,
                 s*a, 0f, -1f, 0f,0f,0f, 0f,1f,0f,
                 0f, -s, -1f, 0f,0f,0f, 0f,1f,0f,
                 0f,  s, -1f, 0f,0f,0f, 0f,1f,0f,
            };
            GL.BindBuffer(BufferTarget.ArrayBuffer, _crossVbo);
            _crossGpuBytes = cross.Length * sizeof(float);
            GL.BufferData(BufferTarget.ArrayBuffer, _crossGpuBytes, cross, BufferUsageHint.StaticDraw);
        }

        GL.UseProgram(_shader);
        var id = Matrix4.Identity;
        GL.UniformMatrix4(_viewL, false, ref id);
        GL.UniformMatrix4(_projL, false, ref id);
        GL.UniformMatrix4(_modelL, false, ref id);
        GL.Uniform3(_colorL, 0f, 0f, 0f);

        bool depthEnabled = GL.IsEnabled(EnableCap.DepthTest);
        GL.Disable(EnableCap.DepthTest);
        GL.BindVertexArray(_crossVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, 4);
        if (depthEnabled) GL.Enable(EnableCap.DepthTest);

        // HUD text: compact enough to keep the city readable.
        _ui.Begin(ClientSize.X, ClientSize.Y);
        Vector3 textCol = new(0.9f, 0.9f, 0.9f);
        Vector3 panel = new(0.035f, 0.043f, 0.048f);
        Vector3 accent = new(0.16f, 0.48f, 0.78f);
        Vector3 warm = new(1f, 0.8f, 0.2f);
        float uiScale = 0.00435f;
        float x = 0.026f;
        float y = 0.03f;
        float line = 0.033f;

        float panelH = _city.IsInside ? 0.238f : 0.205f;
        _ui.Rect(0.014f, 0.018f, 0.245f, panelH, panel);
        _ui.Rect(0.014f, 0.018f, 0.006f, panelH, accent);

        _ui.Text($"ДЕНЬ {_city.Progress.Day}", x, y, uiScale, textCol);
        y += line;
        _ui.Text($"ОСОЗН {(int)_city.Awareness.Level}%", x, y, uiScale, textCol);
        y += line;
        _ui.Text($"ПАМ {(int)_city.Progress.Memory}  ЛЮБ {(int)_city.Progress.Curiosity}", x, y, uiScale, textCol);
        y += line;
        _ui.Text($"ЭМП {(int)_city.Progress.Empathy}  ВОЛ {(int)_city.Progress.Agency}", x, y, uiScale, textCol);
        y += line;
        _ui.Text($"МУЖ {(int)_city.Progress.Courage}", x, y, uiScale, textCol);
        y += line;
        if (_city.IsInside)
            _ui.Text($"ВНУТРИ: {_city.InteriorName()}", x, y, uiScale * 0.9f, warm);

        string msg = _city.Awareness.CurrentMessage;
        if (!string.IsNullOrEmpty(msg))
        {
            float msgSize = 0.0042f;
            float msgWidth = Math.Min(0.56f, _ui.MeasureText(msg, msgSize) + 0.035f);
            _ui.Rect(0.014f, 0.825f, msgWidth, 0.056f, panel);
            _ui.Rect(0.014f, 0.825f, 0.006f, 0.056f, warm);
            _ui.Text(msg, 0.028f, 0.842f, msgSize, warm);
        }

        // Interaction prompt
        if (_currentInteraction.Type != InteractionType.None && _dialogueNpc == null)
        {
            float pSize = 0.005f;
            float pWidth = _ui.MeasureText(_currentInteraction.Prompt, pSize);
            float ppx = 0.5f - pWidth / 2f;
            float ppy = 0.73f;
            _ui.Rect(ppx - 0.01f, ppy - 0.005f, pWidth + 0.02f, 0.044f, panel);
            _ui.Rect(ppx - 0.01f, ppy - 0.005f, 0.006f, 0.044f, accent);
            _ui.Text(_currentInteraction.Prompt, ppx, ppy + 0.004f, pSize, textCol);
        }

        RenderMiniMap();
        RenderFeedback();
        if (_dialogueNpc != null) RenderDialogue();
        _ui.Render(_shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL);
    }

    private void RenderFeedback()
    {
        if (_city == null || _city.FeedbackTimer <= 0f) return;

        float pulse = 0.65f + 0.35f * MathF.Sin(_city.FeedbackTimer * 8f);
        Vector3 color = _city.FeedbackColor * pulse;
        _ui.Rect(0.31f, 0.905f, 0.38f, 0.052f, new Vector3(0.035f, 0.043f, 0.048f));
        _ui.Rect(0.31f, 0.905f, 0.38f * Math.Clamp(_city.FeedbackTimer / 4f, 0f, 1f), 0.008f, color);

        float size = 0.0048f;
        string message = _city.FeedbackMessage.ToUpperInvariant();
        _ui.Text(message, 0.50f - _ui.MeasureText(message, size) / 2f, 0.923f, size, color);
    }

    private void RenderDialogue()
    {
        if (_dialogueNpc == null) return;

        Vector3 bg = new(0.04f, 0.05f, 0.055f);
        Vector3 border = new(0.16f, 0.48f, 0.78f);
        Vector3 textCol = new(0.88f, 0.92f, 0.93f);
        Vector3 nameCol = new(1f, 0.8f, 0.2f);
        Vector3 choiceCol = new(0.75f, 0.8f, 0.85f);
        Vector3 selectedCol = new(0.16f, 0.48f, 0.78f);

        float px = 0.08f;
        float py = 0.55f;
        float pw = 0.84f;
        float ph = 0.38f;
        float pad = 0.025f;
        float lineH = 0.036f;
        float textSize = 0.0045f;

        _ui.Rect(px, py, pw, ph, bg);
        _ui.Rect(px + 0.003f, py + 0.003f, pw - 0.006f, 0.004f, border);

        float cy = py + pad;
        _ui.Text(_dialogueNpc.Name.ToUpperInvariant(), px + pad, cy, textSize * 1.15f, nameCol);
        cy += lineH * 1.3f;

        _ui.Text(_dialogueNpcLine, px + pad, cy, textSize, textCol);
        cy += lineH * 1.8f;

        _ui.Rect(px + pad, cy, pw - pad * 2, 0.002f, new Vector3(0.1f, 0.12f, 0.13f));
        cy += lineH * 0.6f;

        for (int i = 0; i < _dialogueChoices.Length; i++)
        {
            bool sel = i == _dialogueChoiceIndex;
            float cx = px + pad;
            float cw = pw - pad * 2;
            float ch = lineH * 1.2f;

            _ui.Rect(cx, cy, cw, ch, sel ? new Vector3(0.16f, 0.22f, 0.28f) : new Vector3(0.055f, 0.065f, 0.072f));
            if (sel) _ui.Rect(cx, cy, 0.006f, ch, selectedCol);

            string label = $"{i + 1}. {_dialogueChoices[i].Text}";
            _ui.Text(label, cx + 0.012f, cy + 0.006f, textSize * 0.9f, sel ? selectedCol : choiceCol);
            cy += ch + 0.005f;
        }
    }

    private void RenderMiniMap()
    {
        if (_city?.Player == null) return;

        const float mx = 0.78f;
        const float my = 0.04f;
        const float size = 0.20f;
        const float worldRadius = 155f;

        Vector3 pp = _city.Player.Position;
        float cosR = MathF.Cos(_city.Player.Rotation);
        float sinR = MathF.Sin(_city.Player.Rotation);

        _ui.Rect(mx, my, size, size, new Vector3(0.025f, 0.035f, 0.04f));
        _ui.Rect(mx + 0.005f, my + 0.005f, size - 0.010f, size - 0.010f, new Vector3(0.06f, 0.07f, 0.075f));
        _ui.Text("КАРТА", mx + 0.07f, my + 0.01f, 0.0035f, new Vector3(0.6f, 0.7f, 0.7f));

        // Interest markers
        foreach (var marker in _city.InterestMarkers)
        {
            Vector2 p = WorldToMiniMapRotated(marker.Position, pp.X, pp.Z, cosR, sinR, mx, my, size, worldRadius);
            _ui.Rect(p.X - 0.0025f, p.Y - 0.0025f, 0.005f, 0.005f, marker.Color);
        }

        // NPC dots
        foreach (var npc in _city.Npcs)
        {
            if (npc == _city.Player || npc.State == NpcState.Sleeping) continue;
            Vector2 p = WorldToMiniMapRotated(npc.Position, pp.X, pp.Z, cosR, sinR, mx, my, size, worldRadius);
            _ui.Rect(p.X - 0.0015f, p.Y - 0.0015f, 0.003f, 0.003f, new Vector3(0.5f, 0.5f, 0.5f));
        }

        // Player direction triangle
        float triSize = 0.008f;
        float cx = mx + size * 0.5f;
        float cy = my + size * 0.5f;
        Vector3 pc = new(0.15f, 0.44f, 0.95f);
        _ui.Rect(cx - triSize * 0.5f, cy - triSize * 0.5f - triSize * 0.25f, triSize, triSize * 0.5f, pc);
        _ui.Rect(cx - triSize * 0.15f, cy + triSize * 0.25f, triSize * 0.3f, triSize * 0.2f, pc);
    }

    private static Vector2 WorldToMiniMapRotated(Vector3 world, float px, float pz, float cosR, float sinR, float mx, float my, float size, float worldRadius)
    {
        float dx = Math.Clamp((world.X - px) / worldRadius, -1f, 1f);
        float dz = Math.Clamp((world.Z - pz) / worldRadius, -1f, 1f);
        float rx = dx * cosR - dz * sinR;
        float rz = dx * sinR + dz * cosR;
        float scale = size * 0.46f;
        float px2 = mx + size * 0.5f + rx * scale;
        float py2 = my + size * 0.5f - rz * scale;
        return new Vector2(px2, py2);
    }

    private static int MakeShader()
    {
        string vs = @"#version 330 core
layout(location=0)in vec3 p;layout(location=1)in vec3 c;layout(location=2)in vec3 n;
uniform mat4 model,view,proj;uniform vec3 col,amb,light;
out vec3 fC;out vec3 fN;
void main(){vec4 w=model*vec4(p,1);gl_Position=proj*view*w;fC=col.r<-0.5?c:col;fN=mat3(model)*n;}";
        string fs = @"#version 330 core
in vec3 fC;in vec3 fN;uniform vec3 amb,light;out vec4 o;
void main(){vec3 l=normalize(light);float d=max(dot(normalize(fN),l),0);o=vec4(fC*(amb+(1-amb)*d),1);}";
        int v = CompileShader(ShaderType.VertexShader, vs);
        int f = CompileShader(ShaderType.FragmentShader, fs);
        int p = GL.CreateProgram();
        GL.AttachShader(p, v);
        GL.AttachShader(p, f);
        GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int linked);
        GL.DeleteShader(v);
        GL.DeleteShader(f);

        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(p);
            GL.DeleteProgram(p);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        return p;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);

        if (compiled != 0) return shader;

        string log = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException($"{type} compile failed: {log}");
    }

    protected override void OnResize(ResizeEventArgs e) { base.OnResize(e); GL.Viewport(0, 0, e.Width, e.Height); }

    protected override void OnUnload()
    {
        if (_runtimeProfiler is { IsComplete: false })
            _runtimeProfiler.Complete(CreateRuntimeProfileSnapshot(), interrupted: true);

        if (_profileOptions == null)
            _city?.SaveGame();
        _city?.Dispose();
        _ui.Dispose();
        _spriteRenderer?.Dispose();
        _logoSplash?.Dispose();
        if (_crossVbo != 0) GL.DeleteBuffer(_crossVbo);
        if (_crossVao != 0) GL.DeleteVertexArray(_crossVao);
        if (_shader != 0) GL.DeleteProgram(_shader);
        base.OnUnload();
    }

    private void UpdateRuntimeProfileAutomation(float dt)
    {
        if (_city?.Player == null) return;

        _profileElapsedSeconds += dt;
        float t = (float)_profileElapsedSeconds;
        float radius = 34f + 10f * MathF.Sin(t * 0.07f);
        float speed = 0.35f;
        float angle = t * speed;

        Vector3 previous = _city.Player.Position;
        Vector3 next = new(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
        Vector3 velocity = (next - previous) / Math.Max(dt, 0.0001f);
        velocity.Y = 0f;

        _city.Player.Position = next;
        _city.Player.Velocity = velocity;
        if (velocity.LengthSquared > 0.001f)
        {
            _city.Player.Rotation = MathF.Atan2(velocity.X, velocity.Z);
            _city.Player.AnimPhase += velocity.Length * 3.5f * dt;
            _city.Player.AnimBlend = Math.Clamp(velocity.Length / WalkSpeed, 0f, 1f);
        }
        else
            _city.Player.AnimBlend = 0f;
        _city.Player.State = NpcState.Walking;

        _camYaw = _city.Player.Rotation - MathF.PI + MathF.Sin(t * 0.23f) * 0.35f;
        _camPitchDegrees = Math.Clamp(DefaultCameraPitchDegrees + MathF.Sin(t * 0.17f) * 8f, MinCameraPitchDegrees, MaxCameraPitchDegrees);
        _camDist = 10f + MathF.Sin(t * 0.11f) * 2.5f;
        UpdateThirdPerson(0);
    }

    private void UpdateRuntimeProfile(double frameSeconds)
    {
        if (_runtimeProfiler == null) return;

        RuntimeProfileSnapshot snapshot = CreateRuntimeProfileSnapshot();
        _runtimeProfiler.RecordFrame(frameSeconds, snapshot);
        if (!_runtimeProfiler.ShouldComplete) return;

        _runtimeProfiler.Complete(snapshot);
        Close();
    }

    private RuntimeProfileSnapshot CreateRuntimeProfileSnapshot()
    {
        return new RuntimeProfileSnapshot(
            ElapsedSeconds: _profileElapsedSeconds,
            ManagedMemoryBytes: GC.GetTotalMemory(forceFullCollection: false),
            ThreadAllocatedBytes: GC.GetAllocatedBytesForCurrentThread(),
            EstimatedGpuBufferBytes: (_city?.EstimatedGpuBufferBytes ?? 0) + _ui.GpuBufferBytes + _crossGpuBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            NpcCount: _city?.NpcCount ?? 0,
            TimeOfDay: _city?.TimeOfDay ?? 0f);
    }

    private static RuntimeProfileGlInfo ReadGlInfo()
    {
        return new RuntimeProfileGlInfo(
            GL.GetString(StringName.Vendor) ?? "",
            GL.GetString(StringName.Renderer) ?? "",
            GL.GetString(StringName.Version) ?? "");
    }
}
