namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public static class KesifBirimFiyatListesiServisi
{
    public static KesifBirimFiyatListesi ListeEkle(
        KesifProje proje,
        string listeAdi,
        string kaynakDosya,
        IEnumerable<KesifPoz> pozlar,
        bool digerListeleriPasifeAl)
    {
        ArgumentNullException.ThrowIfNull(proje);

        var pozList = pozlar.ToList();
        if (pozList.Count == 0)
            throw new InvalidOperationException("Aktarılacak poz bulunamadı.");

        // Aynı PozNo birden çok kez geldiyse son değeri koru (liste içinde tekilleştir).
        // Boş PozNo'lu satırlar benzersiz anahtarla korunur (yanlışlıkla birleşmesin).
        pozList = pozList
            .GroupBy(p => string.IsNullOrWhiteSpace(p.PozNo)
                ? Guid.NewGuid().ToString()
                : p.PozNo.Trim().ToUpperInvariant())
            .Select(g => g.Last())
            .ToList();

        if (digerListeleriPasifeAl)
        {
            foreach (var list in proje.BirimFiyatListeleri)
                list.AktifMi = false;
        }

        var liste = new KesifBirimFiyatListesi
        {
            Ad = string.IsNullOrWhiteSpace(listeAdi) ? $"Birim Fiyat {DateTime.Now:yyyy.MM.dd HH:mm}" : listeAdi,
            KaynakDosya = kaynakDosya,
            AktifMi = true
        };

        foreach (var poz in pozList)
        {
            poz.Id = Guid.NewGuid();
            poz.BirimFiyatListesiId = liste.Id;
            poz.BirimFiyatListesiAdi = liste.Ad;
            poz.ListePozuMu = true;
            poz.AktifMi = true;
            liste.Pozlar.Add(poz);
        }

        proje.BirimFiyatListeleri.Add(liste);
        PozKutuphaneYenile(proje);
        return liste;
    }

    public static void ListeyiAktifYap(KesifProje proje, KesifBirimFiyatListesi liste)
    {
        liste.AktifMi = true;
        PozKutuphaneYenile(proje);
    }

    public static void ListeyiPasifeAl(KesifProje proje, KesifBirimFiyatListesi liste)
    {
        liste.AktifMi = false;
        PozKutuphaneYenile(proje);
    }

    public static void PozKutuphaneYenile(KesifProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);

        // ManuelPozlar kalıcı kaynak, PozKutuphane ise çalışma zamanı birleşik listedir.
        // Eski akışlarla uyum için ManuelPozlar boşsa PozKutuphane'deki liste-dışı pozlar alınır.
        var manualPozlar = proje.ManuelPozlar.Count > 0
            ? proje.ManuelPozlar.Where(p => !p.ListePozuMu).DistinctBy(p => p.Id).ToList()
            : proje.PozKutuphane.Where(p => !p.ListePozuMu).DistinctBy(p => p.Id).ToList();

        proje.PozKutuphane.Clear();
        foreach (var poz in manualPozlar)
        {
            poz.ListePozuMu = false;
            poz.BirimFiyatListesiId = null;
            poz.BirimFiyatListesiAdi = "";
            proje.PozKutuphane.Add(poz);
        }

        foreach (var liste in proje.BirimFiyatListeleri.Where(l => l.AktifMi))
        {
            foreach (var poz in liste.Pozlar)
            {
                poz.AktifMi = true;
                poz.BirimFiyatListesiId = liste.Id;
                poz.BirimFiyatListesiAdi = liste.Ad;
                poz.ListePozuMu = true;
                proje.PozKutuphane.Add(poz);
            }
        }

        // Kalıcı manuel poz listesini güncel tut — JSON'a yalnız bu yazılır.
        proje.ManuelPozlar.Clear();
        foreach (var poz in manualPozlar)
            proje.ManuelPozlar.Add(poz);
    }
}
