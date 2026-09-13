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
        Settings,
        Ending,
        NewCycleConfirmation,
        SaveFailure,
        LoadFailure,
    }

    private const string GameTitle = "Пробуждение";
    private const float MaxFrameDelta = 0.1f;
    private const float MouseYawSensitivity = MathF.PI / 1800f;
    private const float MousePitchSensitivity = 0.1f;
    private const float MinCameraPitchDegrees = 5f;
    private const float MaxCameraPitchDegrees = 65f;
    private const float DefaultCameraPitchDegrees = 18f;
    private const float ZoomSpeed = 18f;
    private const float WalkSpeed = 8f;
    private const float AutoSaveIntervalSeconds = 30f;
    private const float MaxMouseDeltaPerFrame = 40f;
    private const float MouseSmoothingSpeed = 18f;

    private readonly Camera _cam = new();
    private readonly Input _input;
    private readonly UiRenderer _ui = new();
    private readonly RuntimeProfileOptions? _profileOptions;
    private readonly RuntimeProfiler? _runtimeProfiler;
    private readonly GameSettings _settings;
    private readonly string[] _mainMenuItems = { "ПРОДОЛЖИТЬ", "НОВЫЙ ЦИКЛ", "НАСТРОЙКИ", "ВЫХОД" };
    private readonly string[] _newCycleItems = { "ОТМЕНА", "НАЧАТЬ ЗАНОВО" };
    private string _menuError = "";
    private string _saveWarning = "";
    private string _loadNotice = "";
    private readonly string[] _loadFailureItems = { "ПОВТОРИТЬ ЗАГРУЗКУ", "ВЫХОД" };
    private bool _allowUnsavedClose;
    private readonly string[] _saveFailureItems = { "ПОВТОРИТЬ СОХРАНЕНИЕ", "ВЫЙТИ БЕЗ СОХРАНЕНИЯ", "НАЗАД" };
    private readonly string[] _pauseMenuItems = { "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "ВЫХОД" };
    private readonly string[] _endingMenuItems = { "ПРОДОЛЖИТЬ ИССЛЕДОВАНИЕ", "ВЫХОД" };
    private readonly (int Width, int Height)[] _resolutions =
    {
        (1024, 576),
        (1280, 720),
        (1600, 900),
        (1920, 1080),
    };
    private CityRenderer? _city;
    private CityRenderContext _cityRenderContext;
    private int _shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL, _fogDensityL;
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
    private string _dialogueFeedback = "";
    private float _dialogueFeedbackTimer;
    private double _profileElapsedSeconds;
    private PlayerController? _playerController;
    private float _preDialogueCamDist = 8f;
    private float _preDialogueCamYaw;
    private float _preDialogueCamPitch = DefaultCameraPitchDegrees;
    private bool _dialogueCameraReturning;
    private InteractionDetector? _interactionDetector;
    private InteractionResult _currentInteraction;
    private float _camDist = 8f, _camYaw, _camPitchDegrees = DefaultCameraPitchDegrees;
    private float _smoothedMouseDx, _smoothedMouseDy;
    private readonly CharacterPreviewRenderer _heroPreview = new();
    private readonly int[] _menuViewport = new int[4];
    private float _heroPreviewTime, _heroPreviewYaw = -0.25f;
    private bool _rotatingPreview;
    private GameScreen _settingsReturnScreen = GameScreen.MainMenu;
    private bool _endingAcknowledged;

    public Game(RuntimeProfileOptions? profileOptions = null) : this(profileOptions, GameSettings.Load())
    {
    }

    private Game(RuntimeProfileOptions? profileOptions, GameSettings settings) : base(
        GameWindowSettings.Default,
        new NativeWindowSettings { Size = new Vector2i(settings.Width, settings.Height), Title = GameTitle, NumberOfSamples = 4 })
    {
        _settings = settings;
        _profileOptions = profileOptions;
        _runtimeProfiler = profileOptions != null ? new RuntimeProfiler(profileOptions) : null;
        _input = new Input(this);
        CursorState = CursorState.Normal;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = _profileOptions == null && _settings.VSync ? VSyncMode.On : VSyncMode.Off;
        if (_profileOptions == null && _settings.Fullscreen)
            WindowState = WindowState.Fullscreen;
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.Multisample);

        _shader = MakeShader();
        _modelL = GL.GetUniformLocation(_shader, "model");
        _viewL = GL.GetUniformLocation(_shader, "view");
        _projL = GL.GetUniformLocation(_shader, "proj");
        _colorL = GL.GetUniformLocation(_shader, "col");
        _ambL = GL.GetUniformLocation(_shader, "amb");
        _lightL = GL.GetUniformLocation(_shader, "light");
        _fogColL = GL.GetUniformLocation(_shader, "fogCol");
        _fogDensityL = GL.GetUniformLocation(_shader, "fogDensity");
        _cityRenderContext = new CityRenderContext(_shader, _modelL, _viewL, _projL, _colorL, _fogColL, _fogDensityL, _ambL,
            GL.GetUniformLocation(_shader,"materialMode"), GL.GetUniformLocation(_shader,"worldPass"),
            GL.GetUniformLocation(_shader,"lightMatrix"), GL.GetUniformLocation(_shader,"shadowStrength"),
            GL.GetUniformLocation(_shader,"eyePosition"),GL.GetUniformLocation(_shader,"daylight"));
        GL.UseProgram(_shader);
        GL.Uniform1(GL.GetUniformLocation(_shader,"brickTexture"),0);
        GL.Uniform1(GL.GetUniformLocation(_shader,"sunDepth"),1);


        if (_profileOptions != null)
        {
            ReplaceCity(424242,new HeroProgress(),8f,0f,null,null);
        }
        else if (!TryLoadSavedCity()) return;

        if (_runtimeProfiler != null)
        {
            ClientSize = new Vector2i(1280, 720);
            WindowState = WindowState.Normal;
            _screen = GameScreen.Playing;
            _captured = false;
            CursorState = CursorState.Normal;
            _runtimeProfiler.Start(ReadGlInfo());
            SnapCameraBehindHero();
            Console.WriteLine($"Runtime profile started for {_profileOptions!.DurationSeconds:F0}s. Report: {_profileOptions.ReportPath}");
        }

    }

    private bool TryLoadSavedCity()
    {
        try
        {
            var save = SaveSystem.Load();
            ReplaceCity(save.seed,save.progress,save.timeOfDay,save.awareness,save.npcs,save.player);
            _offlineGrowthMinutes = save.offlineMinutes;
            _loadNotice = save.notice;
            _menuError = "";
            _screen = GameScreen.MainMenu;
            _menuIndex = 0;
            return true;
        }
        catch (SaveLoadException e)
        {
            _menuError = e.Message;
            _screen = GameScreen.LoadFailure;
            _menuIndex = 0;
            return false;
        }
    }

    private void ReplaceCity(int seed, HeroProgress progress, float timeOfDay, float awareness,
        List<SaveSystem.NpcSaveData>? npcs, PlayerSaveData? player)
    {
        var next = new CityRenderer(seed,progress) { ShadowsEnabled = _profileOptions != null || _settings.Shadows };
        try
        {
            next.BuildGeometry();
            next.TimeOfDay = timeOfDay;
            next.Awareness.Restore(awareness);
            next.RestoreNpcs(npcs);
            next.RestorePlayer(player);
            next.UpdateNpcs(0f);
        }
        catch { next.Dispose(); throw; }
        _city?.Dispose();
        _city = next;
        _playerController = new PlayerController(next,_input,_cam);
        _interactionDetector = new InteractionDetector(next);
        SnapCameraBehindHero();
    }

    protected virtual void PollInput() => _input.Update();

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        if (_runtimeProfiler?.TimedOut == true)
        {
            _runtimeProfiler.Complete(CreateRuntimeProfileSnapshot(), interrupted: true);
            Close();
            return;
        }
        if (ClientSize.X <= 0 || ClientSize.Y <= 0)
        {
            System.Threading.Thread.Sleep(16);
            return;
        }
        PollInput();
        float dt = Math.Min((float)args.Time, MaxFrameDelta);

        if (_screen != GameScreen.Playing)
        {
            _heroPreviewTime += dt;
            UpdateMenu();
            return;
        }

        // Dialogue close by Escape (before global pause menu)
        if (_dialogueNpc != null && (_input.KeyPressed(Keys.Escape) || _input.GpBPressed))
        {
            EndDialogue();
            return;
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
            // Мышь + геймпад: поворот камеры вокруг игрока (сглаженный)
            float rawDx = Math.Clamp(_input.Dx, -MaxMouseDeltaPerFrame, MaxMouseDeltaPerFrame);
            float rawDy = Math.Clamp(_input.Dy, -MaxMouseDeltaPerFrame, MaxMouseDeltaPerFrame);
            float mouseSmooth = Math.Clamp(MouseSmoothingSpeed * dt, 0f, 1f);
            _smoothedMouseDx = MathHelper.Lerp(_smoothedMouseDx, rawDx, mouseSmooth);
            _smoothedMouseDy = MathHelper.Lerp(_smoothedMouseDy, rawDy, mouseSmooth);

            _camYaw -= _smoothedMouseDx * MouseYawSensitivity * _settings.MouseSensitivity;
            _camPitchDegrees = Math.Clamp(_camPitchDegrees - _smoothedMouseDy * MousePitchSensitivity * _settings.MouseSensitivity, MinCameraPitchDegrees, MaxCameraPitchDegrees);
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

        if (_city != null)
        {
            _city.CharacterViewPosition = _cam.Pos;
            _city.UpdateNpcs(dt);
        }

        if (_profileOptions == null && !_endingAcknowledged && _city is { Awareness.Level: >= 100f })
        {
            OpenEnding();
            return;
        }

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
                case InteractionType.District:
                    if (_city.InteractWithDistrict(_currentInteraction.DistrictAction))
                    {
                        _playerController?.ResetMotion();
                        if (_profileOptions == null) SaveCurrentGame();
                    }
                    break;
            }
        }

        _saveTimer += dt;
        if (_profileOptions == null && _saveTimer >= AutoSaveIntervalSeconds)
        {
            SaveCurrentGame();
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
                bool applied = _dialogueNpc.ApplyChoice(choice, _city!.Progress);
                if (applied)
                {
                    if (choice.HasQualityGain) _city.Awareness.Add(2f);
                    _city.RegisterTalk();
                }

                // Track daily objective
                bool objectiveCompleted = applied && _city.Progress.RegisterDailyTalk(_dialogueNpc.Id);

                // Build feedback text from stat deltas
                _dialogueFeedback = applied ? BuildChoiceFeedback(choice) : "Этот разговор уже остался в памяти.";
                if (objectiveCompleted)
                    _dialogueFeedback = "ЦЕЛЬ ВЫПОЛНЕНА  |  ПАМ +2  ЛЮБ +1  ВОЛ +1";
                _dialogueFeedbackTimer = 3f;

                EndDialogue();
                _dialogueTimer = 2f;
            }
        }
        else
        {
            _dialogueTimer -= dt;
            if (_dialogueFeedbackTimer > 0f)
                _dialogueFeedbackTimer -= dt;

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
        Vector2 mouse = _input.MousePosition / new Vector2(Math.Max(1, ClientSize.X), Math.Max(1, ClientSize.Y));
        if (_input.LmbPressed)
            _rotatingPreview = mouse.X >= 0.56f && mouse.X <= 0.94f && mouse.Y >= 0.24f && mouse.Y <= 0.84f;
        if (!_input.Lmb) _rotatingPreview = false;
        if (_rotatingPreview)
            _heroPreviewYaw += _input.Dx * 0.012f;
        if (_screen != GameScreen.Settings)
        {
            if (_input.KeyPressed(Keys.Left)) _heroPreviewYaw -= MathHelper.PiOver4;
            if (_input.KeyPressed(Keys.Right)) _heroPreviewYaw += MathHelper.PiOver4;
        }
        if (!_rotatingPreview && (_input.Dx != 0 || _input.Dy != 0 || _input.LmbPressed))
        {
            bool settings = _screen == GameScreen.Settings;
            for (int i = 0; i < items.Length; i++)
            {
                float y = MenuItemY(i, settings);
                if (mouse.X < 0.08f || mouse.X > 0.50f || mouse.Y < y || mouse.Y > y + MenuItemHeight(settings)) continue;
                _menuIndex = i;
                if (_input.LmbPressed) { SelectMenuItem(); return; }
                break;
            }
        }

        if (_input.KeyPressed(Keys.Escape) || _input.GpStartPressed || _input.GpBPressed)
        {
            if (_screen == GameScreen.Settings) CloseSettings();
            else if (_screen == GameScreen.NewCycleConfirmation) { _screen = GameScreen.MainMenu; _menuIndex = 1; }
            else if (_screen == GameScreen.SaveFailure) { _screen = GameScreen.PauseMenu; _menuIndex = 0; }
            else if (_screen == GameScreen.PauseMenu) ResumeGame();
            else Close();
            return;
        }

        if (_input.KeyPressed(Keys.Up) || _input.KeyPressed(Keys.W) || _input.GpLeftY < -0.5f)
            _menuIndex = (_menuIndex - 1 + items.Length) % items.Length;

        if (_input.KeyPressed(Keys.Down) || _input.KeyPressed(Keys.S) || _input.GpLeftY > 0.5f)
            _menuIndex = (_menuIndex + 1) % items.Length;

        if (_screen == GameScreen.Settings)
        {
            if (_input.KeyPressed(Keys.Left) || _input.KeyPressed(Keys.A))
                ChangeSetting(-1);
            if (_input.KeyPressed(Keys.Right) || _input.KeyPressed(Keys.D))
                ChangeSetting(1);
        }

        if (_input.KeyPressed(Keys.Enter) || _input.KeyPressed(Keys.Space) || _input.GpAPressed)
            SelectMenuItem();
    }

    private string[] CurrentMenuItems => _screen switch
    {
        GameScreen.MainMenu => _mainMenuItems,
        GameScreen.PauseMenu => _pauseMenuItems,
        GameScreen.Ending => _endingMenuItems,
        GameScreen.NewCycleConfirmation => _newCycleItems,
        GameScreen.SaveFailure => _saveFailureItems,
        GameScreen.LoadFailure => _loadFailureItems,
        GameScreen.Settings => new[]
        {
            $"РАЗРЕШЕНИЕ  {_settings.Width}x{_settings.Height}",
            $"ПОЛНЫЙ ЭКРАН  {OnOff(_settings.Fullscreen)}",
            $"ВЕРТИКАЛЬНАЯ СИНХР.  {OnOff(_settings.VSync)}",
            $"ЧУВСТВИТЕЛЬНОСТЬ  {(int)(_settings.MouseSensitivity * 100f)}%",
            $"СОЛНЕЧНЫЕ ТЕНИ  {OnOff(_settings.Shadows)}",
            "НАЗАД",
        },
        _ => Array.Empty<string>(),
    };

    private void SelectMenuItem()
    {
        if (_screen == GameScreen.LoadFailure)
        {
            if (_menuIndex == 0) TryLoadSavedCity();
            else Close();
            return;
        }
        if (_screen == GameScreen.SaveFailure)
        {
            if (_menuIndex == 0) Close();
            else if (_menuIndex == 1) { _allowUnsavedClose = true; Close(); }
            else { _screen = GameScreen.PauseMenu; _menuIndex = 0; }
            return;
        }
        if (_screen == GameScreen.NewCycleConfirmation)
        {
            if (_menuIndex == 0) { _screen = GameScreen.MainMenu; _menuIndex = 1; }
            else StartNewCycle();
            return;
        }
        if (_screen == GameScreen.Settings)
        {
            if (_menuIndex == 5)
                CloseSettings();
            else
                ChangeSetting(1);
            return;
        }

        if (_screen == GameScreen.Ending)
        {
            if (_menuIndex == 0)
            {
                _endingAcknowledged = true;
                ResumeGame();
            }
            else
            {
                Close();
            }
            return;
        }

        if (_screen == GameScreen.MainMenu)
        {
            if (_menuIndex == 0) StartGame();
            else if (_menuIndex == 1) { _screen = GameScreen.NewCycleConfirmation; _menuIndex = 0; _menuError = ""; }
            else if (_menuIndex == 2) OpenSettings(GameScreen.MainMenu);
            else Close();
            return;
        }

        if (_menuIndex == 0) ResumeGame();
        else if (_menuIndex == 1) OpenSettings(GameScreen.PauseMenu);
        else Close();
    }

    private static string OnOff(bool enabled) => enabled ? "ВКЛ" : "ВЫКЛ";

    private void OpenSettings(GameScreen returnScreen)
    {
        _settingsReturnScreen = returnScreen;
        _screen = GameScreen.Settings;
        _menuIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
        Title = $"{GameTitle} - Настройки";
    }

    private void CloseSettings()
    {
        _settings.Save();
        _screen = _settingsReturnScreen;
        _menuIndex = 0;
        Title = _screen == GameScreen.PauseMenu ? $"{GameTitle} - Меню" : GameTitle;
    }

    private void ChangeSetting(int direction)
    {
        switch (_menuIndex)
        {
            case 0:
                int current = Array.FindIndex(_resolutions,
                    resolution => resolution.Width == _settings.Width && resolution.Height == _settings.Height);
                if (current < 0) current = 1;
                current = (current + Math.Sign(direction) + _resolutions.Length) % _resolutions.Length;
                (_settings.Width, _settings.Height) = _resolutions[current];
                if (WindowState != WindowState.Fullscreen)
                    Size = new Vector2i(_settings.Width, _settings.Height);
                break;
            case 1:
                _settings.Fullscreen = !_settings.Fullscreen;
                WindowState = _settings.Fullscreen ? WindowState.Fullscreen : WindowState.Normal;
                if (!_settings.Fullscreen)
                    Size = new Vector2i(_settings.Width, _settings.Height);
                break;
            case 2:
                _settings.VSync = !_settings.VSync;
                VSync = _settings.VSync ? VSyncMode.On : VSyncMode.Off;
                break;
            case 3:
                _settings.MouseSensitivity += Math.Sign(direction) * 0.1f;
                _settings.MouseSensitivity = Math.Clamp(_settings.MouseSensitivity, 0.5f, 2f);
                break;
            case 4:
                _settings.Shadows = !_settings.Shadows;
                if (_city != null) _city.ShadowsEnabled = _settings.Shadows;
                break;
        }

        _settings.Save();
    }

    private void StartNewCycle()
    {
        if (_city == null || !_city.SaveGame())
        {
            _menuError = "Не удалось сохранить текущий цикл.";
            return;
        }
        if (!SaveSystem.TryBackupForNewCycle(out _menuError)) return;

        var previousLedger = MemoryRuntime.Current;
        int previousHero = MemoryRuntime.HeroId;
        CityRenderer? next = null;
        try
        {
            MemoryRuntime.Reset();
            next = new CityRenderer(Environment.TickCount) { ShadowsEnabled = _settings.Shadows };
            next.BuildGeometry();
            next.UpdateNpcs(0);
            if (!next.SaveGame()) throw new InvalidOperationException("Could not save the new cycle.");
        }
        catch (Exception e)
        {
            next?.Dispose();
            MemoryRuntime.Replace(previousLedger);
            MemoryRuntime.HeroId = previousHero;
            Console.WriteLine(e);
            _menuError = "Новый цикл не создан. Прежний прогресс сохранён.";
            return;
        }

        _city.Dispose();
        _city = next;
        _playerController = new PlayerController(next, _input, _cam);
        _interactionDetector = new InteractionDetector(next);
        _dialogueNpc = null;
        _dialogueChoices = Array.Empty<DialogueChoice>();
        _currentInteraction = default;
        _dialogueTimer = _dialogueFeedbackTimer = _saveTimer = 0;
        _dialogueFeedback = "";
        _dialogueCameraReturning = _endingAcknowledged = false;
        _offlineGrowthMinutes = 0;
        _menuError = "";
        _saveWarning = "";
        _loadNotice = "";
        StartGame();
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
        SaveCurrentGame();
        _screen = GameScreen.PauseMenu;
        _menuIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
        Title = $"{GameTitle} - Меню";
    }

    private void OpenEnding()
    {
        SaveCurrentGame();
        _screen = GameScreen.Ending;
        _menuIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
        Title = $"{GameTitle} - Финал";
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
        _smoothedMouseDx = 0f;
        _smoothedMouseDy = 0f;
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

        // Dialogue camera: save current orbit state, dialogue framing is pair-based
        if (_city?.Player != null)
        {
            _preDialogueCamDist = _camDist;
            _preDialogueCamYaw = _camYaw;
            _preDialogueCamPitch = _camPitchDegrees;
            _dialogueCameraReturning = false;
        }
    }

    private void EndDialogue()
    {
        _dialogueNpc = null;
        _dialogueCameraReturning = true;
        CaptureMouse();
    }

    private void SnapCameraBehindHero()
    {
        if (_city?.Player == null) return;
        _camYaw = _city.Player.Rotation - MathF.PI;
        _camPitchDegrees = DefaultCameraPitchDegrees;
        _camDist = 8f;
        UpdateThirdPerson(0);
        _cam.SnapToTarget();
    }

    private static string BuildChoiceFeedback(DialogueChoice choice)
    {
        var parts = new List<string>();

        if (MathF.Abs(choice.FriendlinessDelta) > 0.01f)
            parts.Add(choice.FriendlinessDelta > 0 ? $"ДОВ +{choice.FriendlinessDelta:F0}" : $"ДОВ {choice.FriendlinessDelta:F0}");
        if (MathF.Abs(choice.TrustDelta) > 0.01f)
            parts.Add(choice.TrustDelta > 0 ? $"УВ +{choice.TrustDelta:F0}" : $"УВ {choice.TrustDelta:F0}");
        if (choice.MemoryGain > 0.01f)
            parts.Add($"ПАМ +{choice.MemoryGain:F0}");
        if (choice.CuriosityGain > 0.01f)
            parts.Add($"ЛЮБ +{choice.CuriosityGain:F0}");
        if (choice.EmpathyGain > 0.01f)
            parts.Add($"ЭМП +{choice.EmpathyGain:F0}");
        if (choice.AgencyGain > 0.01f)
            parts.Add($"ВОЛ +{choice.AgencyGain:F0}");
        if (choice.CourageGain > 0.01f)
            parts.Add($"МУЖ +{choice.CourageGain:F0}");

        if (parts.Count == 0)
            return "";

        // Max 3 per line, split into at most 2 lines
        if (parts.Count <= 3)
            return string.Join("  ", parts);

        return string.Join("  ", parts[0], parts[1], parts[2]) + "\n" +
               string.Join("  ", parts.GetRange(3, parts.Count - 3));
    }

    private void UpdateThirdPerson(float dt)
    {
        if (_city?.Player == null) return;
        Vector3 p = _city.Player.Position;
        float playerH = _city.Player.Height;
        float lerpFactor = Math.Clamp(6f * dt, 0f, 1f);

        // === DIALOGUE FRAMING CAMERA ===
        if (_dialogueNpc != null)
        {
            Vector3 npcPos = _dialogueNpc.Position;
            float npcH = _dialogueNpc.Height;

            // Direction from player to NPC (horizontal)
            Vector3 pairDir = npcPos - p;
            pairDir.Y = 0f;
            float pairLen = pairDir.Length;
            if (pairLen < 0.01f) pairDir = Vector3.UnitZ;
            else pairDir /= pairLen;

            // Midpoint between player and NPC
            Vector3 midpoint = (p + npcPos) * 0.5f;

            // Side vector for camera offset
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, pairDir));

            // Camera position: behind the midpoint, offset to the side, elevated
            float camHeight = Math.Max(playerH, npcH) * 0.95f;
            Vector3 camTarget = midpoint - pairDir * (pairLen * 0.25f + 1.5f)
                               + side * (pairLen * 0.15f + 0.8f)
                               + new Vector3(0, camHeight, 0);

            // Look at NPC head area (upper third of screen)
            Vector3 lookTarget = npcPos + new Vector3(0, npcH * 0.78f, 0);

            // Smooth lerp camera position and look target
            camTarget = _city.ResolveCameraPosition(lookTarget, camTarget);
            _cam.TargetPos = Vector3.Lerp(_cam.TargetPos, camTarget, lerpFactor);
            _cam.UpdateFollow(dt);
            _cam.Pos = _city.ResolveCameraPosition(lookTarget, _cam.Pos);
            _city.HidePlayerForCamera = Vector3.DistanceSquared(lookTarget, _cam.Pos) < 2.4f * 2.4f;
            _cam.Front = Vector3.Normalize(lookTarget - _cam.Pos);
            _cam.Right = Vector3.Normalize(Vector3.Cross(_cam.Front, Vector3.UnitY));
            _cam.Up = Vector3.Normalize(Vector3.Cross(_cam.Right, _cam.Front));
            return;
        }

        // === NORMAL / RETURNING CAMERA ===
        if (_dialogueCameraReturning)
        {
            _camDist = MathHelper.Lerp(_camDist, _preDialogueCamDist, lerpFactor);
            _camYaw = MathHelper.Lerp(_camYaw, _preDialogueCamYaw, lerpFactor);
            _camPitchDegrees = MathHelper.Lerp(_camPitchDegrees, _preDialogueCamPitch, lerpFactor);
            if (MathF.Abs(_camDist - _preDialogueCamDist) < 0.05f)
            {
                _camDist = _preDialogueCamDist;
                _camYaw = _preDialogueCamYaw;
                _camPitchDegrees = _preDialogueCamPitch;
                _dialogueCameraReturning = false;
            }
        }

        float yr = _camYaw, pr = MathHelper.DegreesToRadians(_camPitchDegrees);
        Vector3 lookAt = p + new Vector3(0, playerH * 0.7f, 0);
        Vector3 desiredCamera = lookAt + new Vector3(
            _camDist * MathF.Cos(pr) * MathF.Sin(yr),
            _camDist * MathF.Sin(pr),
            _camDist * MathF.Cos(pr) * MathF.Cos(yr));
        _cam.TargetPos = _city.ResolveCameraPosition(lookAt, desiredCamera);
        _cam.Yaw = MathHelper.RadiansToDegrees(_camYaw);
        _cam.Pitch = _camPitchDegrees;
        _cam.UpdateFollow(dt);
        _cam.Pos = _city.ResolveCameraPosition(lookAt, _cam.Pos);
        _city.HidePlayerForCamera = Vector3.DistanceSquared(lookAt, _cam.Pos) < 2.4f * 2.4f;
        _cam.Front = Vector3.Normalize(lookAt - _cam.Pos);
        _cam.Right = Vector3.Normalize(Vector3.Cross(_cam.Front, Vector3.UnitY));
        _cam.Up = Vector3.Normalize(Vector3.Cross(_cam.Right, _cam.Front));
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        if (ClientSize.X <= 0 || ClientSize.Y <= 0) return;

        float t = _city?.TimeOfDay ?? 8f;
        SceneLighting lighting = SceneLighting.At(t);
        GL.ClearColor(lighting.Sky.X,lighting.Sky.Y,lighting.Sky.Z,1);
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

            GL.Uniform3(_ambL,lighting.Ambient);
            GL.Uniform3(_lightL,lighting.Sun);

            Vector3 fogCol = lighting.Sky;
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

    private static float MenuItemY(int index, bool settings) => 0.40f + index * (settings ? 0.075f : 0.105f);
    private static float MenuItemHeight(bool settings) => settings ? 0.058f : 0.074f;

    private void RenderMenu()
    {
        _ui.Begin(ClientSize.X, ClientSize.Y);
        Vector3 background = new(0.055f, 0.060f, 0.062f);
        Vector3 accent = new(0.18f, 0.48f, 0.74f);
        Vector3 text = new(0.91f, 0.93f, 0.92f);
        Vector3 dim = new(0.60f, 0.64f, 0.65f);
        Vector3 warm = new(0.87f, 0.71f, 0.44f);
        _ui.Rect(0, 0, 1, 1, background);
        _ui.Rect(0.55f, 0, 0.45f, 1, new Vector3(0.085f, 0.077f, 0.080f));
        _ui.Rect(0.08f, 0.20f, 0.84f, 0.002f, new Vector3(0.20f, 0.22f, 0.23f));
        DrawMenuText("ПРОБУЖДЕНИЕ", 0.08f, 0.095f, 0.008f, 0.84f, text);

        string heading = _screen switch
        {
            GameScreen.PauseMenu => "ПАУЗА",
            GameScreen.Settings => "НАСТРОЙКИ",
            GameScreen.Ending => "ТЫ ПРОСНУЛСЯ",
            GameScreen.NewCycleConfirmation => "НАЧАТЬ С ПЕРВОГО УТРА?",
            GameScreen.SaveFailure => "СОХРАНЕНИЕ НЕ УДАЛОСЬ",
            GameScreen.LoadFailure => "ПРОГРЕСС НЕ ЗАГРУЖЕН",
            _ => $"ДЕНЬ {_city?.Progress.Day ?? 1}",
        };
        DrawMenuText(heading, 0.08f, 0.27f, 0.0044f, 0.42f, dim);
        if (_screen == GameScreen.Ending)
            DrawMenuText("ТЕПЕРЬ ВЫБОР ЗА ТОБОЙ.", 0.08f, 0.33f, 0.003f, 0.42f, warm);
        else if (_screen == GameScreen.NewCycleConfirmation)
            DrawMenuText("ТЕКУЩИЙ ПРОГРЕСС ОСТАНЕТСЯ В КОПИИ.", 0.08f, 0.33f, 0.003f, 0.42f, warm);
        else if (_screen == GameScreen.SaveFailure)
            DrawMenuText("ПОСЛЕДНИЕ ИЗМЕНЕНИЯ НЕ ЗАПИСАНЫ.", 0.08f, 0.33f, 0.003f, 0.42f, warm);
        else if (_screen == GameScreen.LoadFailure)
            DrawMenuText("НОВЫЙ ЦИКЛ НЕ СОЗДАН.", 0.08f, 0.33f, 0.003f, 0.42f, warm);
        else if (_screen == GameScreen.MainMenu && _offlineGrowthMinutes >= 1)
            DrawMenuText($"ГЕРОЙ РОС {Math.Ceiling(_offlineGrowthMinutes)} МИН",
                0.08f, 0.33f, 0.003f, 0.42f, warm);

        bool settings = _screen == GameScreen.Settings;
        string[] items = CurrentMenuItems;
        for (int i = 0; i < items.Length; i++)
        {
            float y = MenuItemY(i, settings), height = MenuItemHeight(settings);
            bool selected = i == _menuIndex;
            _ui.Rect(0.08f, y, 0.42f, height, selected ? accent : new Vector3(0.105f, 0.12f, 0.13f));
            if (selected) _ui.Rect(0.08f, y, 0.003f, height, warm);
            float size = Math.Min(settings ? 0.0033f : 0.0042f,
                0.37f / Math.Max(0.001f, _ui.MeasureText(items[i], 1)));
            _ui.Text(items[i], 0.104f, y + (height - size * 7) * 0.5f, size, selected ? text : dim);
        }
        if (_city?.Player != null)
        {
            DrawMenuText("ГЕРОЙ", 0.60f, 0.87f, 0.0035f, 0.30f, dim);
            _ui.Rect(0.60f, 0.92f, 0.29f, 0.002f, accent);
        }
        if (!string.IsNullOrEmpty(_menuError)) DrawMenuText(_menuError,0.08f,0.86f,0.003f,0.42f,warm);
        else if (!string.IsNullOrEmpty(_saveWarning)) DrawMenuText(_saveWarning,0.08f,0.86f,0.003f,0.42f,warm);
        else if (!string.IsNullOrEmpty(_loadNotice)) DrawMenuText(_loadNotice,0.08f,0.86f,0.003f,0.42f,warm);
        _ui.Render(_shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL);

        if (_city?.Player != null)
        {
            // UI coordinates are top-down; OpenGL viewports start at the bottom.
            GL.GetInteger(GetPName.Viewport, _menuViewport);
            var bounds = new Box2i(
                (int)(_menuViewport[2] * 0.56f), (int)(_menuViewport[3] * 0.16f),
                (int)(_menuViewport[2] * 0.94f), (int)(_menuViewport[3] * 0.76f));
            _heroPreview.Render(_city.Player, _heroPreviewTime, _heroPreviewYaw,
                bounds, _cityRenderContext, _ambL, _lightL);
        }
    }

    private void DrawMenuText(string text, float x, float y, float size, float width, Vector3 color)
    {
        size = Math.Min(size, width / Math.Max(0.001f, _ui.MeasureText(text, 1)));
        _ui.Text(text, x, y, size, color);
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
        float uiScale = Math.Min(0.00435f, 0.218f / _ui.MeasureText("ПАМ 100  ЛЮБ 100", 1f));
        float x = 0.026f;
        float y = 0.03f;
        float line = 0.033f;

        float panelH = _city.IsInside ? 0.278f : 0.245f;
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

        // Daily objective
        if (_city.Progress.DailyObjectiveCompleted)
        {
            _ui.Text($"ЦЕЛЬ {HeroProgress.DailyTalkGoal}/{HeroProgress.DailyTalkGoal}", x, y, uiScale, new Vector3(0.3f, 0.8f, 0.4f));
        }
        else
        {
            _ui.Text($"ЦЕЛЬ {_city.Progress.DailyTalkProgress}/{HeroProgress.DailyTalkGoal}", x, y, uiScale, warm);
        }
        y += line;

        if (_city.IsInside)
            _ui.Text($"ВНУТРИ: {_city.InteriorName()}", x, y, uiScale * 0.9f, warm);

        string msg = _city.Awareness.CurrentMessage;
        if (!string.IsNullOrEmpty(msg) && _dialogueNpc == null)
        {
            float msgSize = Math.Min(0.0042f, 0.525f / Math.Max(1f, _ui.MeasureText(msg,1f)));
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
        if (!string.IsNullOrEmpty(_saveWarning)) DrawMenuText(_saveWarning,0.28f,0.035f,0.0028f,0.45f,warm);
        RenderFeedback();
        if (_dialogueNpc != null) RenderDialogue();
        RenderDialogueFeedback();
        _ui.Render(_shader, _modelL, _viewL, _projL, _colorL, _ambL, _lightL, _fogColL);
    }

    private void RenderFeedback()
    {
        if (_city == null || _city.FeedbackTimer <= 0f || _dialogueNpc != null) return;

        float pulse = 0.65f + 0.35f * MathF.Sin(_city.FeedbackTimer * 8f);
        Vector3 color = _city.FeedbackColor * pulse;
        float size = 0.0033f;
        string message = _city.FeedbackMessage.ToUpperInvariant();
        var lines = _ui.WrapText(message, size, 0.86f);
        float top = 0.985f - lines.Count * 0.029f - 0.02f;
        _ui.Rect(0.06f, top, 0.88f, 0.985f-top, new Vector3(0.035f, 0.043f, 0.048f));
        for (int i = 0; i < lines.Count; i++) _ui.Text(lines[i],0.07f,top+0.01f+i*0.029f,size,color);
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
        float py;
        float pw = 0.84f;
        float ph;
        float pad = 0.025f;
        float lineH = 0.036f;
        float textSize = 0.0045f;
        var speech = _ui.WrapText(_dialogueNpcLine, textSize, pw-pad*2);
        while (speech.Count > 3)
        {
            textSize *= 0.95f;
            speech = _ui.WrapText(_dialogueNpcLine, textSize, pw-pad*2);
        }
        ph = pad*2 + lineH*(1.9f+speech.Count) + _dialogueChoices.Length*(lineH*1.2f+0.005f);
        py = 0.95f-ph;

        _ui.Rect(px, py, pw, ph, bg);
        _ui.Rect(px + 0.003f, py + 0.003f, pw - 0.006f, 0.004f, border);

        float cy = py + pad;
        _ui.Text(_dialogueNpc.Name.ToUpperInvariant(), px + pad, cy, textSize * 1.15f, nameCol);
        cy += lineH * 1.3f;

        foreach (string speechLine in speech)
        {
            _ui.Text(speechLine, px+pad, cy, textSize, textCol);
            cy += lineH;
        }

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
            float choiceSize = Math.Min(textSize*0.9f, (cw-0.024f) / Math.Max(1f,_ui.MeasureText(label,1f)));
            _ui.Text(label, cx + 0.012f, cy + 0.006f, choiceSize, sel ? selectedCol : choiceCol);
            cy += ch + 0.005f;
        }
    }

    private void RenderDialogueFeedback()
    {
        if (_dialogueFeedbackTimer <= 0f || string.IsNullOrEmpty(_dialogueFeedback)) return;

        float alpha = MathF.Min(1f, _dialogueFeedbackTimer / 0.5f);
        Vector3 color = new(0.25f, 0.85f, 1.0f);
        float size = 0.0038f;

        string[] lines = _dialogueFeedback.Split('\n');
        float maxW = 0f;
        foreach (var line in lines)
        {
            float w = _ui.MeasureText(line, size);
            if (w > maxW) maxW = w;
        }

        float fx = 0.5f - maxW / 2f;
        float fy = 0.88f;
        float lineH = 0.028f;
        float boxH = lines.Length * lineH + 0.012f;

        _ui.Rect(fx - 0.015f, fy - 0.006f, maxW + 0.03f, boxH, new Vector3(0.03f, 0.035f, 0.04f));
        for (int i = 0; i < lines.Length; i++)
        {
            _ui.Text(lines[i], fx, fy + i * lineH, size, color * alpha);
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
        var objective = _city.Progress.DistrictEpisode.Objective(_city.Progress.Day);
        Vector2 target = WorldToMiniMapRotated(objective.position, pp.X, pp.Z, cosR, sinR, mx, my, size, worldRadius);
        _ui.Rect(target.X-0.004f, target.Y-0.004f, 0.008f, 0.008f, new Vector3(0.96f,0.72f,0.25f));
        string objectiveText = objective.name + " " + ((int)Vector3.Distance(pp, objective.position)) + " м";
        float objectiveSize = Math.Min(0.0026f, (size-0.01f) / Math.Max(1f, _ui.MeasureText(objectiveText, 1f)));
        _ui.Text(objectiveText, mx, my+size+0.012f, objectiveSize, new Vector3(0.96f,0.8f,0.45f));

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
        string vs = WorldShader.Vertex;
        string fs = WorldShader.Fragment;
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

    private bool SaveCurrentGame()
    {
        bool saved = _city?.SaveGame() ?? true;
        _saveWarning = saved ? "" : "Прогресс не сохранён. Запись будет повторена.";
        _saveTimer = saved ? 0f : AutoSaveIntervalSeconds - 5f;
        return saved;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _profileOptions != null || _allowUnsavedClose || SaveCurrentGame()) return;
        e.Cancel = true;
        _screen = GameScreen.SaveFailure;
        _menuIndex = 0;
        _captured = false;
        CursorState = CursorState.Normal;
    }

    protected override void OnUnload()
    {
        if (_runtimeProfiler is { IsComplete: false })
            _runtimeProfiler.Complete(CreateRuntimeProfileSnapshot(), interrupted: true);

        _city?.Dispose();
        _ui.Dispose();
        _heroPreview.Dispose();
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

        _runtimeProfiler.Complete(snapshot, interrupted: _runtimeProfiler.TimedOut);
        Close();
    }

    private RuntimeProfileSnapshot CreateRuntimeProfileSnapshot()
    {
        return new RuntimeProfileSnapshot(
            ElapsedSeconds: _profileElapsedSeconds,
            ManagedMemoryBytes: GC.GetTotalMemory(forceFullCollection: false),
            ThreadAllocatedBytes: GC.GetAllocatedBytesForCurrentThread(),
            EstimatedGpuBufferBytes: (_city?.EstimatedGpuBufferBytes ?? 0) + _ui.GpuBufferBytes + _crossGpuBytes + _heroPreview.GpuBufferBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            NpcCount: _city?.NpcCount ?? 0,
            TimeOfDay: _city?.TimeOfDay ?? 0f,
            EstimatedGpuTextureBytes: _city?.EstimatedGpuTextureBytes ?? 0);
    }

    private static RuntimeProfileGlInfo ReadGlInfo()
    {
        return new RuntimeProfileGlInfo(
            GL.GetString(StringName.Vendor) ?? "",
            GL.GetString(StringName.Renderer) ?? "",
            GL.GetString(StringName.Version) ?? "");
    }
}
