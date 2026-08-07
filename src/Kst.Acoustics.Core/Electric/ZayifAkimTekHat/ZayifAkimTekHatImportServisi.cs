using System.Text.Json;
using Kst.Acoustics.Core.Electric.BasitProjeCiz;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public static class ZayifAkimTekHatImportServisi
{
    public static ZayifAkimTekHatProje M12DenYukle(string e4p12Path)
    {
        if (!File.Exists(e4p12Path))
            throw new FileNotFoundException($"Dosya bulunamadı: {e4p12Path}");

        var json = File.ReadAllText(e4p12Path);
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        var m12Proje = JsonSerializer.Deserialize<BasitProjeCizProje>(json, opts)
            ?? throw new InvalidOperationException("M12 proje deserialize edilemedi");

        return Donustur(m12Proje, e4p12Path);
    }

    /// <summary>
    /// Bir BasitProjeCiz projesini (M12/M16) bellek içinde Zayıf Akım Tek Hat (M13)
    /// modeline dönüştürür. Dosya/serileştirme adımı yoktur — M16'nın "M13'e Aktar"
    /// akışı bunu doğrudan çağırır; <see cref="M12DenYukle"/> ise dosyadan okuyup buraya verir.
    /// Yalnız zayıf akım sistem devreleri okunur; kuvvetli akım içeriği yok sayılır.
    /// </summary>
    public static ZayifAkimTekHatProje Donustur(BasitProjeCizProje m12Proje, string? kaynakYol = null)
    {
        var m13Proje = new ZayifAkimTekHatProje
        {
            KaynakDosyaYolu = kaynakYol,
            ProjeAdi = m12Proje.ProjeAdi,
            OlusturmaTarihi = DateTime.UtcNow,
            Sayfalar = new()
        };

        // Determine BinaKonfigurasyonu from imported floors
        int maxKatNo = 0;
        int minKatNo = 0;
        bool hasZemin = false;
        bool hasCati = false;

        foreach (var k in m12Proje.Katlar)
        {
            if (k.KatNo == 0) hasZemin = true;
            else if (k.KatNo > 0)
            {
                if (k.Ad != null && k.Ad.Contains("Çatı", StringComparison.OrdinalIgnoreCase))
                {
                    hasCati = true;
                }
                else
                {
                    maxKatNo = Math.Max(maxKatNo, k.KatNo);
                }
            }
            else if (k.KatNo < 0)
            {
                minKatNo = Math.Min(minKatNo, k.KatNo);
            }
        }

        m13Proje.Bina = new Kst.Acoustics.Core.Electric.Models.BinaKonfigurasyonu
        {
            BodrumSayisi = Math.Abs(minKatNo),
            ZeminVar = hasZemin,
            NormalKatSayisi = maxKatNo,
            CatiKati = hasCati ? 1 : 0
        };

        m13Proje.KatlariBinadanGuncelle();

        // Tüm katların tüm sistemlerini topla
        foreach (var kat in m12Proje.Katlar)
        {
            DosyaYonetSistemDevreleri(m13Proje, kat);
        }

        // Data sistemini rack-kabin merkezli yeni modele aktar.
        DataSisteminiDoldur(m13Proje, m12Proje);

        // Kamera/CCTV sistemini Data ile aynı rack-kabin mantığına aktar.
        KameraSisteminiDoldur(m13Proje, m12Proje);

        // Acil anons sistemini kat bazlı geçişli hoparlör hatlarına aktar.
        AnonsSisteminiDoldur(m13Proje, m12Proje);

        // TV sistemini ana santrale inen koaksiyel hatlar ve çatı uydu hatları olarak aktar.
        TvSisteminiDoldur(m13Proje, m12Proje);

        // Yangın algılama sistemini kat panosuz, doğrudan santrale dönen loop'lar olarak aktar.
        YanginSisteminiDoldur(m13Proje, m12Proje);

        // Telefon sistemini TV deseniyle (her priz ayrı hat → santral) aktar.
        TelefonSisteminiDoldur(m13Proje, m12Proje);

        // Diafon/kapı zili sistemini kat üniteleri → santral hatları olarak aktar.
        DiafonSisteminiDoldur(m13Proje, m12Proje);

        // Sayfa bazında merkez bağlantılarını toparla (cross-floor connection resolution)
        foreach (var sayfa in m13Proje.Sayfalar)
        {
            var anaMerkez = sayfa.Bloklar.FirstOrDefault(b => b.Rol == ELVBlokRol.Merkez);
            if (anaMerkez != null)
            {
                foreach (var blok in sayfa.Bloklar)
                {
                    if (blok.Id == anaMerkez.Id) continue;
                    
                    // Eğer AraToplayici veya Cihaz ise ve herhangi bir ebeveyni yoksa, ana merkeze bağla
                    bool hasParent = sayfa.Bloklar.Any(b => b.BagliCocukBlokIds.Contains(blok.Id));
                    if (!hasParent)
                    {
                        if (blok.Rol == ELVBlokRol.AraToplayici || blok.Rol == ELVBlokRol.Cihaz)
                        {
                            if (!anaMerkez.BagliCocukBlokIds.Contains(blok.Id))
                            {
                                anaMerkez.BagliCocukBlokIds.Add(blok.Id);
                            }
                        }
                    }
                }
            }
        }

        return m13Proje;
    }

    /// <summary>
    /// M12 verisinden data rack-kabin topolojisini kurar. Her kattaki data devresi bir rack kabin
    /// olur; data hat sayısı o devredeki DataPriz adedidir. Hiçbir devreye atanmamış DataPriz'ler
    /// (devre olsun olmasın) kat için ayrı bir rack altında toplanır — devre varken devresiz
    /// prizlerin sessizce düşmesi veri kaybıdır. Kablo metrajları M12'de bulunmadığından
    /// kullanıcı tarafından girilir.
    /// </summary>
    private static void DataSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var data = m13.DataSistemi;
        data.RackKabinler.Clear();

        foreach (var kat in m12.Katlar)
        {
            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var dataDevreleri = kat.SistemDevreleri
                .Where(d => d.Sistem == BpcPaftaTipi.Data)
                .ToList();

            int idx = 1;
            foreach (var devre in dataDevreleri)
            {
                int dataHat = devre.CihazIds
                    .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                    .Count(e => e is { Tip: BpcElemanTipi.DataPriz });

                var rack = new ElvRackKabin
                {
                    Ad = dataDevreleri.Count > 1 ? $"{katAdi} Rack {idx}" : $"{katAdi} Rack",
                    KatNo = kat.KatNo,
                    KatAdi = katAdi,
                    DataHatSayisi = dataHat,
                    WirelessSayisi = 0
                };
                HatListesiOlustur(rack.DataHatlari, dataHat, "Data");
                data.RackKabinler.Add(rack);
                idx++;
            }

            var atananIdler = dataDevreleri.SelectMany(d => d.CihazIds).ToHashSet();
            int devresizHat = kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.DataPriz && !atananIdler.Contains(e.Id));
            if (devresizHat > 0)
            {
                var rack = new ElvRackKabin
                {
                    Ad = dataDevreleri.Count > 0 ? $"{katAdi} Rack (Devresiz)" : $"{katAdi} Rack",
                    KatNo = kat.KatNo,
                    KatAdi = katAdi,
                    DataHatSayisi = devresizHat,
                    WirelessSayisi = 0
                };
                HatListesiOlustur(rack.DataHatlari, devresizHat, "Data");
                data.RackKabinler.Add(rack);
            }
        }

        // Üst kat üstte görünsün diye katı büyükten küçüğe sırala.
        data.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(data.AnaMerkez.Ad))
            data.AnaMerkez.Ad = "Ana Merkez";
    }

    /// <summary>
    /// M12 verisinden kamera rack/toplayıcı topolojisini kurar. Her kattaki CCTV devresi
    /// bir rack/PoE switch kabul edilir; kamera sayısı o devredeki Kamera adedidir.
    /// Devresiz Kameralar varsa kat için tek rack altında toplanır.
    /// </summary>
    private static void KameraSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var kamera = m13.KameraSistemi;
        kamera.RackKabinler.Clear();

        foreach (var kat in m12.Katlar)
        {
            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var cctvDevreleri = kat.SistemDevreleri
                .Where(d => d.Sistem == BpcPaftaTipi.Cctv)
                .ToList();

            int idx = 1;
            foreach (var devre in cctvDevreleri)
            {
                int kameraSayisi = devre.CihazIds
                    .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                    .Count(e => e is { Tip: BpcElemanTipi.Kamera });

                var rack = new ElvRackKabin
                {
                    Ad = cctvDevreleri.Count > 1 ? $"{katAdi} CCTV Rack {idx}" : $"{katAdi} CCTV Rack",
                    KatNo = kat.KatNo,
                    KatAdi = katAdi,
                    KameraSayisi = kameraSayisi,
                    KatIciKabloTipi = "Cat6 U/UTP",
                    MerkezeKabloTipi = "Cat6 U/UTP"
                };
                HatListesiOlustur(rack.KameraHatlari, kameraSayisi, "Kamera");
                kamera.RackKabinler.Add(rack);
                idx++;
            }

            // Devre varken de devreye atanmamış kameralar aktarılmalı (veri kaybı önlenir).
            var atananIdler = cctvDevreleri.SelectMany(d => d.CihazIds).ToHashSet();
            int devresizKamera = kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.Kamera && !atananIdler.Contains(e.Id));
            if (devresizKamera > 0)
            {
                var rack = new ElvRackKabin
                {
                    Ad = cctvDevreleri.Count > 0 ? $"{katAdi} CCTV Rack (Devresiz)" : $"{katAdi} CCTV Rack",
                    KatNo = kat.KatNo,
                    KatAdi = katAdi,
                    KameraSayisi = devresizKamera,
                    KatIciKabloTipi = "Cat6 U/UTP",
                    MerkezeKabloTipi = "Cat6 U/UTP"
                };
                HatListesiOlustur(rack.KameraHatlari, devresizKamera, "Kamera");
                kamera.RackKabinler.Add(rack);
            }
        }

        kamera.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(kamera.AnaMerkez.Ad))
            kamera.AnaMerkez.Ad = "CCTV Ana Merkez";
    }

    /// <summary>
    /// M12 verisinden acil anons hatlarını kurar. Her Anons devresi bir geçişli hoparlör
    /// hattı kabul edilir; devre yoksa kattaki tüm hoparlörler tek hat altında toplanır.
    /// </summary>
    private static void AnonsSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var anons = m13.AnonsSistemi;
        anons.RackKabinler.Clear();

        foreach (var kat in m12.Katlar)
        {
            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var anonsDevreleri = kat.SistemDevreleri
                .Where(d => d.Sistem == BpcPaftaTipi.Anons)
                .ToList();

            // Devreye atanmamış hoparlörler ayrı bir hat olarak eklenir (devre varken de).
            var atananIdler = anonsDevreleri.SelectMany(d => d.CihazIds).ToHashSet();
            int devresizHoparlor = kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.Hoparlor && !atananIdler.Contains(e.Id));

            if (anonsDevreleri.Count == 0 && devresizHoparlor == 0)
                continue;

            var rack = new ElvRackKabin
            {
                Ad = $"{katAdi} Anons Hatları",
                KatNo = kat.KatNo,
                KatAdi = katAdi,
                MerkezeKabloTipi = "2x1.5 mm² FE180"
            };

            int hatNo = 1;
            foreach (var devre in anonsDevreleri)
            {
                int hoparlorSayisi = devre.CihazIds
                    .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                    .Count(e => e is { Tip: BpcElemanTipi.Hoparlor });

                rack.AnonsHatlari.Add(new ElvAnonsHatti
                {
                    HatNo = hatNo,
                    Ad = $"Anons Hattı {hatNo}",
                    HoparlorSayisi = hoparlorSayisi
                });
                hatNo++;
            }

            if (devresizHoparlor > 0)
            {
                rack.AnonsHatlari.Add(new ElvAnonsHatti
                {
                    HatNo = hatNo,
                    Ad = anonsDevreleri.Count > 0 ? $"Anons Hattı {hatNo} (Devresiz)" : $"Anons Hattı {hatNo}",
                    HoparlorSayisi = devresizHoparlor
                });
            }

            rack.AnonsHatSayisi = rack.AnonsHatlari.Count;
            anons.RackKabinler.Add(rack);
        }

        anons.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(anons.AnaMerkez.Ad))
            anons.AnaMerkez.Ad = "Acil Anons Merkezi";
    }

    /// <summary>
    /// M12 verisinden TV sistemini kurar. Her TV prizi ayrı koaksiyel hat olarak ana
    /// santrale iner. Ana santralden çatıdaki uydu/antenlere en az bir koaksiyel hat çıkar.
    /// </summary>
    private static void TvSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var tv = m13.TvSistemi;
        tv.RackKabinler.Clear();
        tv.OrtakUclar.Clear();

        foreach (var kat in m12.Katlar)
        {
            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            // Her TV prizi zaten ayrı hat olarak ana santrale iner; devre üyeliğinden bağımsız
            // olarak kattaki tüm TV prizleri sayılır (devre varken devresiz prizler de dahil).
            int tvPrizSayisi = kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.TvPriz);

            if (tvPrizSayisi == 0)
                continue;

            foreach (var eleman in kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.TvPriz))
                tv.OrtakUclar.Add(new ElvTvUcu
                {
                    KaynakM12ElemanId = eleman.Id,
                    Ad = string.IsNullOrWhiteSpace(eleman.Ad) ? $"{katAdi} TV Prizi" : eleman.Ad,
                    KatNo = kat.KatNo,
                    KatAdi = katAdi
                });

            var rack = new ElvRackKabin
            {
                Ad = $"{katAdi} TV Hatları",
                KatNo = kat.KatNo,
                KatAdi = katAdi,
                TvPrizSayisi = tvPrizSayisi,
                KatIciKabloTipi = "RG-6/U Koaksiyel",
                MerkezeKabloTipi = "RG-6/U Koaksiyel"
            };

            HatListesiOlustur(rack.TvHatlari, tvPrizSayisi, "TV", "RG-6/U Koaksiyel");
            tv.RackKabinler.Add(rack);
        }

        tv.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));
        TvCatiHatlariniSayiyaEsitle(tv);
        ElvTvTopolojiServisi.DirektHatlariEsitle(tv);
        ElvTvTopolojiServisi.IpTvRackleriniEsitle(tv);

        if (string.IsNullOrWhiteSpace(tv.AnaMerkez.Ad))
            tv.AnaMerkez.Ad = "TV Ana Santral";
    }

    /// <summary>
    /// M12 verisinden yangın algılama loop'larını kurar. Kat panosu/rack oluşturmaz;
    /// her yangın devresi doğrudan ana santrale dönen bir loop kabul edilir.
    /// </summary>
    private static void YanginSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var yangin = m13.YanginSistemi;
        yangin.KatLoopGruplari.Clear();

        foreach (var kat in m12.Katlar)
        {
            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var yanginDevreleri = kat.SistemDevreleri
                .Where(d => d.Sistem == BpcPaftaTipi.Yangin)
                .ToList();

            var loopGrubu = new ElvYanginKatLoopGrubu
            {
                Ad = $"{katAdi} Yangın Loopları",
                KatNo = kat.KatNo,
                KatAdi = katAdi
            };

            int loopNo = 1;
            foreach (var devre in yanginDevreleri)
            {
                var cihazlar = devre.CihazIds
                    .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                    .Where(e => e is not null && YanginCihaziMi(e.Tip))
                    .Select(e => YanginLoopCihazinaDonustur(e!.Tip))
                    .ToList();

                loopGrubu.Looplar.Add(new ElvYanginLoop
                {
                    LoopNo = loopNo,
                    Ad = $"Loop {loopNo}",
                    Cihazlar = cihazlar
                });
                loopNo++;
            }

            // Devreye atanmamış yangın cihazları ayrı bir loop'ta toplanır (devre varken de).
            var atananIdler = yanginDevreleri.SelectMany(d => d.CihazIds).ToHashSet();
            var devresizCihazlar = kat.Elemanlar
                .Where(e => YanginCihaziMi(e.Tip) && !atananIdler.Contains(e.Id))
                .Select(e => YanginLoopCihazinaDonustur(e.Tip))
                .ToList();
            if (devresizCihazlar.Count > 0)
            {
                loopGrubu.Looplar.Add(new ElvYanginLoop
                {
                    LoopNo = loopNo,
                    Ad = yanginDevreleri.Count > 0 ? $"Loop {loopNo} (Devresiz)" : $"Loop {loopNo}",
                    Cihazlar = devresizCihazlar
                });
            }

            loopGrubu.LoopSayisi = loopGrubu.Looplar.Count;
            if (loopGrubu.LoopSayisi > 0)
                yangin.KatLoopGruplari.Add(loopGrubu);
        }

        yangin.KatLoopGruplari.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(yangin.AnaMerkez.Ad))
            yangin.AnaMerkez.Ad = "Yangın Algılama Santrali";
    }

    /// <summary>
    /// M12 verisinden telefon sistemini kurar. Her telefon prizi ayrı hat ile santrale
    /// iner; TV gibi devre üyeliğinden bağımsız olarak kattaki tüm prizler sayılır.
    /// </summary>
    private static void TelefonSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var telefon = m13.TelefonSistemi;
        telefon.RackKabinler.Clear();

        foreach (var kat in m12.Katlar)
        {
            int prizSayisi = kat.Elemanlar.Count(e => e.Tip == BpcElemanTipi.TelefonPriz);
            if (prizSayisi == 0)
                continue;

            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var rack = new ElvRackKabin
            {
                Ad = $"{katAdi} Telefon Hatları",
                KatNo = kat.KatNo,
                KatAdi = katAdi,
                TelefonPrizSayisi = prizSayisi,
                KatIciKabloTipi = "J-Y(St)Y 2x2x0.6 Telefon Kablosu",
                MerkezeKabloTipi = "J-Y(St)Y 10x2x0.6 Telefon Kablosu"
            };
            HatListesiOlustur(rack.TelefonHatlari, prizSayisi, "Telefon", "J-Y(St)Y 2x2x0.6 Telefon Kablosu");
            telefon.RackKabinler.Add(rack);
        }

        telefon.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(telefon.AnaMerkez.Ad))
            telefon.AnaMerkez.Ad = "Telefon Santrali";
    }

    /// <summary>
    /// M12 verisinden diafon sistemini kurar. Kattaki diafon üniteleri ve kapı zilleri
    /// santrale hat çeken üniteler olarak sayılır (DiafonSantrali merkez cihazıdır, sayılmaz).
    /// </summary>
    private static void DiafonSisteminiDoldur(ZayifAkimTekHatProje m13, BasitProjeCizProje m12)
    {
        var diafon = m13.DiafonSistemi;
        diafon.RackKabinler.Clear();

        foreach (var kat in m12.Katlar)
        {
            int uniteSayisi = kat.Elemanlar.Count(e =>
                e.Tip is BpcElemanTipi.Diafon or BpcElemanTipi.KapiZili);
            if (uniteSayisi == 0)
                continue;

            var katAdi = m13.Katlar.FirstOrDefault(k => k.KatNo == kat.KatNo)?.Ad
                         ?? (string.IsNullOrWhiteSpace(kat.Ad) ? $"{kat.KatNo}. Kat" : kat.Ad);

            var rack = new ElvRackKabin
            {
                Ad = $"{katAdi} Diafon Üniteleri",
                KatNo = kat.KatNo,
                KatAdi = katAdi,
                DiafonUniteSayisi = uniteSayisi,
                KatIciKabloTipi = "J-Y(St)Y 3x2x0.8 Diafon Kablosu",
                MerkezeKabloTipi = "J-Y(St)Y 5x2x0.8 Diafon Kablosu"
            };
            HatListesiOlustur(rack.DiafonUniteHatlari, uniteSayisi, "Diafon Ünitesi", "J-Y(St)Y 3x2x0.8 Diafon Kablosu");
            diafon.RackKabinler.Add(rack);
        }

        diafon.RackKabinler.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));

        if (string.IsNullOrWhiteSpace(diafon.AnaMerkez.Ad))
            diafon.AnaMerkez.Ad = "Diafon Santrali";
    }

    private static void HatListesiOlustur(List<ElvHatMetraj> liste, int adet, string prefix, string kabloTipi = "Cat6 U/UTP")
    {
        liste.Clear();
        for (int i = 1; i <= adet; i++)
        {
            liste.Add(new ElvHatMetraj
            {
                HatNo = i,
                Ad = $"{prefix} {i}",
                KabloTipi = kabloTipi
            });
        }
    }

    private static void TvCatiHatlariniSayiyaEsitle(ElvTvSistemi tv)
    {
        tv.CatiUyduSayisi = Math.Max(1, tv.CatiUyduSayisi);
        while (tv.CatiHatlari.Count < tv.CatiUyduSayisi)
        {
            int no = tv.CatiHatlari.Count + 1;
            tv.CatiHatlari.Add(new ElvTvCatiHatti { HatNo = no, Ad = $"Çatı Uydu {no}" });
        }

        while (tv.CatiHatlari.Count > tv.CatiUyduSayisi)
            tv.CatiHatlari.RemoveAt(tv.CatiHatlari.Count - 1);

        for (int i = 0; i < tv.CatiHatlari.Count; i++)
        {
            tv.CatiHatlari[i].HatNo = i + 1;
            if (string.IsNullOrWhiteSpace(tv.CatiHatlari[i].Ad))
                tv.CatiHatlari[i].Ad = $"Çatı Uydu {i + 1}";
            if (string.IsNullOrWhiteSpace(tv.CatiHatlari[i].KabloTipi))
                tv.CatiHatlari[i].KabloTipi = "RG-11/U Koaksiyel";
        }
    }

    private static bool YanginCihaziMi(BpcElemanTipi tip) => tip is
        BpcElemanTipi.DumanDedektor or
        BpcElemanTipi.IsiDedektor or
        BpcElemanTipi.KombineDedektor or
        BpcElemanTipi.AsmaTavanDedektorGrubu or
        BpcElemanTipi.YanginButonu or
        BpcElemanTipi.Siren or
        BpcElemanTipi.Flasor;

    private static ElvYanginLoopCihazi YanginLoopCihazinaDonustur(BpcElemanTipi tip)
    {
        var (elvTip, ad) = tip switch
        {
            BpcElemanTipi.DumanDedektor => (ElvYanginCihazTipi.DumanDedektoru, "Duman Dedektörü"),
            BpcElemanTipi.IsiDedektor => (ElvYanginCihazTipi.IsiDedektoru, "Isı Dedektörü"),
            BpcElemanTipi.KombineDedektor => (ElvYanginCihazTipi.KombineDedektor, "Kombine Dedektör"),
            BpcElemanTipi.AsmaTavanDedektorGrubu => (ElvYanginCihazTipi.AsmaTavanDedektoru, "Asma Tavan Dedektör Grubu"),
            BpcElemanTipi.YanginButonu => (ElvYanginCihazTipi.YanginButonu, "Yangın İhbar Butonu"),
            BpcElemanTipi.Siren => (ElvYanginCihazTipi.Siren, "Yangın Sireni"),
            BpcElemanTipi.Flasor => (ElvYanginCihazTipi.Flasor, "Flaşör"),
            _ => throw new ArgumentOutOfRangeException(nameof(tip), tip, "Yangın loop cihazı değil.")
        };
        return new ElvYanginLoopCihazi { Tip = elvTip, Ad = ad };
    }

    private static void DosyaYonetSistemDevreleri(ZayifAkimTekHatProje m13Proje, BasitProjeKat kat)
    {
        foreach (var devre in kat.SistemDevreleri)
        {
            var tanim = BpcSistemKatalogu.Get(devre.Sistem);

            // Sistem sayfası var mı?
            var sayfa = m13Proje.Sayfalar.FirstOrDefault(s => s.Sistem == devre.Sistem);
            if (sayfa == null)
            {
                sayfa = new ELVSayfa
                {
                    Sistem = devre.Sistem,
                    SistemAdi = tanim.Ad,
                    KabloRengi = tanim.KabloRengi,
                    Bloklar = new()
                };
                m13Proje.Sayfalar.Add(sayfa);
            }

            // Blokları oluştur: Merkez → Aratoplayıcı → Cihazlar
            DosyaYonetBloklar(sayfa, devre, kat, tanim);
        }
    }

    private static void DosyaYonetBloklar(ELVSayfa sayfa, BpcSistemDevre devre, BasitProjeKat kat, BpcSistemTanim tanim)
    {
        // Merkez bloku
        string? merkezBlokId = null;
        if (!string.IsNullOrEmpty(devre.MerkezId))
        {
            var merkezEleman = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.MerkezId);
            if (merkezEleman != null)
            {
                var merkezBlok = new ELVBlok
                {
                    ElemanTipi = merkezEleman.Tip,
                    ElemanAdi = merkezEleman.Ad,
                    Kat = kat.KatNo,
                    Rol = ELVBlokRol.Merkez
                };
                sayfa.Bloklar.Add(merkezBlok);
                merkezBlokId = merkezBlok.Id;
            }
        }

        // Aratoplayıcı bloku
        string? araToplayiciBlokId = null;
        if (!string.IsNullOrEmpty(devre.AraToplayiciId))
        {
            var araEleman = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.AraToplayiciId);
            if (araEleman != null)
            {
                var araBlok = new ELVBlok
                {
                    ElemanTipi = araEleman.Tip,
                    ElemanAdi = araEleman.Ad,
                    Kat = kat.KatNo,
                    Rol = ELVBlokRol.AraToplayici
                };
                sayfa.Bloklar.Add(araBlok);
                araToplayiciBlokId = araBlok.Id;

                // Aratoplayıcı merkeze bağlı
                if (merkezBlokId != null)
                {
                    var merkezBlok = sayfa.Bloklar.First(b => b.Id == merkezBlokId);
                    if (!merkezBlok.BagliCocukBlokIds.Contains(araToplayiciBlokId))
                        merkezBlok.BagliCocukBlokIds.Add(araToplayiciBlokId);
                }
            }
        }

        // Cihaz blokları
        var cihazParent = araToplayiciBlokId ?? merkezBlokId;
        if (cihazParent != null)
        {
            var cihazParentBlok = sayfa.Bloklar.First(b => b.Id == cihazParent);

            var cihazElemanlari = devre.CihazIds
                .Select(cihazId => kat.Elemanlar.FirstOrDefault(e => e.Id == cihazId))
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();

            var groupedCihazlar = cihazElemanlari
                .GroupBy(e => e.Tip)
                .ToList();

            foreach (var group in groupedCihazlar)
            {
                var modelEleman = group.First();
                var cihazBlok = new ELVBlok
                {
                    ElemanTipi = modelEleman.Tip,
                    ElemanAdi = modelEleman.Ad,
                    Kat = kat.KatNo,
                    Rol = ELVBlokRol.Cihaz,
                    Miktar = group.Count()
                };
                sayfa.Bloklar.Add(cihazBlok);

                // Cihazı ebeveyne bağla
                if (!cihazParentBlok.BagliCocukBlokIds.Contains(cihazBlok.Id))
                    cihazParentBlok.BagliCocukBlokIds.Add(cihazBlok.Id);
            }
        }
    }
}
