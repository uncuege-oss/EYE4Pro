using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Tests;

public static class M08HardeningRegressionTests
{
    public static void PartialUnassignedFacadeIsRejected()
    {
        var room = Rect("Oda", 0, 0, 4, 4);
        var assigned = new Bolme
        {
            Ad = "Atanmış cephe",
            Alan = 12,
            HasGeometry = true,
            KaynakMekan = room,
            YapiElemani = new YapiElemani { Ad = "Cephe", Tip = YapiElemaniTipi.Duvar, Rw = 50 }
        };
        var missing = new Bolme
        {
            Ad = "Atanmamış cephe",
            Alan = 12,
            HasGeometry = true,
            KaynakMekan = room,
            YapiElemani = null
        };
        room.Bolmeler.Add(assigned);
        room.Bolmeler.Add(missing);

        var service = new DisGurultuServisi();
        var dntw = service.HesaplaDisCepheYalitimi(room, [assigned, missing]);
        if (double.IsFinite(dntw) && dntw > 0)
            throw new InvalidOperationException($"Kısmi malzemesiz cephe geçerli DnT,w üretmemeli; alınan {dntw:F1} dB.");

        var project = new BinaProje { DisGurultuDb = 65 };
        var spectral = service.HesaplaSpektralDisCephe(project, assigned);
        if (spectral != null)
            throw new InvalidOperationException("Kısmi malzemesiz oda cephesi spektral uygunluk sonucu üretmemeli.");
    }

    public static void MissingReceiverVolumeIsNotAValidImpactResult()
    {
        var source = Rect("Kaynak", 0, 0, 4, 4);
        var receiver = Rect("Alıcı", 0, 0, 4, 4);
        receiver.Alan = 0;
        receiver.Yukseklik = 0;
        var partition = new Bolme
        {
            Ad = "Döşeme",
            Alan = 16,
            KaynakMekan = source,
            AliciMekan = receiver,
            YapiElemani = new YapiElemani { Ad = "BA döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 72 }
        };

        var value = new DarbeSesiHesapServisi().HesaplaApparentLnTw(partition);
        if (double.IsFinite(value) && value > 0)
            throw new InvalidOperationException($"Hacimsiz alıcı için L'nT,w geçerli sayı olmamalı; alınan {value:F1} dB.");
        if (partition.ApparentLnTw > 0)
            throw new InvalidOperationException("Hacimsiz alıcıya geçerli ApparentLnTw yazılmamalı.");
    }

    public static void MissingAbsorptionRemainsDataMissing()
    {
        var project = new BinaProje
        {
            BinaIslevi = BinaIslevi.BuroIdari,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C
        };
        var room = Rect("Toplantı Odası", 0, 0, 5, 4);
        room.KullanimTipi = "Toplantı Odası";
        room.EmilimAlani = 0;

        var result = EK6ReverberasyonServisi.Degerlendir(project, room)
            ?? throw new InvalidOperationException("Toplantı odası EK-6 satırıyla eşleşmeli.");
        if (result.Durum != UyumDurumu.VeriEksik)
            throw new InvalidOperationException($"Emilim girilmemiş RT yalnız ön tahmin/veri eksik olmalı; alınan {result.Durum}.");
    }

    public static void MultiBedPatientRoomSelectsSpecificRows()
    {
        var ek4 = EK4IcGurultuServisi.LimitBul(
            BinaIslevi.SaglikVeYasliEvleri,
            AkustikPerformansSinifi.C,
            "Çok Yataklı Hasta Odası",
            "Çok Yataklı Hasta Odası")
            ?? throw new InvalidOperationException("Çok yataklı hasta odası EK-4 satırıyla eşleşmeli.");
        if (Math.Abs(ek4.LimitDb - 39) > 0.01)
            throw new InvalidOperationException($"Çok yataklı hasta odası EK-4 C limiti 39 dBA olmalı; alınan {ek4.LimitDb}.");

        var project = new BinaProje
        {
            BinaIslevi = BinaIslevi.SaglikVeYasliEvleri,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C
        };
        var room = Rect("Çok Yataklı Hasta Odası", 0, 0, 6, 5);
        room.KullanimTipi = "Çok Yataklı Hasta Odası";
        room.EmilimAlani = 20;
        var ek6 = EK6ReverberasyonServisi.Degerlendir(project, room)
            ?? throw new InvalidOperationException("Çok yataklı hasta odası EK-6 satırıyla eşleşmeli.");
        if (ek6.TMaxSn is not { } tMax || Math.Abs(tMax - 1.0) > 0.01)
            throw new InvalidOperationException($"Çok yataklı hasta odası EK-6 limiti 1,0 s olmalı; alınan {ek6.TMaxSn}.");
    }

    public static void ExcludedReceiverIsNotUsedForImpactPaths()
    {
        var floor = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 72 };
        var lowerRoom = Rect("Hesap dışı şaft", 0, 0, 4, 4);
        lowerRoom.Sinif = MekanSinifi.HesapDisi;
        var upperRoom = Rect("Üst oda", 0, 0, 4, 4);
        upperRoom.ZeminYapiElemani = floor;

        var lower = new Kat { KatNo = 1, KatYuksekligi = 3 };
        lower.Mekanlar.Add(lowerRoom);
        var upper = new Kat { KatNo = 2, KatYuksekligi = 3 };
        upper.Mekanlar.Add(upperRoom);

        var paths = new KatlarArasiDarbeYoluServisi().BulAltMekanlar([lower, upper], upper, upperRoom);
        if (paths.Count != 0)
            throw new InvalidOperationException("Hesap dışı alıcı mekan katlar arası darbe yoluna katılmamalı.");
    }

    public static void MissingPartitionsBlockBuildingClass()
    {
        var source = Rect("Kaynak", 0, 0, 3, 3);
        source.KaynakDerecesi = "OG";
        var receiver = Rect("Alıcı", 3, 0, 6, 3);
        receiver.AliciSinifi = "II";
        source.Bolmeler.Add(new Bolme
        {
            Ad = "Tam duvar",
            Alan = 9,
            KaynakMekan = source,
            AliciMekan = receiver,
            YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 },
            Dntw = 55
        });
        source.Bolmeler.Add(new Bolme
        {
            Ad = "Eksik duvar",
            Alan = 9,
            KaynakMekan = source,
            AliciMekan = receiver,
            YapiElemani = null
        });
        var floor = new Kat { KatNo = 1 };
        floor.Mekanlar.Add(source);
        floor.Mekanlar.Add(receiver);
        var project = new BinaProje { BinaSinifi = BinaSinifi.S1 };
        project.Katlar.Add(floor);

        var result = new SinifAtamaServisi().BinaSinifiDegerlendir(project);
        var wallResult = result.Ozellikler.Single(x => x.Ozellik == SinifAtamaServisi.AkustikOzellik.BolmeHavaSesi);
        if (!wallResult.Sonuc.Belirlenemedi || wallResult.BelirlenemeyenEleman == 0)
            throw new InvalidOperationException("Malzemesiz zorunlu bölme varken hava sesi sınıfı belirlenmemeli.");
        if (!result.Genel.Belirlenemedi)
            throw new InvalidOperationException("Eksik zorunlu bölme varken genel bina sınıfı verilmemeli.");
    }

    public static void TopologyRefreshPreservesIdentitiesAndResults()
    {
        var room = Rect("Oda", 0, 0, 4, 4);
        var service = new OdaTespitServisi();
        service.KomsuluklariTespitEt([room]);
        if (room.Bolmeler.Count != 4)
            throw new InvalidOperationException("Test kurulumu dört dış duvar üretmeli.");

        var material = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 50 };
        foreach (var wall in room.Bolmeler)
        {
            wall.YapiElemani = material;
            wall.ApparentRw = 47;
            wall.Dntw = 44;
        }
        var ids = room.Bolmeler.Select(x => x.Id).ToHashSet();

        service.KomsuluklariTespitEt([room]);
        if (!ids.SetEquals(room.Bolmeler.Select(x => x.Id)))
            throw new InvalidOperationException("Değişmeyen topolojide bölme kimlikleri yenilenmemeli.");
        if (room.Bolmeler.Any(x => x.ApparentRw != 47 || x.Dntw != 44 || !ReferenceEquals(x.YapiElemani, material)))
            throw new InvalidOperationException("Değişmeyen topolojide hesap sonuçları ve atamalar korunmalı.");
    }

    public static void UndoClonePreservesAcousticState()
    {
        var room = Rect("Oda", 0, 0, 4, 4);
        room.TesisatSurekliDb = 31;
        room.TesisatKesikliDb = 37;
        var wall = new Bolme
        {
            Ad = "Duvar",
            Alan = 12,
            KaynakMekan = room,
            ApparentRw = 51,
            ApparentLnw = 62,
            ApparentLnTw = 58,
            Dntw = 49,
            Menfezler = [new Menfez { Ad = "Menfez", DneDb = 44 }],
            Junctions = [new Junction { KijOverrideDb = 12, Uzunluk = 3 }]
        };
        room.Bolmeler.Add(wall);

        var clone = MekanKlonServisi.Klonla(room);
        var clonedWall = clone.Bolmeler.Single();
        if (clone.TesisatSurekliDb != 31 || clone.TesisatKesikliDb != 37)
            throw new InvalidOperationException("Undo klonu tesisat gürültüsü girdilerini korumalı.");
        if (clonedWall.ApparentLnTw != 58 || clonedWall.Menfezler.Count != 1 || clonedWall.Junctions.Count != 1)
            throw new InvalidOperationException("Undo klonu darbe sonucu, menfez ve junction verisini korumalı.");
        if (ReferenceEquals(clonedWall.Menfezler[0], wall.Menfezler[0]) || ReferenceEquals(clonedWall.Junctions[0], wall.Junctions[0]))
            throw new InvalidOperationException("Undo klonu değiştirilebilir menfez/junction nesnelerini paylaşmamalı.");
    }

    public static void GeneratedSpectrumIsMarkedAsModel()
    {
        var material = new YapiElemani
        {
            Ad = "Model duvar",
            Tip = YapiElemaniTipi.Duvar,
            Rw = 50,
            SpektrumKaynagi = SpektrumVeriKaynagi.Belirsiz,
            HasRealOktavData = false
        };
        YapiElemaniKutuphanesi.TamamlaOktavVerisi(material);
        if (material.RThirdOctave.Count < 16)
            throw new InvalidOperationException("Model spektrumu tam 16 bant üretilmeli.");
        if (material.SpektrumKaynagi != SpektrumVeriKaynagi.Model || material.HasRealOktavData)
            throw new InvalidOperationException("Otomatik üretilen spektrum ölçüm değil Model kökenli olmalı.");
    }

    public static void ForeignJsonDocumentIsRejected()
    {
        const string foreign = """
            { "ModuleId": "M50", "SchemaVersion": 1, "ProjeAdi": "Mimari", "Katlar": [] }
            """;
        if (BinaProjeMigrator.DosyaKimliginiDogrula(foreign, "mimari.e4bina.json", out _))
            throw new InvalidOperationException("M50 belgesi M08 projesi olarak kabul edilmemeli.");

        const string anonymousJson = """
            { "ProjeAdi": "Anonim", "Katlar": [] }
            """;
        if (BinaProjeMigrator.DosyaKimliginiDogrula(anonymousJson, "anonim.json", out _))
            throw new InvalidOperationException("Kimliksiz genel JSON dosyası M08 projesi olarak kabul edilmemeli.");
        if (!BinaProjeMigrator.DosyaKimliginiDogrula(anonymousJson, "eski.e4p08", out var legacyError))
            throw new InvalidOperationException($"Ayırt edici alanları olan eski .e4p08 belgesi taşınabilmeli: {legacyError}");

        var future = $$"""
            { "ModuleId": "M08", "SchemaVersion": {{BinaProje.CurrentSchemaVersion + 1}}, "ProjeAdi": "Gelecek", "Katlar": [] }
            """;
        if (BinaProjeMigrator.DosyaKimliginiDogrula(future, "gelecek.e4p08", out _))
            throw new InvalidOperationException("Daha yeni şemalı M08 belgesi sessizce açılmamalı.");
    }

    public static void TurkishDecimalSpectrumRoundTrips()
    {
        var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
        var oldUiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            System.Globalization.CultureInfo.CurrentCulture = culture;
            System.Globalization.CultureInfo.CurrentUICulture = culture;

            var expected = Enumerable.Range(0, DisGurultuServisi.ExtendedThirdOctaveFreqler.Length)
                .Select(i => 50.5 + i)
                .ToArray();
            var text = string.Join("; ", expected.Select(x => x.ToString("0.0", culture)));
            var vm = new Kst.Acoustics.App.BuildingAcoustics.ViewModels.BinaProjesiViewModel();
            vm.DisGurultuSpektrumMetni = text;

            if (vm.Proje.DisGurultuThirdOctave.Count != expected.Length)
                throw new InvalidOperationException("Türkçe ondalıklı spektrum tam 19 bant olarak ayrıştırılmalı.");
            for (var i = 0; i < expected.Length; i++)
                if (Math.Abs(vm.Proje.DisGurultuThirdOctave[i] - expected[i]) > 1e-9)
                    throw new InvalidOperationException($"Spektrum bandı {i} bozuldu: {expected[i]} yerine {vm.Proje.DisGurultuThirdOctave[i]}.");

            var rendered = vm.DisGurultuSpektrumMetni;
            if (!rendered.Contains("50,5", StringComparison.Ordinal))
                throw new InvalidOperationException("Spektrum metni etkin Türkçe kültürle kayıpsız gösterilmeli.");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = oldCulture;
            System.Globalization.CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    public static void SyntheticReportRowsHaveStableFilterIds()
    {
        var project = new BinaProje
        {
            BinaIslevi = BinaIslevi.BuroIdari,
            AkustikPerformansSinifi = AkustikPerformansSinifi.C
        };
        var room = Rect("Toplantı Odası", 0, 0, 5, 4);
        room.KullanimTipi = "Toplantı Odası";
        room.EmilimAlani = 18;
        room.TesisatSurekliDb = 32;
        var floor = new Kat { KatNo = 1 };
        floor.Mekanlar.Add(room);
        project.Katlar.Add(floor);

        var service = new BinaAkustikRaporServisi();
        var rows = service.TabloVerisiOlustur(project);
        var hvac = rows.Single(x => x.SonucTuru == AkustikRaporSonucTuru.TesisatSurekli);
        var rt = rows.Single(x => x.SonucTuru == AkustikRaporSonucTuru.Reverberasyon);
        if (hvac.SonucId != RaporSonucIdleri.Tesisat(room.Id, kesikli: false))
            throw new InvalidOperationException("Sürekli tesisat sonucu kararlı mekan-türü kimliği kullanmalı.");
        if (rt.SonucId != RaporSonucIdleri.Reverberasyon(room.Id))
            throw new InvalidOperationException("RT sonucu kararlı mekan-türü kimliği kullanmalı.");

        var excluded = new HashSet<Guid> { hvac.SonucId, rt.SonucId };
        var filtered = service.TabloVerisiOlustur(project, excluded);
        if (filtered.Any(x => x.SonucTuru is AkustikRaporSonucTuru.TesisatSurekli or AkustikRaporSonucTuru.Reverberasyon))
            throw new InvalidOperationException("Tesisat ve RT sonuçları rapor filtresinden gerçekten çıkarılmalı.");
    }

    public static void MaterialLibraryUsesPerUserOverridesAndPreservesProvenance()
    {
        string root = Path.Combine(Path.GetTempPath(), $"eye4pro-m08-library-{Guid.NewGuid():N}");
        string seedPath = Path.Combine(root, "seed", "buildingelements.json");
        string userPath = Path.Combine(root, "user", "buildingelements.user.json");
        Directory.CreateDirectory(Path.GetDirectoryName(seedPath)!);

        try
        {
            var seed = new List<YapiElemani>
            {
                new() { Ad = "Test tohum duvar", Tip = YapiElemaniTipi.Duvar, Rw = 42 },
                new() { Ad = "Silinecek test elemanı", Tip = YapiElemaniTipi.Duvar, Rw = 35 }
            };
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            string seedJson = System.Text.Json.JsonSerializer.Serialize(seed, jsonOptions);
            File.WriteAllText(seedPath, seedJson);

            var (loaded, _) = YapiElemaniKutuphanesi.YukleVeDogrula(seedPath, userPath);
            var wall = loaded.Single(e => e.Ad == "Test tohum duvar");
            wall.Rw = 57;
            wall.SpektrumKaynagi = SpektrumVeriKaynagi.Uretici;
            wall.R63 = 30; wall.R125 = 35; wall.R250 = 42; wall.R500 = 50;
            wall.R1k = 57; wall.R2k = 62; wall.R4k = 66; wall.R8k = 68;
            loaded.RemoveAll(e => e.Ad == "Silinecek test elemanı");
            loaded.Add(new YapiElemani
            {
                Ad = "Kullanıcı test döşemesi",
                Tip = YapiElemaniTipi.Doseme,
                Rw = 55,
                Lnw = 70,
                R63 = 35, R125 = 40, R250 = 47, R500 = 53,
                R1k = 57, R2k = 61, R4k = 64, R8k = 66,
                L63 = 82, L125 = 79, L250 = 75, L500 = 71,
                L1k = 68, L2k = 65, L4k = 63,
                SpektrumKaynagi = SpektrumVeriKaynagi.Uretici,
                DarbeSpektrumKaynagi = SpektrumVeriKaynagi.Olcum
            });

            YapiElemaniKutuphanesi.Kaydet(loaded, seedPath, userPath);

            if (File.ReadAllText(seedPath) != seedJson)
                throw new InvalidOperationException("Kütüphane kaydı paket/tohum katalog dosyasını değiştirmemeli.");
            if (!File.Exists(userPath))
                throw new InvalidOperationException("Kullanıcı fark kataloğu uygulama-verisi hedefinde oluşturulmalı.");

            var (reloaded, _) = YapiElemaniKutuphanesi.YukleVeDogrula(seedPath, userPath);
            var reloadedWall = reloaded.Single(e => e.Ad == "Test tohum duvar");
            if (Math.Abs(reloadedWall.Rw - 57) > 0.01 ||
                reloadedWall.SpektrumKaynagi != SpektrumVeriKaynagi.Uretici)
                throw new InvalidOperationException("Tohum eleman üzerindeki kullanıcı değişikliği birleştirmede korunmalı.");
            if (reloaded.Any(e => e.Ad == "Silinecek test elemanı"))
                throw new InvalidOperationException("Kullanıcının sildiği tohum eleman tombstone ile geri gelmemeli.");

            var floor = reloaded.Single(e => e.Ad == "Kullanıcı test döşemesi");
            if (floor.SpektrumKaynagi != SpektrumVeriKaynagi.Uretici ||
                floor.DarbeSpektrumKaynagi != SpektrumVeriKaynagi.Olcum)
                throw new InvalidOperationException("Hava ve darbe spektrumu kökenleri ayrı ayrı saklanmalı.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static void MaterialLibrarySaveFailureIsReportedToCaller()
    {
        string root = Path.Combine(Path.GetTempPath(), $"eye4pro-m08-library-failure-{Guid.NewGuid():N}");
        string seedPath = Path.Combine(root, "seed.json");
        string directoryAsFile = Path.Combine(root, "target-is-a-directory");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(directoryAsFile);
        File.WriteAllText(seedPath, "[]");

        try
        {
            bool threw = false;
            try
            {
                YapiElemaniKutuphanesi.Kaydet(
                    [new YapiElemani { Ad = "Geçerli duvar", Tip = YapiElemaniTipi.Duvar, Rw = 50 }],
                    seedPath,
                    directoryAsFile);
            }
            catch (IOException)
            {
                threw = true;
            }

            if (!threw)
                throw new InvalidOperationException("Yazma hatası yutulmamalı; çağırana IOException ulaşmalı.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static void LegacyMaterialLibraryArrayMigratesDeterministically()
    {
        string root = Path.Combine(Path.GetTempPath(), $"eye4pro-m08-library-legacy-{Guid.NewGuid():N}");
        string seedPath = Path.Combine(root, "seed.json");
        string userPath = Path.Combine(root, "buildingelements.user.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(seedPath,
            """[{"Ad":"Tohum","Tip":"Duvar","Rw":45}]""");
        File.WriteAllText(userPath,
            """[{"Ad":"Eski kullanıcı elemanı","Tip":"Duvar","Rw":61,"SpektrumKaynagi":"Uretici"}]""");

        try
        {
            var (loaded, _) = YapiElemaniKutuphanesi.YukleVeDogrula(seedPath, userPath);
            if (loaded.Count != 1 || loaded[0].Ad != "Eski kullanıcı elemanı")
                throw new InvalidOperationException("Eski tam-katalog dizisi aynı içerikle yüklenmeli.");

            using var migrated = System.Text.Json.JsonDocument.Parse(File.ReadAllText(userPath));
            if (migrated.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !migrated.RootElement.TryGetProperty("SchemaVersion", out _))
                throw new InvalidOperationException("Eski dizi katalog güncel sürümlü fark şemasına taşınmalı.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static void LegacyBaseDirectoryCatalogDifferencesMigrateToUserData()
    {
        string root = Path.Combine(Path.GetTempPath(), $"eye4pro-m08-library-base-migration-{Guid.NewGuid():N}");
        string seedPath = Path.Combine(root, "app", "buildingelements.json");
        string userPath = Path.Combine(root, "user", "buildingelements.user.json");
        string absentLegacyUserPath = Path.Combine(root, "user", "buildingelements.json");
        Directory.CreateDirectory(Path.GetDirectoryName(seedPath)!);
        File.WriteAllText(seedPath,
            """[{"Ad":"Eski uygulama özel duvarı","Tip":"Duvar","Rw":61}]""");

        try
        {
            var (firstLoad, _) = YapiElemaniKutuphanesi.YukleVeDogrula(
                seedPath, userPath, absentLegacyUserPath);
            if (firstLoad.Single(e => e.Ad == "Eski uygulama özel duvarı").Rw != 61)
                throw new InvalidOperationException("Eski uygulama-klasörü katalog kaydı ilk yüklemede korunmalı.");
            if (!File.Exists(userPath))
                throw new InvalidOperationException("Eski uygulama-klasörü farkları per-user override dosyasına taşınmalı.");

            // Tohum daha sonra değişse bile taşınmış eski kullanıcı değeri kaybolmamalı.
            File.WriteAllText(seedPath,
                """[{"Ad":"Eski uygulama özel duvarı","Tip":"Duvar","Rw":25}]""");
            var (secondLoad, _) = YapiElemaniKutuphanesi.YukleVeDogrula(seedPath, userPath);
            if (secondLoad.Single(e => e.Ad == "Eski uygulama özel duvarı").Rw != 61)
                throw new InvalidOperationException("Taşınmış eski katalog farkı yeni tohum değerinin altında kaybolmamalı.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    public static void BuildingFunctionMapsToVisibleRegulationClass()
    {
        var vm = new Kst.Acoustics.App.BuildingAcoustics.ViewModels.BinaProjesiViewModel();
        vm.SeciliBinaIslevi = BinaIsleviHelper.BilgiAl(BinaIslevi.BuroIdari);
        if (vm.Proje.BinaSinifi != BinaSinifi.S3 || vm.SeciliBinaSinifi.Sinif != BinaSinifi.S3)
            throw new InvalidOperationException("Büro/idari işlevi görünür S3 yönetmelik sınıfına eşlenmeli.");

        vm.SeciliBinaIslevi = BinaIsleviHelper.BilgiAl(BinaIslevi.EgitimTesisleri);
        if (vm.Proje.BinaSinifi != BinaSinifi.S4 || vm.SeciliBinaSinifi.Sinif != BinaSinifi.S4)
            throw new InvalidOperationException("Eğitim işlevi görünür S4 yönetmelik sınıfına eşlenmeli.");

        vm.SeciliBinaSinifi = BinaSinifiHelper.BilgiAl(BinaSinifi.S5);
        if (vm.Proje.BinaSinifi != BinaSinifi.S5)
            throw new InvalidOperationException("Kullanıcının görünür S1-S5 override seçimi proje modeline yazılmalı.");
    }

    private static Mekan Rect(string name, double x1, double y1, double x2, double y2) => new()
    {
        Ad = name,
        KullanimTipi = name,
        Alan = Math.Abs((x2 - x1) * (y2 - y1)),
        Yukseklik = 3,
        Poligon =
        [
            new Point2D(x1, y1),
            new Point2D(x2, y1),
            new Point2D(x2, y2),
            new Point2D(x1, y2)
        ]
    };
}
