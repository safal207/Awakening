using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace Probuzhdenie.FreeCity;

internal sealed class SceneMaterials : IDisposable
{
    private int _brick;
    internal long TextureBytes { get; private set; }

    internal void Load()
    {
        if (_brick != 0) return;
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "materials", "brick-albedo.png");
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        _brick = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _brick);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Srgb8Alpha8, image.Width, image.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        TextureBytes = (long)image.Width * image.Height * 4 * 4 / 3;
    }

    internal void Bind()
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _brick);
    }

    public void Dispose()
    {
        if (_brick != 0) GL.DeleteTexture(_brick);
        _brick = 0;
        TextureBytes = 0;
    }
}
