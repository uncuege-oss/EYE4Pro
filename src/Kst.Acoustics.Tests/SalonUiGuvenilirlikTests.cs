using System;
using System.Collections.Generic;
using Kst.Acoustics.App.Salon.ViewModels;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.Tests;

/// M51 arayuz durumunun belge degisimi ve binding yenileme sozlesmeleri.
public static class SalonUiGuvenilirlikTests
{
    public static void UygulanmamisTaslakBasliktaKirliGorunur()
    {
        var vm = new SalonTasarlaViewModel();
        Dogru(!vm.PencereBasligi.EndsWith(" *", StringComparison.Ordinal),
            "Yeni belge basligi temiz baslamali.");

        vm.UygulanmamisTaslakVar = true;
        Dogru(vm.PencereBasligi.EndsWith(" *", StringComparison.Ordinal),
            "Modele uygulanmamis panel taslagi baslikta kaydedilmemis olarak gorunmeli.");

        vm.UygulanmamisTaslakVar = false;
        vm.IsDirty = true;
        Dogru(vm.PencereBasligi.EndsWith(" *", StringComparison.Ordinal),
            "Model degisikligi baslikta kaydedilmemis olarak gorunmeli.");
    }

    public static void BelgeDegisimiEskiSecimleriSifirlar()
    {
        var vm = new SalonTasarlaViewModel();
        var eskiSeyirci = new SeyirciSekciyonu();
        var eskiBalkon = new BalkonGeometri();
        var eskiTavan = new TavanKirilimNoktasi();
        var eskiAciklik = new DuvarAcikligi();
        vm.Salon.SeyirciSekciyonlari.Add(eskiSeyirci);
        vm.Salon.Balkonlar.Add(eskiBalkon);
        vm.Salon.TavanProfili.Add(eskiTavan);
        vm.Salon.Acikliklar.Add(eskiAciklik);
        vm.SeciliSeyirci = eskiSeyirci;
        vm.SeciliBalkon = eskiBalkon;
        vm.SeciliTavanNoktasi = eskiTavan;
        vm.SeciliAciklik = eskiAciklik;

        var yeni = SalonGeometriServisi.YeniOlustur("Yeni belge");
        vm.Salon = yeni;

        Ayni(yeni.Katmanlar, vm.Katmanlar, "Katman binding'i yeni belgeyi izlemeli.");
        Ayni(yeni.CizimOgeleri, vm.CizimOgeleri, "Cizim binding'i yeni belgeyi izlemeli.");
        Ayni(yeni.SeyirciSekciyonlari, vm.SeyirciSekciyonlari, "Seyirci binding'i yeni belgeyi izlemeli.");
        Dogru(vm.SeciliSeyirci is null && vm.SeciliBalkon is null &&
              vm.SeciliTavanNoktasi is null && vm.SeciliAciklik is null,
            "Yeni belgeye geciste onceki belgenin alt oge secimleri kalmamali.");
        Dogru(vm.SeciliKatman is null || yeni.Katmanlar.Contains(vm.SeciliKatman),
            "Aktif katman yeni belgeye ait olmali.");
    }

    public static void NormalizeSonrasiKoleksiyonBindingleriYenilenir()
    {
        var vm = new SalonTasarlaViewModel();
        var bildirilenler = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                bildirilenler.Add(e.PropertyName);
        };

        vm.ModelBaglariniYenile();

        foreach (var ad in new[]
                 {
                     nameof(vm.Katmanlar), nameof(vm.CizimOgeleri),
                     nameof(vm.SeyirciSekciyonlari), nameof(vm.Balkonlar),
                     nameof(vm.TavanProfili), nameof(vm.Acikliklar)
                 })
        {
            Dogru(bildirilenler.Contains(ad), $"{ad} binding yenileme bildirimi eksik.");
        }
    }

    private static void Dogru(bool kosul, string mesaj)
    {
        if (!kosul) throw new InvalidOperationException(mesaj);
    }

    private static void Ayni(object beklenen, object gercek, string mesaj)
    {
        if (!ReferenceEquals(beklenen, gercek))
            throw new InvalidOperationException(mesaj);
    }
}
