using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Probuzhdenie;

public sealed class UiRenderer : IDisposable
{
    private const int FloatsPerVertex = 9;
    private const int VertexStrideBytes = FloatsPerVertex * sizeof(float);

    private readonly List<float> _vertices = new(8192);
    private int _vao;
    private int _vbo;
    private int _bufferCapacityBytes;
    private float _textPixelWidthScale = 1f;

    public int GpuBufferBytes => _bufferCapacityBytes;

    public void Begin(int viewportWidth = 1, int viewportHeight = 1)
    {
        _vertices.Clear();
        _textPixelWidthScale = viewportWidth > 0 ? viewportHeight / (float)viewportWidth : 1f;
    }

    public float MeasureText(string text, float pixelSize)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        return Math.Max(0, text.Length * 6f - 1f) * pixelSize * _textPixelWidthScale;
    }

    public void Text(string text, float x, float y, float pixelSize, Vector3 color)
    {
        float cursor = x;
        float pixelWidth = pixelSize * _textPixelWidthScale;
        foreach (char raw in text)
        {
            char ch = char.ToUpperInvariant(raw);
            if (!Glyphs.TryGetValue(ch, out string[]? glyph))
            {
                glyph = Glyphs['?'];
            }

            for (int row = 0; row < glyph.Length; row++)
            {
                string line = glyph[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] == '1')
                        Rect(cursor + col * pixelWidth, y + row * pixelSize, pixelWidth, pixelSize, color);
                }
            }

            cursor += 6f * pixelWidth;
        }
    }

    public void Rect(float x, float y, float width, float height, Vector3 color)
    {
        float x1 = x * 2f - 1f;
        float y1 = 1f - y * 2f;
        float x2 = (x + width) * 2f - 1f;
        float y2 = 1f - (y + height) * 2f;

        Emit(x1, y1, color);
        Emit(x1, y2, color);
        Emit(x2, y2, color);
        Emit(x1, y1, color);
        Emit(x2, y2, color);
        Emit(x2, y1, color);
    }

    public unsafe void Render(
        int shader,
        int modelLocation,
        int viewLocation,
        int projectionLocation,
        int colorLocation,
        int ambientLocation,
        int lightLocation,
        int fogColorLocation)
    {
        if (_vertices.Count == 0) return;
        EnsureObjects();

        bool depthEnabled = GL.IsEnabled(EnableCap.DepthTest);
        bool cullEnabled = GL.IsEnabled(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.UseProgram(shader);
        var id = Matrix4.Identity;
        GL.UniformMatrix4(modelLocation, false, ref id);
        GL.UniformMatrix4(viewLocation, false, ref id);
        GL.UniformMatrix4(projectionLocation, false, ref id);
        GL.Uniform3(colorLocation, -1f, -1f, -1f);
        GL.Uniform3(ambientLocation, 1f, 1f, 1f);
        GL.Uniform3(lightLocation, 0f, 0f, 1f);
        GL.Uniform3(fogColorLocation, 0f, 0f, 0f);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        int byteCount = _vertices.Count * sizeof(float);
        EnsureBufferCapacity(byteCount);

        Span<float> span = CollectionsMarshal.AsSpan(_vertices);
        fixed (float* p = span)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, byteCount, (nint)p);

        GL.DrawArrays(PrimitiveType.Triangles, 0, _vertices.Count / FloatsPerVertex);

        if (depthEnabled) GL.Enable(EnableCap.DepthTest);
        if (cullEnabled) GL.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        _vbo = 0;
        _vao = 0;
        _bufferCapacityBytes = 0;
    }

    private void EnsureObjects()
    {
        if (_vao != 0) return;

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, VertexStrideBytes, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
    }

    private void EnsureBufferCapacity(int byteCount)
    {
        if (byteCount <= _bufferCapacityBytes) return;

        _bufferCapacityBytes = Math.Max(byteCount, Math.Max(4096, _bufferCapacityBytes * 2));
        GL.BufferData(BufferTarget.ArrayBuffer, _bufferCapacityBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
    }

    private void Emit(float x, float y, Vector3 color)
    {
        _vertices.Add(x);
        _vertices.Add(y);
        _vertices.Add(-1f);
        _vertices.Add(color.X);
        _vertices.Add(color.Y);
        _vertices.Add(color.Z);
        _vertices.Add(0f);
        _vertices.Add(0f);
        _vertices.Add(1f);
    }

    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        [' '] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" },
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" },
        ['6'] = new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" },
        [':'] = new[] { "00000", "00100", "00100", "00000", "00100", "00100", "00000" },
        ['%'] = new[] { "11001", "11010", "00100", "01000", "10110", "00110", "00000" },
        ['.'] = new[] { "00000", "00000", "00000", "00000", "00000", "00100", "00100" },
        ['!'] = new[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" },
        ['?'] = new[] { "01110", "10001", "00001", "00010", "00100", "00000", "00100" },
        ['-'] = new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
        [','] = new[] { "00000", "00000", "00000", "00000", "00100", "00100", "01000" },
        [';'] = new[] { "00000", "00100", "00100", "00000", "00100", "00100", "01000" },
        ['+'] = new[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" },
        ['/'] = new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" },
        ['#'] = new[] { "01010", "11111", "01010", "01010", "11111", "01010", "00000" },
        ['*'] = new[] { "00000", "10101", "01110", "11111", "01110", "10101", "00000" },
        ['('] = new[] { "00010", "00100", "01000", "01000", "01000", "00100", "00010" },
        [')'] = new[] { "01000", "00100", "00010", "00010", "00010", "00100", "01000" },
        ['['] = new[] { "01110", "01000", "01000", "01000", "01000", "01000", "01110" },
        [']'] = new[] { "01110", "00010", "00010", "00010", "00010", "00010", "01110" },
        ['_'] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "11111" },
        ['='] = new[] { "00000", "00000", "11111", "00000", "11111", "00000", "00000" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['C'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
        ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01111", "10000", "10000", "10011", "10001", "10001", "01111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" },
        ['J'] = new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" },
        ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['Q'] = new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
        ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
        ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
        ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
        ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
        ['Ё'] = new[] { "01010", "11111", "10000", "11110", "10000", "10000", "11111" },
        ['А'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['Б'] = new[] { "11111", "10000", "10000", "11110", "10001", "10001", "11110" },
        ['В'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['Г'] = new[] { "11111", "10000", "10000", "10000", "10000", "10000", "10000" },
        ['Д'] = new[] { "00110", "01010", "10010", "10010", "10010", "11111", "10001" },
        ['Е'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['Ж'] = new[] { "10101", "10101", "01110", "00100", "01110", "10101", "10101" },
        ['З'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['И'] = new[] { "10001", "10011", "10101", "10101", "11001", "10001", "10001" },
        ['Й'] = new[] { "01010", "00100", "10011", "10101", "11001", "10001", "10001" },
        ['К'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['Л'] = new[] { "00111", "01001", "10001", "10001", "10001", "10001", "10001" },
        ['М'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['Н'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['О'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['П'] = new[] { "11111", "10001", "10001", "10001", "10001", "10001", "10001" },
        ['Р'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['С'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
        ['Т'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['У'] = new[] { "10001", "10001", "10001", "01111", "00001", "00001", "11110" },
        ['Ф'] = new[] { "00100", "01110", "10101", "10101", "01110", "00100", "00100" },
        ['Х'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
        ['Ц'] = new[] { "10010", "10010", "10010", "10010", "10010", "11111", "00001" },
        ['Ч'] = new[] { "10001", "10001", "10001", "01111", "00001", "00001", "00001" },
        ['Ш'] = new[] { "10101", "10101", "10101", "10101", "10101", "10101", "11111" },
        ['Щ'] = new[] { "10101", "10101", "10101", "10101", "10101", "11111", "00001" },
        ['Ъ'] = new[] { "11000", "01000", "01000", "01110", "01001", "01001", "01110" },
        ['Ы'] = new[] { "10001", "10001", "10001", "11101", "10011", "10011", "11101" },
        ['Ь'] = new[] { "10000", "10000", "10000", "11110", "10001", "10001", "11110" },
        ['Э'] = new[] { "11110", "00001", "00001", "01111", "00001", "00001", "11110" },
        ['Ю'] = new[] { "10010", "10101", "10101", "11101", "10101", "10101", "10010" },
        ['Я'] = new[] { "01111", "10001", "10001", "01111", "00101", "01001", "10001" },
    };
}
