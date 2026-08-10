using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Simulation;

/// <summary>
/// Bir simülasyon sonucunu onu üreten geometri, nokta, materyal ve ayarlara bağlayan
/// deterministik iz. Sözlükler sıralanır; süre, sonuç ve UI durumu hesaba katılmaz.
/// </summary>
public static class SimulationInputFingerprint
{
    public static string Compute(AcousticsProject project, SimulationParameters parameters)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Add(hash, "EYE4PRO-M01-SIM-V2");
        Add(hash, (int)project.Unit);
        Add(hash, project.RoomVolume);

        Add(hash, project.Surfaces.Count);
        foreach (var surface in project.Surfaces)
        {
            Add(hash, surface.Id);
            Add(hash, surface.Name);
            Add(hash, surface.Category);
            Add(hash, surface.OwnerId);
            Add(hash, surface.Vertices.Count);
            foreach (var vertex in surface.Vertices)
            {
                Add(hash, vertex.X);
                Add(hash, vertex.Y);
                Add(hash, vertex.Z);
            }
        }

        AddPoints(hash, project.Sources);
        AddPoints(hash, project.Receivers);
        AddMap(hash, project.CategoryMaterials);
        AddMap(hash, project.SurfaceMaterials);
        AddResolvedMaterials(hash, project.CategoryMaterials.Values.Concat(project.SurfaceMaterials.Values));
        AddResolvedLoudspeakers(hash, project.Sources);

        Add(hash, parameters.RayCount);
        Add(hash, parameters.MaxOrder);
        Add(hash, parameters.ReceiverRadius);
        Add(hash, parameters.EnergyThreshold);
        Add(hash, parameters.AirTemperatureCelsius);
        Add(hash, parameters.RelativeHumidity);
        Add(hash, parameters.IsmOrder);
        Add(hash, parameters.TransitionOrder);
        Add(hash, parameters.Seed ?? int.MinValue);
        Add(hash, parameters.MaskGridOutsideRoom);
        Add(hash, parameters.ConvergenceModeIndex);

        if (parameters.ReceiverGrid is { } grid)
        {
            Add(hash, true);
            Add(hash, grid.PlaneZ);
            Add(hash, grid.XMin);
            Add(hash, grid.XMax);
            Add(hash, grid.YMin);
            Add(hash, grid.YMax);
            Add(hash, grid.CellSize);
        }
        else
        {
            Add(hash, false);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AddPoints(IncrementalHash hash, IReadOnlyList<AcousticPoint> points)
    {
        Add(hash, points.Count);
        foreach (var point in points)
        {
            Add(hash, point.Id);
            Add(hash, point.Label);
            Add(hash, (int)point.Type);
            Add(hash, point.Position.X);
            Add(hash, point.Position.Y);
            Add(hash, point.Position.Z);
            Add(hash, (int)point.Directivity);
            Add(hash, point.DirectivityAzimuthDeg);
            Add(hash, point.DirectivityElevationDeg);
            Add(hash, point.DirectivityRollDeg);
            Add(hash, point.LoudspeakerId);
        }
    }

    private static void AddMap(IncrementalHash hash, IReadOnlyDictionary<string, string> map)
    {
        // UI her kategori için varsayılan materyali açıkça yazar; eski veya dışarıdan
        // oluşturulan projeler ise aynı semantiği boş eşlemeyle taşıyabilir. Bu iki
        // gösterim simülasyon girdisi açısından eşdeğerdir ve aynı izi üretmelidir.
        var effective = map
            .Where(kvp => !string.Equals(
                kvp.Value, MaterialLibrary.Default.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Add(hash, effective.Length);
        foreach (var item in effective)
        {
            Add(hash, item.Key);
            Add(hash, item.Value);
        }
    }

    private static void AddResolvedMaterials(IncrementalHash hash, IEnumerable<string> materialIds)
    {
        var ids = materialIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Append(MaterialLibrary.Default.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Add(hash, ids.Length);
        foreach (var id in ids)
        {
            Add(hash, id);
            var material = MaterialDatabase.Instance.FindById(id) ?? MaterialLibrary.FindById(id);
            if (material is null)
            {
                Add(hash, -1);
                continue;
            }

            Add(hash, material.Alpha.Length);
            foreach (var value in material.Alpha) Add(hash, value);
            Add(hash, material.Scattering.Length);
            foreach (var value in material.Scattering) Add(hash, value);
        }
    }

    private static void AddResolvedLoudspeakers(IncrementalHash hash, IReadOnlyList<AcousticPoint> sources)
    {
        var ids = sources.Select(s => s.LoudspeakerId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Add(hash, ids.Length);
        foreach (var id in ids)
        {
            Add(hash, id);
            var speaker = LoudspeakerDatabase.Instance.FindById(id);
            if (speaker is null)
            {
                Add(hash, -1);
                continue;
            }

            Add(hash, speaker.Sensitivity.Length);
            foreach (var value in speaker.Sensitivity) Add(hash, value);
            Add(hash, speaker.Balloons.Length);
            foreach (var balloon in speaker.Balloons)
            {
                Add(hash, balloon.Resolution);
                var data = balloon.AttenuationData;
                Add(hash, data.GetLength(0));
                Add(hash, data.GetLength(1));
                foreach (var value in data) Add(hash, value);
            }
        }
    }

    private static void Add(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            Add(hash, -1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        Add(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void Add(IncrementalHash hash, bool value) => Add(hash, value ? 1 : 0);

    private static void Add(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void Add(IncrementalHash hash, double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        hash.AppendData(bytes);
    }
}
