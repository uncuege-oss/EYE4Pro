using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.App.Electric.ViewModels;

namespace Kst.Acoustics.App.Electric.Views;

public partial class TekHatSemasiWindow
{
    // === UI GÜNCELLEME ===

    private static string KarOzetKisa(Pano pano)
    {
        if (!pano.KacakAkimRolesiAktif) return "Kapalı";
        if (pano.KacakAkimRolesi == null) return "-";
        if (!pano.KacakAkimRolesi.TroidTipiMi) return pano.KacakAkimRolesi.Ad;
        return pano.KacakAkimAkimTrafosu == null
            ? $"{pano.KacakAkimRolesi.Ad} + AT yok"
            : $"{pano.KacakAkimRolesi.Ad} + {pano.KacakAkimAkimTrafosu.PrimerA}/{pano.KacakAkimAkimTrafosu.SekonderA}A";
    }

    private static string KarOzet(Pano pano)
    {
        if (!pano.KacakAkimRolesiAktif) return "Kapalı (kullanıcı seçimi)";
        if (pano.KacakAkimRolesi == null) return "—";
        if (!pano.KacakAkimRolesi.TroidTipiMi) return pano.KacakAkimRolesi.Ad;

        var at = pano.KacakAkimAkimTrafosu;
        return at == null
            ? $"{pano.KacakAkimRolesi.Ad}\nAkım Trafosu: uygun yok"
            : $"{pano.KacakAkimRolesi.Ad}\nAkım Trafosu: {at.Ad} ({at.PrimerA}/{at.SekonderA}A, Ø{at.IcCapMm}mm)";
    }

    private void TabloGuncelle()
    {
        if (_vm.SeciliPano == null) return;
        var pano = _vm.SeciliPano;

        LinyeGrid.ItemsSource = null;
        LinyeGrid.ItemsSource = pano.Linyeler;

        var f = pano.FazDagilimi;
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
        {
            FazDagilimText.Text = $"Kontrol panosu: {pano.KontrolButonlari.Count} buton";
            PanoOzetText.Text = $"Kontrol Butonu: {pano.KontrolButonlari.Count}   |   " +
                                $"Sinyal Kablosu: {pano.KontrolSinyalKablolari.Count} hat   |   " +
                                $"Damar: {pano.KontrolSinyalKablolari.Sum(k => k.DamarSayisi)}   |   " +
                                $"Metraj: {pano.KontrolSinyalKablolari.Sum(k => k.MetrajM):N1} m";
        }
        else if (pano.Linyeler.Count > 0 || pano.AltPanolar.Count > 0 || pano.ToplamGucW > 0)
        {
            FazDagilimText.Text = $"R: {f.R_W:N0}W ({f.R_A:N1}A)   |   S: {f.S_W:N0}W ({f.S_A:N1}A)   |   T: {f.T_W:N0}W ({f.T_A:N1}A)   |   Dengesizlik: %{f.DengesizlikYuzde:N1}";
            PanoOzetText.Text = $"Toplam: {pano.ToplamGucW:N0} W ({pano.ToplamGucW / 1000.0:N2} kW)   |   Giriş: {pano.GirisAkimA:N1} A   |   Sigorta: {pano.GirisSigortaA}A   |   KAR: {KarOzetKisa(pano)}   |   Modül: {pano.ModulSayisi}   |   Kablo: {pano.KabloKesiti} {pano.KabloTipi}";
        }
        else
        {
            FazDagilimText.Text = $"Pano: {pano.Ad} — linye ekleyin";
        }

        ToplamGucText.Text = $"{pano.ToplamGucW:N0} W ({pano.ToplamGucW / 1000.0:N2} kW)";
        ToplamVAText.Text = $"{pano.ToplamGorunurVA:N0} VA ({pano.ToplamGorunurVA / 1000.0:N2} kVA)";
        GirisAkimText.Text = $"{pano.GirisAkimA:N2} A";
        GirisSigortaText.Text = $"{pano.GirisSigortaA} A";
        _karUiYukleniyor = true;
        try
        {
            KarAktifToggle.IsVisible = pano.PanoTipi != PanoTipi.KontrolPanosu;
            KarAktifToggle.IsChecked = pano.KacakAkimRolesiAktif;
            KarText.Text = KarOzet(pano);
        }
        finally { _karUiYukleniyor = false; }
        ModulText.Text = $"{pano.ModulSayisi} modül";
        // Seçilen pano kimliği: duvar tipi standart alana yuvarlanır (ör. 0,26 → 0,30 m²),
        // dikili tip yalnız derinlikle gösterilir (m² yok). Ham gerekli alan ekranda öne çıkmaz.
        PanoGovdeText.Text = PanoBoyutlandirmaServisi.MetrajEtiketi(pano);
        string alanBasis = $"Modül {pano.ModulAlaniM2:N3} + Klemens {pano.KlemensAlaniM2:N3} m²";
        PanoAlanText.Text = pano.GovdeTipi == PanoGovdeTipi.DikiliTip
            ? $"{alanBasis} → {pano.PanoDerinlikCm} cm dikili"
            : $"{alanBasis} → Standart {pano.PanoOnYuzAlaniM2:N2} m²";
        KabloText.Text = string.IsNullOrEmpty(pano.KabloKesiti) ? "—" : $"{pano.KabloKesiti} {pano.KabloTipi}";
        GerilimDususText.Text = pano.UstPano == null ? "—" : $"%{pano.KabloGerilimDususYuzde:N2}";
        IsiKaybiText.Text = pano.UstPano == null ? "—" : $"{pano.KabloIsiKaybiW:N1} W";
        UstCikisSigortaText.Text = pano.UstPano == null ? "—" : $"{pano.UstPanoCikisSigortaA} A";

        SayacPanel.IsVisible = pano.SayacEkle;
        if (pano.SayacEkle) SayacText.Text = $"{pano.SayacTipi} sayaç";

        // Kompanzasyon/Kontrol panosu — özel görünüm (linye formu/grid gizlenir)
        bool isKomp = pano.PanoTipi == PanoTipi.Kompanzasyon;
        bool isKontrol = pano.PanoTipi == PanoTipi.KontrolPanosu;
        LinyeFormPanel.IsVisible = !isKomp && !isKontrol;
        FazDagilimPanel.IsVisible = !isKomp && !isKontrol;
        LinyeGrid.IsVisible = !isKomp && !isKontrol;
        KompanzasyonPanoPanel.IsVisible = isKomp;
        KontrolPanoPanel.IsVisible = isKontrol;
        if (isKontrol)
        {
            _vm.KontrolSinyalKablolariniGuncelle(pano);
            KontrolButonGrid.ItemsSource = null;
            KontrolButonGrid.ItemsSource = pano.KontrolButonlari.ToList();
            KontrolSinyalKabloGrid.ItemsSource = null;
            KontrolSinyalKabloGrid.ItemsSource = pano.KontrolSinyalKablolari;
        }
        if (isKomp)
        {
            KompRoleText.Text = pano.SeciliKompanzasyonRolesi?.ToString() ?? "(kütüphanede uygun yok)";
            KompNHText.Text = pano.GirisBicakliSigorta?.ToString() ?? "—";
            KompFiltreText.Text = pano.HarmonikFiltreliMi
                ? (pano.SeciliHarmonikFiltre?.ToString() ?? "(seçilmedi)")
                : "Yok";
            KompReaktorText.Text = pano.SeciliReaktor?.ToString() ?? "—";
            KompSvcText.Text = pano.SeciliSvcSurucu?.ToString() ?? "—";
            var k = _vm.Proje.Kompanzasyon;
            KompGerekliText.Text = k != null ? $"{k.GerekliQkVar:N1} kVAr (cosφ {k.MevcutCosF} → {k.HedefCosF})" : "—";

            // Kondansatör + Reaktör kademe listeleri (düzenlenebilir)
            _kompUiYukleniyor = true;
            try
            {
                // Kondansatör kademe sayısı combo: 6/12/18 → index 0/1/2
                int kondSayi = k?.KondansatorKademeSayisi ?? 6;
                KompKondKademeSayiCombo.SelectedIndex = kondSayi switch { 12 => 1, 18 => 2, _ => 0 };
                KompKondList.ItemsSource = null;
                KompKondList.ItemsSource = k?.KondansatorKademeleri.ToList();
                KompKondOzet.Text = k != null
                    ? $"Toplam Kondansatör: {k.ToplamKondansatorKVar:N1} kVAr ({k.KondansatorKademeleri.Count} kademe)"
                    : "";

                // Reaktör (SVC) toggle + akort
                KompReaktorToggle.IsChecked = k?.ReaktorAktif ?? false;
                double akort = k?.ReaktorAkortYuzde ?? 7.0;
                KompAkortCombo.SelectedIndex = akort < 6 ? 0 : akort >= 10 ? 2 : 1;
                KompReaktorList.ItemsSource = null;
                KompReaktorList.ItemsSource = k?.ReaktorKademeleri.ToList();

                // SVC sürücü combo
                KompSvcSurucuCombo.ItemsSource = _vm.SvcSurucuTipleri;
                KompSvcSurucuCombo.SelectedItem = k?.SecilenYukSurucu;

                KompReaktorOzet.Text = k?.ReaktorAktif == true
                    ? $"Reaktör toplam: {k.ToplamReaktorKVar:N2} kVAr  |  Sürücü: {k.SecilenYukSurucu?.ToString() ?? "—"}  |  Toplam kademe: {k.ToplamKademeSayisi}"
                    : "Reaktör kapalı";
            }
            finally { _kompUiYukleniyor = false; }
        }

        // Çıkış sigortaları paneli — bu panonun alt panoları varsa göster. Doğrudan Pano nesnelerine
        // bağlanır ki "Cihaz" (Sigorta/TMŞ) ve "kA" (kesme kapasitesi) açılır menüleri düzenlenebilsin.
        var cikisPanolari = pano.AltPanolar.Where(p => p.PanoTipi != PanoTipi.KontrolPanosu).ToList();
        CikisSigortalariPanel.IsVisible = cikisPanolari.Count > 0 && !isKontrol;
        if (CikisSigortalariPanel.IsVisible)
        {
            CikisSigortaGrid.ItemsSource = null;
            CikisSigortaGrid.ItemsSource = cikisPanolari;
        }

        // Aydınlatma Panosu paneli
        bool isAydinlatma = pano.PanoTipi == PanoTipi.AydinlatmaPanosu;
        AydinlatmaPanel.IsVisible = isAydinlatma;
        if (isAydinlatma)
        {
            AzrText.Text = pano.AstronomikZamanRolesi?.ToString() ?? "(kütüphanede uygun yok)";
            var kontaktorler = pano.Linyeler
                .Where(l => l.Kontaktor != null)
                .Select(l => $"{l.LinyeNo}: {l.Kontaktor!.AnmaAkimA}A")
                .ToList();
            KontaktorText.Text = kontaktorler.Count == 0
                ? "(aydınlatma linyesi eklenmedi)"
                : string.Join("\n", kontaktorler);
        }

        // Busbar bağlantısı
        BusbarBaglantiPanel.IsVisible = pano.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrEmpty(pano.BaglandigiBusbarId);
        if (BusbarBaglantiPanel.IsVisible)
        {
            var bb = _vm.Proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            var kutu = bb?.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == pano.Ad);
            double kutuPozisyonM = kutu?.PozisyonM ?? 0;
            BusbarBaglantiText.Text = bb == null
                ? "—"
                : $"Hat: {bb.Ad} ({bb.BusbarTipiAd})\nKutu Pozisyonu: {kutuPozisyonM:0.##} m\nÇıkış Kutusu Sigortası: {pano.CikisKutusuSigortaA} A\nKutu-Pano Kablo: {pano.KabloMesafeM} m";
        }

        // Bu panodan çıkan busbarlar
        CikanBusbarPanel.IsVisible = pano.CikanBusbarlar.Count > 0;
        if (CikanBusbarPanel.IsVisible)
        {
            var lines = pano.CikanBusbarlar.Select(b =>
                $"{b.Ad} | {(b.OtomatikTipSec ? "Otomatik" : "Manuel")} {b.BusbarTipiAd} | L={b.ToplamUzunlukM:N1}m | Kutu: {b.CikisKutulari.Count} | I={b.ToplamAkimA:N1}A | ΔU=%{b.GerilimDususYuzde:N2} | {b.IsiKaybiW:N1}W");
            CikanBusbarText.Text = string.Join("\n", lines);
        }

        var komp = _vm.Proje.Kompanzasyon;
        KompanzasyonPanel.IsVisible = komp != null && komp.KondansatorKademeleri.Count > 0;
        if (komp != null && komp.KondansatorKademeleri.Count > 0)
        {
            KompGerekliQ.Text = $"Gerekli: {komp.GerekliQkVar:N1} kVAr (cosφ {komp.MevcutCosF} → {komp.HedefCosF})";
            KompKademeler.Text = string.Join("\n", komp.KondansatorKademeleri.Select(k =>
                $"  Kademe {k.KademeNo}: {k.KVar} kVAr ({k.KondansatorAd})"));

            _kompUiYukleniyor = true;
            try { KompReaktorOzetToggle.IsChecked = komp.ReaktorAktif; }
            finally { _kompUiYukleniyor = false; }

            if (komp.ReaktorAktif && komp.ReaktorKademeleri.Count > 0)
            {
                KompReaktor.Text = $"Reaktör (SVC) toplam: {komp.ToplamReaktorKVar:N2} kVAr | Sürücü: {komp.SecilenYukSurucu?.ToString() ?? "—"}\n" +
                                   string.Join("\n", komp.ReaktorKademeleri.Select(r =>
                                       $"  R{r.KademeNo}: {r.ReaktorAd} — {r.KVar} kVAr, L={r.L_mH:N2} mH ({r.AkortFrekansHz:N0} Hz)"));
            }
            else
            {
                KompReaktor.Text = "Reaktör (SVC) kapalı — yukarıdaki toggle ile aktif edebilirsiniz.";
            }
        }

        // Jeneratör paneli (proje seviyesi — herhangi bir pano seçili iken görünür)
        var jenGrup = _vm.AktifJeneratorGrubu;
        var jen = _vm.AktifJenerator;
        JeneratorPanel.IsVisible = jenGrup != null || jen != null;
        if (jenGrup != null)
        {
            var satirlar = jenGrup.Jeneratorler.Select((j, i) =>
            {
                var t = _vm.JeneratorTipleri.FirstOrDefault(x => x.Ad == j.JeneratorTanimAd);
                if (t == null) return $"{i + 1}. (otomatik)";
                double kva = JeneratorHesapServisi.JeneratorGucKVA(t, jenGrup.Mod);
                return $"{i + 1}. {t.Ad} ({kva:N0} kVA)";
            });
            var transferSalter = _vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == jenGrup.TransferSalterAd);

            JenTipText.Text = string.Join("\n", satirlar);
            JenModText.Text = jenGrup.Mod == JeneratorMod.Prime
                ? $"Prime (PRP) — emniyet ×{jenGrup.EmniyetFaktoru:N2}"
                : $"Standby (ESP) — emniyet ×{jenGrup.EmniyetFaktoru:N2}";
            JenYukText.Text = $"{jenGrup.HesaplananYukKW:N1} kW / {jenGrup.HesaplananYukKVA:N1} kVA  |  Kapasite: {jenGrup.ToplamGucKW:N0} kW / {jenGrup.ToplamGucKVA:N0} kVA";
            JenGerekliText.Text = $"{jenGrup.GerekliKVAMin:N1} kVA min.";
            JenYeterliText.Text = jenGrup.YukYeterliMi ? "✓ Evet" : "✗ Yetersiz";
            JenYeterliText.Foreground = jenGrup.YukYeterliMi
                ? Avalonia.Media.Brushes.LightGreen
                : Avalonia.Media.Brushes.OrangeRed;
            JenYakitText.Text = jenGrup.YakitTuketimLh > 0 ? $"{jenGrup.YakitTuketimLh:N1} L/h toplam" : "—";
            JenTransferPanoText.Text = jenGrup.SenkronPanoAd;
            JenTransferSalterText.Text = transferSalter?.ToString() ?? "(otomatik/seçilmedi)";
            JenUnitKabloMesafeText.Text = jenGrup.Jeneratorler.Count == 0
                ? "—"
                : string.Join("  |  ", jenGrup.Jeneratorler.Select((j, i) => $"J{i + 1}: {j.SenkronPanoKabloMesafeM:N1} m"));
            JenSenkronHedefMesafeText.Text = $"{jenGrup.SenkronHedefPanoKabloMesafeM:N1} m → {jenGrup.AnaPanoAd}";
        }
        else if (jen != null)
        {
            var jenTanim = _vm.JeneratorTipleri.FirstOrDefault(j => j.Ad == jen.JeneratorTanimAd);
            var transferSalter = _vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == jen.TransferSalterAd);

            JenTipText.Text = jenTanim?.ToString() ?? "(seçilmedi)";
            JenModText.Text = jen.Mod == JeneratorMod.Prime
                ? $"Prime (PRP) — emniyet ×{jen.EmniyetFaktoru:N2}"
                : $"Standby (ESP) — emniyet ×{jen.EmniyetFaktoru:N2}";
            JenYukText.Text = $"{jen.HesaplananYukKW:N1} kW / {jen.HesaplananYukKVA:N1} kVA";
            JenGerekliText.Text = $"{jen.GerekliKVAMin:N1} kVA min.";
            JenYeterliText.Text = jen.YukYeterliMi ? "✓ Evet" : "✗ Yetersiz";
            JenYeterliText.Foreground = jen.YukYeterliMi
                ? Avalonia.Media.Brushes.LightGreen
                : Avalonia.Media.Brushes.OrangeRed;
            JenYakitText.Text = jen.YakitTuketimLh > 0 ? $"{jen.YakitTuketimLh:N1} L/h" : "—";
            JenTransferPanoText.Text = jen.TransferPanoAd;
            JenTransferSalterText.Text = transferSalter?.ToString() ?? "(seçilmedi)";
            JenUnitKabloMesafeText.Text = $"{jen.SenkronPanoKabloMesafeM:N1} m";
            JenSenkronHedefMesafeText.Text = $"{jen.SenkronHedefPanoKabloMesafeM:N1} m → {jen.AnaPanoAd}";
        }

        // Orta gerilim paneli (proje seviyesi)
        var og = _vm.Proje.OrtaGerilim;
        OrtaGerilimPanel.IsVisible = og != null;
        if (og != null)
        {
            var trafo = _vm.TrafoTipleri.FirstOrDefault(t => t.Ad == og.TrafoTanimAd);
            OgKoskText.Text = OgKoskKisa(og.KoskTipi);
            OgTrafoText.Text = trafo?.ToString() ?? "(seçilmedi)";
            OgYeterliText.Text = og.TrafoYeterliMi ? "✓ Evet" : "✗ Yetersiz";
            OgYeterliText.Foreground = og.TrafoYeterliMi
                ? Avalonia.Media.Brushes.LightGreen
                : Avalonia.Media.Brushes.OrangeRed;
            OgAkimText.Text = $"AG {og.TrafoAgAkimA:N0} A / OG {og.TrafoOgAkimA:N1} A";
            OgIkText.Text = $"{og.AgBaraIkKA:N1} kA";
            int uyariSayisi = og.Hucreler.Count(h => !h.AnmaAkimYeterliMi);
            OgHucreText.Text = uyariSayisi > 0
                ? $"{og.Hucreler.Count} ({uyariSayisi} ⚠)"
                : og.Hucreler.Count.ToString();
            OgBaglantiText.Text = OgBaglantiKisa(og.CikisBaglanti);
            OgSebekeHucreKabloText.Text = OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.SebekeHucreXlpeKablosu);
            OgHucreTrafoKabloText.Text = OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.HucreTrafoXlpeKablosu);
        }

        // UPS kritik panosu — UPS + akü özetini PanoOzetText'e yaz
        var upsKritik = _vm.KritikPanoUps(pano);
        if (pano.PanoTipi == PanoTipi.UpsPanosu && upsKritik != null)
        {
            var ut = _vm.UpsTipleri.FirstOrDefault(u => u.Ad == upsKritik.UpsTanimAd);
            var at = _vm.AkuTipleri.FirstOrDefault(a => a.Ad == upsKritik.AkuTanimAd);
            double upsGucuKVA = upsKritik.SeciliUpsGucKVA > 0
                ? upsKritik.SeciliUpsGucKVA
                : ut?.GucKVA ?? upsKritik.GerekliKVA;
            PanoOzetText.Text =
                $"🔋 UPS: {ut?.Ad ?? "(otomatik)"} {upsGucuKVA:N0}kVA  |  " +
                $"Kritik yük: {upsKritik.KritikYukKW:N1}kW / {upsKritik.KritikYukKVA:N1}kVA  |  " +
                $"Giriş: {upsKritik.GirisAkimA:N1}A  Çıkış: {upsKritik.CikisAkimA:N1}A  |  " +
                $"Akü: {at?.Ad ?? "—"} — {upsKritik.SeriBlokSayisi}S×{upsKritik.ParalelStringSayisi}P, " +
                $"{upsKritik.ToplamAh:N0}Ah / {upsKritik.ToplamEnerjiKWh:N1}kWh @ {upsKritik.OtonomiDk}dk";
        }

        var uyarilar = new System.Collections.Generic.List<string>();
        if (f.DengesizlikYuzde > 10) uyarilar.Add($"Faz dengesizliği %{f.DengesizlikYuzde:N1} — önerilen max %10");
        if (pano.KacakAkimRolesiAktif && pano.ToplamGucW > 0 && pano.KacakAkimRolesi == null)
            uyarilar.Add("KAR bulunamadı — kütüphanede uygun anma akımı yok");
        if (pano.KacakAkimRolesi?.TroidTipiMi == true && pano.KacakAkimAkimTrafosu == null)
            uyarilar.Add("Troid KAR için uygun akım trafosu bulunamadı — Akım Trafoları kütüphanesini kontrol edin.");
        if (jen != null && !jen.YukYeterliMi) uyarilar.Add("Seçilen jeneratör hesaplanan yükü karşılayamıyor — daha büyük tip seçin veya emniyet faktörünü düşürün.");
        if (upsKritik != null && !upsKritik.YukYeterliMi) uyarilar.Add("Seçilen UPS kritik yükü karşılayamıyor — daha büyük UPS seçin veya emniyet faktörünü düşürün.");
        if (upsKritik != null && !upsKritik.AkuYeterliMi) uyarilar.Add("Akü grubu yetersiz — deşarj sonu gerilimi DC alt sınırı sağlamıyor ya da deşarj tablosu eksik.");

        // A2 — kablo kesiti yetersiz (en büyük standart kesit × azami paralel kol bile karşılamıyor)
        if (pano.UstPano != null && !pano.KabloKesitiYeterli)
            uyarilar.Add($"Besleme kablosu kesiti yetersiz ({pano.KabloKesiti}) — mesafe/akım için en büyük standart kesit bile segment ΔU veya termal sınırı karşılamıyor; mesafeyi azaltın ya da besleme noktasını yakınlaştırın.");
        int kesitYetersizLinye = pano.Linyeler.Count(l => !l.KabloKesitiYeterli);
        if (kesitYetersizLinye > 0)
            uyarilar.Add($"{kesitYetersizLinye} linyenin kablo kesiti yetersiz — segment ΔU/termal sınır en büyük kesitle bile sağlanamıyor.");

        // A1 — kümülatif (toplam) gerilim düşümü sınırı aşımı
        double toplamSinir = _vm.Proje.IzinVerilenToplamGerilimDususYuzde;
        if (pano.GerilimDususuAsiyor)
            uyarilar.Add($"Toplam gerilim düşümü %{pano.KumulatifGerilimDususYuzde:N2} — izin verilen %{toplamSinir:N1} sınırı aşıldı (kaynaktan bu panoya kadar birikimli).");
        var asanLinyeler = pano.Linyeler.Where(l => l.GerilimDususuAsiyor).ToList();
        if (asanLinyeler.Count > 0)
        {
            double enKotu = asanLinyeler.Max(l => l.KumulatifGerilimDususYuzde);
            var noListe = asanLinyeler.Select(l => string.IsNullOrWhiteSpace(l.LinyeNo) ? "?" : l.LinyeNo);
            uyarilar.Add($"{asanLinyeler.Count} linyede toplam ΔU %{toplamSinir:N1} sınırını aşıyor (en kötü %{enKotu:N2}): {string.Join(", ", noListe)}.");
        }

        UyariPanel.IsVisible = uyarilar.Count > 0;
        UyariText.Text = string.Join("\n", uyarilar);
    }

}
