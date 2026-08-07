using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

/// <summary>
/// Kullanıcı arayüzünde bir kaynak (Source) veya alıcı (Receiver) noktasını temsil eder.
/// Düzenlenebilir alanlar INotifyPropertyChanged ile bağlanır.
/// </summary>
public sealed class AcousticPointViewModel : ViewModelBase
{
    private int _id;
    private string _label = string.Empty;
    private AcousticPointType _type;
    private string _xText = "0";
    private string _yText = "0";
    private string _zText = "0";
    private DirectivityPattern _directivity = DirectivityPattern.Omnidirectional;
    private string _azimuthText = "0";
    private string _elevationText = "0";
    private string _rollText = "0";
    private string? _loudspeakerId;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public AcousticPointType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>X koordinatı — kullanıcı metin kutusuna bağlanır.</summary>
    public string XText
    {
        get => _xText;
        set => SetProperty(ref _xText, value);
    }

    /// <summary>Y koordinatı — kullanıcı metin kutusuna bağlanır.</summary>
    public string YText
    {
        get => _yText;
        set => SetProperty(ref _yText, value);
    }

    /// <summary>Z koordinatı — kullanıcı metin kutusuna bağlanır.</summary>
    public string ZText
    {
        get => _zText;
        set => SetProperty(ref _zText, value);
    }

    public DirectivityPattern Directivity
    {
        get => _directivity;
        set
        {
            if (SetProperty(ref _directivity, value))
            {
                OnPropertyChanged(nameof(DirectivityLabel));
            }
        }
    }

    public string DirectivityLabel => Directivity switch
    {
        DirectivityPattern.Omnidirectional => "Omni",
        DirectivityPattern.Cardioid => "Kardiyoid",
        DirectivityPattern.SuperCardioid => "Süper kardiyoid",
        DirectivityPattern.Figure8 => "Sekiz biçimli",
        _ => Directivity.ToString()
    };

    public string AzimuthText
    {
        get => _azimuthText;
        set => SetProperty(ref _azimuthText, value);
    }

    public string ElevationText
    {
        get => _elevationText;
        set => SetProperty(ref _elevationText, value);
    }

    public string RollText
    {
        get => _rollText;
        set => SetProperty(ref _rollText, value);
    }

    public string? LoudspeakerId
    {
        get => _loudspeakerId;
        set => SetProperty(ref _loudspeakerId, value);
    }

    public bool TryGetX(out double x) => TryParseFinite(XText, out x);

    public bool TryGetY(out double y) => TryParseFinite(YText, out y);

    public bool TryGetZ(out double z) => TryParseFinite(ZText, out z);

    public bool TryGetAzimuth(out double azimuth) => TryParseFinite(AzimuthText, out azimuth);

    public bool TryGetElevation(out double elevation) => TryParseFinite(ElevationText, out elevation);

    public bool TryGetRoll(out double roll) => TryParseFinite(RollText, out roll);

    private static bool TryParseFinite(string? text, out double value) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>
    /// Geçerli bir AcousticPoint nesnesi döner; koordinat parse edilemezse null.
    /// </summary>
    public AcousticPoint? TryBuildPoint()
    {
        var label = Label;
        var loudspeakerId = LoudspeakerId;
        if (string.IsNullOrWhiteSpace(label) ||
            !Enum.IsDefined(Type) || !Enum.IsDefined(Directivity) ||
            !TryGetX(out var x) || !TryGetY(out var y) || !TryGetZ(out var z) ||
            !TryGetAzimuth(out var azimuth) || !TryGetElevation(out var elevation) ||
            !TryGetRoll(out var roll))
        {
            return null;
        }

        return new AcousticPoint(
            Id,
            label.Trim(),
            Type,
            new Kst.Acoustics.Core.Geometry.Point3D(x, y, z),
            Directivity,
            azimuth,
            elevation,
            roll,
            string.IsNullOrWhiteSpace(loudspeakerId) ? null : loudspeakerId.Trim());
    }

    /// <summary>Mevcut bir AcousticPoint modelinden ViewModel oluşturur.</summary>
    public static AcousticPointViewModel FromModel(AcousticPoint model)
    {
        return new AcousticPointViewModel
        {
            Id = model.Id,
            Label = model.Label,
            Type = model.Type,
            XText = model.Position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            YText = model.Position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ZText = model.Position.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            Directivity = model.Directivity,
            AzimuthText = model.DirectivityAzimuthDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ElevationText = model.DirectivityElevationDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            RollText = model.DirectivityRollDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            LoudspeakerId = model.LoudspeakerId
        };
    }
}
