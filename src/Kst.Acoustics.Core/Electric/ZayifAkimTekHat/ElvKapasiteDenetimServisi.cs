using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvKapasiteSorunSeviyesi { Bilgi, Uyari }

public sealed record ElvKapasiteSorunu(ElvKapasiteSorunSeviyesi Seviye, string Kaynak, string Mesaj);

public sealed record ElvKapasiteDenetimRaporu(IReadOnlyList<ElvKapasiteSorunu> Sorunlar)
{
    public bool Temiz => Sorunlar.All(s => s.Seviye == ElvKapasiteSorunSeviyesi.Bilgi);
    public int UyariSayisi => Sorunlar.Count(s => s.Seviye == ElvKapasiteSorunSeviyesi.Uyari);
}

/// <summary>
/// Cihaz adlarındaki "N Port" / "N Kanal" bilgisinden rack ve merkez kapasitelerini denetler.
/// Katalog adları serbest metin olduğundan yalnız tanımlı cihazı olan rack'lerde konuşur;
/// cihaz girilmemiş rack için gürültü üretmez.
/// </summary>
public static partial class ElvKapasiteDenetimServisi
{
    [GeneratedRegex(@"(\d+)\s*Port", RegexOptions.IgnoreCase)]
    private static partial Regex PortDeseni();

    [GeneratedRegex(@"(\d+)\s*Kanal", RegexOptions.IgnoreCase)]
    private static partial Regex KanalDeseni();

    /// <summary>Data sistemi: rack switch portları ve patch panel portları uç sayısını karşılamalı.</summary>
    public static ElvKapasiteDenetimRaporu DataDenetle(ElvDataSistemi data)
    {
        var sorunlar = new List<ElvKapasiteSorunu>();
        foreach (var rack in data.RackKabinler)
        {
            int ucSayisi = rack.DataHatSayisi + rack.WirelessSayisi + rack.DataEkUcSayisi
                           + rack.ToplamDijitalAnonsUcu;
            int switchPort = ToplamPort(rack.Cihazlar, c => SwitchMi(c.Ad));
            int panelPort = ToplamPort(rack.Cihazlar, c => PatchPanelMi(c.Ad));

            if (ucSayisi == 0) continue;
            if (switchPort > 0 && switchPort < ucSayisi)
                Uyari(rack.Ad, $"Switch port kapasitesi yetersiz: {ucSayisi} uç için {switchPort} port tanımlı.");
            else if (switchPort == 0 && rack.Cihazlar.Count > 0)
                Uyari(rack.Ad, $"{ucSayisi} uç var ama rack'te port bilgisi olan switch tanımlı değil.");

            if (panelPort > 0 && panelPort < ucSayisi)
                Uyari(rack.Ad, $"Patch panel kapasitesi yetersiz: {ucSayisi} uç için {panelPort} port tanımlı.");
        }

        if (sorunlar.Count == 0)
            sorunlar.Add(new(ElvKapasiteSorunSeviyesi.Bilgi, "Data", "Port kapasite denetiminde sorun bulunmadı."));
        return new(sorunlar);

        void Uyari(string kaynak, string mesaj)
            => sorunlar.Add(new(ElvKapasiteSorunSeviyesi.Uyari, kaynak, mesaj));
    }

    /// <summary>Kamera sistemi: rack PoE portları kamera sayısını, merkez NVR kanalları toplam kamerayı karşılamalı.</summary>
    public static ElvKapasiteDenetimRaporu KameraDenetle(ElvKameraSistemi kamera)
    {
        var sorunlar = new List<ElvKapasiteSorunu>();
        foreach (var rack in kamera.RackKabinler)
        {
            if (rack.KameraSayisi == 0) continue;
            int switchPort = ToplamPort(rack.Cihazlar, c => SwitchMi(c.Ad));
            if (switchPort > 0 && switchPort < rack.KameraSayisi)
                Uyari(rack.Ad, $"PoE/switch port kapasitesi yetersiz: {rack.KameraSayisi} kamera için {switchPort} port tanımlı.");
            else if (switchPort == 0 && rack.Cihazlar.Count > 0)
                Uyari(rack.Ad, $"{rack.KameraSayisi} kamera var ama rack'te port bilgisi olan switch tanımlı değil.");
        }

        int toplamKamera = kamera.ToplamKamera;
        if (toplamKamera > 0)
        {
            int nvrKanal = kamera.AnaMerkez.Cihazlar
                .Where(c => c.Ad.Contains("NVR", StringComparison.OrdinalIgnoreCase))
                .Sum(c => KanalSayisi(c.Ad) * Math.Max(1, c.Adet));
            if (nvrKanal == 0)
                Uyari(kamera.AnaMerkez.Ad, $"{toplamKamera} kamera için ana merkezde NVR tanımlı değil.");
            else if (nvrKanal < toplamKamera)
                Uyari(kamera.AnaMerkez.Ad, $"NVR kanal kapasitesi yetersiz: {toplamKamera} kamera için {nvrKanal} kanal tanımlı.");
        }

        if (sorunlar.Count == 0)
            sorunlar.Add(new(ElvKapasiteSorunSeviyesi.Bilgi, "Kamera", "Kanal/port kapasite denetiminde sorun bulunmadı."));
        return new(sorunlar);

        void Uyari(string kaynak, string mesaj)
            => sorunlar.Add(new(ElvKapasiteSorunSeviyesi.Uyari, kaynak, mesaj));
    }

    private static bool SwitchMi(string ad)
        => ad.Contains("Switch", StringComparison.OrdinalIgnoreCase)
           && !PatchPanelMi(ad);

    private static bool PatchPanelMi(string ad)
        => ad.Contains("Patch Panel", StringComparison.OrdinalIgnoreCase);

    private static int ToplamPort(IEnumerable<ElvCihaz> cihazlar, Func<ElvCihaz, bool> filtre)
        => cihazlar.Where(filtre).Sum(c => PortSayisi(c.Ad) * Math.Max(1, c.Adet));

    private static int PortSayisi(string ad)
    {
        var m = PortDeseni().Match(ad);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    private static int KanalSayisi(string ad)
    {
        var m = KanalDeseni().Match(ad);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }
}
