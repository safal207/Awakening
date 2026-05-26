using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Probuzhdenie;

public class Input
{
    private readonly GameWindow _w;
    private readonly bool[] _currentKeys = new bool[(int)Keys.LastKey + 1];
    private readonly bool[] _previousKeys = new bool[(int)Keys.LastKey + 1];
    private bool _first = true;
    private Vector2 _last;
    private int _wheelZoomInFrames;
    private int _wheelZoomOutFrames;
    public float Dx { get; private set; }
    public float Dy { get; private set; }
    public float ScrollDeltaY { get; private set; }
    public bool Lmb { get; private set; }
    public bool Rmb { get; private set; }
    public bool LmbPressed { get; private set; }
    public bool RmbPressed { get; private set; }

    // Gamepad
    public bool GpConnected { get; private set; }
    public float GpLeftX { get; private set; }
    public float GpLeftY { get; private set; }
    public float GpRightX { get; private set; }
    public float GpRightY { get; private set; }
    public bool GpAPressed { get; private set; }
    public bool GpBPressed { get; private set; }
    public bool GpStartPressed { get; private set; }
    public bool GpSprintHeld { get; private set; }

    private bool _prevGpA, _prevGpB, _prevGpStart;

    public Input(GameWindow w) => _w = w;

    public void Update()
    {
        Array.Copy(_currentKeys, _previousKeys, _currentKeys.Length);

        var kb = _w.KeyboardState;
        for (int i = 0; i < _currentKeys.Length; i++)
            _currentKeys[i] = kb.IsKeyDown((Keys)i);

        var m = _w.MouseState;
        if (_first) { _last = new Vector2(m.X, m.Y); _first = false; }
        Dx = m.X - _last.X; Dy = m.Y - _last.Y;
        _last = new Vector2(m.X, m.Y);
        ScrollDeltaY = m.ScrollDelta.Y;
        if (ScrollDeltaY > 0.001f)
            _wheelZoomInFrames = Math.Min(30, _wheelZoomInFrames + (int)MathF.Ceiling(ScrollDeltaY * 8f));
        else if (ScrollDeltaY < -0.001f)
            _wheelZoomOutFrames = Math.Min(30, _wheelZoomOutFrames + (int)MathF.Ceiling(-ScrollDeltaY * 8f));

        bool left = m.IsButtonDown(MouseButton.Left);
        bool right = m.IsButtonDown(MouseButton.Right);
        LmbPressed = left && !Lmb;
        RmbPressed = right && !Rmb;
        Lmb = left;
        Rmb = right;

        // Gamepad
        GpConnected = false;
        try
        {
            if (_w.JoystickStates != null && _w.JoystickStates.Count > 0)
            {
                var js = _w.JoystickStates[0];
                int axisCount = js.AxisCount;
                if (axisCount > 0)
                {
                    GpConnected = true;
                    GpLeftX = axisCount > 0 ? DeadZone(js.GetAxis(0)) : 0f;
                    GpLeftY = axisCount > 1 ? DeadZone(js.GetAxis(1)) : 0f;
                    GpRightX = axisCount > 2 ? DeadZone(js.GetAxis(2)) : 0f;
                    GpRightY = axisCount > 3 ? DeadZone(js.GetAxis(3)) : 0f;

                    bool a = js.IsButtonDown(0);
                    bool b = js.IsButtonDown(1);
                    bool start = js.IsButtonDown(7);
                    GpSprintHeld = axisCount > 5 && js.GetAxis(5) > 0.5f;

                    GpAPressed = a && !_prevGpA;
                    GpBPressed = b && !_prevGpB;
                    GpStartPressed = start && !_prevGpStart;

                    _prevGpA = a;
                    _prevGpB = b;
                    _prevGpStart = start;
                }
            }
        }
        catch { }
    }

    public bool KeyDown(Keys key)
    {
        if (!IsTracked(key)) return false;
        if (key == Keys.J && _wheelZoomInFrames > 0)
        {
            _wheelZoomInFrames--;
            return true;
        }
        if (key == Keys.K && _wheelZoomOutFrames > 0)
        {
            _wheelZoomOutFrames--;
            return true;
        }
        return _currentKeys[(int)key];
    }

    public bool KeyPressed(Keys key)
    {
        if (!IsTracked(key)) return false;

        bool physicalKeyPressed = _currentKeys[(int)key] && !_previousKeys[(int)key];
        if (key != Keys.E) return physicalKeyPressed;

        if (physicalKeyPressed || LmbPressed) return true;
        if (!GpAPressed) return false;

        // Reuse the existing E interaction path for gamepad A, but prevent
        // the same press from immediately confirming the newly opened dialogue.
        GpAPressed = false;
        return true;
    }

    public void ResetMouse() => _first = true;

    private static bool IsTracked(Keys key) => (int)key >= 0 && (int)key <= (int)Keys.LastKey;
    private static float DeadZone(float v) => Math.Abs(v) < 0.15f ? 0f : Math.Sign(v) * (Math.Abs(v) - 0.15f) / 0.85f;
}