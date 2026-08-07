using System.Text.Json;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>
/// Metraj kalemi → poz eşleşmelerinin global (projeden bağımsız) öğrenme veritabanı.
/// Kullanıcı bir kez eşleştirince anahtar (Ana+Alt+Teknik+Birim) kalıcı saklanır; sonraki
/// projelerde aynı metraj otomatik tanınır. Bir referans excel'i silinse bile eşleşmeler durur.
/// AppData deseni sibling modüllerle aynıdır (bkz. BasitProjeCizKutuphaneServisi).
/// </summary>
public sealed class KesifPozEslemeServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly List<KesifPozEsleme> _eslemeler = new();

    /// <param name="dosyaYolu">Test için özel yol; null ise AppData varsayılanı kullanılır.</param>
    public KesifPozEslemeServisi(string? dosyaYolu = null)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Eye4Pro", "Electric", "KesifHazirla");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "PozEslemeVeritabani.json");
        }
        else
        {
            var dir = Path.GetDirectoryName(dosyaYolu);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            _filePath = dosyaYolu;
        }

        Yukle();
    }

    public IReadOnlyList<KesifPozEsleme> Tum() => _eslemeler;

    /// <summary>Anahtara (Ana+Alt+Teknik+Birim) göre eşleşmeyi bulur; yoksa null.</summary>
    public KesifPozEsleme? EslemeBul(string eslemeAnahtari)
        => _eslemeler.FirstOrDefault(e => string.Equals(e.EslemeAnahtari, eslemeAnahtari, StringComparison.Ordinal));

    /// <summary>Bir metraj satırının eşleşmesini bulur; yoksa null.</summary>
    public KesifPozEsleme? EslemeBul(KesifMetrajSatiri kalem) => EslemeBul(kalem.EslemeAnahtari);

    /// <summary>Aynı anahtar varsa poz bilgisini günceller, yoksa yeni eşleşme ekler; diske yazar.</summary>
    public KesifPozEsleme EkleVeyaGuncelle(
        KesifMetrajSatiri kalem,
        string pozNo,
        string birimAdi,
        bool hemenKaydet = true)
    {
        var mevcut = EslemeBul(kalem.EslemeAnahtari);
        if (mevcut is null)
        {
            mevcut = new KesifPozEsleme
            {
                AnaGrup = kalem.AnaGrup,
                AltGrup = kalem.AltGrup,
                TeknikOzellik = kalem.TeknikOzellik,
                Birim = kalem.Birim
            };
            _eslemeler.Add(mevcut);
        }

        mevcut.PozNo = pozNo;
        mevcut.BirimAdi = birimAdi;
        mevcut.SonGuncellemeTarihi = DateTime.Now;
        if (hemenKaydet)
            Kaydet();
        return mevcut;
    }

    public void Sil(Guid id)
    {
        var mevcut = _eslemeler.FirstOrDefault(e => e.Id == id);
        if (mevcut is not null)
        {
            _eslemeler.Remove(mevcut);
            Kaydet();
        }
    }

    public void Kaydet()
    {
        var json = JsonSerializer.Serialize(_eslemeler, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private void Yukle()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var liste = JsonSerializer.Deserialize<List<KesifPozEsleme>>(json, JsonOptions);
            if (liste is not null)
            {
                _eslemeler.Clear();
                _eslemeler.AddRange(liste);
            }
        }
        catch (JsonException)
        {
            // Bozuk dosya → boş başla (kullanıcı verisi yeniden öğrenilebilir).
        }
    }
}
