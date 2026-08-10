using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Kst.Acoustics.App.Bina.ViewModels;
using Kst.Acoustics.App.Bina.Views;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace Kst.Acoustics.Tests;

/// M50'nin UI komut katmanındaki geri-al, seçim ve güvenli düzenleme sözleşmeleri.
/// Program.cs kaydı bütünleştirme adımında merkezi olarak yapılır.
public static class BinaGeometriUiGuvenilirlikTests
{
    public static void KaydetGeriAlKaydetRevizyonVeYoluKorur()
    {
        string path = Path.Combine(Path.GetTempPath(), $"m50-ui-rev-{Guid.NewGuid():N}.e4bina.json");
        try
        {
            var (vm, _, _) = TekOdaliVm();
            vm.Geo.KaynakModul = "M08";
            vm.Geo.KaynakArtifactKimligi = "artifact-42";
            vm.KatAdiGuncelle("Kaydedilen Ad");

            BinaGeometriServisi.Kaydet(vm.Geo, path);
            vm.TemizIsaretle();
            Esit(1, vm.Geo.GeometriRevizyon, "İlk kayıt revizyonu");
            Dogru(!vm.IsDirty, "Başarılı kayıt checkpoint'i temiz olmalı.");

            vm.GeriAl();
            Dogru(vm.IsDirty, "Kayıt checkpoint'inin öncesine undo dirty olmalı.");
            Esit(1, vm.Geo.GeometriRevizyon, "Undo revizyonu geri sarmamalı");
            Esit(path, vm.Geo.DosyaYolu, "Undo dosya yolunu korumalı");
            Esit("M08", vm.Geo.KaynakModul, "Undo kaynak modülü korumalı");
            Esit("artifact-42", vm.Geo.KaynakArtifactKimligi, "Undo artifact kimliğini korumalı");

            BinaGeometriServisi.Kaydet(vm.Geo, vm.Geo.DosyaYolu!);
            vm.TemizIsaretle();
            Esit(2, vm.Geo.GeometriRevizyon, "Undo sonrası ikinci kayıt revizyonu");
            Dogru(!vm.IsDirty, "İkinci kayıt yeni checkpoint'i temizlemeli.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    public static void RedoDaliKayitCheckpointiniDogruIzler()
    {
        var (vm, _, _) = TekOdaliVm();
        vm.BinaAdiGuncelle("A");
        vm.TemizIsaretle();
        vm.BinaAdiGuncelle("B");
        Dogru(vm.IsDirty, "Checkpoint sonrası düzenleme dirty olmalı.");

        vm.GeriAl();
        Esit("A", vm.Geo.BinaAdi, "Undo bina adı");
        Dogru(!vm.IsDirty, "Undo kayıt checkpoint'ine dönünce temiz olmalı.");

        vm.IleriAl();
        Esit("B", vm.Geo.BinaAdi, "Redo bina adı");
        Dogru(vm.IsDirty, "Redo checkpoint dışındaki dala dönünce dirty olmalı.");

        vm.GeriAl();
        vm.KatAdiGuncelle("Yeni Dal");
        Dogru(!vm.IleriAlinabilir, "Undo sonrası yeni komut redo dalını temizlemeli.");
    }

    public static void HariciKirletmeUndoSonrasiDirtyKalmayaDevamEder()
    {
        var (vm, _, _) = TekOdaliVm();
        vm.Kirlet(); // Kurtarma açılışı gibi history dışı, henüz kaydedilmemiş durum.
        vm.BinaAdiGuncelle("Kurtarma Üzerinde Edit");
        vm.GeriAl();
        Dogru(vm.IsDirty,
            "Edit undo edilince kaydedilmemiş harici/kurtarma durumu yanlışlıkla temiz sayılamaz.");
        Esit("Başlangıç", vm.Geo.BinaAdi, "Harici durum üstündeki edit undo edilmeli");

        var yayinKontrolu = typeof(BinaGeometriWindow).GetMethod(
            "KurtarmaYayiniGecerliMi", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Otomatik kurtarma yayın kapısı bulunamadı.");
        bool Yayinlanabilir(bool dirty, bool kapandi, long beklenen, long guncel, string yazilan, string son)
            => (bool)(yayinKontrolu.Invoke(null, [dirty, kapandi, beklenen, guncel, yazilan, son]) ?? false);

        Dogru(Yayinlanabilir(true, false, 7, 7, "durum-a", "durum-a"),
            "Dirty ve aynı nesildeki değişmemiş snapshot yayınlanabilmeli.");
        Dogru(!Yayinlanabilir(false, false, 7, 7, "durum-a", "durum-a"),
            "Başarılı kayıtla temizlenen snapshot yayınlanmamalı.");
        Dogru(!Yayinlanabilir(true, false, 7, 8, "durum-a", "durum-a"),
            "Kayıt/temizlik sonrası eski nesil snapshot yayınlanmamalı.");
        Dogru(!Yayinlanabilir(true, true, 7, 7, "durum-a", "durum-a"),
            "Kapanmış pencerenin snapshot'ı yayınlanmamalı.");
        Dogru(!Yayinlanabilir(true, false, 7, 7, "durum-a", "durum-b"),
            "Yazım sürerken değişen snapshot yayınlanmamalı.");

        var geciciDosyaUret = typeof(BinaGeometriWindow).GetMethod(
            "YeniKurtarmaGeciciDosyasi", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Otomatik kurtarma geçici dosya üreticisi bulunamadı.");
        string gecici1 = (string)(geciciDosyaUret.Invoke(null, null)
            ?? throw new InvalidOperationException("İlk geçici kurtarma yolu üretilemedi."));
        string gecici2 = (string)(geciciDosyaUret.Invoke(null, null)
            ?? throw new InvalidOperationException("İkinci geçici kurtarma yolu üretilemedi."));
        Dogru(!string.Equals(gecici1, gecici2, StringComparison.Ordinal),
            "Eşzamanlı kurtarma işleri aynı geçici dosyayı paylaşmamalı.");
        Dogru(gecici1.EndsWith(".tmp", StringComparison.Ordinal),
            "Kurtarma işi geçici dosya uzantısı kullanmalı.");
    }

    public static void TamBelgeKatSilmeGeriAlinir()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Kat Undo");
        var ust = new BinaKat { Id = "ust", Ad = "Üst", KatNo = 1 };
        var alt = new BinaKat { Id = "alt", Ad = "Alt", KatNo = 2 };
        geo.Katlar = new ObservableCollection<BinaKat>([ust, alt]);
        var vm = new BinaGeometriViewModel { Geo = geo, SeciliKat = alt };

        vm.KatSil();
        Esit(1, vm.Geo.Katlar.Count, "Kat silme sonrası sayı");
        vm.GeriAl();
        Esit(2, vm.Geo.Katlar.Count, "Kat silme undo sonrası sayı");
        Esit("alt", vm.SeciliKat?.Id, "Undo seçili kat kimliği");
        vm.IleriAl();
        Esit(1, vm.Geo.Katlar.Count, "Kat silme redo sonrası sayı");
    }

    public static void SnapshotMetadataVeBilinmeyenAlanlariKorur()
    {
        var (vm, kat, oda) = TekOdaliVm();
        var ac = new BinaAciklik
        {
            Id = "ac-ext", Bas = new(1, 0), Bit = new(2, 0), GenislikM = 1,
            EkAlanlar = Alan("ac-extra", "1")
        };
        kat.Acikliklar.Add(ac);
        var uye = new BinaBirlesimUyesi
        {
            KatId = kat.Id, KatNo = kat.KatNo, OdaId = oda.Id,
            EkAlanlar = Alan("uye-extra", "1")
        };
        var birlesim = new BinaDuseyBirlesim
        {
            Id = "bir-ext", Uyeler = [uye], EkAlanlar = Alan("bir-extra", "1")
        };
        vm.Geo.DuseyBirlesimleri.Add(birlesim);
        vm.Geo.EkAlanlar = Alan("geo-extra", "1");
        kat.EkAlanlar = Alan("kat-extra", "1");
        oda.EkAlanlar = Alan("oda-extra", "1");
        kat.DxfBirimOlcegiM = 0.001;
        kat.DxfNormalizeOfsetiM = new Point2D(120, 80);
        kat.DxfKaynakBolgesiMinM = new Point2D(100, 70);
        kat.DxfKaynakBolgesiMaxM = new Point2D(150, 100);
        kat.DxfSegmentler.Add(new BinaDxfSegment
        {
            A = new Point2D(0, 0), B = new Point2D(1, 0),
            EkAlanlar = Alan("segment-extra", "1")
        });

        vm.BinaAdiGuncelle("Snapshot Sonrası");
        vm.Geo.EkAlanlar!["geo-extra"] = Eleman("99");
        vm.Geo.Katlar[0].EkAlanlar!["kat-extra"] = Eleman("99");
        vm.Geo.Katlar[0].Odalar[0].EkAlanlar!["oda-extra"] = Eleman("99");
        vm.Geo.Katlar[0].Acikliklar[0].EkAlanlar!["ac-extra"] = Eleman("99");
        vm.Geo.DuseyBirlesimleri[0].EkAlanlar!["bir-extra"] = Eleman("99");
        vm.Geo.DuseyBirlesimleri[0].Uyeler[0].EkAlanlar!["uye-extra"] = Eleman("99");

        vm.GeriAl();
        var geriKat = vm.Geo.Katlar[0];
        Esit(1, vm.Geo.EkAlanlar!["geo-extra"].GetInt32(), "Kök bilinmeyen alan");
        Esit(1, geriKat.EkAlanlar!["kat-extra"].GetInt32(), "Kat bilinmeyen alan");
        Esit(1, geriKat.Odalar[0].EkAlanlar!["oda-extra"].GetInt32(), "Oda bilinmeyen alan");
        Esit(1, geriKat.Acikliklar[0].EkAlanlar!["ac-extra"].GetInt32(), "Açıklık bilinmeyen alan");
        Esit(1, vm.Geo.DuseyBirlesimleri[0].EkAlanlar!["bir-extra"].GetInt32(), "Birleşim bilinmeyen alan");
        Esit(1, vm.Geo.DuseyBirlesimleri[0].Uyeler[0].EkAlanlar!["uye-extra"].GetInt32(), "Üye bilinmeyen alan");
        Esit(1, geriKat.DxfSegmentler[0].EkAlanlar!["segment-extra"].GetInt32(), "DXF bilinmeyen alan");
        Yaklasik(0.001, geriKat.DxfBirimOlcegiM, "DXF ölçek metadata'sı");
        Yaklasik(120, geriKat.DxfNormalizeOfsetiM.X, "DXF normalize X");
        Yaklasik(100, geriKat.DxfKaynakBolgesiMinM!.Value.X, "DXF kaynak min X");
        Yaklasik(150, geriKat.DxfKaynakBolgesiMaxM!.Value.X, "DXF kaynak max X");
    }

    public static void AciklikTaraYenidenAcistaAyniDxfDonusumunuKullanir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"m50-reopen-transform-{Guid.NewGuid():N}.dxf");
        try
        {
            var dxf = new DxfDocument();
            dxf.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
            dxf.Entities.Add(new Line(new Vector2(1000, 2000), new Vector2(2000, 2000)));
            dxf.Entities.Add(new Line(new Vector2(10000, 2000), new Vector2(11000, 2000)));
            dxf.Save(path);

            // Kayıttan yeniden açılmış kat: JsonIgnore tam segmentler boş, kalıcı
            // ölçek/ofset/bölge metadata'sı dolu.
            var kat = new BinaKat
            {
                Id = "kat-reopen", Ad = "Kat", KatNo = 1, DxfFilePath = path,
                DxfBirimOlcegiM = 0.001,
                DxfNormalizeOfsetiM = new Point2D(1, 2),
                DxfKaynakBolgesiMinM = new Point2D(0.9, 1.9),
                DxfKaynakBolgesiMaxM = new Point2D(2.1, 2.1)
            };
            var segmentler = new DxfKatPlaniParser().ExtractRawSegmentsFromFile(path, kat);
            Esit(1, segmentler.Count, "Kaynak bölgesi dışındaki plan elenmeli");
            Yaklasik(0, segmentler[0].A.X, "Yeniden okuma normalize A.X");
            Yaklasik(0, segmentler[0].A.Y, "Yeniden okuma normalize A.Y");
            Yaklasik(1, segmentler[0].B.X, "Yeniden okuma normalize B.X");

            var geo = BinaGeometriServisi.YeniOlustur("Reopen");
            geo.Katlar.Add(kat);
            var vm = new BinaGeometriViewModel { Geo = geo };
            vm.AciklikTara();
            Dogru(!vm.DurumMesaji.Contains("segmenti yok", StringComparison.OrdinalIgnoreCase),
                "AciklikTara JsonIgnore listeler boşken metadata-aware DXF yeniden okumasını kullanmalı.");

            File.Delete(path);
            kat.DxfSegmentler.Add(new BinaDxfSegment { A = new(0, 0), B = new(1, 0) });
            kat.Acikliklar.Add(new BinaAciklik
            {
                Id = "korunacak", Bas = new(0, 0), Bit = new(1, 0), GenislikM = 1
            });
            int oncekiAciklik = kat.Acikliklar.Count;
            Dogru(!vm.AciklikTaranabilir,
                "Yalnız sadeleştirilmiş önizleme segmenti tam taramayı etkinleştirmemeli.");
            vm.AciklikTara();
            Esit(oncekiAciklik, kat.Acikliklar.Count,
                "Kaynak DXF yokken kayıplı önizleme kümesi açıklıkları değiştirmemeli");
            Dogru(vm.DurumMesaji.Contains("tam", StringComparison.OrdinalIgnoreCase) &&
                  vm.DurumMesaji.Contains("DXF", StringComparison.OrdinalIgnoreCase),
                "Tam segment yokluğu kullanıcıya açıkça bildirilmelidir.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void OdaSilmeYetimAcikligiTemizler()
    {
        var (vm, kat, oda) = TekOdaliVm();
        kat.Acikliklar.Add(new BinaAciklik
        {
            Id = "yetim", Bas = new(1, 0), Bit = new(2, 0), GenislikM = 1
        });
        kat.SayilariYenile();
        vm.SeciliOdaId = oda.Id;
        vm.SeciliOdaSil();
        Esit(0, kat.Odalar.Count, "Oda silinmeli");
        Esit(0, kat.Acikliklar.Count, "Duvardan yoksun açıklık silinmeli");
    }

    public static void YansitilmisAciklikDuzenlemeleriSenkronKalir()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Açıklık Çifti");
        var kat = new BinaKat { Id = "kat-pair", Ad = "Kat", KatNo = 1, KatYuksekligiM = 3 };
        kat.Odalar.Add(new BinaOda
        {
            Id = "sol", Ad = "Sol", AlanM2 = 16,
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
        });
        kat.Odalar.Add(new BinaOda
        {
            Id = "sag", Ad = "Sağ", AlanM2 = 15.2,
            Poligon = [new(4.2, 0), new(8, 0), new(8, 4), new(4.2, 4)]
        });
        var a = new BinaAciklik { Id = "a", Bas = new(4, 1), Bit = new(4, 2), GenislikM = 1, YukseklikM = 2 };
        var b = new BinaAciklik { Id = "a-yansima", Bas = new(4.2, 1), Bit = new(4.2, 2), GenislikM = 1, YukseklikM = 2 };
        var bagimsiz = new BinaAciklik
        {
            Id = "bagimsiz", Kaynak = "manuel", Bas = new(4.4, 1), Bit = new(4.4, 2),
            GenislikM = 1, YukseklikM = 2, Tip = BinaAciklikTipi.Kapi
        };
        kat.Acikliklar.AddRange([a, b, bagimsiz]);
        geo.Katlar.Add(kat);
        var vm = new BinaGeometriViewModel { Geo = geo, SeciliAciklikId = a.Id };

        vm.SeciliAciklikTipGuncelle(BinaAciklikTipi.Pencere);
        vm.SeciliAciklikYukseklikGuncelle(1.4);
        vm.SeciliAciklikEsikGuncelle(0.8);
        vm.SeciliAciklikIptalToggle();
        var cift = kat.Acikliklar.Where(x => x.Id is "a" or "a-yansima").ToList();
        Dogru(cift.All(x => x.Tip == BinaAciklikTipi.Pencere), "Tip iki yüzde senkron olmalı.");
        Dogru(cift.All(x => Math.Abs(x.YukseklikM - 1.4) < 1e-9), "Yükseklik iki yüzde senkron olmalı.");
        Dogru(cift.All(x => Math.Abs(x.EsikYuksekligiM - 0.8) < 1e-9), "Eşik iki yüzde senkron olmalı.");
        Dogru(cift.All(x => x.IptalEdildi), "İptal iki yüzde senkron olmalı.");
        Dogru(bagimsiz.Tip == BinaAciklikTipi.Kapi && !bagimsiz.IptalEdildi &&
              Math.Abs(bagimsiz.YukseklikM - 2) < 1e-9 && Math.Abs(bagimsiz.EsikYuksekligiM) < 1e-9,
            "Yakındaki bağımsız açıklık çift düzenlemesinden etkilenmemeli.");

        vm.SeciliAciklikSil();
        Esit(1, kat.Acikliklar.Count, "Fiziksel açıklığın yalnız iki yüzü birlikte silinmeli");
        Esit("bagimsiz", kat.Acikliklar.Single().Id, "Bağımsız açıklık silinmemeli");
    }

    public static void DuvarTasimaAciklikKoordinatlariniDonusturur()
    {
        var (vm, kat, oda) = TekOdaliVm();
        var ac = new BinaAciklik { Id = "ac-wall", Bas = new(1, 0), Bit = new(2, 0), GenislikM = 1 };
        var yansima = new BinaAciklik
        {
            Id = "ac-wall-yansima", Bas = new(1, -0.2), Bit = new(2, -0.2), GenislikM = 1
        };
        kat.Odalar.Add(new BinaOda
        {
            Id = "oda-komsu", Ad = "Komşu", AlanM2 = 15.2,
            Poligon = [new(0, -0.2), new(4, -0.2), new(4, -4), new(0, -4)]
        });
        kat.Acikliklar.AddRange([ac, yansima]);
        vm.SeciliOdaId = oda.Id;
        vm.UndoSnapshotAl(); // Canvas gerçek sürüklemede eşiği geçince bunu bir kez çağırır.
        vm.VertexGuncelle(1, 8, 0);
        Yaklasik(2, ac.Bas.X, "Duvar uzarken açıklık başlangıcı parametrik taşınmalı");
        Yaklasik(4, ac.Bit.X, "Duvar uzarken açıklık bitişi parametrik taşınmalı");
        Yaklasik(2, ac.GenislikM, "Duvar uzarken açıklık genişliği güncellenmeli");
        Yaklasik(2, yansima.Bas.X, "Yansıtılmış açıklık başlangıcı eşle birlikte taşınmalı");
        Yaklasik(4, yansima.Bit.X, "Yansıtılmış açıklık bitişi eşle birlikte taşınmalı");
        Yaklasik(-0.2, yansima.Bas.Y, "Yansıtılmış açıklığın signed normal duvar ofseti korunmalı");
        Yaklasik(-0.2, yansima.Bit.Y, "Yansıtılmış açıklık kendi paralel duvarında kalmalı");
        vm.GeriAl();
        var geriAc = vm.Geo.Katlar[0].Acikliklar.Single(x => x.Id == "ac-wall");
        var geriYansima = vm.Geo.Katlar[0].Acikliklar.Single(x => x.Id == "ac-wall-yansima");
        Yaklasik(1, geriAc.Bas.X, "Duvar taşıma undo başlangıcı");
        Yaklasik(2, geriAc.Bit.X, "Duvar taşıma undo bitişi");
        Yaklasik(1, geriYansima.Bas.X, "Yansıma undo başlangıcı");
        Yaklasik(-0.2, geriYansima.Bas.Y, "Yansıma undo duvar ofseti");
    }

    public static void GecersizSayisalGirdilerAtomikReddedilir()
    {
        var (vm, kat, _) = TekOdaliVm();
        double eskiYukseklik = kat.KatYuksekligiM;
        Dogru(!vm.KatYuksekligiGuncelle(double.NaN), "NaN kat yüksekliği reddedilmeli.");
        Dogru(!vm.KatYuksekligiGuncelle(0.1), "Fiziksel aralık dışı kat yüksekliği reddedilmeli.");
        Yaklasik(eskiYukseklik, kat.KatYuksekligiM, "Reddedilen değer modeli değiştirmemeli");
        Dogru(!vm.IsDirty, "Reddedilen sayısal giriş dirty üretmemeli.");

        vm.KatOfsetX = double.PositiveInfinity;
        Yaklasik(0, kat.OriginOffset.X, "Sonsuz ofset modeli değiştirmemeli");
        Dogru(!vm.IsDirty, "Reddedilen ofset dirty üretmemeli.");
    }

    public static void SeciliKatDegisinceOdaVeAciklikSecimiTemizlenir()
    {
        var (vm, kat, oda) = TekOdaliVm();
        var ac = new BinaAciklik { Id = "secim-ac", Bas = new(1, 0), Bit = new(2, 0), GenislikM = 1 };
        kat.Acikliklar.Add(ac);
        var ikinci = vm.KatEkle("İkinci");
        vm.SeciliKat = kat;
        vm.SeciliOdaId = oda.Id;
        vm.SeciliAciklikId = ac.Id;
        vm.SeciliKat = ikinci;
        Dogru(vm.SeciliOdaId is null, "Kat değişince oda seçimi temizlenmeli.");
        Dogru(vm.SeciliAciklikId is null, "Kat değişince açıklık seçimi temizlenmeli.");
    }

    public static void CanvasModlariBirbiriniIptalEder()
    {
        var mod = BinaEditorModDurumu.Bos.OdaCiziminiBaslat();
        Dogru(mod.OdaCizim && !mod.AciklikEkle, "Oda çizim modu tek aktif mod olmalı.");
        mod = mod.AciklikEklemeyiBaslat();
        Dogru(!mod.OdaCizim && mod.AciklikEkle, "Açıklık modu oda çizimini iptal etmeli.");
        mod = mod.OdaCiziminiBaslat();
        Dogru(mod.OdaCizim && !mod.AciklikEkle, "Oda çizimi açıklık modunu iptal etmeli.");
        mod = mod.TumunuIptalEt();
        Dogru(!mod.OdaCizim && !mod.AciklikEkle, "Toplu iptal bütün modları kapatmalı.");
    }

    public static void CanvasNoOpSuruklemeSnapshotBaslatmaz()
    {
        var kapi = typeof(BinaGeometriCanvas).GetMethod(
            "MutasyonKapisi", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Canvas mutasyon kapısı bulunamadı.");
        int snapshotSayisi = 0;
        Action snapshot = () => snapshotSayisi++;

        object?[] noOpArgs = [false, false, snapshot];
        bool noOpUygulansin = (bool)(kapi.Invoke(null, noOpArgs) ?? true);
        Dogru(!noOpUygulansin, "Aynı snap koordinatı mutasyon üretmemeli.");
        Dogru(noOpArgs[0] is false, "No-op sürükleme snapshot bayrağını açmamalı.");
        Esit(0, snapshotSayisi, "No-op sürükleme snapshot sayısı");

        object?[] ilkDegisimArgs = [false, true, snapshot];
        Dogru((bool)(kapi.Invoke(null, ilkDegisimArgs) ?? false), "Gerçek değişim uygulanmalı.");
        Dogru(ilkDegisimArgs[0] is true, "İlk gerçek değişim snapshot bayrağını açmalı.");
        Esit(1, snapshotSayisi, "İlk gerçek değişim snapshot sayısı");

        object?[] devamArgs = [true, true, snapshot];
        Dogru((bool)(kapi.Invoke(null, devamArgs) ?? false), "Aynı drag içindeki sonraki değişim uygulanmalı.");
        Esit(1, snapshotSayisi, "Bir drag içinde snapshot yalnız bir kez alınmalı");
    }

    public static void BelgeKomutuAktifEditoruOnceVeTekKezCommitEder()
    {
        var aktif = new object();
        var baska = new object();
        int aktifCommit = 0, baskaCommit = 0;
        bool bulundu = BinaEditorCommitYardimci.Uygula(
            aktif,
            (baska, () => baskaCommit++),
            (aktif, () => aktifCommit++),
            (aktif, () => aktifCommit++));
        Dogru(bulundu, "Aktif editor eşleşmesi bulunmalı.");
        Esit(1, aktifCommit, "Aktif editor commit sayısı");
        Esit(0, baskaCommit, "Odakta olmayan editor commit sayısı");

        Dogru(!BinaEditorCommitYardimci.Uygula(new object(),
            (aktif, () => aktifCommit++)), "Eşleşmeyen kaynak commit edilmemeli.");
        Esit(1, aktifCommit, "Eşleşmeyen kaynak sonrası commit sayısı");
    }

    private static (BinaGeometriViewModel Vm, BinaKat Kat, BinaOda Oda) TekOdaliVm()
    {
        var geo = BinaGeometriServisi.YeniOlustur("Başlangıç");
        var kat = new BinaKat { Id = "kat-1", Ad = "Kat 1", KatNo = 1, KatYuksekligiM = 3 };
        var oda = new BinaOda
        {
            Id = "oda-1", Ad = "Oda 1", AlanM2 = 16,
            Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)]
        };
        kat.Odalar.Add(oda);
        geo.Katlar.Add(kat);
        var vm = new BinaGeometriViewModel { Geo = geo };
        vm.SeciliOdaId = oda.Id;
        return (vm, kat, oda);
    }

    private static Dictionary<string, JsonElement> Alan(string ad, string json)
        => new(StringComparer.Ordinal) { [ad] = Eleman(json) };

    private static JsonElement Eleman(string json)
    {
        using var belge = JsonDocument.Parse(json);
        return belge.RootElement.Clone();
    }

    private static void Dogru(bool kosul, string mesaj)
    {
        if (!kosul) throw new InvalidOperationException(mesaj);
    }

    private static void Esit<T>(T beklenen, T gercek, string alan)
    {
        if (!EqualityComparer<T>.Default.Equals(beklenen, gercek))
            throw new InvalidOperationException($"{alan}: beklenen '{beklenen}', bulunan '{gercek}'.");
    }

    private static void Yaklasik(double beklenen, double gercek, string alan, double tolerans = 1e-9)
    {
        if (!double.IsFinite(gercek) || Math.Abs(beklenen - gercek) > tolerans)
            throw new InvalidOperationException($"{alan}: beklenen {beklenen}, bulunan {gercek}.");
    }
}
