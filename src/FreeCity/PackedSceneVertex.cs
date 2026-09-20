using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Half = System.Half;

namespace Probuzhdenie.FreeCity;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct PackedSceneVertex
{
    internal const int Stride = 28;
    internal readonly Vector3 Position;
    internal readonly Half R, G, B, A;
    internal readonly short Nx, Ny, Nz, Padding;

    internal PackedSceneVertex(ReadOnlySpan<float> source)
    {
        Position = new(source[0], source[1], source[2]);
        R = (Half)source[3]; G = (Half)source[4]; B = (Half)source[5]; A = (Half)1;
        Nx = PackNormal(source[6]); Ny = PackNormal(source[7]); Nz = PackNormal(source[8]);
        Padding = 0;
    }

    private static short PackNormal(float value) => (short)MathF.Round(Math.Clamp(value, -1, 1) * short.MaxValue);
}
