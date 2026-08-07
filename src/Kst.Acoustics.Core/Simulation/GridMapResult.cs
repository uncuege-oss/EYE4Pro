using Kst.Acoustics.Core.Projects;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Simulation;

public enum GridMapParameter { RT60, C80, C50, STI }

/// <summary>Izgaralı alıcı simülasyonunun akustik parametre haritası.</summary>
public sealed class GridMapResult
{
    public ReceiverGrid Grid { get; init; } = null!;
    public int Cols { get; init; }
    public int Rows { get; init; }
    public double[][] Rt60 { get; init; } = Array.Empty<double[]>();
    public double[][] C80 { get; init; } = Array.Empty<double[]>();
    public double[][] C50 { get; init; } = Array.Empty<double[]>();
    public double[][] Sti { get; init; } = Array.Empty<double[]>();

    public GridMapResult() { }

    public GridMapResult(ReceiverGrid grid, int cols, int rows,
        double[,] rt60, double[,] c80, double[,] c50, double[,] sti)
        : this(grid, cols, rows, ToJagged(rt60, rows, cols), ToJagged(c80, rows, cols),
            ToJagged(c50, rows, cols), ToJagged(sti, rows, cols))
    {
    }

    public GridMapResult(ReceiverGrid grid, int cols, int rows,
        double[][] rt60, double[][] c80, double[][] c50, double[][] sti)
    {
        Grid = grid;
        Cols = cols;
        Rows = rows;
        Rt60 = Normalize(rt60, rows, cols);
        C80  = Normalize(c80,  rows, cols);
        C50  = Normalize(c50,  rows, cols);
        Sti  = Normalize(sti,  rows, cols);
    }

    public double GetValue(GridMapParameter p, int row, int col) => p switch
    {
        GridMapParameter.RT60 => Rt60[row][col],
        GridMapParameter.C80  => C80[row][col],
        GridMapParameter.C50  => C50[row][col],
        GridMapParameter.STI  => Sti[row][col],
        _                     => double.NaN
    };

    public (double min, double max) GetRange(GridMapParameter p)
    {
        double min = double.MaxValue, max = double.MinValue;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var v = GetValue(p, r, c);
                if (double.IsNaN(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
        }
        return min > max ? (0, 1) : (min, max);
    }

    private static double[][] ToJagged(double[,] source, int rows, int cols)
    {
        var result = new double[rows][];
        for (var r = 0; r < rows; r++)
        {
            result[r] = new double[cols];
            for (var c = 0; c < cols; c++)
                result[r][c] = source[r, c];
        }
        return result;
    }

    private static double[][] Normalize(double[][]? source, int rows, int cols)
    {
        var result = new double[rows][];
        for (var r = 0; r < rows; r++)
        {
            result[r] = new double[cols];
            Array.Fill(result[r], double.NaN);
            if (source is null || r >= source.Length || source[r] is null) continue;

            var width = Math.Min(cols, source[r].Length);
            Array.Copy(source[r], result[r], width);
        }
        return result;
    }
}
