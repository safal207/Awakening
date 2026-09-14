using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public sealed class CharacterPreviewRenderer : IDisposable
{
    private readonly CharacterMesh _mesh = new();
    private readonly int[] _viewport = new int[4];
    private readonly int[] _scissor = new int[4];
    private int _vao, _vbo, _capacity;
    public int GpuBufferBytes => _capacity;

    public unsafe void Render(NpcCharacter hero, float time, float yaw, Box2i bounds,
        CityRenderContext context, int ambientLocation, int lightLocation)
    {
        if (bounds.Size.X < 1 || bounds.Size.Y < 1) return;
        _mesh.Clear();
        _mesh.Append(hero, time, Vector3.Zero, yaw, 0, showAwareness: false);
        if (_mesh.VertexCount == 0) return;
        GL.GetInteger(GetPName.Viewport, _viewport);
        GL.GetInteger(GetPName.ScissorBox, _scissor);
        bool depth = GL.IsEnabled(EnableCap.DepthTest);
        bool cull = GL.IsEnabled(EnableCap.CullFace);
        bool scissor = GL.IsEnabled(EnableCap.ScissorTest);
        GL.GetBoolean(GetPName.DepthWritemask, out bool depthWrite);
        try
        {
            GL.Viewport(bounds.Min.X, bounds.Min.Y, bounds.Size.X, bounds.Size.Y);
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(bounds.Min.X, bounds.Min.Y, bounds.Size.X, bounds.Size.Y);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            if (_vao == 0)
            {
                _vao = GL.GenVertexArray();
                _vbo = GL.GenBuffer();
                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                for (int i = 0; i < 3; i++)
                {
                    GL.VertexAttribPointer(i, 3, VertexAttribPointerType.Float, false, 36, i * 12);
                    GL.EnableVertexAttribArray(i);
                }
            }
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            int bytes = _mesh.FloatCount * sizeof(float);
            if (bytes > _capacity)
            {
                _capacity = Math.Max(bytes, _capacity * 2);
                GL.BufferData(BufferTarget.ArrayBuffer, _capacity, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }
            fixed (float* data = _mesh.Data)
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, (IntPtr)data);

            float aspect = bounds.Size.X / (float)bounds.Size.Y;
            float height = Math.Max(hero.Height * 1.14f, hero.Height * 0.55f / aspect);
            var view = Matrix4.LookAt(new Vector3(0, hero.Height * 0.5f, 4),
                new Vector3(0, hero.Height * 0.5f, 0), Vector3.UnitY);
            var projection = Matrix4.CreateOrthographic(height * aspect, height, 0.1f, 10);
            var model = Matrix4.Identity;
            GL.UseProgram(context.Shader);
            GL.Uniform1(context.FogDensityLocation,0f);
            GL.UniformMatrix4(context.ModelLocation, false, ref model);
            GL.UniformMatrix4(context.ViewLocation, false, ref view);
            GL.UniformMatrix4(context.ProjectionLocation, false, ref projection);
            GL.Uniform3(context.ColorLocation, -1f, -1f, -1f);
            GL.Uniform3(ambientLocation, 0.62f, 0.62f, 0.62f);
            GL.Uniform3(lightLocation, -0.5f, 0.7f, 1f);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _mesh.VertexCount);
        }
        finally
        {
            GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
            GL.Scissor(_scissor[0], _scissor[1], _scissor[2], _scissor[3]);
            GL.DepthMask(depthWrite);
            Set(EnableCap.DepthTest, depth);
            Set(EnableCap.CullFace, cull);
            Set(EnableCap.ScissorTest, scissor);
        }
    }

    private static void Set(EnableCap cap, bool enabled)
    {
        if (enabled) GL.Enable(cap); else GL.Disable(cap);
    }

    public void Dispose()
    {
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        _vbo = _vao = _capacity = 0;
    }
}
