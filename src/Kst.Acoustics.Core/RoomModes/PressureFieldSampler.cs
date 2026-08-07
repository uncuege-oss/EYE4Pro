using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Bir mod için basınç alanı örneklemesi yapar.
/// </summary>
public static class PressureFieldSampler
{
    /// <summary>
    /// Belirli bir mod için yüzeylerdeki basınç dağılımını örnekler.
    /// </summary>
    public static List<PressurePoint> SampleSurface(RoomMode mode, RoomModeInput room, int resolution = 20)
    {
        var points = new List<PressurePoint>();

        // 6 Yüzey (X=0, X=Lx, Y=0, Y=Ly, Z=0, Z=Lz)
        // X-Y yüzeyleri (Z=0, Z=Lz)
        SampleFace(points, mode, room, resolution, Axis.X, Axis.Y, Axis.Z, 0);
        SampleFace(points, mode, room, resolution, Axis.X, Axis.Y, Axis.Z, room.LengthZ);

        // X-Z yüzeyleri (Y=0, Y=Ly)
        SampleFace(points, mode, room, resolution, Axis.X, Axis.Z, Axis.Y, 0);
        SampleFace(points, mode, room, resolution, Axis.X, Axis.Z, Axis.Y, room.LengthY);

        // Y-Z yüzeyleri (X=0, X=Lx)
        SampleFace(points, mode, room, resolution, Axis.Y, Axis.Z, Axis.X, 0);
        SampleFace(points, mode, room, resolution, Axis.Y, Axis.Z, Axis.X, room.LengthX);

        return points;
    }

    private static void SampleFace(List<PressurePoint> points, RoomMode mode, RoomModeInput room, int res, Axis a1, Axis a2, Axis staticAxis, double staticVal)
    {
        double len1 = GetLength(room, a1);
        double len2 = GetLength(room, a2);

        for (int i = 0; i <= res; i++)
        {
            double v1 = (i / (double)res) * len1;
            for (int j = 0; j <= res; j++)
            {
                double v2 = (j / (double)res) * len2;

                double x = 0, y = 0, z = 0;
                SetCoord(ref x, ref y, ref z, a1, v1);
                SetCoord(ref x, ref y, ref z, a2, v2);
                SetCoord(ref x, ref y, ref z, staticAxis, staticVal);

                double p = CalculatePressure(x, y, z, mode, room);
                points.Add(new PressurePoint(x, y, z, p));
            }
        }
    }

    public static double CalculatePressure(double x, double y, double z, RoomMode mode, RoomModeInput room)
    {
        return Math.Cos(mode.Nx * Math.PI * x / room.LengthX) *
               Math.Cos(mode.Ny * Math.PI * y / room.LengthY) *
               Math.Cos(mode.Nz * Math.PI * z / room.LengthZ);
    }

    private static double GetLength(RoomModeInput room, Axis axis) => axis switch
    {
        Axis.X => room.LengthX,
        Axis.Y => room.LengthY,
        _ => room.LengthZ
    };

    private static void SetCoord(ref double x, ref double y, ref double z, Axis axis, double val)
    {
        switch (axis)
        {
            case Axis.X: x = val; break;
            case Axis.Y: y = val; break;
            case Axis.Z: z = val; break;
        }
    }

    private enum Axis { X, Y, Z }
}

public record PressurePoint(double X, double Y, double Z, double Pressure);
