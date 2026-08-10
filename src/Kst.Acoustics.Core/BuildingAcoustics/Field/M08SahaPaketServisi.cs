using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;

namespace Kst.Acoustics.Core.BuildingAcoustics.Field;

public sealed class M08SahaGorevi
{
    public Guid SonucId { get; set; }
    public string Ad { get; set; } = "";
    public SahaOlcumTuru Tur { get; set; }
    public string KatKimligi { get; set; } = "";
    public Guid? KaynakMekanId { get; set; }
    public Guid? AliciMekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public double BeklenenDeger { get; set; }
    public double LimitDeger { get; set; }
    public string Birim { get; set; } = "dB";
}

public sealed class M08SahaPaketVerisi
{
    public const string CurrentFormat = "EYE4PRO-M08-OFFLINE-FIELD";
    public const int CurrentVersion = 1;
    public string Format { get; set; } = CurrentFormat;
    public int Version { get; set; } = CurrentVersion;
    public Guid ProjeId { get; set; }
    public string ProjeAdi { get; set; } = "";
    public string KaynakRaporParmakIzi { get; set; } = "";
    public DateTimeOffset OlusturulmaUtc { get; set; }
    public List<M08SahaGorevi> Gorevler { get; set; } = new();
}

public sealed class M08SahaDonusBelgesi
{
    public const string CurrentFormat = "EYE4PRO-M08-FIELD-RETURN";
    public const int CurrentVersion = 1;
    public string Format { get; set; } = CurrentFormat;
    public int Version { get; set; } = CurrentVersion;
    public Guid ProjeId { get; set; }
    public string KaynakRaporParmakIzi { get; set; } = "";
    public DateTimeOffset DisaAktarilmaUtc { get; set; }
    public List<M08SahaDonusKaydi> Kayitlar { get; set; } = new();
}

public sealed class M08SahaDonusKaydi
{
    public Guid Id { get; set; }
    public Guid SonucId { get; set; }
    public string Ad { get; set; } = "Ölçüm";
    public SahaOlcumTuru Tur { get; set; }
    public string KatKimligi { get; set; } = "";
    public Guid? KaynakMekanId { get; set; }
    public Guid? AliciMekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public double Deger { get; set; }
    public string Birim { get; set; } = "dB";
    public double? Belirsizlik { get; set; }
    public List<double> BantFrekanslariHz { get; set; } = new();
    public List<double> BantDegerleri { get; set; } = new();
    public DateTime OlcumTarihi { get; set; }
    public string Standart { get; set; } = "";
    public string Yontem { get; set; } = "";
    public string Cihaz { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Notlar { get; set; } = "";
    public bool RaporlamaIcinOnayli { get; set; }
}

public sealed class M08SahaBirlesimSonucu
{
    public int Eklenen { get; internal set; }
    public int Guncellenen { get; internal set; }
    public int Atlanan { get; internal set; }
    public List<SahaOlcumKaydi> KabulEdilenKayitlar { get; } = new();
    public List<string> Hatalar { get; } = new();
    public List<string> Uyarilar { get; } = new();
    public bool Basarili => Hatalar.Count == 0;
}

/// <summary>Sunucu gerektirmeyen tek HTML saha formu ve kontrollü JSON geri birleştirme.</summary>
public static class M08SahaPaketServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static M08SahaPaketVerisi PaketVerisiOlustur(
        BinaProje proje,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        DateTimeOffset? zamanUtc = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var snapshot = AkustikRaporSnapshotServisi.Olustur(proje, haricTutulanSonucIdleri, zamanUtc);
        var rooms = proje.Katlar.SelectMany(k => k.Mekanlar.Select(m => (Kat: k, Mekan: m))).ToList();
        var partitions = rooms.SelectMany(x => x.Mekan.Bolmeler.Select(b => (x.Kat, x.Mekan, Bolme: b)))
            .GroupBy(x => x.Bolme.Id).ToDictionary(x => x.Key, x => x.First());
        var package = new M08SahaPaketVerisi
        {
            ProjeId = proje.Id,
            ProjeAdi = proje.ProjeAdi,
            KaynakRaporParmakIzi = snapshot.RaporParmakIziSha256,
            OlusturulmaUtc = snapshot.OlusturulmaUtc,
        };
        foreach (var row in snapshot.Sonuclar)
        {
            var task = new M08SahaGorevi
            {
                SonucId = row.SonucId,
                Ad = $"{row.Kat} / {row.Mekan} / {row.Bolme}",
                Tur = Tur(row.SonucTuru),
                KatKimligi = row.Kat,
                BeklenenDeger = row.Deger,
                LimitDeger = row.LimitDeger,
                Birim = row.Birim,
            };
            if (partitions.TryGetValue(row.SonucId, out var physical))
            {
                task.BolmeId = physical.Bolme.Id;
                task.KaynakMekanId = physical.Bolme.KaynakMekan?.Id ?? physical.Mekan.Id;
                task.AliciMekanId = physical.Bolme.AliciMekan?.Id;
            }
            else
            {
                var room = rooms.FirstOrDefault(x => RaporSonucIdleri.DisCephe(x.Mekan.Id) == row.SonucId);
                if (room.Mekan != null) task.KaynakMekanId = room.Mekan.Id;
            }
            package.Gorevler.Add(task);
        }
        return package;
    }

    public static string HtmlOlustur(BinaProje proje, ISet<Guid>? haricTutulanSonucIdleri = null, DateTimeOffset? zamanUtc = null)
    {
        var data = PaketVerisiOlustur(proje, haricTutulanSonucIdleri, zamanUtc);
        var json = JsonSerializer.Serialize(data, JsonOptions).Replace("</", "<\\/", StringComparison.Ordinal);
        return $$$$"""
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
<title>M08 Çevrimdışı Saha — {{{{Html(data.ProjeAdi)}}}}</title><style>
:root{font-family:system-ui,sans-serif;color:#17212b;background:#eef2f5}body{max-width:1100px;margin:auto;padding:18px}header,.card{background:#fff;border:1px solid #d9e1e8;border-radius:10px;padding:16px;margin-bottom:12px}h1{margin:0 0 6px}.muted{color:#617282;font-size:.9rem}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(155px,1fr));gap:9px}label{font-size:.78rem;color:#536475}input,textarea{box-sizing:border-box;width:100%;padding:8px;border:1px solid #b9c6d0;border-radius:6px}textarea{min-height:64px}.actions{position:sticky;bottom:0;background:#17212b;padding:10px;border-radius:9px;display:flex;gap:8px}button{padding:10px 14px;border:0;border-radius:6px;font-weight:700;cursor:pointer}.primary{background:#2d8cff;color:#fff}.status{color:#fff;align-self:center}.warn{padding:10px;background:#fff6cc;border:1px solid #e1bf25;border-radius:6px;margin:12px 0}
</style></head><body><header><h1>M08 Çevrimdışı Saha Formu</h1><div>{{{{Html(data.ProjeAdi)}}}}</div><div class="muted">Paket: {{{{data.KaynakRaporParmakIzi}}}}</div><div class="warn">Bu form yerel tarayıcıda çalışır. Ölçüm sonuçları tasarım hesabını değiştirmez; doğrulama ve rapor onayı masaüstü uygulamada yapılır.</div></header>
<main id="tasks"></main><div class="actions"><button id="save">Yerel kaydet</button><button id="export" class="primary">JSON dışa aktar</button><span id="status" class="status"></span></div>
<script type="application/json" id="package">{{{{json}}}}</script><script>
const pkg=JSON.parse(document.getElementById('package').textContent), key='eye4pro-m08-field:'+pkg.projeId+':'+pkg.kaynakRaporParmakIzi;
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let values={};try{values=JSON.parse(localStorage.getItem(key)||'{}')}catch{}
const root=document.getElementById('tasks');
for(const t of pkg.gorevler){const v=values[t.sonucId]||{};const e=document.createElement('section');e.className='card';e.dataset.id=t.sonucId;e.innerHTML=`<h3>${esc(t.ad)}</h3><div class="muted">Beklenen ${esc(t.beklenenDeger)} ${esc(t.birim)} · limit ${esc(t.limitDeger)}</div><div class="grid"><label>Ölçülen değer<input name="deger" type="number" step="any" value="${esc(v.deger??'')}"></label><label>Belirsizlik<input name="belirsizlik" type="number" min="0" step="any" value="${esc(v.belirsizlik??'')}"></label><label>Ölçüm tarihi<input name="tarih" type="datetime-local" value="${esc(v.tarih??'')}"></label><label>Standart<input name="standart" value="${esc(v.standart??'')}"></label><label>Yöntem<input name="yontem" value="${esc(v.yontem??'')}"></label><label>Cihaz<input name="cihaz" value="${esc(v.cihaz??'')}"></label><label>Operatör<input name="operator" value="${esc(v.operator??'')}"></label></div><label>Notlar<textarea name="notlar">${esc(v.notlar??'')}</textarea></label><label><input name="onayli" type="checkbox" style="width:auto" ${v.onayli?'checked':''}> Raporlama için doğrulandı</label>`;root.appendChild(e)}
function collect(){const o={};for(const e of document.querySelectorAll('.card')){const q=n=>e.querySelector(`[name="${n}"]`);o[e.dataset.id]={deger:q('deger').value,belirsizlik:q('belirsizlik').value,tarih:q('tarih').value,standart:q('standart').value,yontem:q('yontem').value,cihaz:q('cihaz').value,operator:q('operator').value,notlar:q('notlar').value,onayli:q('onayli').checked}}return o}
document.getElementById('save').onclick=()=>{values=collect();localStorage.setItem(key,JSON.stringify(values));document.getElementById('status').textContent='Yerel kaydedildi'};
document.getElementById('export').onclick=()=>{values=collect();localStorage.setItem(key,JSON.stringify(values));const records=[];for(const t of pkg.gorevler){const v=values[t.sonucId];if(!v||v.deger==='')continue;records.push({id:crypto.randomUUID(),sonucId:t.sonucId,ad:t.ad,tur:t.tur,katKimligi:t.katKimligi,kaynakMekanId:t.kaynakMekanId,aliciMekanId:t.aliciMekanId,bolmeId:t.bolmeId,deger:Number(v.deger),birim:t.birim,belirsizlik:v.belirsizlik===''?null:Number(v.belirsizlik),bantFrekanslariHz:[],bantDegerleri:[],olcumTarihi:v.tarih?new Date(v.tarih).toISOString():new Date().toISOString(),standart:v.standart,yontem:v.yontem,cihaz:v.cihaz,operator:v.operator,notlar:v.notlar,raporlamaIcinOnayli:v.onayli})}const out={format:'EYE4PRO-M08-FIELD-RETURN',version:1,projeId:pkg.projeId,kaynakRaporParmakIzi:pkg.kaynakRaporParmakIzi,disaAktarilmaUtc:new Date().toISOString(),kayitlar:records};const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(out,null,2)],{type:'application/json'}));a.download='m08-saha-donus.json';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000)};
</script></body></html>
""";
    }

    public static M08SahaBirlesimSonucu GeriDonusuBirlesitir(
        BinaProje proje,
        string donusJson,
        bool projeyeUygula = true,
        bool guncelOlmayanPaketiKabulEt = false,
        string kullanici = "")
    {
        ArgumentNullException.ThrowIfNull(proje);
        var result = new M08SahaBirlesimSonucu();
        M08SahaDonusBelgesi? document;
        try { document = JsonSerializer.Deserialize<M08SahaDonusBelgesi>(donusJson, JsonOptions); }
        catch (JsonException ex) { result.Hatalar.Add("Geçersiz saha dönüş JSON'u: " + ex.Message); return result; }
        if (document == null || document.Format != M08SahaDonusBelgesi.CurrentFormat || document.Version != M08SahaDonusBelgesi.CurrentVersion)
        { result.Hatalar.Add("Desteklenmeyen saha dönüş belgesi."); return result; }
        if (document.ProjeId != proje.Id)
        { result.Hatalar.Add("Saha dönüş belgesi başka bir projeye ait."); return result; }
        document.Kayitlar ??= new();
        proje.SahaOlcumleri ??= new();
        proje.DenetimKayitlari ??= new();

        var current = AkustikRaporSnapshotServisi.Olustur(proje);
        if (!StringComparer.OrdinalIgnoreCase.Equals(current.RaporParmakIziSha256, document.KaynakRaporParmakIzi))
        {
            if (!guncelOlmayanPaketiKabulEt)
            { result.Hatalar.Add("Proje, saha paketi çıkarıldıktan sonra değişmiş. Açık onay olmadan birleştirme yapılmadı."); return result; }
            result.Uyarilar.Add("Güncel olmayan saha paketindeki kayıtlar kullanıcı onayıyla birleştirildi.");
        }

        var validResults = current.Sonuclar.Select(x => x.SonucId).ToHashSet();
        var roomIds = proje.Katlar.SelectMany(x => x.Mekanlar).Select(x => x.Id).ToHashSet();
        var partitionIds = proje.Katlar.SelectMany(x => x.Mekanlar).SelectMany(x => x.Bolmeler).Select(x => x.Id).ToHashSet();
        var duplicateIds = document.Kayitlar.GroupBy(x => x.Id).Where(x => x.Key == Guid.Empty || x.Count() > 1).Select(x => x.Key).ToHashSet();
        var existing = proje.SahaOlcumleri.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.Last());
        foreach (var incoming in document.Kayitlar)
        {
            var error = Validate(incoming, validResults, roomIds, partitionIds);
            if (duplicateIds.Contains(incoming.Id)) error = "Boş veya yinelenen ölçüm kimliği.";
            if (error != null) { result.Atlanan++; result.Uyarilar.Add($"{incoming.Ad}: {error}"); continue; }
            var record = Convert(incoming, document.KaynakRaporParmakIzi);
            if (existing.TryGetValue(record.Id, out var old))
            {
                if (record.OlcumTarihi < old.OlcumTarihi)
                { result.Atlanan++; result.Uyarilar.Add($"{record.Ad}: projedeki daha yeni kayıt korundu."); continue; }
                if (projeyeUygula)
                {
                    var index = proje.SahaOlcumleri.IndexOf(old);
                    proje.SahaOlcumleri[index] = record;
                }
                result.Guncellenen++;
            }
            else
            {
                if (projeyeUygula) proje.SahaOlcumleri.Add(record);
                result.Eklenen++;
            }
            result.KabulEdilenKayitlar.Add(record);
        }
        if (projeyeUygula && result.KabulEdilenKayitlar.Count > 0)
            proje.DenetimKayitlari.Add(new DenetimKaydi
            {
                Eylem = DenetimEylemi.IceriAktarma,
                Tarih = DateTime.UtcNow,
                Kullanici = kullanici,
                HedefTuru = nameof(SahaOlcumKaydi),
                HedefId = proje.Id.ToString("D"),
                Aciklama = $"Çevrimdışı saha paketinden {result.Eklenen} yeni, {result.Guncellenen} güncellenmiş ölçüm birleştirildi.",
                GirdiParmakIzi = document.KaynakRaporParmakIzi,
            });
        return result;
    }

    private static string? Validate(M08SahaDonusKaydi x, HashSet<Guid> results, HashSet<Guid> rooms, HashSet<Guid> partitions)
    {
        if (x.Id == Guid.Empty) return "Ölçüm kimliği boş.";
        if (!results.Contains(x.SonucId)) return "Sonuç kimliği güncel proje kapsamındaki bir sonuçla eşleşmiyor.";
        if (!double.IsFinite(x.Deger)) return "Ölçüm değeri sonlu bir sayı olmalı.";
        if (x.Belirsizlik is { } u && (!double.IsFinite(u) || u < 0)) return "Belirsizlik negatif veya geçersiz.";
        if (x.KaynakMekanId is { } source && !rooms.Contains(source)) return "Kaynak mekan bulunamadı.";
        if (x.AliciMekanId is { } receiver && !rooms.Contains(receiver)) return "Alıcı mekan bulunamadı.";
        if (x.BolmeId is { } partition && !partitions.Contains(partition)) return "Bölme bulunamadı.";
        x.BantFrekanslariHz ??= new();
        x.BantDegerleri ??= new();
        if (x.BantFrekanslariHz.Count != x.BantDegerleri.Count) return "Bant frekansı ve değer adetleri eşit değil.";
        return null;
    }

    private static SahaOlcumKaydi Convert(M08SahaDonusKaydi x, string fingerprint) => new()
    {
        Id = x.Id,
        Ad = x.Ad,
        Tur = x.Tur,
        Durum = x.RaporlamaIcinOnayli ? SahaOlcumDurumu.Dogrulandi : SahaOlcumDurumu.Taslak,
        KatKimligi = x.KatKimligi,
        KaynakMekanId = x.KaynakMekanId,
        AliciMekanId = x.AliciMekanId,
        BolmeId = x.BolmeId,
        Deger = x.Deger,
        Birim = x.Birim,
        Belirsizlik = x.Belirsizlik,
        BantFrekanslariHz = x.BantFrekanslariHz,
        BantDegerleri = x.BantDegerleri,
        OlcumTarihi = x.OlcumTarihi,
        Standart = x.Standart,
        Yontem = x.Yontem,
        Cihaz = x.Cihaz,
        Operator = x.Operator,
        KaynakDosya = "Çevrimdışı M08 saha HTML paketi",
        KaynakDosyaSha256 = fingerprint,
        KaynakKayitId = x.SonucId.ToString("D"),
        Notlar = x.Notlar,
        RaporlamaIcinOnayli = x.RaporlamaIcinOnayli,
    };

    private static SahaOlcumTuru Tur(string resultType) => resultType switch
    {
        nameof(AkustikRaporSonucTuru.DisCephe) => SahaOlcumTuru.DisCepheHavaSesi,
        nameof(AkustikRaporSonucTuru.DarbeSesi) => SahaOlcumTuru.DarbeSesi,
        nameof(AkustikRaporSonucTuru.TesisatSurekli) => SahaOlcumTuru.IcGurultuSurekli,
        nameof(AkustikRaporSonucTuru.TesisatKesikli) => SahaOlcumTuru.IcGurultuKesikli,
        nameof(AkustikRaporSonucTuru.Reverberasyon) => SahaOlcumTuru.Reverberasyon,
        _ => SahaOlcumTuru.BolmeHavaSesi,
    };

    private static string Html(string? value) => (value ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&#39;");
}
