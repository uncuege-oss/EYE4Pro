using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Line = Avalonia.Controls.Shapes.Line;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Common.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class BasitProjeCizWindow : Window
{
    private readonly BasitProjeCizViewModel _vm = new();
    private readonly BasitProjeCizDxfServisi _dxf = new();
    // Çoklu eleman drag: diğer seçili elemanların birinciye göre ofseti
    private readonly System.Collections.Generic.Dictionary<string, (double dX, double dY)>
        _dragOtherOffsets = new();
    // Sürüklemede undo snapshot yalnız ilk gerçek harekette bir kez alınır
    private bool _elemanDragSnapshotAlindi;

    /// M12 (kuvvetli akım) kabuğu — varsayılan kurucu.
    public BasitProjeCizWindow() : this(BpcDisiplin.KuvvetliAkim) { }

    /// Ortak motorun modüle sabitlenmiş kabuğu: M12 = KuvvetliAkim (.e4p12),
    /// M16 = ZayifAkim (.e4p16). Disiplin çalışma zamanında değiştirilemez.
    public BasitProjeCizWindow(BpcDisiplin modulModu)
    {
        InitializeComponent();
        _vm.ModulModuAyarla(modulModu);
        DataContext = _vm;
        Title = _vm.PencereBasligi;
        // IsDirty değişince başlıktaki `*` güncellensin (maincodex §4.2).
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BasitProjeCizViewModel.PencereBasligi))
                Title = _vm.PencereBasligi;
        };
        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
        SizeChanged += (_, _) => ElemanGridYuksekliginiGuncelle();
    }

    // ── Kaydedilmemiş değişiklik koruması (maincodex §4.2) ───────────────────

    private bool _kapanisOnaylandi;

    /// Pencere X ile kapatılırken kaydedilmemiş değişiklik varsa sorar.
    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_kapanisOnaylandi || !_vm.IsDirty) return;

        e.Cancel = true;
        if (!await KaydedilmemisDegisiklikOnayi("Pencereyi kapat")) return;

        _kapanisOnaylandi = true;
        Close();
    }

    /// IsDirty ise Kaydet / Kaydetmeden Devam Et / İptal sorar. Devam edilebilirse
    /// (kaydedildi ya da kullanıcı kaydetmeden devamı seçti) true döner.
    private async Task<bool> KaydedilmemisDegisiklikOnayi(string eylemAdi)
    {
        if (!_vm.IsDirty) return true;

        var dialog = new Kst.Acoustics.App.BuildingAcoustics.Views.OnayDiyaloguWindow(
            "Kaydedilmemiş Değişiklikler",
            $"Projede kaydedilmemiş değişiklikler var. '{eylemAdi}' devam ederse bu değişiklikler kaybolur.\n\n" +
            "Önce kaydetmek ister misiniz?",
            tamamYazi: "Kaydet ve Devam Et",
            tamamSonuc: Kst.Acoustics.App.BuildingAcoustics.Views.OnaySonucu.Tamam,
            iptalYazi: "İptal",
            ekstraYazi: "Kaydetmeden Devam Et",
            ekstraSonuc: Kst.Acoustics.App.BuildingAcoustics.Views.OnaySonucu.KaydetmedenDevamEt);
        var sonuc = await dialog.ShowDialog<Kst.Acoustics.App.BuildingAcoustics.Views.OnaySonucu>(this);
        switch (sonuc)
        {
            case Kst.Acoustics.App.BuildingAcoustics.Views.OnaySonucu.Tamam:
                if (string.IsNullOrWhiteSpace(_vm.ProjeDosyaYolu))
                    await FarkliKaydet();
                else
                    _vm.ProjeyiKaydet(_vm.ProjeDosyaYolu);
                return !_vm.IsDirty; // kaydetme iptal/hata olduysa devam etme
            case Kst.Acoustics.App.BuildingAcoustics.Views.OnaySonucu.KaydetmedenDevamEt:
                return true;
            default:
                return false;
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        AktifPaftaSembolBarGuncelle();
        AnahtarSembolBarGuncelle();
        PanoSembolBarGuncelle();
        if (SagPanelBorder is not null)
            SagPanelBorder.SizeChanged += (_, _) => ElemanGridYuksekliginiGuncelle();
        ElemanGridYuksekliginiGuncelle();

        var preview = this.FindControl<GeometryPreviewControl>("GeometryPreview");
        if (preview is not null)
        {
            // 3.3 sökümü: geometri düzenleme (vertex/edge sürükleme, köşe ekle/sil,
            // oda çizimi, açıklık bağlam menüsü) M50'ye taşındı — M12'de geometriyi
            // değiştiren hiçbir canvas etkileşimi kalmaz.

            preview.IsElemanAtPoint = (worldX, worldY) =>
            {
                var kat = _vm.SeciliKat;
                if (kat is null || _vm.ElemanEkleModu) return false;
                double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
                return kat.Elemanlar.Any(e =>
                {
                    double dx = e.X + ox - worldX, dy = e.Y + oy - worldY;
                    return dx * dx + dy * dy < 0.09; // 0.3m yarıçap
                });
            };

            // Kablo modunda imleç altındaki hedef elemanı ön-vurgula (turkuaz halka).
            preview.OnHoverWorldPoint = (worldX, worldY) => _vm.KabloModuHover(worldX, worldY);

            // Sağ tık = aktif hattı bitir (CAD alışkanlığı). Kablo modu dışında dokunma.
            preview.OnViewportRightClicked = (worldX, worldY, _) =>
            {
                if (_vm.KabloModuAktif && _vm.KabloAktifHatVarMi)
                    _vm.KabloModuHattiBitir();
            };

            // Bant seçim (Shift+sol sürükleme): hat açıkken armatür/prizleri topluca ata.
            preview.BandSecimAktifMi = () => _vm.KabloBandSecimUygunMu;
            preview.OnBandSelected = (x1, y1, x2, y2) => _vm.KabloModuBandSecim(x1, y1, x2, y2);

            preview.OnElemanDragStarted = (worldX, worldY, multiSelect) =>
            {
                var kat = _vm.SeciliKat;
                if (kat is null) return;
                double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
                var el = kat.Elemanlar
                    .Where(e => { double dx = e.X + ox - worldX, dy = e.Y + oy - worldY; return dx * dx + dy * dy < 0.09; })
                    .OrderBy(e => { double dx = e.X + ox - worldX, dy = e.Y + oy - worldY; return dx * dx + dy * dy; })
                    .FirstOrDefault();
                if (el is null) return;

                if (_vm.KilitliyseDur("eleman taşımak")) return;

                if (multiSelect)
                {
                    // Ctrl/Cmd: listeye ekle/çıkar (toggle)
                    if (!_vm.SeciliElemanlar.Remove(el))
                        _vm.SeciliElemanlar.Add(el);
                }
                else if (!_vm.SeciliElemanlar.Contains(el))
                {
                    // Normal tıklama, seçili değil: tek seçime düş
                    _vm.SeciliElemanlar.Clear();
                    _vm.SeciliElemanlar.Add(el);
                }
                // Normal tıklama ama el zaten çoklu seçimde: grubu koru (birlikte taşımak için).
                // Tek seçime ayıklamak isteniyorsa salt tıklama (taşımasız) bırakınca aşağıda düşürülür.

                // Eleman seçilince oda seçimini temizle (görsel karışıklığı önle)
                if (_vm.SeciliOda is not null || _vm.SeciliOdalar.Count > 0)
                {
                    _vm.SeciliOdalar.Clear();
                    _vm.SeciliOda = null;
                    _vm.GuncelleOdaVurgu();
                }

                _vm.SeciliEleman = el;
                _vm.GuncelleElemanVurgu();
                _vm.ElemanTasimaModu = true;
                _elemanDragSnapshotAlindi = false;

                // Diğer seçili elemanların birinciye göre ofsetini sakla
                _dragOtherOffsets.Clear();
                foreach (var sel in _vm.SeciliElemanlar)
                {
                    if (sel == el) continue;
                    _dragOtherOffsets[sel.Id] = (sel.X - el.X, sel.Y - el.Y);
                }
            };

            preview.OnElemanDragMoved = (worldX, worldY) =>
            {
                var el  = _vm.SeciliEleman;
                var kat = _vm.SeciliKat;
                if (el is null || kat is null || !_vm.ElemanTasimaModu) return;
                if (_vm.KilitliyseDur("eleman taşımak")) return;
                // Undo snapshot'ı taşıma öncesi durumu yakalasın diye ilk harekette al
                if (!_elemanDragSnapshotAlindi)
                {
                    _vm.ElemanTasimaSnapshotAl();
                    _elemanDragSnapshotAlindi = true;
                }
                el.X = worldX - kat.OriginOffset.X;
                el.Y = worldY - kat.OriginOffset.Y;
                // Diğer seçili elemanları aynı deltayla taşı
                foreach (var sel in _vm.SeciliElemanlar)
                {
                    if (sel == el) continue;
                    if (_dragOtherOffsets.TryGetValue(sel.Id, out var rel))
                    { sel.X = el.X + rel.dX; sel.Y = el.Y + rel.dY; }
                }
            };

            preview.OnElemanDragCommitted = (worldX, worldY) =>
            {
                var el  = _vm.SeciliEleman;
                var kat = _vm.SeciliKat;
                if (el is null || kat is null || !_vm.ElemanTasimaModu) return;
                if (_vm.KilitliyseDur("eleman taşımak"))
                {
                    _vm.ElemanTasimaModu = false;
                    _elemanDragSnapshotAlindi = false;
                    _dragOtherOffsets.Clear();
                    return;
                }
                _vm.ManuelCokElemanKonumGuncelle(el,
                    worldX - kat.OriginOffset.X,
                    worldY - kat.OriginOffset.Y);
                _vm.ElemanTasimaModu = false;
                _elemanDragSnapshotAlindi = false;
                _dragOtherOffsets.Clear();
            };

            // Taşımasız (salt) tıklama: çoklu seçimden tek elemana ayıkla
            preview.OnElemanClickNoDrag = multiSelect =>
            {
                if (!multiSelect && _vm.SeciliElemanlar.Count > 1 && _vm.SeciliEleman is { } el)
                {
                    _vm.SeciliElemanlar.Clear();
                    _vm.SeciliElemanlar.Add(el);
                    _vm.GuncelleElemanVurgu();
                }
                _vm.ElemanTasimaModu = false;       // taşıma olmadı
                _elemanDragSnapshotAlindi = false;
                _dragOtherOffsets.Clear();
            };

            preview.OnViewportClicked = (worldX, worldY, multiSelect) =>
            {
                var seciliKat = _vm.SeciliKat;
                if (seciliKat == null) return;

                // Sembol yerleştirme modu
                if (_vm.ElemanEkleModu)
                {
                    _vm.EklenecekSembolKanvasaEkle(
                        worldX - seciliKat.OriginOffset.X,
                        worldY - seciliKat.OriginOffset.Y);
                    return;
                }

                if (_vm.ManuelAciklikModu)
                {
                    _vm.ManuelAciklikTiklama(worldX, worldY);
                    return;
                }

                if (_vm.KabloTavasiCizimAktif)
                {
                    _vm.KabloTavasiNoktaEkle(worldX, worldY);
                    return;
                }

                if (_vm.KabloTavasiTaliHatModuAktif)
                {
                    _vm.KabloTavasiTaliHatNoktaEkle(worldX, worldY);
                    return;
                }

                if (_vm.TopraklamaAlanSecimAktif)
                {
                    _vm.TopraklamaNoktaEkle(worldX, worldY);
                    return;
                }

                if (_vm.TopraklamaKazikEkleModuAktif)
                {
                    _vm.TopraklamaKazikEkle(worldX, worldY);
                    return;
                }

                // Kablo modu öncelikli
                if (_vm.KabloModuAktif)
                {
                    _vm.KabloModuTiklama(worldX, worldY);
                    return;
                }

                // Manuel eleman ekleme artık üst sembol çubuğundan başlatılır.
                BpcElemanTipi? manuelTip = null;
                if (_vm.KabloTavasiAktif)
                {
                    manuelTip = null;
                }
                else if (_vm.TopraklamaAktif)
                {
                    manuelTip = null;
                }
                else if (_vm.ZayifAkimAktif)
                {
                    manuelTip = _vm.SeciliZayifElemanSecenek?.Tip;
                }

                if (manuelTip is { } yerlesTip)
                {
                    double lx = worldX - seciliKat.OriginOffset.X;
                    double ly = worldY - seciliKat.OriginOffset.Y;
                    var p = new Point2D(lx, ly);
                    var tiklanenOda = seciliKat.Odalar.FirstOrDefault(
                        o => PoligonYardimci.IsPointInside(p, o.Poligon));

                    if (tiklanenOda != null && tiklanenOda != _vm.SeciliOda)
                    {
                        // İlk tıklama: sadece odayı seç, eleman ekleme
                        _vm.SeciliOda = tiklanenOda;
                    }
                    else
                    {
                        // Aynı odaya ikinci tıklama (veya oda dışı): eleman ekle
                        _vm.ManuelElemanEkle(yerlesTip, p);
                    }
                    return;
                }

                // Kablo seçimi (kablo mod kapalıyken kabloya tıklama)
                if (_vm.KabloTavasiAktif && _vm.KabloTavasiTiklamaDetect(worldX, worldY))
                    return;
                _vm.KabloTiklamaDetect(worldX, worldY);

                // Açıklık seçimi: kabloya/elemana değilse en yakın açıklığı seç
                // (salt görsel vurgu — açıklığa müdahale M50'ye taşındı)
                if (_vm.TrySelectAciklik(worldX, worldY))
                    return;

                // Eleman hit-test seçimi (tıklama modu — drag değil)
                {
                    double ox = seciliKat.OriginOffset.X, oy = seciliKat.OriginOffset.Y;
                    var hitEl = seciliKat.Elemanlar
                        .Where(e => { double dx = e.X + ox - worldX, dy = e.Y + oy - worldY; return dx * dx + dy * dy < 0.09; })
                        .OrderBy(e => { double dx = e.X + ox - worldX, dy = e.Y + oy - worldY; return dx * dx + dy * dy; })
                        .FirstOrDefault();
                    if (hitEl is not null)
                    {
                        if (multiSelect)
                        {
                            if (!_vm.SeciliElemanlar.Remove(hitEl))
                                _vm.SeciliElemanlar.Add(hitEl);
                        }
                        else
                        {
                            _vm.SeciliElemanlar.Clear();
                            _vm.SeciliElemanlar.Add(hitEl);
                        }
                        _vm.SeciliEleman = hitEl;
                        _vm.GuncelleElemanVurgu();
                        return;
                    }
                    else if (!multiSelect)
                    {
                        _vm.SeciliElemanlar.Clear();
                        _vm.GuncelleElemanVurgu();
                    }
                }

                // Oda seçimi (Cmd/Ctrl ile çoklu)
                _vm.PickRoomAt(worldX, worldY, multiSelect);
            };

            preview.OnViewportDoubleClicked = (worldX, worldY) =>
            {
                var kat = _vm.SeciliKat;
                if (kat is not null)
                {
                    var hitEl = kat.Elemanlar
                        .Where(e => e.PaftaTipi == _vm.AktifPafta || _vm.AktifPafta == BpcPaftaTipi.Aydinlatma)
                        .OrderBy(e =>
                        {
                            var dx = e.X + kat.OriginOffset.X - worldX;
                            var dy = e.Y + kat.OriginOffset.Y - worldY;
                            return dx * dx + dy * dy;
                        })
                        .FirstOrDefault(e =>
                        {
                            var dx = e.X + kat.OriginOffset.X - worldX;
                            var dy = e.Y + kat.OriginOffset.Y - worldY;
                            return dx * dx + dy * dy < 0.30 * 0.30;
                        });

                    if (hitEl is not null)
                    {
                        _vm.SeciliElemanlar.Clear();
                        _vm.SeciliElemanlar.Add(hitEl);
                        _vm.SeciliEleman = hitEl;
                        _vm.GuncelleElemanVurgu();
                        _vm.SeciliElemandanOtomatikYerlesim();
                    }
                }
            };

            _vm.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    // 3.3 sökümü: EditablePolygon iletilmez — köşe tutamaçları
                    // M12'de görünmez (vertex düzenleme M50'ye taşındı).
                    case nameof(_vm.KatDxfPaneliAcik):
                    case nameof(_vm.KatlarPaneliAcik):
                    case nameof(_vm.OdalarPaneliAcik):
                    case nameof(_vm.AktarimPaneliAcik):
                    case nameof(_vm.ElemanlarPaneliAcik):
                    case nameof(_vm.KabloModuAktif):
                        GuncelleYanPanelGorunurlugu();
                        ElemanGridYuksekliginiGuncelle();
                        break;
                    case nameof(_vm.SeciliEleman):
                        ElemanGridYuksekliginiGuncelle();
                        break;
                    case nameof(_vm.AktifPafta):
                        AktifPaftaSembolBarGuncelle();
                        AnahtarSembolBarGuncelle();
                        PanoSembolBarGuncelle();
                        break;
                }
            };
        }

        GuncelleYanPanelGorunurlugu();

        KeyDown += (_, e) =>
        {
            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.Key == Key.Z)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _vm.IleriAlGenelCommand.Execute(null);
                else
                    _vm.GeriAlGenelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.Key == Key.Y)
            {
                _vm.IleriAlGenelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.Key == Key.O)
            {
                Ac_OnClick(this, null!);
                e.Handled = true;
                return;
            }

            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.Key == Key.S)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _ = FarkliKaydet();
                else
                    Kaydet_OnClick(this, null!);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_vm.ElemanEkleModu)
                {
                    _vm.ElemanEkleModIptalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                // Kablo modu: ilk Esc yarım hattı iptal eder, ikinci Esc modu kapatır.
                if (_vm.KabloModuAktif && !_vm.KabloTavasiCizimAktif)
                {
                    if (_vm.KabloAktifHatVarMi) _vm.KabloModuHattiIptal();
                    else _vm.KabloModuKapat();
                    e.Handled = true;
                    return;
                }
            }

            // Kablo modu: Enter aktif hattı bitirir (rota hesaplanır, sıradakine geçilir).
            if (e.Key == Key.Return && _vm.KabloModuAktif && !_vm.KabloTavasiCizimAktif &&
                e.Source is not TextBox and not NumericUpDown)
            {
                if (_vm.KabloAktifHatVarMi)
                {
                    _vm.KabloModuHattiBitir();
                    e.Handled = true;
                    return;
                }
            }

            if ((e.Key == Key.Delete || e.Key == Key.Back) &&
                e.Source is not TextBox and not NumericUpDown)
            {
                // Eleman seçimi varsa önce elemanlar silinir (çoklu veya tekli)
                if (_vm.SeciliElemanlar.Count > 0)
                {
                    _vm.SeciliElemanlariSil();
                    e.Handled = true;
                }
                else if (_vm.SeciliDevreId != null)
                {
                    _vm.KabloSeciliDevreyiSilCommand.Execute(null);
                    e.Handled = true;
                }
                else if (_vm.SeciliKabloTavasi != null)
                {
                    _vm.SeciliKabloTavasiSilCommand.Execute(null);
                    e.Handled = true;
                }
                // 3.3 sökümü: açıklık/oda silme Delete kısayolu kaldırıldı —
                // geometri yalnız M50'de düzenlenir.
                return;
            }

            if (_vm.KabloTavasiCizimAktif)
            {
                if (e.Key == Key.F8)
                {
                    _vm.KabloTavasiDuzCizimDegistirCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _vm.KabloTavasiCizIptalCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Return && _vm.KabloTavasiCizBitirCommand.CanExecute(null))
                {
                    _vm.KabloTavasiCizBitirCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Escape && _vm.SeciliAciklik != null)
            {
                _vm.SeciliAciklikTemizle();
                e.Handled = true;
                return;
            }
        };
    }

    private async void Back_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KaydedilmemisDegisiklikOnayi("Ana menüye dön")) return;
        var launcher = new ElectricLauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = launcher;
        launcher.Show();
        _kapanisOnaylandi = true;
        Close();
    }

    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KaydedilmemisDegisiklikOnayi("Yeni Proje")) return;
        _vm.YeniProje();
    }

    // 3.3 sökümü: DXF kat yükleme M50'ye taşındı — geometri .e4bina üzerinden gelir.

    /// Bağlı .e4bina'yı M50 penceresinde açar (M12 penceresi açık kalır).
    private void M50DeDuzenle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new Kst.Acoustics.App.Bina.Views.BinaGeometriWindow(null);
        var yol = _vm.Proje.BinaGeometriYolu;
        if (!string.IsNullOrWhiteSpace(yol) && File.Exists(yol))
            win.DosyaAc(yol!);
        win.Show();
        _vm.DurumMesaji = string.IsNullOrWhiteSpace(yol)
            ? "M50 Bina Geometrisi açıldı — geometriyi kaydedip 'Bina Geo.' ile bu projeye yükleyin."
            : "Bağlı geometri M50'de açıldı — kaydedince 'Bina Geo.' ile yeniden yükleyin.";
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KaydedilmemisDegisiklikOnayi("Proje Aç")) return;
        // M16 eski .e4p12 dosyalarını da açıp zayıf akımı ayıklayabilir; M12 yalnız .e4p12.
        var patterns = _vm.ModulZayifMi
            ? new[] { "*.e4p16", "*.e4p12" }
            : new[] { "*.e4p12", "*.eye4elecproj.json", "*.json" };
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"{_vm.ProjeDosyaEtiketi} Aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(_vm.ProjeDosyaEtiketi) { Patterns = patterns }]
        });
        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var proje = JsonSerializer.Deserialize<BasitProjeCizProje>(json, BasitProjeCizViewModel.ProjeJsonAyarlari)
                        ?? new BasitProjeCizProje();
            _vm.ProjeyiYukle(proje, path);
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Proje açılamadı: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ProjeDosyaYolu))
            await FarkliKaydet();
        else
            _vm.ProjeyiKaydet(_vm.ProjeDosyaYolu);
    }

    private async void BinaGeometrisiYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Bina Geometrisi Yükle (.e4bina)",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Bina Geometrisi (.e4bina.json)")
                { Patterns = ["*.e4bina.json"] }]
        });
        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            // 3.4 — Mutabakat: askıda kalacak veri varsa önce diyalogla onay al.
            var (geo, rapor) = _vm.BinaGeometriOnizle(path);
            if (rapor.AskidaVeriVar)
            {
                var devam = await AskidaVeriDiyalogu(rapor);
                if (!devam)
                {
                    _vm.DurumMesaji = "Bina geometrisi yüklemesi iptal edildi (askıda veri).";
                    return;
                }
            }
            _vm.BinaGeometrisiUygula(geo, path);
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Bina geometrisi yüklenemedi: {ex.Message}";
        }
    }

    /// 3.4 — Askıda-veri diyaloğu: yeni geometride karşılığı olmayan oda bağlarını
    /// (eleman/devre/mahal) listeler; kullanıcı yine de yükleyebilir veya vazgeçer.
    private async Task<bool> AskidaVeriDiyalogu(Kst.Acoustics.Core.Bina.Services.GeometriMutabakatRaporu rapor)
    {
        var sonuc = false;

        var liste = new ListBox
        {
            ItemsSource = rapor.AskidaKayitlar
                .Select(k => $"[{k.Tur}]  {k.Aciklama}   ({k.KatAd})")
                .ToList(),
            Background = Avalonia.Media.Brushes.Transparent,
            MaxHeight = 260
        };

        var yukleBtn = new Button
        {
            Content = "Yine de Yükle",
            Width = 120, Height = 32,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xB0, 0x60, 0x20)),
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0)
        };
        var vazgecBtn = new Button
        {
            Content = "Vazgeç",
            Width = 90, Height = 32,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dlg = new Window
        {
            Title = "Geometri Mutabakatı — Askıda Veri",
            Width = 560, Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x0A, 0x14, 0x0C)),
            Content = new Grid
            {
                Margin = new Avalonia.Thickness(16),
                RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Yeni geometride aşağıdaki kayıtların bağlı olduğu odalar bulunamadı.\n" +
                               "Yüklerseniz bu kayıtlar askıda kalır (oda bağı kopar, veri silinmez).",
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xD8, 0xE8, 0xD0)),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        [Grid.RowProperty] = 0
                    },
                    new TextBlock
                    {
                        Text = rapor.Ozet(),
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xF0, 0xB0, 0x60)),
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Margin = new Avalonia.Thickness(0, 10, 0, 8),
                        [Grid.RowProperty] = 1
                    },
                    new Border
                    {
                        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x24, 0x54, 0x24)),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(4),
                        Child = new ScrollViewer { Content = liste },
                        [Grid.RowProperty] = 2
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Margin = new Avalonia.Thickness(0, 12, 0, 0),
                        Children = { vazgecBtn, yukleBtn },
                        [Grid.RowProperty] = 3
                    }
                }
            }
        };

        yukleBtn.Click += (_, _) => { sonuc = true; dlg.Close(); };
        vazgecBtn.Click += (_, _) => dlg.Close();

        await dlg.ShowDialog(this);
        return sonuc;
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await FarkliKaydet();

    private async Task FarkliKaydet()
    {
        var uzanti = _vm.ProjeUzanti;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"{_vm.ProjeDosyaEtiketi} Kaydet",
            DefaultExtension = uzanti,
            SuggestedFileName = $"ProjeTasarimi.{uzanti}",
            FileTypeChoices = [new FilePickerFileType(_vm.ProjeDosyaEtiketi) { Patterns = [$"*.{uzanti}"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        _vm.ProjeyiKaydet(path);
    }

    private async void DxfCikti_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKat == null)
        {
            _vm.DurumMesaji = "DXF çıktı için önce kat yükleyin.";
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "DXF çıktısı kaydet",
            DefaultExtension = "dxf",
            SuggestedFileName = $"{_vm.SeciliKat.Ad}_ProjeTasarimi.dxf",
            FileTypeChoices = [new FilePickerFileType("DXF") { Patterns = ["*.dxf"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var tumTanimlar = _vm.Armatürler.Concat(_vm.Anahtarlar).Concat(_vm.Panolar).Concat(_vm.Prizler).Concat(_vm.Klimalar).ToList();
            _dxf.DxfCiktiAl(_vm.SeciliKat, path, tumTanimlar);
            _vm.DurumMesaji = $"DXF çıktı kaydedildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF çıktı alınamadı: {ex.Message}";
        }
    }

    // Tek Hat Şeması (.e4p11) ile birebir aynı JSON ayarları olmalı ki Tek Hat dosyayı açabilsin.
    private static readonly JsonSerializerOptions _tekHatJsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private async void TekHataAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tumTanimlar = _vm.Armatürler.Concat(_vm.Anahtarlar).Concat(_vm.Panolar).Concat(_vm.Prizler).Concat(_vm.Klimalar).ToList();
        var sonuc = TekHatAktarimServisi.Donustur(_vm.Proje, tumTanimlar);
        if (!sonuc.Basarili)
        {
            _vm.DurumMesaji = $"Tek Hat aktarımı: {sonuc.Hata}";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Tek Hat Şeması olarak aktar",
            DefaultExtension = "e4p11",
            SuggestedFileName = $"{_vm.Proje.ProjeAdi}_TekHat.e4p11",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO M11 Tek Hat (.e4p11)") { Patterns = ["*.e4p11"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!path.EndsWith(".e4p11", StringComparison.OrdinalIgnoreCase)) path += ".e4p11";

        try
        {
            var json = JsonSerializer.Serialize(sonuc.Proje, _tekHatJsonOpts);
            await File.WriteAllTextAsync(path, json);
            var uyari = sonuc.Uyarilar.Count > 0 ? $" — {sonuc.Uyarilar.Count} uyarı" : "";
            _vm.DurumMesaji = $"Tek Hat Şemasına aktarıldı: {Path.GetFileName(path)}{uyari}";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Tek Hat aktarımı başarısız: {ex.Message}";
        }
    }

    private void Kutuphane_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.SeciliKutuphaneArmatür = _vm.SeciliArmatür;
        var win = new ArmaturKutuphaneWindow(_vm);
        if (_vm.PrizAktif)
        {
            win.KutuphaneTabControl.SelectedIndex = 3;
        }
        win.Show(this);
    }

    // Tek örnek: pencere zaten açıksa yeniden oluşturmak yerine öne getirilir.
    // Kullanıcının filtre/kaydırma/seçim durumu korunur; pencere yığılması engellenir.
    private MahalListesiWindow? _mahalListesiWindow;

    private void MahalListesi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_mahalListesiWindow is { } acik)
        {
            acik.Activate();
            return;
        }
        var win = new MahalListesiWindow(_vm);
        _mahalListesiWindow = win;
        win.Closed += (_, _) => _mahalListesiWindow = null;
        win.Show(this);
    }

    private void Kurallar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new KurallarWindow();
        win.Show(this);
    }

    private void TaliPanoSecimTemizle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.SeciliTaliPanoHedefPano = null;
    }

    private void DedektorYerles_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.SeciliOdayaDedektorYerlestir();
    }

    private void OdaList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OdaList.SelectedItem is BasitProjeOda oda)
        {
            _vm.SeciliOda = oda;
        }
    }

    // 3.3 sökümü: oda/kat adı düzenleme ve kat silme M50'ye taşındı.
    // Oda adına tıklama yalnız seçim yapar.
    private void OdaAdi_Tapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is BasitProjeOda oda)
        {
            _vm.SeciliOda = oda;
            OdaList.SelectedItem = oda;
        }
    }

    private void KatAdi_Tapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is BasitProjeKat kat)
        {
            _vm.SeciliKat = kat;
            KatList.SelectedItem = kat;
        }
    }

    private async Task<bool> OnayDiyalogu(string mesaj)
    {
        var sonuc = false;

        var evetBtn = new Button
        {
            Content = "Evet, Sil",
            Width = 90, Height = 32,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xB0, 0x30, 0x20)),
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0)
        };
        var hayirBtn = new Button
        {
            Content = "İptal",
            Width = 70, Height = 32,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dlg = new Window
        {
            Title = "Onay",
            Width = 320, Height = 150,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x10, 0x1C, 0x10)),
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = mesaj,
                        Foreground = Avalonia.Media.Brushes.White,
                        FontSize = 13,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { evetBtn, hayirBtn }
                    }
                }
            }
        };

        evetBtn.Click += (_, _) => { sonuc = true; dlg.Close(); };
        hayirBtn.Click += (_, _) => { dlg.Close(); };
        await dlg.ShowDialog(this);
        return sonuc;
    }

    private void ElemanSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.SeciliElemanSilCommand.Execute(null);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // 5.1: Ctrl+N oda çizimi kaldırıldı — geometri M50'de düzenlenir.
    }

    private static int ParseInt(string? text, int fallback)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double ParseDouble(string? text, double fallback)
        => double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static BpcElemanTipi ManualType(int index) => index switch
    {
        1 => BpcElemanTipi.Anahtar,
        2 => BpcElemanTipi.Komütatör,
        3 => BpcElemanTipi.DarbeAnahtari,
        4 => BpcElemanTipi.Vavien,
        5 => BpcElemanTipi.KomütatörVavien,
        6 => BpcElemanTipi.Deviatör,
        7 => BpcElemanTipi.Dimmer,
        8 => BpcElemanTipi.IkiKutupluAnahtar,
        9 => BpcElemanTipi.KatPanosu,
        10 => BpcElemanTipi.Buat,
        11 => BpcElemanTipi.Sensör,
        _ => BpcElemanTipi.Anahtar
    };

    private static BpcElemanTipi ManualTypePriz(int index) => index switch
    {
        1 => BpcElemanTipi.Priz,
        2 => BpcElemanTipi.KapakliPriz,
        3 => BpcElemanTipi.SivaUstuPriz,
        4 => BpcElemanTipi.TrifazePriz,
        5 => BpcElemanTipi.KombinePrizKutusu,
        6 => BpcElemanTipi.TavanPrizi,
        7 => BpcElemanTipi.UsbliMasaPrizi,
        8 => BpcElemanTipi.UpsPrizi,
        9 => BpcElemanTipi.KapakliUpsPrizi,
        10 => BpcElemanTipi.SivaUstuUpsPrizi,
        11 => BpcElemanTipi.IkiliPriz,
        12 => BpcElemanTipi.UcluPriz,
        13 => BpcElemanTipi.Buat,
        14 => BpcElemanTipi.KatPanosu,
        _ => BpcElemanTipi.Priz
    };

    private static BpcElemanTipi ManualTypeMekanik(int index) => index switch
    {
        1 => BpcElemanTipi.KlimaIcUnite,
        2 => BpcElemanTipi.KlimaDisUnite,
        3 => BpcElemanTipi.KatPanosu,
        _ => BpcElemanTipi.KlimaIcUnite
    };

    private void SensorToggle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is ToggleButton tb)
            tb.Content = _vm.SensörluMod ? "Sensörlü" : "Sensörsüz";
    }

    private void KabloModu_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.KabloModuToggleCommand.Execute(null);

    // Pano içeriği linye tablosunu alt tutamaçtan sürükleyerek yeniden boyutlandır.
    private void PanoTabloResize_OnDragDelta(object? sender, VectorEventArgs e)
        => _vm.PanoTabloYuksekligi = System.Math.Clamp(_vm.PanoTabloYuksekligi + e.Vector.Y, 120, 720);

    // Sağdaki Araçlar paneli sol tutamaçtan boyutlandırılır: sola sürükle → genişle.
    private void AraclarPaneliResize_OnDragDelta(object? sender, VectorEventArgs e)
        => _vm.AraclarPaneliGenisligi = System.Math.Clamp(_vm.AraclarPaneliGenisligi - e.Vector.X, 180, 480);

    private void KomutatorDevre1_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.KomutatorDevreNo = 1;

    private void KomutatorDevre2_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.KomutatorDevreNo = 2;

    private void RotaTipi_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        _vm.KabloRotaTipi = combo.SelectedIndex == 0
            ? BpcRotaTipi.DaisyChain
            : BpcRotaTipi.DogruCizgi;
    }

    private async void KatPlaniDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SeciliKat == null) return;

        try
        {
            var hedef = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Kat Planını Dışa Aktar",
                DefaultExtension = "e4kp",
                SuggestedFileName = $"{GuvenliDosyaAdi(_vm.SeciliKat.Ad)}.e4kp",
                FileTypeChoices =
                [
                    new FilePickerFileType("EYE4PRO Kat Planı") { Patterns = ["*.e4kp"] }
                ]
            });
            if (hedef is null) return;

            var e4kpFile = hedef.Path.LocalPath;

            var plan = new OrtakKatPlani
            {
                KatNo = _vm.SeciliKat.KatNo,
                KatAdi = _vm.SeciliKat.Ad,
                KatYuksekligiM = _vm.SeciliKat.KatYuksekligiM,
                Odalar = _vm.SeciliKat.Odalar.Select(o => new OrtakOda
                {
                    Ad = o.Ad,
                    AlanM2 = o.AlanM2,
                    Poligon = o.Poligon
                }).ToList(),
                HamSegmentler = _vm.SeciliKat.DxfSegmentleri.Select(s => (s.A, s.B)).ToList(),
                DxfDosyaYolu = _vm.SeciliKat.DxfFilePath,
                BirimUyarisi = _vm.SeciliKat.UnitWarning
            };
            OrtakKatPlaniServisi.Kaydet(plan, e4kpFile);
            _vm.DurumMesaji = $"Kat planı kaydedildi: {e4kpFile}";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Kat planı kaydedilemedi: {ex.Message}";
        }
    }

    /// Dosya adından Windows/macOS'ta geçersiz karakterleri ayıklar; boşsa "kat" döner.
    private static string GuvenliDosyaAdi(string? ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return "kat";
        var temiz = new string(ad.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(temiz) ? "kat" : temiz;
    }

    // M13 (Zayıf Akım Tek Hat) native .e4p13 serileştirme ayarlarıyla birebir aynı olmalı
    // ki üretilen dosyayı M13 "Proje Aç" ile açabilsin.
    private static readonly JsonSerializerOptions _m13JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private async void M13eAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Bellek içi dönüşüm — dosya round-trip'i yok, serileştirme uyumsuzluğu riski yok.
        var m13 = Kst.Acoustics.Core.Electric.ZayifAkimTekHat.ZayifAkimTekHatImportServisi
            .Donustur(_vm.Proje, _vm.ProjeDosyaYolu);

        if (m13.ToplamCihazSayisi == 0 &&
            m13.DataSistemi.RackKabinler.Count == 0 &&
            m13.KameraSistemi.RackKabinler.Count == 0 &&
            m13.AnonsSistemi.RackKabinler.Count == 0 &&
            m13.TvSistemi.RackKabinler.Count == 0 &&
            m13.YanginSistemi.KatLoopGruplari.Count == 0)
        {
            _vm.DurumMesaji = "M13 aktarımı: projede aktarılacak zayıf akım sistemi bulunamadı.";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Zayıf Akım Tek Hat (M13) olarak aktar",
            DefaultExtension = "e4p13",
            SuggestedFileName = $"{_vm.Proje.ProjeAdi}_ZayifAkimTekHat.e4p13",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO M13 Zayıf Akım (.e4p13)") { Patterns = ["*.e4p13"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(m13, _m13JsonOpts));
            _vm.DurumMesaji = $"M13 aktarımı kaydedildi: {Path.GetFileName(path)} " +
                              $"({m13.Sayfalar.Count} sistem, {m13.ToplamCihazSayisi} cihaz). " +
                              "Zayıf Akım Tek Hat modülünde 'Proje Aç' ile açın.";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"M13 aktarımı başarısız: {ex.Message}";
        }
    }

    private GridLength _savedSolWidth = new GridLength(320);
    private GridLength _savedSagWidth = new GridLength(330);

    private void ToggleSolMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return;
        if (mainGrid.ColumnDefinitions[0].Width.Value > 0)
        {
            _savedSolWidth = mainGrid.ColumnDefinitions[0].Width;
            mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
            mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
        else
        {
            mainGrid.ColumnDefinitions[0].Width = _savedSolWidth;
            mainGrid.ColumnDefinitions[1].Width = new GridLength(4);
        }
    }

    private void EnsureSolMenuOpen()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return;
        if (mainGrid.ColumnDefinitions[0].Width.Value > 0) return;
        mainGrid.ColumnDefinitions[0].Width = _savedSolWidth;
        mainGrid.ColumnDefinitions[1].Width = new GridLength(4);
    }

    private void GuncelleYanPanelGorunurlugu()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return;

        bool solAcik = _vm.KatDxfPaneliAcik || _vm.KatlarPaneliAcik ||
                       _vm.OdalarPaneliAcik || _vm.AktarimPaneliAcik;
        bool sagAcik = _vm.ElemanlarPaneliAcik || _vm.KabloModuAktif;

        if (solAcik)
        {
            mainGrid.ColumnDefinitions[0].Width = _savedSolWidth;
            mainGrid.ColumnDefinitions[1].Width = new GridLength(4);
        }
        else
        {
            mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
            mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }

        if (sagAcik)
        {
            mainGrid.ColumnDefinitions[4].Width = _savedSagWidth;
            mainGrid.ColumnDefinitions[3].Width = new GridLength(4);
        }
        else
        {
            mainGrid.ColumnDefinitions[4].Width = new GridLength(0);
            mainGrid.ColumnDefinitions[3].Width = new GridLength(0);
        }
    }

    private void AktifPaftaSembolBarGuncelle()
    {
        if (AktifPaftaSembolBar is null) return;

        AktifPaftaSembolBar.Children.Clear();

        if (_vm.AktifPaftaMalzemeleri.Count == 0)
            return;

        foreach (var malzeme in _vm.AktifPaftaMalzemeleri)
        {
            var button = new Button
            {
                Width = 36,
                Height = 28,
                Padding = new Avalonia.Thickness(0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Content = SembolMiniCanvasOlustur(malzeme)
            };
            ToolTip.SetTip(button, malzeme.Ad);
            button.Click += (_, _) =>
            {
                if (_vm.ElemanEkleModeBaslatCommand.CanExecute(malzeme))
                    _vm.ElemanEkleModeBaslatCommand.Execute(malzeme);
            };
            AktifPaftaSembolBar.Children.Add(button);
        }
    }

    private void AnahtarSembolBarGuncelle()
    {
        if (AnahtarSembolBar is null) return;

        AnahtarSembolBar.Children.Clear();
        if (!_vm.AydinlatmaAktif)
            return;

        foreach (var anahtar in _vm.Anahtarlar)
        {
            var button = new Button
            {
                Width = 36,
                Height = 28,
                Padding = new Avalonia.Thickness(0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Content = SembolMiniCanvasOlustur(anahtar)
            };
            ToolTip.SetTip(button, anahtar.Ad);
            button.Click += (_, _) =>
            {
                if (_vm.ElemanEkleModeBaslatCommand.CanExecute(anahtar))
                    _vm.ElemanEkleModeBaslatCommand.Execute(anahtar);
            };
            AnahtarSembolBar.Children.Add(button);
        }
    }

    private void PanoSembolBarGuncelle()
    {
        if (PanoSembolBar is null) return;

        PanoSembolBar.Children.Clear();
        if (!_vm.KuvvetliAkimAktif)
            return;

        foreach (var pano in _vm.Panolar)
        {
            var button = new Button
            {
                Width = 36,
                Height = 28,
                Padding = new Avalonia.Thickness(0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Content = SembolMiniCanvasOlustur(pano)
            };
            ToolTip.SetTip(button, pano.Ad);
            button.Click += (_, _) =>
            {
                if (_vm.ElemanEkleModeBaslatCommand.CanExecute(pano))
                    _vm.ElemanEkleModeBaslatCommand.Execute(pano);
            };
            PanoSembolBar.Children.Add(button);
        }
    }

    private void ElemanGridYuksekliginiGuncelle()
    {
        if (ElemanGrid is null || SagPanelBorder is null) return;

        var panelHeight = SagPanelBorder.Bounds.Height;
        if (panelHeight <= 0) return;

        var reservedHeight = _vm.SeciliElemanAyarVar ? 430 : 125;
        ElemanGrid.Height = Math.Max(260, panelHeight - reservedHeight);
    }

    private static Canvas SembolMiniCanvasOlustur(BpcArmatürTanim tanim)
    {
        const double size = 24;
        const double margin = 3;
        var canvas = new Canvas { Width = size, Height = size };

        if (tanim.SembolCizgiler.Count == 0)
        {
            canvas.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(tanim.SembolAdi) ? "?" : tanim.SembolAdi[..Math.Min(1, tanim.SembolAdi.Length)],
                Foreground = Brushes.LightGreen,
                FontSize = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            return canvas;
        }

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var s in tanim.SembolCizgiler)
        {
            minX = Math.Min(minX, Math.Min(s.Ax, s.Bx));
            maxX = Math.Max(maxX, Math.Max(s.Ax, s.Bx));
            minY = Math.Min(minY, Math.Min(s.Ay, s.By));
            maxY = Math.Max(maxY, Math.Max(s.Ay, s.By));
        }

        var maxDim = Math.Max(maxX - minX, maxY - minY);
        if (maxDim < 1e-9) return canvas;

        var scale = (size - 2 * margin) / maxDim;
        var cx = (minX + maxX) / 2.0;
        var cy = (minY + maxY) / 2.0;

        double X(double x) => size / 2.0 + (x - cx) * scale;
        double Y(double y) => size / 2.0 - (y - cy) * scale;

        foreach (var seg in tanim.SembolCizgiler)
        {
            canvas.Children.Add(new Line
            {
                StartPoint = new Avalonia.Point(X(seg.Ax), Y(seg.Ay)),
                EndPoint = new Avalonia.Point(X(seg.Bx), Y(seg.By)),
                Stroke = Brushes.LightGreen,
                StrokeThickness = 1.3
            });
        }

        return canvas;
    }

    private void ToggleSagMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return;
        if (mainGrid.ColumnDefinitions[4].Width.Value > 0)
        {
            _savedSagWidth = mainGrid.ColumnDefinitions[4].Width;
            mainGrid.ColumnDefinitions[4].Width = new GridLength(0);
            mainGrid.ColumnDefinitions[3].Width = new GridLength(0);
        }
        else
        {
            mainGrid.ColumnDefinitions[4].Width = _savedSagWidth;
            mainGrid.ColumnDefinitions[3].Width = new GridLength(4);
        }
    }

    private void EnsureSagMenuOpen()
    {
        var mainGrid = this.FindControl<Grid>("MainGrid");
        if (mainGrid == null) return;
        if (mainGrid.ColumnDefinitions[4].Width.Value > 0) return;
        mainGrid.ColumnDefinitions[4].Width = _savedSagWidth;
        mainGrid.ColumnDefinitions[3].Width = new GridLength(4);
    }

    private void ToolbarKatDxf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool kapat = _vm.KatDxfPaneliAcik || _vm.KatlarPaneliAcik;
        TumUstPanelIcerikleriniKapat();
        if (kapat) return;
        EnsureSolMenuOpen();
        _vm.KatDxfPaneliAcik = true;
        _vm.KatlarPaneliAcik = true;
    }

    private void ToolbarAktarim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool kapat = _vm.AktarimPaneliAcik;
        TumUstPanelIcerikleriniKapat();
        if (kapat) return;
        EnsureSolMenuOpen();
        _vm.AktarimPaneliAcik = true;
    }

    private void ToolbarOdalar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool kapat = _vm.OdalarPaneliAcik;
        TumUstPanelIcerikleriniKapat();
        if (kapat) return;
        EnsureSolMenuOpen();
        _vm.OdalarPaneliAcik = true;
    }

    private void ToolbarKablo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool kapat = _vm.KabloModuAktif;
        TumUstPanelIcerikleriniKapat();
        if (kapat) return;
        EnsureSagMenuOpen();
        _vm.KabloModuToggleCommand.Execute(null);
    }

    private void ToolbarElemanlar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool kapat = _vm.ElemanlarPaneliAcik;
        TumUstPanelIcerikleriniKapat();
        if (kapat) return;
        EnsureSagMenuOpen();
        _vm.ElemanlarPaneliAcik = true;
    }

    private void TumUstPanelIcerikleriniKapat()
    {
        _vm.KatDxfPaneliAcik = false;
        _vm.KatlarPaneliAcik = false;
        _vm.OdalarPaneliAcik = false;
        _vm.AktarimPaneliAcik = false;
        _vm.ElemanlarPaneliAcik = false;
        if (_vm.KabloModuAktif)
            _vm.KabloModuKapat();
    }
}
