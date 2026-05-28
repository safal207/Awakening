using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Probuzhdenie;

public sealed class SpriteRenderer : IDisposable
{
    private const int FloatsPerVertex = 7;
    private const int VertexStrideBytes = FloatsPerVertex * sizeof(float);

    private readonly List<float> _verts = new(4096);
    private int _vao, _vbo;
    private int _bufferCapacityBytes;
    private int _shader;
    private int _viewL, _projL, _tintL, _texL, _alphaL;
    private bool _started;

    public SpriteRenderer()
    {
        _shader = CompileShader();
        _viewL = GL.GetUniformLocation(_shader, "view");
        _projL = GL.GetUniformLocation(_shader, "proj");
        _tintL = GL.GetUniformLocation(_shader, "tint");
        _texL = GL.GetUniformLocation(_shader, "tex");
        _alphaL = GL.GetUniformLocation(_shader, "alpha");
    }

    public void Begin()
    {
        _verts.Clear();
        _started = true;
    }

    public void Add(Vector3 center, float width, float height, Vector3 tint, float alpha = 1f)
    {
        if (!_started) return;
        float hw = width * 0.5f, hh = height * 0.5f;
        AddVert(center, 0f, 0f, hw, hh, tint, alpha);
        AddVert(center, 1f, 0f, hw, hh, tint, alpha);
        AddVert(center, 1f, 1f, hw, hh, tint, alpha);
        AddVert(center, 0f, 0f, hw, hh, tint, alpha);
        AddVert(center, 1f, 1f, hw, hh, tint, alpha);
        AddVert(center, 0f, 1f, hw, hh, tint, alpha);
    }

    private void AddVert(Vector3 center, float u, float v, float hw, float hh, Vector3 tint, float alpha)
    {
        _verts.Add(center.X); _verts.Add(center.Y); _verts.Add(center.Z);
        _verts.Add(u); _verts.Add(v);
        _verts.Add(hw); _verts.Add(hh);
    }

    public void Flush(ref Matrix4 view, ref Matrix4 proj)
    {
        if (_verts.Count == 0) return;

        EnsureGlObjects();
        GL.UseProgram(_shader);
        GL.UniformMatrix4(_viewL, false, ref view);
        GL.UniformMatrix4(_projL, false, ref proj);
        GL.Uniform1(_texL, 0);
        GL.Uniform1(_alphaL, 1f);
        GL.Uniform3(_tintL, 1f, 1f, 1f);

        bool depthEnabled = GL.IsEnabled(EnableCap.DepthTest);
        bool cullEnabled = GL.IsEnabled(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        int byteCount = _verts.Count * sizeof(float);
        if (byteCount > _bufferCapacityBytes)
        {
            _bufferCapacityBytes = Math.Max(byteCount, Math.Max(4096, _bufferCapacityBytes * 2));
            GL.BufferData(BufferTarget.ArrayBuffer, _bufferCapacityBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        Span<float> span = CollectionsMarshal.AsSpan(_verts);
        unsafe
        {
            fixed (float* p = span)
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, byteCount, (nint)p);
        }

        GL.DrawArrays(PrimitiveType.Triangles, 0, _verts.Count / FloatsPerVertex);

        if (depthEnabled) GL.Enable(EnableCap.DepthTest);
        if (cullEnabled) GL.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_shader != 0) GL.DeleteProgram(_shader);
    }

    private void EnsureGlObjects()
    {
        if (_vao != 0) return;
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, VertexStrideBytes, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, VertexStrideBytes, 5 * sizeof(float));
        GL.EnableVertexAttribArray(2);
    }

    private static int CompileShader()
    {
        string vs = @"#version 330 core
layout(location=0)in vec3 pos;
layout(location=1)in vec2 uv;
layout(location=2)in vec2 size;
uniform mat4 view,proj;
uniform float alpha;
out vec2 fUv;
void main(){
    vec3 r = vec3(view[0][0],view[1][0],view[2][0]);
    vec3 u = vec3(view[0][1],view[1][1],view[2][1]);
    vec3 c = pos+(uv.x-.5)*size.x*r+(uv.y-.5)*size.y*u;
    gl_Position=proj*view*vec4(c,1);
    fUv=uv;
}";
        string fs = @"#version 330 core
in vec2 fUv;
uniform sampler2D tex;
uniform vec3 tint;
uniform float alpha;
out vec4 o;
void main(){
    vec4 t=texture(tex,fUv);
    o=t*vec4(tint,alpha);
    if(o.a<.01)discard;
}";
        int v = CompileGl(ShaderType.VertexShader, vs);
        int f = CompileGl(ShaderType.FragmentShader, fs);
        int p = GL.CreateProgram();
        GL.AttachShader(p, v); GL.AttachShader(p, f);
        GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int ok);
        GL.DeleteShader(v); GL.DeleteShader(f);
        if (ok == 0) throw new InvalidOperationException($"Sprite shader link: {GL.GetProgramInfoLog(p)}");
        return p;
    }

    private static int CompileGl(ShaderType type, string src)
    {
        int s = GL.CreateShader(type);
        GL.ShaderSource(s, src);
        GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok != 0) return s;
        string log = GL.GetShaderInfoLog(s);
        GL.DeleteShader(s);
        throw new InvalidOperationException($"{type} compile: {log}");
    }
}
