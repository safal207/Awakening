using System;
using OpenTK.Mathematics;

namespace Probuzhdenie;

public class Camera
{
    public Vector3 Pos;
    public Vector3 TargetPos;
    public Vector3 Front = -Vector3.UnitZ;
    public Vector3 Up = Vector3.UnitY;
    public Vector3 Right = Vector3.UnitX;
    public float Pitch, Yaw = -90f;

    public void UpdateFollow(float dt)
    {
        Pos = Vector3.Lerp(Pos, TargetPos, Math.Clamp(12f * dt, 0f, 1f));
    }

    public void SnapToTarget()
    {
        Pos = TargetPos;
    }

    public Matrix4 View => Matrix4.LookAt(Pos, Pos + Front, Up);
    public Matrix4 Proj(float a) => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(70f), a, 0.05f, 300f);
}
