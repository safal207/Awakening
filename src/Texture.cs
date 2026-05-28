using System;
using OpenTK.Graphics.OpenGL4;

namespace Probuzhdenie;

public sealed class Texture : IDisposable
{
    public int GlHandle { get; }
    public int Width { get; }
    public int Height { get; }

    public Texture(byte[] rgba, int width, int height, bool mipmap = false)
    {
        Width = width;
        Height = height;
        GlHandle = GL.GenTexture();
        Bind();
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            mipmap ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        if (mipmap) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Bind(int unit = 0)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(TextureTarget.Texture2D, GlHandle);
    }

    public void Dispose()
    {
        if (GlHandle != 0) GL.DeleteTexture(GlHandle);
    }

    public static Texture CreateTree()
    {
        int w = 64, h = 128;
        byte[] p = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)w;
                float dx = fx - 0.5f, dy = fy - 0.5f;
                int i = (y * w + x) * 4;

                if (fy < 0.2f)
                {
                    float trunk = 0.3f - Math.Abs(dx) * 0.6f;
                    if (trunk > 0.15f)
                    {
                        p[i] = 80; p[i + 1] = 50; p[i + 2] = 20; p[i + 3] = 255;
                    }
                    else
                    {
                        p[i] = 0; p[i + 1] = 0; p[i + 2] = 0; p[i + 3] = 0;
                    }
                }
                else
                {
                    float r = MathF.Sqrt(dx * dx + dy * dy * 1.8f);
                    if (r < 0.42f)
                    {
                        float glow = 0.5f + 0.5f * (1f - r / 0.42f);
                        float var = (MathF.Sin(fx * 12f + fy * 7f) * 0.5f + 0.5f) * 0.2f + 0.8f;
                        p[i] = (byte)(30 * var * glow);
                        p[i + 1] = (byte)(120 * var * glow);
                        p[i + 2] = (byte)(40 * var * glow);
                        p[i + 3] = 255;
                    }
                    else
                    {
                        p[i] = 0; p[i + 1] = 0; p[i + 2] = 0; p[i + 3] = 0;
                    }
                }
            }
        }
        return new Texture(p, w, h, mipmap: true);
    }

    public static Texture CreateLogo()
    {
        int w = 512, h = 128;
        byte[] p = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)w;
                int i = (y * w + x) * 4;

                float dxL = fx - 0.106f, dyL = fy - 0.5f;
                if (dxL > -0.075f && dxL < 0.075f && Math.Abs(dyL) < 0.35f)
                {
                    float edge = 1f - Math.Abs(dyL) / 0.35f;
                    float alpha = Math.Clamp(edge * 2f, 0f, 1f);
                    p[i] = 255; p[i + 1] = 200; p[i + 2] = 80; p[i + 3] = (byte)(alpha * 255);
                }
                else
                {
                    float dist = MathF.Sqrt((fx - 0.55f) * (fx - 0.55f) * 3f + dyL * dyL);
                    float glow = Math.Clamp(1f - dist / 0.45f, 0f, 1f);
                    float alpha2 = glow * glow * 0.9f;
                    float hue = MathF.Sin(fx * 8f + fy * 5f) * 0.2f + 0.8f;
                    p[i] = (byte)(30 * hue * glow);
                    p[i + 1] = (byte)(180 * hue * glow);
                    p[i + 2] = (byte)(120 * hue * glow);
                    p[i + 3] = (byte)(alpha2 * 255);
                }
            }
        }
        return new Texture(p, w, h, mipmap: true);
    }

    public static Texture CreateSpark()
    {
        int s = 32;
        byte[] p = new byte[s * s * 4];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                int i = (y * s + x) * 4;
                if (r < 0.5f)
                {
                    float a = Math.Clamp((0.5f - r) * 4f, 0f, 1f);
                    byte v = (byte)(255 * a);
                    p[i] = v; p[i + 1] = v; p[i + 2] = v; p[i + 3] = v;
                }
            }
        }
        return new Texture(p, s, s, mipmap: true);
    }
}
