using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Canlı projeye dokunmadan bir M08 projesini hesaplar. Senaryo, optimizasyon ve
/// belirsizlik analizleri aynı hesap yolunu kullanabilsin diye bütün işlemler derin
/// kopya üzerinde yürütülür.
/// </summary>
public sealed class BinaAkustikDegerlendirmeServisi
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        MaxDepth = 256,
    };

    public sealed record DegerlendirmeSonucu(
        BinaProje HesaplananProje,
        IReadOnlyList<RaporSatiri> Satirlar,
        string GirdiParmakIzi)
    {
        public RaporSatiri? SonucBul(Guid sonucId, AkustikRaporSonucTuru? tur = null) =>
            Satirlar.FirstOrDefault(s => s.SonucId == sonucId && (tur is null || s.SonucTuru == tur));
    }

    public DegerlendirmeSonucu Hesapla(
        BinaProje kaynak,
        ISet<Guid>? haricTutulanSonucIdleri = null)
    {
        ArgumentNullException.ThrowIfNull(kaynak);

        var proje = DerinKlonla(kaynak);
        SonuclariTemizle(proje);

        var hava = new HavaSesiHesapServisi();
        var darbe = new DarbeSesiHesapServisi();
        var dis = new DisGurultuServisi();

        foreach (var kat in proje.Katlar)
        foreach (var mekan in kat.Mekanlar)
        {
            if (HesapDisiMekan(mekan)) continue;

            foreach (var bolme in mekan.Bolmeler)
            {
                if (HesapDisiBolme(bolme) || bolme.YapiElemani is null) continue;

                if (bolme.YapiElemani.Tip == YapiElemaniTipi.Doseme)
                {
                    if (bolme.AliciMekan is null) continue;
                    darbe.HesaplaApparentLnw(bolme);
                    darbe.HesaplaApparentLnTw(bolme);
                    darbe.HesaplaOktavApparentLn(bolme);
                    continue;
                }

                hava.HesaplaUcuncuOktavApparentR(bolme);
                hava.HesaplaOktavApparentR(bolme);
                if (!bolme.IsDisCephe)
                    hava.HesaplaDntw(bolme);
            }

            // Dış cephe tek tek parçaların değil, mekanın bütün cephelerinin enerji
            // toplamıdır. Servis sonucu aynı mekanın bütün cephe parçalarına yazar.
            var temsilciCephe = mekan.Bolmeler.FirstOrDefault(b =>
                b.IsDisCephe && !HesapDisiBolme(b));
            if (temsilciCephe is not null)
                dis.HesaplaSpektralDisCephe(proje, temsilciCephe);
        }

        // Rapor servisi; fiziksel hava/cephe bölmelerine ek olarak sentetik darbe,
        // tesisat ve reverberasyon satırlarını aynı kararlı SonucId'lerle üretir.
        var satirlar = new BinaAkustikRaporServisi()
            .TabloVerisiOlustur(proje, haricTutulanSonucIdleri);

        return new DegerlendirmeSonucu(proje, satirlar, GirdiParmakIzi(kaynak));
    }

    public static BinaProje DerinKlonla(BinaProje kaynak)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        var json = JsonSerializer.Serialize(kaynak, CloneOptions);
        var clone = JsonSerializer.Deserialize<BinaProje>(json, CloneOptions)
            ?? throw new InvalidOperationException("M08 proje analiz kopyası oluşturulamadı.");
        BinaProjeMigrator.Normalize(clone);
        return clone;
    }

    /// <summary>
    /// Analiz sonucunun hangi girdiye ait olduğunu belirleyen, hesap çıktılarından
    /// bağımsız ve sıralaması kararlı özet parmak izi.
    /// </summary>
    public static string GirdiParmakIzi(BinaProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var sb = new StringBuilder(4096);
        sb.Append(proje.Id).Append('|')
          .Append((int)proje.BinaSinifi).Append('|')
          .Append((int)proje.BinaIslevi).Append('|')
          .Append((int)proje.AkustikPerformansSinifi).Append('|')
          .Append(proje.DisGurultuDb.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
          .Append(proje.BinaGeometriRevizyon?.ToString() ?? "-").Append('|');

        foreach (var kat in proje.Katlar.OrderBy(k => k.BinaGeometriKatId).ThenBy(k => k.KatNo))
        foreach (var mekan in kat.Mekanlar.OrderBy(m => m.Id))
        {
            sb.Append(mekan.Id).Append(':').Append(mekan.Ad).Append(':')
              .Append(mekan.Alan.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':')
              .Append(mekan.Yukseklik.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':')
              .Append(mekan.KaynakDerecesi).Append(':').Append(mekan.AliciSinifi).Append(':')
              .Append(MalzemeOzeti(mekan.ZeminYapiElemani)).Append(':')
              .Append(MalzemeOzeti(mekan.TavanYapiElemani)).Append('|');

            foreach (var bolme in mekan.Bolmeler.OrderBy(b => b.Id))
            {
                sb.Append(bolme.Id).Append(':')
                  .Append(bolme.Alan.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':')
                  .Append(MalzemeOzeti(bolme.YapiElemani)).Append(':');
                foreach (var aciklik in bolme.Acikliklar.OrderBy(a => a.AciklikId).ThenBy(a => a.Baslangic.X))
                    sb.Append(aciklik.AciklikId ?? "elle").Append('=')
                      .Append(MalzemeOzeti(aciklik.YapiElemani)).Append(',');
                foreach (var junction in bolme.Junctions.OrderBy(j => j.Id))
                    sb.Append(junction.Id).Append('=')
                      .Append(junction.KijOverrideDb.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                sb.Append('|');
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string MalzemeOzeti(YapiElemani? e)
    {
        if (e is null) return "-";
        return string.Join('~', e.Ad, (int)e.Tip,
            e.Rw.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            e.DeltaRDb.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            e.Lnw.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            e.DeltaLw.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(',', e.RThirdOctave.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static void SonuclariTemizle(BinaProje proje)
    {
        foreach (var bolme in proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler))
        {
            bolme.ApparentRw = 0;
            bolme.ApparentLnw = 0;
            bolme.ApparentLnTw = 0;
            bolme.Dntw = 0;
            bolme.OktavHavaSesiSonucu = null;
            bolme.OktavDarbeSesiSonucu = null;
            bolme.UcuncuOktavHavaSesiSonucu = null;
            bolme.DisCepheSpektralSonuc = null;
        }
    }

    private static bool HesapDisiMekan(Mekan? mekan) =>
        mekan is not null &&
        (mekan.Sinif == MekanSinifi.HesapDisi ||
         EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
         MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi);

    private static bool HesapDisiBolme(Bolme bolme) =>
        HesapDisiMekan(bolme.KaynakMekan) || HesapDisiMekan(bolme.AliciMekan);
}
