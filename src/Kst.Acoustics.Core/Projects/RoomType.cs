namespace Kst.Acoustics.Core.Projects;

public enum RoomType
{
    General,
    ConferenceHall,      // Konferans Salonu
    Theater,             // Tiyatro Salonu
    MusicHall,           // Konser Salonu (Klasik)
    Multipurpose,        // Çok Amaçlı Salon
    Classroom,           // Derslik
    Cinema,              // Sinema
    SportsHall,          // Spor Salonu / Basketbol
    SwimmingPool,        // Kapalı Yüzme Havuzu
    HospitalRoom,        // Hastane Odası
    OfficeOpenPlan,      // Açık Ofis
    RecordingStudio,     // Kayıt Stüdyosu
    Library,             // Kütüphane
    Restaurant           // Restoran
}

public sealed record RoomTypeCriteria(
    RoomType Type,
    string DisplayName,
    Func<double, (double Min, double Max)> GetTargetRtMid); // V (m3) -> (Min, Max)

public static class RoomTypeRegistry
{
    public static IReadOnlyList<RoomTypeCriteria> All { get; } = 
    [
        new(RoomType.General, "Genel / Tanımsız", v => (0.5, 1.5)),
        
        new(RoomType.ConferenceHall, "Konferans Salonu", v => {
            var target = 0.15 * Math.Log10(Math.Max(v, 100)) + 0.1;
            return (target * 0.9, target * 1.1);
        }),
        
        new(RoomType.Theater, "Tiyatro Salonu", v => {
            var target = 0.2 * Math.Log10(Math.Max(v, 100)) - 0.1;
            return (target * 0.85, target * 1.15);
        }),

        new(RoomType.MusicHall, "Konser Salonu (Klasik)", v => {
            var target = 0.5 * Math.Log10(Math.Max(v, 100)) - 0.2;
            return (target * 0.9, target * 1.1);
        }),

        new(RoomType.Multipurpose, "Çok Amaçlı Salon", v => {
            var target = 0.25 * Math.Log10(Math.Max(v, 100)) - 0.1;
            return (target * 0.8, target * 1.2);
        }),

        new(RoomType.Classroom, "Derslik / Eğitim", v => (0.6, 0.8)),

        new(RoomType.Cinema, "Sinema", v => (0.4, 0.6)),

        new(RoomType.SportsHall, "Spor Salonu / Basketbol", v => (1.5, 2.2)),

        new(RoomType.SwimmingPool, "Kapalı Yüzme Havuzu", v => (2.0, 3.0)),

        new(RoomType.HospitalRoom, "Hastane Odası", v => (0.5, 0.7)),

        new(RoomType.OfficeOpenPlan, "Açık Ofis", v => (0.4, 0.6)),

        new(RoomType.RecordingStudio, "Kayıt Stüdyosu", v => (0.2, 0.4)),

        new(RoomType.Library, "Kütüphane / Sessiz Çalışma", v => (0.8, 1.1)),

        new(RoomType.Restaurant, "Restoran / Kafe", v => (0.6, 0.9))
    ];
}
