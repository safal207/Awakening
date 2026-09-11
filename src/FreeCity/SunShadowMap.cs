using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal sealed class SunShadowMap : IDisposable
{
    private const int Resolution = 2048;
    private int _texture, _framebuffer, _program, _matrixLocation, _previousFramebuffer;
    private readonly int[] _viewport = new int[4];
    internal Matrix4 LightMatrix { get; private set; }
    internal long TextureBytes => _texture == 0 ? 0 : Resolution * Resolution * 4L;

    internal void Begin(Vector3 focus, Vector3 sun)
    {
        if (_texture == 0) Initialize();
        Vector3 direction = sun.Normalized();
        var view = Matrix4.LookAt(focus + direction * 100, focus, Vector3.UnitY);
        var projection = Matrix4.CreateOrthographic(110, 110, 0.1f, 230);
        LightMatrix = view * projection;
        GL.GetInteger(GetPName.Viewport, _viewport);
        _previousFramebuffer = GL.GetInteger(GetPName.FramebufferBinding);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, Resolution, Resolution);
        GL.ColorMask(false, false, false, false);
        GL.DepthMask(true);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.PolygonOffset(1.5f, 3f);
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.UseProgram(_program);
        var matrix = LightMatrix;
        GL.UniformMatrix4(_matrixLocation, false, ref matrix);
    }

    internal void End()
    {
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.ColorMask(true, true, true, true);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFramebuffer);
        GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
    }

    internal void Bind()
    {
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private void Initialize()
    {
        _texture = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.DepthComponent24,Resolution,Resolution,0,
            PixelFormat.DepthComponent,PixelType.Float,IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureBorderColor,new float[] {1,1,1,1});
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureCompareMode,(int)TextureCompareMode.CompareRefToTexture);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureCompareFunc,(int)DepthFunction.Lequal);
        int previous = GL.GetInteger(GetPName.FramebufferBinding);
        _framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer,_framebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.DepthAttachment,TextureTarget.Texture2D,_texture,0);
        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer,previous);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("Sun shadow framebuffer: " + status);
        int vertex = 0, fragment = 0;
        try
        {
            vertex = Compile(ShaderType.VertexShader,"#version 330 core\nlayout(location=0)in vec3 p;uniform mat4 lightMatrix;void main(){gl_Position=lightMatrix*vec4(p,1);}");
            fragment = Compile(ShaderType.FragmentShader,"#version 330 core\nvoid main(){}");
            _program = GL.CreateProgram();
            GL.AttachShader(_program,vertex); GL.AttachShader(_program,fragment); GL.LinkProgram(_program);
            GL.GetProgram(_program,GetProgramParameterName.LinkStatus,out int linked);
            if (linked == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(_program));
            _matrixLocation = GL.GetUniformLocation(_program,"lightMatrix");
        }
        finally
        {
            if (vertex != 0) GL.DeleteShader(vertex);
            if (fragment != 0) GL.DeleteShader(fragment);
            GL.ActiveTexture(TextureUnit.Texture0);
        }
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader,source); GL.CompileShader(shader);
        GL.GetShader(shader,ShaderParameter.CompileStatus,out int compiled);
        if (compiled != 0) return shader;
        string error = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException(error);
    }

    public void Dispose()
    {
        if (_program != 0) GL.DeleteProgram(_program);
        if (_framebuffer != 0) GL.DeleteFramebuffer(_framebuffer);
        if (_texture != 0) GL.DeleteTexture(_texture);
        _program = _framebuffer = _texture = 0;
    }
}
