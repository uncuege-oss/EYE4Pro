using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum TvDagitimTeknolojisi
{
    DirektLnb,
    MerkeziKoaksiyel,
    IpTv
}

public sealed class ElvTvUcu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakM12ElemanId { get; set; } = "";
    public string Ad { get; set; } = "TV Prizi";
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    public string MahalAdi { get; set; } = "";
    public int TunerSayisi { get; set; } = 1;
}

public sealed class ElvDirektTvHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int HatNo { get; set; }
    public string TvUcuId { get; set; } = "";
    public string Ad { get; set; } = "";
    public int TunerNo { get; set; } = 1;
    public int LnbCikisNo { get; set; }
    public bool DiseqcVar { get; set; }
    public string KabloTipi { get; set; } = "RG-6/U Koaksiyel";
    public double Metraj { get; set; }
}

public sealed class ElvDirektLnbSistemi
{
    public string CanakAdi { get; set; } = "Uydu Çanağı";
    public string UyduPozisyonu { get; set; } = "42°E";
    public string LnbTipi { get; set; } = "Quad LNB";
    public int LnbCikisSayisi { get; set; } = 4;
    public int LnbSayisi { get; set; } = 1;
    public List<ElvDirektTvHatti> Hatlar { get; set; } = new();
    public int GerekliCikis => Hatlar.Count;
}

public sealed class ElvIpTvAgAyarlari
{
    public int VlanId { get; set; } = 200;
    public bool Multicast { get; set; } = true;
    public bool IgmpSnooping { get; set; } = true;
    public bool IgmpQuerier { get; set; } = true;
    public int IgmpSurumu { get; set; } = 2;
    public int KanalSayisi { get; set; } = 40;
    public int EszamanliBenzersizKanal { get; set; } = 12;
    public double OrtalamaKanalBitrateMbps { get; set; } = 12;
    public double KapasiteRezervYuzde { get; set; } = 20;
}

public sealed class ElvIpTvAkisi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KanalAdi { get; set; } = "Yeni Kanal";
    public string Kaynak { get; set; } = "SAT>IP Gateway";
    public string MulticastGrupIp { get; set; } = "239.10.0.1";
    public int Port { get; set; } = 5000;
    public string Codec { get; set; } = "H.264";
    public string Cozunurluk { get; set; } = "1080p";
    public double BitrateMbps { get; set; } = 12;
}

public sealed class ElvIpTvSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "IP-TV Ana Merkez / Headend" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();
    public ElvIpTvAgAyarlari AgAyarlari { get; set; } = new();
    public List<ElvIpTvAkisi> Akislar { get; set; } = new();
    public int ToplamUc => RackKabinler.Sum(r => r.TvPrizSayisi);
    public double GerekliMerkezBantGenisligiMbps
    {
        get
        {
            int akis = AgAyarlari.Multicast
                ? Math.Min(Math.Max(1, AgAyarlari.EszamanliBenzersizKanal), Math.Max(1, AgAyarlari.KanalSayisi))
                : Math.Max(1, ToplamUc);
            double ham = Akislar.Count > 0
                ? (AgAyarlari.Multicast
                    ? Akislar.OrderByDescending(a => a.BitrateMbps).Take(akis).Sum(a => Math.Max(0, a.BitrateMbps))
                    : Math.Max(1, ToplamUc) * Akislar.Average(a => Math.Max(0, a.BitrateMbps)))
                : akis * Math.Max(0, AgAyarlari.OrtalamaKanalBitrateMbps);
            return ham * (1 + Math.Max(0, AgAyarlari.KapasiteRezervYuzde) / 100.0);
        }
    }
}

public sealed record ElvTvDogrulamaSorunu(string Kaynak, string Mesaj, bool Hata);

public static class ElvTvTopolojiServisi
{
    public static void TeknolojiyiHazirla(ElvTvSistemi tv, TvDagitimTeknolojisi teknoloji)
    {
        TvUclariniLegacydenTamamla(tv);
        tv.Teknoloji = teknoloji;
        if (teknoloji == TvDagitimTeknolojisi.DirektLnb)
            DirektHatlariEsitle(tv);
        else if (teknoloji == TvDagitimTeknolojisi.IpTv)
            IpTvRackleriniEsitle(tv);
    }

    public static void TvUclariniLegacydenTamamla(ElvTvSistemi tv)
    {
        if (tv.OrtakUclar.Count > 0) return;
        foreach (var rack in tv.RackKabinler)
            for (int i = 0; i < rack.TvPrizSayisi; i++)
                tv.OrtakUclar.Add(new ElvTvUcu
                {
                    Ad = $"{rack.KatAdi} TV Prizi {i + 1}",
                    KatNo = rack.KatNo,
                    KatAdi = rack.KatAdi
                });
    }

    public static void DirektHatlariEsitle(ElvTvSistemi tv)
    {
        var gerekli = tv.OrtakUclar
            .SelectMany(u => Enumerable.Range(1, Math.Max(1, u.TunerSayisi)).Select(t => (Uc: u, Tuner: t)))
            .ToList();
        var mevcut = tv.DirektLnb.Hatlar.ToDictionary(h => (h.TvUcuId, h.TunerNo));
        tv.DirektLnb.Hatlar = gerekli.Select((x, i) =>
        {
            if (mevcut.TryGetValue((x.Uc.Id, x.Tuner), out var hat))
            {
                hat.HatNo = i + 1;
                hat.Ad = $"{x.Uc.Ad} / Tuner {x.Tuner}";
                return hat;
            }
            return new ElvDirektTvHatti
            {
                HatNo = i + 1,
                TvUcuId = x.Uc.Id,
                TunerNo = x.Tuner,
                LnbCikisNo = i + 1,
                Ad = $"{x.Uc.Ad} / Tuner {x.Tuner}"
            };
        }).ToList();
    }

    public static void IpTvRackleriniEsitle(ElvTvSistemi tv)
    {
        foreach (var kat in tv.OrtakUclar.GroupBy(u => new { u.KatNo, u.KatAdi }))
        {
            var rack = tv.IpTv.RackKabinler.FirstOrDefault(r => r.KatNo == kat.Key.KatNo);
            if (rack is null)
            {
                rack = new ElvRackKabin
                {
                    Ad = $"{kat.Key.KatAdi} IP-TV Rack",
                    KatNo = kat.Key.KatNo,
                    KatAdi = kat.Key.KatAdi,
                    KatIciKabloTipi = "Cat6A F/UTP LSZH",
                    MerkezeKabloTipi = "Fiber 12 Core Singlemode"
                };
                tv.IpTv.RackKabinler.Add(rack);
            }
            var uclar = kat.ToList();
            rack.TvPrizSayisi = uclar.Count;
            while (rack.TvHatlari.Count < uclar.Count)
                rack.TvHatlari.Add(new ElvHatMetraj());
            while (rack.TvHatlari.Count > uclar.Count)
                rack.TvHatlari.RemoveAt(rack.TvHatlari.Count - 1);
            for (int i = 0; i < uclar.Count; i++)
            {
                rack.TvHatlari[i].HatNo = i + 1;
                rack.TvHatlari[i].Ad = uclar[i].Ad;
                if (string.IsNullOrWhiteSpace(rack.TvHatlari[i].KabloTipi)
                    || rack.TvHatlari[i].KabloTipi.Contains("RG-", StringComparison.OrdinalIgnoreCase))
                    rack.TvHatlari[i].KabloTipi = "Cat6A F/UTP LSZH";
            }
        }
        tv.IpTv.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));
    }

    public static IReadOnlyList<ElvTvDogrulamaSorunu> Dogrula(ElvTvSistemi tv)
    {
        var sorunlar = new List<ElvTvDogrulamaSorunu>();
        if (tv.Teknoloji == TvDagitimTeknolojisi.DirektLnb)
        {
            if (tv.DirektLnb.LnbTipi.Contains("Quattro", StringComparison.OrdinalIgnoreCase))
                sorunlar.Add(new("LNB", "Quattro LNB doğrudan TV prizlerine bağlanamaz; multiswitch kullanılmalıdır.", true));
            if (tv.DirektLnb.LnbCikisSayisi * Math.Max(1, tv.DirektLnb.LnbSayisi) < tv.DirektLnb.Hatlar.Count)
                sorunlar.Add(new("LNB", "LNB çıkış kapasitesi toplam tuner hattını karşılamıyor.", true));
            if (tv.DirektLnb.Hatlar.GroupBy(h => h.LnbCikisNo).Any(g => g.Key > 0 && g.Count() > 1))
                sorunlar.Add(new("LNB", "Aynı LNB çıkışı birden fazla tuner hattına atanmış.", true));
            if (tv.DirektLnb.Hatlar.Any(h => h.KabloTipi.Contains("Cat", StringComparison.OrdinalIgnoreCase)))
                sorunlar.Add(new("Kablo", "Direkt LNB hatlarında koaksiyel kablo kullanılmalıdır.", true));
        }
        else if (tv.Teknoloji == TvDagitimTeknolojisi.IpTv)
        {
            var ag = tv.IpTv.AgAyarlari;
            if (ag.VlanId is < 1 or > 4094)
                sorunlar.Add(new("VLAN", "IP-TV VLAN kimliği 1–4094 aralığında olmalıdır.", true));
            if (ag.Multicast && !ag.IgmpSnooping)
                sorunlar.Add(new("IGMP", "Multicast IP-TV için IGMP snooping etkin olmalıdır.", true));
            if (ag.Multicast && !ag.IgmpQuerier)
                sorunlar.Add(new("IGMP", "Multicast router yoksa IP-TV VLAN'ında IGMP querier gereklidir.", true));
            if (tv.IpTv.Akislar.GroupBy(a => (a.MulticastGrupIp.Trim(), a.Port)).Any(g => g.Count() > 1))
                sorunlar.Add(new("Akışlar", "Aynı multicast grup IP/port çifti birden fazla kanalda kullanılmış.", true));
            if (tv.IpTv.Akislar.Any(a => !System.Net.IPAddress.TryParse(a.MulticastGrupIp, out var ip)
                    || ip.GetAddressBytes()[0] is < 224 or > 239))
                sorunlar.Add(new("Akışlar", "Multicast grup adresi 224.0.0.0–239.255.255.255 aralığında olmalıdır.", true));
            foreach (var rack in tv.IpTv.RackKabinler)
            {
                int port = rack.Cihazlar.Sum(c => PortSayisi(c.Ad) * Math.Max(1, c.Adet));
                if (rack.Cihazlar.Count > 0 && port < rack.TvPrizSayisi)
                    sorunlar.Add(new(rack.Ad, $"Switch portu yetersiz: {port}/{rack.TvPrizSayisi}.", true));
                if (rack.TvHatlari.Any(h => h.KabloTipi.Contains("RG-", StringComparison.OrdinalIgnoreCase)))
                    sorunlar.Add(new(rack.Ad, "IP-TV uç hatlarında Cat veya fiber kablo kullanılmalıdır.", true));
                if (rack.TvHatlari.Any(h => h.Metraj > 100 && h.KabloTipi.Contains("Cat", StringComparison.OrdinalIgnoreCase)))
                    sorunlar.Add(new(rack.Ad, "100 m üzerindeki bakır IP-TV hattı fiber/aktif çözüm gerektirir.", false));
            }
        }
        return sorunlar;
    }

    private static int PortSayisi(string ad)
    {
        var m = Regex.Match(ad ?? "", @"(\d+)\s*Port", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out int p) ? p : 0;
    }
}
