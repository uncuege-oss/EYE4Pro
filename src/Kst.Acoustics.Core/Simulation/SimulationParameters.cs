using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Simulation;

public sealed record SimulationParameters(
    int           RayCount              = 5_000,
    int           MaxOrder              = 150,
    double        ReceiverRadius        = 1.0,    // proje birimi cinsinden
    double        EnergyThreshold       = 1e-6,
    double        AirTemperatureCelsius = 20.0,   // ISO 9613-1 hava sıcaklığı (°C)
    double        RelativeHumidity      = 50.0,   // ISO 9613-1 bağıl nem (%)
    int           IsmOrder              = 1,      // 0=kapalı, 1=birinci mertebe, 2=ikinci mertebe
    int           TransitionOrder       = 30,     // bu bounce'dan sonra istatistiksel kuyruk eklenir
    ReceiverGrid? ReceiverGrid          = null,   // null = ızgara haritası kapalı
    int?          Seed                  = null,   // null = Random.Shared (hızlı, tekrar üretilemez); değer = deterministik/tekrar üretilebilir koşu
    double        UnitScale             = 1.0,    // proje birimi → metre; Run project.Unit'ten enjekte eder (G referansı 10m metre cinsinden)
    bool          MaskGridOutsideRoom   = true,   // kapalı kabukta oda dışı ızgara hücrelerini NaN ile maskeler
    string?       InputFingerprint      = null,   // sonucu üreten değişmez girdi snapshot'ının SHA-256 izi
    int           ConvergenceModeIndex  = 1);     // UI yakınsama profili; proje ayarlarıyla birlikte saklanır
