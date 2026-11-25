using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    public abstract record TasarimAsamasi(string Ad, string Yonerge);
    public sealed record AsamaSecenegi<T>(T Asama, string Ad, string Yonerge) : TasarimAsamasi(Ad, Yonerge);

    public IReadOnlyList<AsamaSecenegi<BpcAydinlatmaAsamasi>> AydinlatmaAsamalari { get; } =
    [
        new(BpcAydinlatmaAsamasi.Pano, "Pano yerleşimi", "Panoyu plana yerleştirin. Her mahalin içinde sağ tıklayıp bağlı olduğu panoyu seçin."),
        new(BpcAydinlatmaAsamasi.Armatur, "1 · Armatürler", "Mahal listesinde armatür ve yerleşim kurallarını seçin veya planda tek tek yerleştirin."),
        new(BpcAydinlatmaAsamasi.Anahtar, "2 · Anahtarlar", "Mahal listesinde kumanda tiplerini seçin. Duvar anahtarları kapı yanına yerleştirilir."),
        new(BpcAydinlatmaAsamasi.Buat, "3 · Buatlar", "Her dört kumanda için bir buat oluşturulur. Ek buat yerleştirmek için sembolü seçin."),
        new(BpcAydinlatmaAsamasi.MahalKablosu, "4 · Mahal içi kablolar", "Armatürleri ve kumandaları mahal içindeki buatlara bağlayın."),
        new(BpcAydinlatmaAsamasi.PanoKablosu, "5 · Buat → pano", "Buatları mahallere atadığınız panolara bağlayın. Aydınlatmayı tamamladığınızda pano sigortaları oluşturulur."),
        new(BpcAydinlatmaAsamasi.Sigortalar, "Pano sigortaları", "Pano giriş ve çıkış sigortalarını kontrol edin. Çizim değiştiğinde sigortaları yeniden hesaplayabilirsiniz.")
    ];

    public IReadOnlyList<AsamaSecenegi<BpcPrizAsamasi>> PrizAsamalari { get; } =
    [
        new(BpcPrizAsamasi.Priz, "1 · Priz yerleşimi", "Mahal listesinde priz tipini, adedini ve yerleşim kuralını seçin. Elle eklemek için sembolü seçip plana tıklayın."),
        new(BpcPrizAsamasi.Buat, "2 · Priz buatları", "Her mahalde buata bağlanacak her yedi priz için bir priz buatı gerekir. Mevcut buatlar hesaba katılır."),
        new(BpcPrizAsamasi.MahalKablosu, "3 · Mahal içi kablolar", "Prizleri kendi mahallerindeki priz buatlarına bağlayın."),
        new(BpcPrizAsamasi.PanoKablosu, "4 · Buat → pano", "Priz buatlarını ve doğrudan beslenen prizleri atanan panolara bağlayın. Tüm katların hatları hazır olduğunda priz projesini tamamlayın."),
        new(BpcPrizAsamasi.Sigortalar, "Pano sigortaları", "Priz linyelerinin çıkış sigortalarını ve toplam aydınlatma ve priz yüküne göre hesaplanan pano girişini kontrol edin.")
    ];

    public IReadOnlyList<TasarimAsamasi> AktifTasarimAsamalari => PrizAkisiAktif ? PrizAsamalari : AydinlatmaAsamalari;
    public bool AydinlatmaAkisiAktif => ModulKuvvetliMi && AydinlatmaAktif;
    public bool PrizAkisiAktif => ModulKuvvetliMi && PrizAktif;
    public bool AsamaliPaftaAktif => AydinlatmaAkisiAktif || PrizAkisiAktif;
    public bool AsamaAraclariEtkin => SeciliKat is not null && ProjeDuzenlenebilir;
    public BpcAydinlatmaAsamasi AydinlatmaAsamasi
    {
        get => Proje.AydinlatmaAsamasi;
        set
        {
            if (!Enum.IsDefined(value) || AydinlatmaAsamasi == value) return;
            bool panoIcerigiDegisecek = AydinlatmaSigortaAdimi || value == BpcAydinlatmaAsamasi.Sigortalar;
            Proje.AydinlatmaAsamasi = value;
            AsamaAraclariniSifirla();
            AsamaGorunumunuBildir();
            if (panoIcerigiDegisecek) GuncellePanoIcerik();
        }
    }

    public BpcPrizAsamasi PrizAsamasi
    {
        get => Proje.PrizAsamasi;
        set
        {
            if (!Enum.IsDefined(value) || PrizAsamasi == value) return;
            bool panoIcerigiDegisecek = PrizSigortaAdimi || value == BpcPrizAsamasi.Sigortalar;
            Proje.PrizAsamasi = value;
            AsamaAraclariniSifirla();
            AsamaGorunumunuBildir();
            if (panoIcerigiDegisecek) GuncellePanoIcerik();
        }
    }

    public TasarimAsamasi SeciliAsamaSecenegi
    {
        get => PrizAkisiAktif ? PrizAsamalari[(int)PrizAsamasi] : AydinlatmaAsamalari[(int)AydinlatmaAsamasi];
        set
        {
            if (PrizAkisiAktif && value is AsamaSecenegi<BpcPrizAsamasi> priz) PrizAsamasi = priz.Asama;
            else if (AydinlatmaAkisiAktif && value is AsamaSecenegi<BpcAydinlatmaAsamasi> aydinlatma) AydinlatmaAsamasi = aydinlatma.Asama;
        }
    }

    public bool AsamaMahalListesiVar => PrizAkisiAktif ? PrizAsamasi == BpcPrizAsamasi.Priz :
        AydinlatmaAkisiAktif && AydinlatmaAsamasi is BpcAydinlatmaAsamasi.Armatur or BpcAydinlatmaAsamasi.Anahtar;
    public bool AsamaBuatMi => PrizAkisiAktif ? PrizAsamasi == BpcPrizAsamasi.Buat :
        AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.Buat;
    public bool AsamaMahalKablosuMu => AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.MahalKablosu;
    public bool AsamaPanoKablosuMu => AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.PanoKablosu;
    public bool PrizMahalKablosuAdimi => PrizAkisiAktif && PrizAsamasi == BpcPrizAsamasi.MahalKablosu;
    public bool PrizPanoKablosuAdimi => PrizAkisiAktif && PrizAsamasi == BpcPrizAsamasi.PanoKablosu;
    public bool AsamaKabloMu => AsamaMahalKablosuMu || AsamaPanoKablosuMu || PrizMahalKablosuAdimi || PrizPanoKablosuAdimi;
    public bool AydinlatmaSigortaAdimi => AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.Sigortalar;
    public bool PrizSigortaAdimi => PrizAkisiAktif && PrizAsamasi == BpcPrizAsamasi.Sigortalar;
    public bool AsamaSigortaMi => AydinlatmaSigortaAdimi || PrizSigortaAdimi;
    public string PanoGucBasligi => AsamaSigortaMi ? "Aydınlatma + priz" : "Toplam güç";
    public bool AsamaYerlesimMi => PrizAkisiAktif ? PrizAsamasi <= BpcPrizAsamasi.Buat :
        AydinlatmaAkisiAktif && AydinlatmaAsamasi <= BpcAydinlatmaAsamasi.Buat;

    private void AsamaAraclariniSifirla()
    {
        if (ElemanEkleModu) ElemanEkleModIptal();
        if (KabloModuAktif) KabloModuKapat();
        SeciliElemanlar.Clear();
        SeciliEleman = null;
        SeciliDevreId = null;
        GuncelleElemanVurgu();
    }

    private void AsamaGorunumunuBildir()
    {
        OnPropertyChanged(nameof(AydinlatmaAkisiAktif));
        OnPropertyChanged(nameof(AydinlatmaAsamasi));
        OnPropertyChanged(nameof(PrizAkisiAktif));
        OnPropertyChanged(nameof(PrizAsamasi));
        OnPropertyChanged(nameof(AsamaliPaftaAktif));
        OnPropertyChanged(nameof(AktifTasarimAsamalari));
        OnPropertyChanged(nameof(SeciliAsamaSecenegi));
        OnPropertyChanged(nameof(AsamaMahalListesiVar));
        OnPropertyChanged(nameof(AsamaBuatMi));
        OnPropertyChanged(nameof(AsamaMahalKablosuMu));
        OnPropertyChanged(nameof(AsamaPanoKablosuMu));
        OnPropertyChanged(nameof(PrizMahalKablosuAdimi));
        OnPropertyChanged(nameof(PrizPanoKablosuAdimi));
        OnPropertyChanged(nameof(AsamaKabloMu));
        OnPropertyChanged(nameof(SadecaEksikleriEkle));
        OnPropertyChanged(nameof(YerlestirModuAciklama));
        OnPropertyChanged(nameof(AsamaSigortaMi));
        OnPropertyChanged(nameof(AydinlatmaSigortaAdimi));
        OnPropertyChanged(nameof(PanoGucBasligi));
        OnPropertyChanged(nameof(AsamaYerlesimMi));
        GuncelleFiltrelenmisElemanlar();
        GuncelleDevreOzetleri();
    }

    public bool AsamadaDuzenlenebilir(BasitProjeEleman eleman)
    {
        if (PrizAkisiAktif)
        {
            return PrizAsamasi switch
            {
                BpcPrizAsamasi.Priz => BasitProjeCizKabloServisi.ElemanPrizMi(eleman.Tip),
                BpcPrizAsamasi.Buat => eleman.Tip == BpcElemanTipi.Buat && eleman.PaftaTipi == BpcPaftaTipi.Priz,
                _ => false
            };
        }
        if (!AydinlatmaAkisiAktif) return true;
        if (AydinlatmaAsamasi == BpcAydinlatmaAsamasi.Pano)
            return eleman.Tip == BpcElemanTipi.KatPanosu;
        if (eleman.PaftaTipi != BpcPaftaTipi.Aydinlatma) return false;
        return AydinlatmaAsamasi switch
        {
            BpcAydinlatmaAsamasi.Armatur => BasitProjeCizKabloServisi.ElemanArmaturMi(eleman.Tip),
            BpcAydinlatmaAsamasi.Anahtar => BasitProjeCizKabloServisi.ElemanAnahtarMi(eleman.Tip),
            BpcAydinlatmaAsamasi.Buat => eleman.Tip == BpcElemanTipi.Buat,
            _ => false
        };
    }

    public IEnumerable<BpcArmatürTanim> AsamaSembolleri => PrizAkisiAktif ? PrizAsamasi switch
    {
        BpcPrizAsamasi.Priz => Prizler.Where(p => BasitProjeCizKabloServisi.ElemanPrizMi(BasitProjeCizKutuphaneServisi.TanimTipi(p))),
        BpcPrizAsamasi.Buat => Panolar.Where(p => p.BlokAdi == "E4P_BUAT"),
        _ => []
    } : AydinlatmaAsamasi switch
    {
        BpcAydinlatmaAsamasi.Pano => Panolar.Where(p => p.BlokAdi is "E4P_ANA_PANO" or "E4P_TALI_PANO" or "E4P_ALT_PANO")
            .OrderBy(p => p.BlokAdi == "E4P_ANA_PANO" ? 0 : p.BlokAdi == "E4P_TALI_PANO" ? 1 : 2),
        BpcAydinlatmaAsamasi.Armatur => Armatürler,
        BpcAydinlatmaAsamasi.Anahtar => Anahtarlar,
        BpcAydinlatmaAsamasi.Buat => Panolar.Where(p => p.BlokAdi == "E4P_BUAT"),
        _ => []
    };

    private void ProjeAsamasiniAc()
    {
        if (!ModulKuvvetliMi) return;
        if (!Enum.IsDefined(Proje.AydinlatmaAsamasi)) Proje.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Pano;
        if (!Enum.IsDefined(Proje.PrizAsamasi)) Proje.PrizAsamasi = BpcPrizAsamasi.Priz;
        AktifPafta = Proje.SonTasarimPaftasi == BpcPaftaTipi.Priz ? BpcPaftaTipi.Priz : BpcPaftaTipi.Aydinlatma;
        AsamaAraclariniSifirla();
        AsamaGorunumunuBildir();
    }
}
