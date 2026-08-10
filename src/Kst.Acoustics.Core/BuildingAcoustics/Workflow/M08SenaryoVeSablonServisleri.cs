using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Workflow;

/// <summary>M08 akustik girdilerini kararlı kimliklerle senaryoya alır ve canlı modele yerinde uygular.</summary>
public sealed class M08SenaryoServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public AkustikSenaryo Kaydet(BinaProje proje, string ad, string aciklama = "")
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentException.ThrowIfNullOrWhiteSpace(ad);
        var hesap = new BinaAkustikDegerlendirmeServisi().Hesapla(proje);
        var senaryo = new AkustikSenaryo
        {
            Ad = ad.Trim(), Aciklama = aciklama.Trim(),
            KaynakBinaGeometriRevizyon = proje.BinaGeometriRevizyon,
            GirdiParmakIzi = hesap.GirdiParmakIzi,
            Sonuclar = hesap.Satirlar.Select(x => new SenaryoSonucOzeti
            {
                SonucId = x.SonucId, SonucTuru = x.SonucTuru.ToString(), Deger = x.Deger,
                Limit = x.LimitDeger, Marj = x.MarjDb, Durum = x.Durum, Birim = x.Birim,
            }).ToList(),
        };
        ProjeAlanlari(proje, senaryo);
        foreach (var mekan in proje.Katlar.SelectMany(k => k.Mekanlar))
        {
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "KullanimTipi", mekan.KullanimTipi);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "KaynakDerecesi", mekan.KaynakDerecesi);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "AliciSinifi", mekan.AliciSinifi);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "EmilimAlani", mekan.EmilimAlani);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "TesisatSurekliDb", mekan.TesisatSurekliDb);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "TesisatKesikliDb", mekan.TesisatKesikliDb);
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "ZeminYapiElemaniId", EtkinId(mekan.ZeminYapiElemani));
            Ekle(senaryo, SenaryoHedefTuru.Mekan, mekan.Id, "TavanYapiElemaniId", EtkinId(mekan.TavanYapiElemani));
            foreach (var bolme in mekan.Bolmeler)
            {
                Ekle(senaryo, SenaryoHedefTuru.Bolme, bolme.Id, "YapiElemaniId", EtkinId(bolme.YapiElemani));
                foreach (var aciklik in bolme.Acikliklar)
                    Ekle(senaryo, SenaryoHedefTuru.Aciklik, aciklik.Id, "YapiElemaniId", EtkinId(aciklik.YapiElemani));
                foreach (var junction in bolme.Junctions)
                    Ekle(senaryo, SenaryoHedefTuru.Junction, junction.Id, "KijOverrideDb", junction.KijOverrideDb);
            }
        }
        proje.Senaryolar ??= new();
        proje.Senaryolar.Add(senaryo);
        proje.AktifSenaryoId = senaryo.Id;
        return senaryo;
    }

    public int Uygula(BinaProje proje, AkustikSenaryo senaryo)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(senaryo);
        var mekanlar = proje.Katlar.SelectMany(k => k.Mekanlar).ToDictionary(x => x.Id);
        var bolmeler = mekanlar.Values.SelectMany(x => x.Bolmeler).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var acikliklar = bolmeler.Values.SelectMany(x => x.Acikliklar).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var junctions = bolmeler.Values.SelectMany(x => x.Junctions).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
        var elemanlar = Elemanlar(proje).ToDictionary(EtkinKimlik);
        var uygulanmis = 0;
        foreach (var d in senaryo.Degisiklikler)
        {
            switch (d.HedefTuru)
            {
                case SenaryoHedefTuru.Proje: uygulanmis += ProjeAlaniUygula(proje, d); break;
                case SenaryoHedefTuru.Mekan when d.HedefId is { } id && mekanlar.TryGetValue(id, out var m):
                    uygulanmis += MekanAlaniUygula(m, d, elemanlar); break;
                case SenaryoHedefTuru.Bolme when d.HedefId is { } id && bolmeler.TryGetValue(id, out var b):
                    if (d.AlanAdi == "YapiElemaniId") { b.YapiElemani = ElemanOku(d, elemanlar); uygulanmis++; } break;
                case SenaryoHedefTuru.Aciklik when d.HedefId is { } id && acikliklar.TryGetValue(id, out var a):
                    if (d.AlanAdi == "YapiElemaniId") { a.YapiElemani = ElemanOku(d, elemanlar); uygulanmis++; } break;
                case SenaryoHedefTuru.Junction when d.HedefId is { } id && junctions.TryGetValue(id, out var j):
                    if (d.AlanAdi == "KijOverrideDb") { j.KijOverrideDb = Oku<double>(d); uygulanmis++; } break;
            }
        }
        proje.AktifSenaryoId = senaryo.Id;
        senaryo.GuncelDegil = senaryo.KaynakBinaGeometriRevizyon != proje.BinaGeometriRevizyon;
        senaryo.GuncellikNotu = senaryo.GuncelDegil ? "Senaryo farklı bir M50 geometri revizyonunda oluşturuldu." : "";
        return uygulanmis;
    }

    private static void ProjeAlanlari(BinaProje p, AkustikSenaryo s)
    {
        Ekle(s, SenaryoHedefTuru.Proje, null, "BinaIslevi", p.BinaIslevi);
        Ekle(s, SenaryoHedefTuru.Proje, null, "BinaSinifi", p.BinaSinifi);
        Ekle(s, SenaryoHedefTuru.Proje, null, "AkustikPerformansSinifi", p.AkustikPerformansSinifi);
        Ekle(s, SenaryoHedefTuru.Proje, null, "DisGurultuDb", p.DisGurultuDb);
        Ekle(s, SenaryoHedefTuru.Proje, null, "DisGurultuCepheMaruziyeti", p.DisGurultuCepheMaruziyeti);
    }

    private static int ProjeAlaniUygula(BinaProje p, SenaryoDegisiklikKaydi d)
    {
        switch (d.AlanAdi)
        {
            case "BinaIslevi": p.BinaIslevi = Oku<BinaIslevi>(d); break;
            case "BinaSinifi": p.BinaSinifi = Oku<BinaSinifi>(d); break;
            case "AkustikPerformansSinifi": p.AkustikPerformansSinifi = Oku<AkustikPerformansSinifi>(d); break;
            case "DisGurultuDb": p.DisGurultuDb = Oku<double>(d); break;
            case "DisGurultuCepheMaruziyeti": p.DisGurultuCepheMaruziyeti = Oku<CepheMaruziyet>(d); break;
            default: return 0;
        }
        return 1;
    }

    private static int MekanAlaniUygula(Mekan m, SenaryoDegisiklikKaydi d, IReadOnlyDictionary<Guid, YapiElemani> es)
    {
        switch (d.AlanAdi)
        {
            case "KullanimTipi": m.KullanimTipi = Oku<string>(d) ?? ""; break;
            case "KaynakDerecesi": m.KaynakDerecesi = Oku<string>(d) ?? ""; break;
            case "AliciSinifi": m.AliciSinifi = Oku<string>(d) ?? ""; break;
            case "EmilimAlani": m.EmilimAlani = Math.Max(0, Oku<double>(d)); break;
            case "TesisatSurekliDb": m.TesisatSurekliDb = Math.Max(0, Oku<double>(d)); break;
            case "TesisatKesikliDb": m.TesisatKesikliDb = Math.Max(0, Oku<double>(d)); break;
            case "ZeminYapiElemaniId": m.ZeminYapiElemani = ElemanOku(d, es); break;
            case "TavanYapiElemaniId": m.TavanYapiElemani = ElemanOku(d, es); break;
            default: return 0;
        }
        return 1;
    }

    private static void Ekle<T>(AkustikSenaryo s, SenaryoHedefTuru tur, Guid? id, string alan, T deger) =>
        s.Degisiklikler.Add(new() { HedefTuru = tur, HedefId = id, AlanAdi = alan,
            YeniDegerJson = JsonSerializer.Serialize(deger, JsonOptions) });
    private static T Oku<T>(SenaryoDegisiklikKaydi d) =>
        JsonSerializer.Deserialize<T>(d.YeniDegerJson, JsonOptions)!;
    private static YapiElemani? ElemanOku(SenaryoDegisiklikKaydi d, IReadOnlyDictionary<Guid, YapiElemani> es)
    {
        var id = Oku<Guid?>(d);
        return id is { } x && es.TryGetValue(x, out var e) ? e : null;
    }
    private static Guid? EtkinId(YapiElemani? e) => e is null ? null : EtkinKimlik(e);
    private static Guid EtkinKimlik(YapiElemani e) => e.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(e) : e.Id;
    private static IEnumerable<YapiElemani> Elemanlar(BinaProje p) => p.YapiElemaniKutuphanesi
        .Concat(p.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler.Select(b => b.YapiElemani)
            .Concat(m.Bolmeler.SelectMany(b => b.Acikliklar).Select(a => a.YapiElemani))
            .Append(m.ZeminYapiElemani).Append(m.TavanYapiElemani)))
        .Where(x => x is not null).Cast<YapiElemani>().GroupBy(EtkinKimlik).Select(x => x.First());
}

/// <summary>Proje kurallarını taşınabilir JSON şablonuna dönüştürür ve kimlik güvenli uygular.</summary>
public static class M08SablonServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() },
    };

    public static BinaAkustikSablon ProjedenOlustur(BinaProje p, string ad)
    {
        ArgumentNullException.ThrowIfNull(p);
        var s = new BinaAkustikSablon { Ad = string.IsNullOrWhiteSpace(ad) ? "M08 Proje Şablonu" : ad.Trim(),
            BinaIslevi = p.BinaIslevi, BinaSinifi = p.BinaSinifi,
            AkustikPerformansSinifi = p.AkustikPerformansSinifi, DisGurultuDb = p.DisGurultuDb,
            DisGurultuCepheMaruziyeti = p.DisGurultuCepheMaruziyeti };
        foreach (var m in p.Katlar.SelectMany(k => k.Mekanlar).GroupBy(x => x.Ad, StringComparer.CurrentCultureIgnoreCase).Select(x => x.First()))
            s.MekanKurallari.Add(new() { AdKalibi = $"^{Regex.Escape(m.Ad)}$", RegexMi = true,
                KullanimTipi = m.KullanimTipi, KaynakDerecesi = m.KaynakDerecesi, AliciSinifi = m.AliciSinifi,
                ZeminYapiElemaniId = EtkinId(m.ZeminYapiElemani), TavanYapiElemaniId = EtkinId(m.TavanYapiElemani) });
        foreach (var b in p.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler)
                     .Where(x => x.YapiElemani is not null).GroupBy(x => new { x.IsDisCephe, x.KomsulukTipi,
                         Kaynak = x.KaynakMekan?.Ad ?? "", Alici = x.AliciMekan?.Ad ?? "" }).Select(x => x.First()))
            s.BolmeKurallari.Add(new() { DisCepheMi = b.IsDisCephe, KomsulukTipi = b.KomsulukTipi,
                KaynakMekanAdKalibi = b.KaynakMekan?.Ad ?? "", AliciMekanAdKalibi = b.AliciMekan?.Ad ?? "",
                YapiElemaniId = EtkinId(b.YapiElemani) });
        return s;
    }

    public static SablonUygulamaSonucu Uygula(BinaProje p, BinaAkustikSablon s)
    {
        ArgumentNullException.ThrowIfNull(p); ArgumentNullException.ThrowIfNull(s);
        var r = new SablonUygulamaSonucu();
        var es = p.YapiElemaniKutuphanesi.Where(x => x is not null).GroupBy(EtkinKimlik).ToDictionary(x => x.Key, x => x.First());
        p.BinaIslevi = s.BinaIslevi; p.BinaSinifi = s.BinaSinifi;
        p.AkustikPerformansSinifi = s.AkustikPerformansSinifi; p.DisGurultuDb = s.DisGurultuDb;
        p.DisGurultuCepheMaruziyeti = s.DisGurultuCepheMaruziyeti;
        foreach (var m in p.Katlar.SelectMany(k => k.Mekanlar))
        {
            var k = s.MekanKurallari.FirstOrDefault(x => Eslesir(m.Ad, x.AdKalibi, x.RegexMi));
            if (k is null) { r.AtlananSayisi++; continue; }
            m.KullanimTipi = k.KullanimTipi; m.KaynakDerecesi = k.KaynakDerecesi; m.AliciSinifi = k.AliciSinifi;
            if (k.ZeminYapiElemaniId is { } z && es.TryGetValue(z, out var ze)) m.ZeminYapiElemani = ze;
            if (k.TavanYapiElemaniId is { } t && es.TryGetValue(t, out var te)) m.TavanYapiElemani = te;
            r.MekanGuncellemeSayisi++;
        }
        foreach (var b in p.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler))
        {
            var k = s.BolmeKurallari.FirstOrDefault(x => (x.DisCepheMi is null || x.DisCepheMi == b.IsDisCephe) &&
                (x.KomsulukTipi is null || x.KomsulukTipi == b.KomsulukTipi) &&
                Eslesir(b.KaynakMekan?.Ad ?? "", x.KaynakMekanAdKalibi, false) &&
                Eslesir(b.AliciMekan?.Ad ?? "", x.AliciMekanAdKalibi, false));
            if (k?.YapiElemaniId is { } id && es.TryGetValue(id, out var e)) { b.YapiElemani = e; r.BolmeAtamaSayisi++; }
        }
        return r;
    }

    public static void Kaydet(BinaAkustikSablon s, string yol) => AtomikYaz(yol, JsonSerializer.Serialize(s, JsonOptions));
    public static BinaAkustikSablon Oku(string yol) => JsonSerializer.Deserialize<BinaAkustikSablon>(File.ReadAllText(yol), JsonOptions)
        ?? throw new InvalidDataException("M08 şablonu okunamadı.");

    private static bool Eslesir(string value, string pattern, bool regex) => string.IsNullOrWhiteSpace(pattern) ||
        (regex ? Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250)) : string.Equals(value.Trim(), pattern.Trim(), StringComparison.CurrentCultureIgnoreCase));
    private static Guid? EtkinId(YapiElemani? e) => e is null ? null : EtkinKimlik(e);
    private static Guid EtkinKimlik(YapiElemani e) => e.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(e) : e.Id;
    private static void AtomikYaz(string yol, string metin)
    {
        var tam = Path.GetFullPath(yol); Directory.CreateDirectory(Path.GetDirectoryName(tam)!);
        var tmp = tam + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(tmp, metin); File.Move(tmp, tam, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
