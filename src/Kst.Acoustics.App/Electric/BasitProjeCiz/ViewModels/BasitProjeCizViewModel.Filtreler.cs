using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    public record FiltreOdaSecenek(string EkranAdi, string? OdaId)
    {
        public override string ToString() => EkranAdi;
    }

    [ObservableProperty] private string _elemanFiltreKat = "Tüm Katlar";
    
    private static readonly FiltreOdaSecenek TumOdalarSecenek = new("Tüm Odalar", null);
    [ObservableProperty] private FiltreOdaSecenek? _elemanFiltreOda = TumOdalarSecenek;
    
    [ObservableProperty] private string _elemanFiltreTip = "Tümü";

    partial void OnElemanFiltreKatChanged(string value) 
    {
        OnPropertyChanged(nameof(ElemanFiltreOdaSecenekleri));
        ElemanFiltreOda = TumOdalarSecenek;
        GuncelleFiltrelenmisElemanlar();
    }
    
    partial void OnElemanFiltreOdaChanged(FiltreOdaSecenek? value)
    {
        GuncelleFiltrelenmisElemanlar();
        // Eğer filtreden spesifik bir oda seçilirse onu 2D/3D'de vurgula
        if (value != null && value.OdaId != null)
        {
            var oda = Proje.Katlar.SelectMany(k => k.Odalar).FirstOrDefault(o => o.Id == value.OdaId);
            if (oda != null)
            {
                var odaKat = Proje.Katlar.FirstOrDefault(k => k.Odalar.Contains(oda));
                if (odaKat != null && SeciliKat != odaKat) SeciliKat = odaKat;
                SeciliOda = oda;
            }
        }
    }
    
    partial void OnElemanFiltreTipChanged(string value) => GuncelleFiltrelenmisElemanlar();

    public IEnumerable<string> ElemanFiltreKatSecenekleri
    {
        get
        {
            yield return "Tüm Katlar";
            foreach (var k in Proje.Katlar)
                yield return string.IsNullOrWhiteSpace(k.Ad) ? $"Kat {k.KatNo}" : $"Kat {k.KatNo} ({k.Ad})";
        }
    }

    public IEnumerable<FiltreOdaSecenek> ElemanFiltreOdaSecenekleri
    {
        get
        {
            yield return TumOdalarSecenek;
            var katlar = ElemanFiltreKat == "Tüm Katlar" 
                ? Proje.Katlar 
                : Proje.Katlar.Where(x => (string.IsNullOrWhiteSpace(x.Ad) ? $"Kat {x.KatNo}" : $"Kat {x.KatNo} ({x.Ad})") == ElemanFiltreKat);
            
            var gruplar = katlar.SelectMany(k => k.Odalar).GroupBy(o => o.Ad).OrderBy(g => g.Key);
            foreach (var grup in gruplar)
            {
                var liste = grup.ToList();
                if (liste.Count == 1)
                    yield return new FiltreOdaSecenek(liste[0].Ad, liste[0].Id);
                else
                    for (int i = 0; i < liste.Count; i++)
                        yield return new FiltreOdaSecenek($"{liste[i].Ad} ({i + 1})", liste[i].Id);
            }
        }
    }

    public IEnumerable<string> ElemanFiltreTipSecenekleri => new[]
    {
        "Tümü", "Kuvvetli Akım", "Zayıf Akım", "Data", "TV", "Telefon", "CCTV", "Yangın", "Anahtar", "Priz", "Aydınlatma", "Mekanik"
    };

    public IEnumerable<BasitProjeEleman> FiltrelenmisElemanlar
    {
        get
        {
            IEnumerable<BasitProjeEleman> liste = Enumerable.Empty<BasitProjeEleman>();
            
            if (ElemanFiltreKat == "Tüm Katlar")
            {
                liste = Proje.Katlar.SelectMany(k => k.Elemanlar);
            }
            else
            {
                var k = Proje.Katlar.FirstOrDefault(x => (string.IsNullOrWhiteSpace(x.Ad) ? $"Kat {x.KatNo}" : $"Kat {x.KatNo} ({x.Ad})") == ElemanFiltreKat);
                if (k != null) liste = k.Elemanlar;
            }

            if (ElemanFiltreOda != null && ElemanFiltreOda.OdaId != null)
            {
                liste = liste.Where(e => e.OdaId == ElemanFiltreOda.OdaId);
            }

            if (ElemanFiltreTip != "Tümü" && !string.IsNullOrEmpty(ElemanFiltreTip))
            {
                liste = liste.Where(e => UygunMuTip(e, ElemanFiltreTip));
            }

            return liste.ToList();
        }
    }

    private static bool IsAnahtarTip(BpcElemanTipi tip) => tip is BpcElemanTipi.Anahtar or BpcElemanTipi.Komütatör or BpcElemanTipi.DarbeAnahtari or BpcElemanTipi.Vavien or BpcElemanTipi.KomütatörVavien or BpcElemanTipi.Deviatör or BpcElemanTipi.Dimmer or BpcElemanTipi.IkiKutupluAnahtar;

    private static bool IsArmaturTip(BpcElemanTipi tip) => tip is BpcElemanTipi.Armatür or BpcElemanTipi.SarkitArmatur or BpcElemanTipi.GlopArmatur or BpcElemanTipi.CiplakLamba or BpcElemanTipi.BantArmatur or BpcElemanTipi.Avize or BpcElemanTipi.WallWasher;

    private bool UygunMuTip(BasitProjeEleman e, string tipFiltre)
    {
        switch (tipFiltre)
        {
            case "Kuvvetli Akım":
                return e.PaftaTipi == BpcPaftaTipi.Aydinlatma || e.PaftaTipi == BpcPaftaTipi.Priz || e.PaftaTipi == BpcPaftaTipi.Mekanik || e.Tip == BpcElemanTipi.KatPanosu;
            case "Zayıf Akım":
                return e.PaftaTipi != BpcPaftaTipi.Aydinlatma && e.PaftaTipi != BpcPaftaTipi.Priz && e.PaftaTipi != BpcPaftaTipi.Mekanik && e.Tip != BpcElemanTipi.KatPanosu;
            case "Data": return e.PaftaTipi == BpcPaftaTipi.Data;
            case "TV": return e.PaftaTipi == BpcPaftaTipi.Tv;
            case "Telefon": return e.PaftaTipi == BpcPaftaTipi.Telefon;
            case "CCTV": return e.PaftaTipi == BpcPaftaTipi.Cctv;
            case "Yangın": return e.PaftaTipi == BpcPaftaTipi.Yangin;
            case "Anahtar": return IsAnahtarTip(e.Tip);
            case "Priz": return e.PaftaTipi == BpcPaftaTipi.Priz;
            case "Aydınlatma": return IsArmaturTip(e.Tip);
            case "Mekanik": return e.PaftaTipi == BpcPaftaTipi.Mekanik;
            default: return true;
        }
    }

    public void GuncelleFiltrelenmisElemanlar()
    {
        OnPropertyChanged(nameof(FiltrelenmisElemanlar));
    }
}
