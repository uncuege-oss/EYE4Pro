using Kst.Acoustics.Core.BuildingAcoustics.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

/// <summary>
/// Deserialize edilmiş bir .e4p08 JSON'unu (eski/eksik alanlar dahil) güvenli varsayılanlarla
/// tamamlar. Eski JSON'larda bazı liste alanları açık `null` olarak yazılmış olabilir
/// (System.Text.Json bu durumda property'ye null atar, koleksiyon initializer'ı devreye girmez);
/// bu, ilerleyen kodda NullReferenceException'a yol açar. ProjeAc, deserialize edilen projeyi
/// doğrudan (Proje = yuklenen) atamadan önce bunu çağırmalıdır.
/// </summary>
public static class BinaProjeMigrator
{
    /// <summary>
    /// Bir dosyayı deserialize etmeden önce M08 belge kimliğini denetler. Eski .e4p08
    /// belgelerinde ModuleId bulunmadığı için yalnız doğru uzantı ve ayırt edici kök alanlar
    /// birlikte mevcutsa geriye uyumlu olarak kabul edilir. Genel *.json dosyaları kabul edilmez.
    /// </summary>
    public static bool DosyaKimliginiDogrula(string json, string dosyaYolu, out string hata)
    {
        hata = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                hata = "M08 proje belgesinin kökü bir JSON nesnesi olmalıdır.";
                return false;
            }

            var root = doc.RootElement;
            static bool JsonListeMi(JsonElement element) =>
                element.ValueKind == JsonValueKind.Array ||
                (element.ValueKind == JsonValueKind.Object &&
                 element.TryGetProperty("$values", out var values) &&
                 values.ValueKind == JsonValueKind.Array);

            if (root.TryGetProperty(nameof(BinaProje.ModuleId), out var moduleElement))
            {
                if (moduleElement.ValueKind != JsonValueKind.String)
                {
                    hata = "M08 belge kimliği metin biçiminde olmalıdır.";
                    return false;
                }
                var moduleId = moduleElement.GetString();
                if (!string.Equals(moduleId, BinaProje.CurrentModuleId, StringComparison.OrdinalIgnoreCase))
                {
                    hata = $"Bu dosya M08'e ait değil (ModuleId: {moduleId ?? "yok"}).";
                    return false;
                }
            }
            else
            {
                // ModuleId'siz yalnız eski, özgün .e4p08 belgelerine izin ver. Böylece
                // .e4bina.json veya başka modüllerin kısmen benzer JSON'ları açılmaz.
                if (!string.Equals(Path.GetExtension(dosyaYolu), ".e4p08", StringComparison.OrdinalIgnoreCase) ||
                    !root.TryGetProperty(nameof(BinaProje.Katlar), out var katlar) ||
                    !JsonListeMi(katlar) ||
                    !root.TryGetProperty(nameof(BinaProje.ProjeAdi), out var projeAdi) ||
                    projeAdi.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    hata = "Dosyada M08 belge kimliği yok. Yalnız eski .e4p08 belgeleri dönüştürülebilir.";
                    return false;
                }
            }

            if (root.TryGetProperty(nameof(BinaProje.SchemaVersion), out var schemaElement))
            {
                if (schemaElement.ValueKind != JsonValueKind.Number ||
                    !schemaElement.TryGetInt32(out var schemaVersion) || schemaVersion <= 0)
                {
                    hata = "M08 şema sürümü pozitif bir tam sayı olmalıdır.";
                    return false;
                }
                if (schemaVersion > BinaProje.CurrentSchemaVersion)
                {
                    hata = $"Proje şeması ({schemaVersion}) bu uygulamanın desteklediği sürümden " +
                           $"({BinaProje.CurrentSchemaVersion}) daha yeni.";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            hata = $"Geçersiz JSON: {ex.Message}";
            return false;
        }
    }

    public static void Normalize(BinaProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var kaynakSurum = proje.SchemaVersion <= 0 ? 1 : proje.SchemaVersion;

        // ModuleId doğrulaması deserialize öncesinde yapılır. Normalize hiçbir zaman başka
        // bir modül kimliğini M08'e çevirmez; yalnız kimliksiz eski belgeyi yükseltir.
        if (string.IsNullOrWhiteSpace(proje.ModuleId))
            proje.ModuleId = BinaProje.CurrentModuleId;
        if (proje.SchemaVersion <= 0)
            proje.SchemaVersion = 1;
        proje.ProjeAdi ??= "";
        proje.Adres ??= "";
        proje.MusteriAdi ??= "";
        proje.SorumluKisiKurulus ??= "";
        proje.DisGurultuThirdOctave ??= new();
        proje.BagimsizBolumler ??= new();
        proje.DuseyMekanBirlesimleri ??= new();
        proje.YapiElemaniKutuphanesi ??= new();
        proje.AkustikNoktalar ??= new();
        proje.DisGurultuKaynaklari ??= new();
        proje.Katlar ??= new();
        proje.Senaryolar ??= new();
        proje.OnayKayitlari ??= new();
        proje.DenetimKayitlari ??= new();
        proje.SahaOlcumleri ??= new();
        proje.RaporAyarlari ??= new();
        proje.RaporAyarlari.StandartSurumNotu = BosIse(
            proje.RaporAyarlari.StandartSurumNotu, M08GuncelStandartSurumleri.DogrulamaNotu);
        proje.RaporAyarlari.HesapStandartlariSurumu = BosIse(
            proje.RaporAyarlari.HesapStandartlariSurumu, M08GuncelStandartSurumleri.Hesap);
        proje.RaporAyarlari.DerecelendirmeStandartlariSurumu = BosIse(
            proje.RaporAyarlari.DerecelendirmeStandartlariSurumu, M08GuncelStandartSurumleri.Derecelendirme);
        proje.RaporAyarlari.OlcumStandartlariSurumu = BosIse(
            proje.RaporAyarlari.OlcumStandartlariSurumu, M08GuncelStandartSurumleri.Olcum);
        proje.RaporAyarlari.ServisOlcumStandartlariSurumu = BosIse(
            proje.RaporAyarlari.ServisOlcumStandartlariSurumu, M08GuncelStandartSurumleri.ServisOlcumu);
        proje.RaporAyarlari.BelirsizlikStandardiSurumu = BosIse(
            proje.RaporAyarlari.BelirsizlikStandardiSurumu, M08GuncelStandartSurumleri.Belirsizlik);
        proje.RaporKapsamDisiKayitlari ??= new();
        proje.GorevDurumlari ??= new();
        proje.MetrajAyarlari ??= new();
        proje.MetrajAyarlari.ParaBirimi = BosIse(proje.MetrajAyarlari.ParaBirimi, "TRY");
        proje.MetrajAyarlari.VarsayilanFireYuzdesi =
            Math.Clamp(proje.MetrajAyarlari.VarsayilanFireYuzdesi, 0m, 100m);

        if (proje.BinaIslevi == BinaIslevi.Tanimsiz)
            proje.BinaIslevi = BinaIslevi.Konut;
        if (proje.DisGurultuDb <= 0)
            proje.DisGurultuDb = 65.0;

        foreach (var eleman in proje.YapiElemaniKutuphanesi.Where(x => x is not null))
            NormalizeYapiElemani(eleman);

        foreach (var kat in proje.Katlar.Where(x => x is not null))
            NormalizeKat(kat, kaynakSurum);

        NormalizeProfesyonelKayitlar(proje);

        // Bellekteki belge artık güncel biçimde kaydedilir.
        proje.SchemaVersion = BinaProje.CurrentSchemaVersion;
    }

    private static void NormalizeKat(Kat kat, int kaynakSurum)
    {
        kat.Ad ??= "";
        kat.DxfFilePath ??= "";
        kat.Mekanlar ??= new();

        foreach (var mekan in kat.Mekanlar.Where(x => x is not null))
            NormalizeMekan(mekan, kaynakSurum);
    }

    private static void NormalizeMekan(Mekan mekan, int kaynakSurum)
    {
        mekan.Ad ??= "";
        mekan.KullanimTipi ??= "";
        mekan.KaynakDerecesi ??= "";
        mekan.AliciSinifi ??= "";
        mekan.Poligon ??= new();
        // Mekan.Bolmeler setter zaten null-guard'lı (value ?? new ObservableCollection<Bolme>()).

        NormalizeYapiElemani(mekan.ZeminYapiElemani);
        NormalizeYapiElemani(mekan.TavanYapiElemani);

        foreach (var bolme in mekan.Bolmeler.Where(x => x is not null))
            NormalizeBolme(bolme, kaynakSurum);
    }

    private static void NormalizeBolme(Bolme bolme, int kaynakSurum)
    {
        bolme.Ad ??= "";
        bolme.Acikliklar ??= new();
        bolme.Menfezler ??= new();
        bolme.Junctions ??= new();
        NormalizeYapiElemani(bolme.YapiElemani);

        for (var i = 0; i < bolme.Acikliklar.Count; i++)
        {
            var aciklik = bolme.Acikliklar[i];
            if (aciklik is null)
                continue;

            if (kaynakSurum < 3 || aciklik.Id == Guid.Empty)
            {
                var harici = string.IsNullOrWhiteSpace(aciklik.AciklikId)
                    ? $"{aciklik.Baslangic.X.ToString("R", CultureInfo.InvariantCulture)};" +
                      $"{aciklik.Baslangic.Y.ToString("R", CultureInfo.InvariantCulture)};" +
                      $"{aciklik.Bitis.X.ToString("R", CultureInfo.InvariantCulture)};" +
                      $"{aciklik.Bitis.Y.ToString("R", CultureInfo.InvariantCulture)};{i}"
                    : aciklik.AciklikId;
                aciklik.Id = M08KararliKimlik.Olustur("M08-ACIKLIK", bolme.Id.ToString("N"), harici);
            }
            aciklik.AciklikId ??= "";
            NormalizeYapiElemani(aciklik.YapiElemani);
        }

        foreach (var menfez in bolme.Menfezler.Where(x => x is not null))
        {
            menfez.Ad ??= "";
            menfez.DneThirdOctave ??= new();
        }
    }

    private static void NormalizeYapiElemani(YapiElemani? eleman)
    {
        if (eleman is null)
            return;

        eleman.Ad ??= "";
        eleman.Uretici ??= "";
        eleman.UrunKodu ??= "";
        eleman.Kategori ??= "";
        eleman.PozNo ??= "";
        eleman.ParaBirimi = BosIse(eleman.ParaBirimi, "TRY");
        eleman.Katmanlar ??= new();
        eleman.RThirdOctave ??= new();
        eleman.DeltaRThirdOctave ??= new();
        eleman.Belgeler ??= new();
        eleman.BirimFiyat = Math.Max(0m, eleman.BirimFiyat);
        eleman.FireYuzdesi = Math.Clamp(eleman.FireYuzdesi, 0m, 100m);
        if (eleman.Id == Guid.Empty)
            eleman.Id = M08KararliKimlik.YapiElemani(eleman);

        foreach (var katman in eleman.Katmanlar.Where(x => x is not null))
            katman.MalzemeAdi ??= "";

        foreach (var belge in eleman.Belgeler.Where(x => x is not null))
        {
            if (belge.Id == Guid.Empty)
                belge.Id = Guid.NewGuid();
            belge.Ad ??= "";
            belge.DosyaYolu ??= "";
            belge.Url ??= "";
            belge.BelgeNo ??= "";
            belge.VerenKurulus ??= "";
            belge.Sha256 ??= "";
            belge.MimeType ??= "";
            belge.Notlar ??= "";
        }
    }

    private static void NormalizeProfesyonelKayitlar(BinaProje proje)
    {
        foreach (var senaryo in proje.Senaryolar.Where(x => x is not null))
        {
            if (senaryo.Id == Guid.Empty) senaryo.Id = Guid.NewGuid();
            senaryo.Ad = BosIse(senaryo.Ad, "Yeni Senaryo");
            senaryo.Aciklama ??= "";
            senaryo.GirdiParmakIzi ??= "";
            senaryo.GuncellikNotu ??= "";
            senaryo.Degisiklikler ??= new();
            senaryo.Sonuclar ??= new();
            foreach (var degisiklik in senaryo.Degisiklikler.Where(x => x is not null))
            {
                if (degisiklik.Id == Guid.Empty) degisiklik.Id = Guid.NewGuid();
                degisiklik.HariciHedefId ??= "";
                degisiklik.AlanAdi ??= "";
                degisiklik.OncekiDegerJson ??= "";
                degisiklik.YeniDegerJson ??= "";
            }
            foreach (var sonuc in senaryo.Sonuclar.Where(x => x is not null))
            {
                sonuc.SonucTuru ??= "";
                sonuc.Durum ??= "";
                sonuc.Birim ??= "";
            }
        }

        foreach (var onay in proje.OnayKayitlari.Where(x => x is not null))
        {
            if (onay.Id == Guid.Empty) onay.Id = Guid.NewGuid();
            onay.Aciklama ??= "";
            onay.Onaylayan ??= "";
            onay.Kurulus ??= "";
            onay.ImzaOzeti ??= "";
            onay.GirdiParmakIzi ??= "";
        }

        foreach (var denetim in proje.DenetimKayitlari.Where(x => x is not null))
        {
            if (denetim.Id == Guid.Empty) denetim.Id = Guid.NewGuid();
            denetim.Kullanici ??= "";
            denetim.HedefTuru ??= "";
            denetim.HedefId ??= "";
            denetim.Aciklama ??= "";
            denetim.OncekiDegerOzeti ??= "";
            denetim.YeniDegerOzeti ??= "";
            denetim.GirdiParmakIzi ??= "";
        }

        foreach (var olcum in proje.SahaOlcumleri.Where(x => x is not null))
        {
            if (olcum.Id == Guid.Empty) olcum.Id = Guid.NewGuid();
            olcum.Ad = BosIse(olcum.Ad, "Ölçüm");
            olcum.KatKimligi ??= "";
            olcum.Birim = BosIse(olcum.Birim, olcum.Tur == SahaOlcumTuru.Reverberasyon ? "s" : "dB");
            olcum.BantFrekanslariHz ??= new();
            olcum.BantDegerleri ??= new();
            olcum.Standart ??= "";
            olcum.Yontem ??= "";
            olcum.Cihaz ??= "";
            olcum.Operator ??= "";
            olcum.KaynakDosya ??= "";
            olcum.KaynakDosyaSha256 ??= "";
            olcum.KaynakKayitId ??= "";
            olcum.Notlar ??= "";
        }

        foreach (var gorev in proje.GorevDurumlari.Where(x => x is not null))
        {
            gorev.Anahtar ??= "";
            gorev.Baslik ??= "";
            gorev.Aciklama ??= "";
            gorev.KatKimligi ??= "";
            gorev.Sorumlu ??= "";
            gorev.Not ??= "";
        }
    }

    private static string BosIse(string? deger, string varsayilan) =>
        string.IsNullOrWhiteSpace(deger) ? varsayilan : deger;
}
