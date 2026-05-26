using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Probuzhdenie.FreeCity;

namespace Probuzhdenie.Player;

public sealed class PlayerController
{
    private const float WalkSpeed = 8f;
    private const float SprintMultiplier = 2.5f;
    private const float Accel = 20f;
    private const float Decel = 30f;
    private const float RotSpeed = 8f;

    private readonly CityRenderer _city;
    private readonly Input _input;
    private readonly Camera _cam;

    private Vector3 _targetVel;
    private float _currentSpeed;

    public PlayerController(CityRenderer city, Input input, Camera cam)
    {
        _city = city;
        _input = input;
        _cam = cam;
    }

    public PlayerState CurrentState { get; private set; }
    public float CurrentSpeed => _currentSpeed;

    public void Update(float dt)
    {
        var player = _city.Player;
        if (player == null) return;

        // Camera-relative direction vectors
        Vector3 fwd = new(_cam.Front.X, 0f, _cam.Front.Z);
        if (fwd.LengthSquared < 0.001f)
            fwd = Vector3.UnitZ;
        else
            fwd.Normalize();

        Vector3 right = new(_cam.Right.X, 0f, _cam.Right.Z);
        if (right.LengthSquared < 0.001f)
            right = Vector3.UnitX;
        else
            right.Normalize();

        // Read input intent
        Vector3 move = Vector3.Zero;
        if (_input.KeyDown(Keys.W)) move += fwd;
        if (_input.KeyDown(Keys.S)) move -= fwd;
        if (_input.KeyDown(Keys.A)) move -= right;
        if (_input.KeyDown(Keys.D)) move += right;
        if (_input.GpConnected)
            move += fwd * (-_input.GpLeftY) + right * _input.GpLeftX;

        bool sprint = _input.KeyDown(Keys.LeftShift) || _input.GpSprintHeld;

        // Apply movement
        if (move.LengthSquared > 0.1f)
        {
            if (move.LengthSquared > 1f) move.Normalize();
            _targetVel = move * WalkSpeed;

            float targetSpeed = WalkSpeed;
            if (sprint) targetSpeed *= SprintMultiplier;

            _currentSpeed = _currentSpeed < targetSpeed
                ? Math.Min(targetSpeed, _currentSpeed + Accel * dt)
                : Math.Max(targetSpeed, _currentSpeed - Decel * dt);

            Vector3 desiredPos = player.Position + _targetVel.Normalized() * _currentSpeed * dt;
            desiredPos = _city.ClampToWalkable(desiredPos, 0.3f);
            desiredPos = _city.AdjustForNpcCollision(desiredPos, 0.3f, player);
            Vector3 actualMove = desiredPos - player.Position;
            player.Position = desiredPos;
            player.Velocity = actualMove / Math.Max(dt, 0.0001f);

            float targetRot = MathF.Atan2(_targetVel.X, _targetVel.Z);
            float rotDiff = targetRot - player.Rotation;
            if (rotDiff > MathF.PI) rotDiff -= MathHelper.TwoPi;
            else if (rotDiff < -MathF.PI) rotDiff += MathHelper.TwoPi;
            player.Rotation += rotDiff * Math.Clamp(RotSpeed * dt, 0f, 1f);

            player.State = NpcState.Walking;
            CurrentState = sprint ? PlayerState.Sprinting : PlayerState.Walking;
        }
        else
        {
            _targetVel = Vector3.Zero;
            _currentSpeed = Math.Max(0f, _currentSpeed - Decel * dt);
            if (_currentSpeed > 0.01f)
            {
                Vector3 desiredPos = player.Position + player.Velocity.Normalized() * _currentSpeed * dt;
                desiredPos = _city.ClampToWalkable(desiredPos, 0.3f);
                desiredPos = _city.AdjustForNpcCollision(desiredPos, 0.3f, player);
                player.Position = desiredPos;
                player.Velocity = (player.Velocity.Normalized() * _currentSpeed);
            }
            else
            {
                _currentSpeed = 0f;
                player.Velocity = Vector3.Zero;
                if (player.State == NpcState.Walking)
                    player.State = NpcState.Relaxing;
                CurrentState = PlayerState.Idle;
            }
        }

        // Animation
        player.AnimPhase += _currentSpeed * 3.5f * dt;
        player.AnimBlend = Math.Clamp(_currentSpeed / WalkSpeed, 0f, 1f);
    }

    public void ResetMotion()
    {
        _targetVel = Vector3.Zero;
        _currentSpeed = 0f;
        if (_city.Player != null)
        {
            _city.Player.Velocity = Vector3.Zero;
            _city.Player.State = NpcState.Relaxing;
        }
        CurrentState = PlayerState.Idle;
    }
}
