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
    // === GÖRSEL ŞEMA ÇİZİMİ ===

    private const double FloorHeight = 110;
    private const double ColWidth = 170;
    private const double FloorTopMargin = 50;
    private const double FloorLabelX = 16;
    private const double TreeStartX = 80;
    private const double SameFloorChildYOffset = 52;
    private const double CableLaneGap = 16;
    // OG (orta gerilim) bloğu için kat bantlarının üstünde ayrılan dikey alan (şebeke→hücreler→trafo)
    private const double OgBandYuksekligi = 220;

    private static double CableLaneX(double nodeCenterX, int laneIndex)
    {
        return nodeCenterX - 86 - laneIndex * CableLaneGap;
    }

    /// <summary>
    /// Kablo etiketine kesit ekini (" • 4x50 mm²") döndürür; kesit boşsa boş string.
    /// </summary>
    private static string KesitEk(string? kabloKesiti)
        => string.IsNullOrWhiteSpace(kabloKesiti) ? "" : $" • {kabloKesiti.Trim()}";

    /// <summary>
    /// Bir yatay kablo parçası için çakışmayan bir Y kanalı seçer. Tercih edilen Y boşsa onu,
    /// değilse [x1,x2] aralığında çakışmayan en yakın Y'yi döndürür ve seçilen kanalı kaydeder.
    /// yon verildiğinde arama o yönde yapılır; X-aralığı çakışmayan kablolar aynı Y'yi paylaşabilir.
    /// </summary>
    private double KabloYatayKanal(double tercihY, double x1, double x2, int yon = 0)
    {
        double a = System.Math.Min(x1, x2) - 2;
        double b = System.Math.Max(x1, x2) + 2;
        bool Bos(double y) => !_kabloYatayKanallar.Any(s =>
            System.Math.Abs(s.Y - y) < KabloKanalAdim - 0.5 && s.X1 <= b && s.X2 >= a);

        double secilen = tercihY;
        if (!Bos(secilen))
        {
            for (int k = 1; k < 400; k++)
            {
                if (yon != 0)
                {
                    double aday = tercihY + yon * k * KabloKanalAdim;
                    if (Bos(aday)) { secilen = aday; break; }
                }
                else
                {
                    if (Bos(tercihY + k * KabloKanalAdim)) { secilen = tercihY + k * KabloKanalAdim; break; }
                    if (Bos(tercihY - k * KabloKanalAdim)) { secilen = tercihY - k * KabloKanalAdim; break; }
                }
            }
        }
        _kabloYatayKanallar.Add((secilen, a, b));
        return secilen;
    }

    /// <summary>Konumu sabit (kanal seçilemeyen) bir yatay kablo parçasını dolu olarak kaydeder.</summary>
    private void KabloKanalKaydet(double y, double x1, double x2)
        => _kabloYatayKanallar.Add((y, System.Math.Min(x1, x2) - 2, System.Math.Max(x1, x2) + 2));

    private static double PozisyonBul(BusbarHatti? bb, Pano child)
    {
        if (bb == null) return 0;
        var kutu = bb.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == child.Ad);
        return kutu?.PozisyonM ?? 0;
    }

    // Hızlı arka arkaya eklemelerde şemayı her seferinde değil, son işlemden 350 ms sonra bir kez çizer.
    private void SemaCizGecikmeli()
    {
        _semaCizTimer.Stop();
        _semaCizTimer.Start();
    }

    // Bir pano/öğe dikdörtgenini içerik sınır kutusuna kat.
    private void IcerikGenislet(double sol, double ust, double sag, double alt)
    {
        if (sol < _icerikMinX) _icerikMinX = sol;
        if (sag > _icerikMaxX) _icerikMaxX = sag;
        if (ust < _icerikMinY) _icerikMinY = ust;
        if (alt > _icerikMaxY) _icerikMaxY = alt;
    }

    // Görünümü yalnız içeriği (pano + kat planı) kapsayacak şekilde viewport'a sığdırır.
    private void SemaSigdir()
    {
        if (SemaViewport == null) return;
        double vw = SemaViewport.Bounds.Width, vh = SemaViewport.Bounds.Height;
        double iw = _icerikMaxX - _icerikMinX, ih = _icerikMaxY - _icerikMinY;
        if (vw < 5 || vh < 5 || iw < 5 || ih < 5) return;

        const double kenar = 24; // içerik çevresinde küçük boşluk
        double s = Math.Min((vw - kenar) / iw, (vh - kenar) / ih);
        s = Math.Clamp(s, 0.05, 3.0);
        _semaScale.ScaleX = s;
        _semaScale.ScaleY = s;
        // İçeriği viewport'a ortala
        _semaTranslate.X = (vw - iw * s) / 2 - _icerikMinX * s;
        _semaTranslate.Y = (vh - ih * s) / 2 - _icerikMinY * s;
    }

    // Şema kanvasına fare tekerleğiyle zoom, boş alanda sol-sürükle ile pan ekler.
    private void SemaZoomPanBagla()
    {
        if (SemaViewport == null || SemaCanvas == null) return;

        // Zoom + pan dönüşümlerini kanvasa bağla
        var grup = new TransformGroup();
        grup.Children.Add(_semaScale);
        grup.Children.Add(_semaTranslate);
        SemaCanvas.RenderTransform = grup;

        // Eski ScrollViewer kenar boşluğunu (Margin=20) telafi eden başlangıç ötelemesi
        _semaTranslate.X = 20;
        _semaTranslate.Y = 20;

        SemaViewport.PointerWheelChanged += (s, e) =>
        {
            var pos = e.GetPosition(SemaViewport);
            double eski = _semaScale.ScaleX <= 0 ? 1.0 : _semaScale.ScaleX;
            // Zoom hızı 1/3'e düşürüldü: kademe başına ~%3.3 (eski ~%10)
            double carpan = e.Delta.Y > 0 ? 1.0333 : 1.0 / 1.0333;
            double yeni = Math.Clamp(eski * carpan, 0.2, 5.0);
            if (Math.Abs(yeni - eski) < 1e-9) return;

            // İmlecin altındaki dünya noktası zoom sonrası sabit kalsın
            double dunyaX = (pos.X - _semaTranslate.X) / eski;
            double dunyaY = (pos.Y - _semaTranslate.Y) / eski;
            _semaScale.ScaleX = yeni;
            _semaScale.ScaleY = yeni;
            _semaTranslate.X = pos.X - dunyaX * yeni;
            _semaTranslate.Y = pos.Y - dunyaY * yeni;
            e.Handled = true;
        };

        SemaViewport.PointerPressed += (s, e) =>
        {
            var nokta = e.GetCurrentPoint(SemaViewport);
            if (!nokta.Properties.IsLeftButtonPressed) return;
            // Kontrol modundayken boş alana sol tık → kontrolden çık (panolar kendi tıklamasını
            // e.Handled=true ile tükettiğinden bu handler yalnız boşlukta tetiklenir).
            if (_kontrolModu)
            {
                KontrolModunuKapat();
                e.Handled = true;
                return;
            }
            // Pano kutuları kendi PointerPressed'inde e.Handled=true yapar; orada pan başlamaz.
            // Bant/çizgi/etiket gibi pasif öğelerde ve boş alanda pan başlar.
            _semaPanAktif = true;
            _semaPanBaslangic = nokta.Position;
            _semaPanTx = _semaTranslate.X;
            _semaPanTy = _semaTranslate.Y;
            SemaViewport.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(SemaViewport);
        };

        SemaViewport.PointerMoved += (s, e) =>
        {
            if (!_semaPanAktif) return;
            var pos = e.GetPosition(SemaViewport);
            _semaTranslate.X = _semaPanTx + (pos.X - _semaPanBaslangic.X);
            _semaTranslate.Y = _semaPanTy + (pos.Y - _semaPanBaslangic.Y);
        };

        SemaViewport.PointerReleased += (s, e) =>
        {
            if (!_semaPanAktif) return;
            _semaPanAktif = false;
            SemaViewport.Cursor = Cursor.Default;
            e.Pointer.Capture(null);
        };

        // Modal pencere veya başka bir kontrol pointer yakalamasını devralırsa pan durumu
        // mutlaka kapanmalı; aksi halde sonraki fare hareketleri tuş basılıymış gibi davranır.
        SemaViewport.PointerCaptureLost += (_, _) =>
        {
            _semaPanAktif = false;
            SemaViewport.Cursor = Cursor.Default;
        };

        // Boş alanda çift tıkla → görünümü içeriğe sığdır
        SemaViewport.DoubleTapped += (s, e) =>
        {
            if (e.Source is Control c && c != SemaViewport && c != SemaCanvas) return;
            SemaSigdir();
        };

        // Viewport gerçek boyutuna kavuşunca ilk sığdırmayı yap (henüz yapılmadıysa)
        SemaViewport.SizeChanged += (s, e) => IlkSigdirmayiDene();
    }

    private double FloorY(string kat)
    {
        if (_floorStartY.TryGetValue(kat, out double startY))
            return startY + 30;
        // fallback if called before SemaCiz builds the dict
        var katlar = _vm.Proje.Bina?.KatEtiketleri() ?? new System.Collections.Generic.List<string> { "Z" };
        int idx = katlar.IndexOf(kat);
        if (idx < 0) idx = 0;
        return FloorTopMargin + idx * FloorHeight + 30;
    }

    private void SemaCiz()
    {
        if (SemaCanvas == null) return;
        SemaCanvas.Children.Clear();
        _kabloYatayKanallar.Clear();
        _icerikMinX = double.MaxValue; _icerikMinY = double.MaxValue;
        _icerikMaxX = double.MinValue; _icerikMaxY = double.MinValue;

        var katlar = _vm.Proje.Bina?.KatEtiketleri() ?? new System.Collections.Generic.List<string> { "Z" };

        // Toplam genişliği bul (kök panoların toplam sütun sayısı)
        var kokPanolar = _vm.Proje.TumPanolar
            .Where(p => p.UstPano == null && !AktifJeneratorTransferPanosuMu(p))
            .ToList();
        int toplamCols = kokPanolar.Sum(p => DiagramColCount(p));
        if (toplamCols < 1) toplamCols = 1;
        double canvasW = TreeStartX + toplamCols * ColWidth + 80;

        // ── Dinamik kat bant yüksekliği ──────────────────────────────────────
        // Her "yığın kökü" pano (kök + busbar çocukları + kat değiştiren kablolu çocuklar)
        // bulunduğu katta kendi aynı-kat alt ağacı kadar dikey yer ister. Bu, busbar üzerinden
        // bağlanan panonun kablolu alt panolarının üst üste binmesini önler.
        var floorMaxRows = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var root in kokPanolar)
            KatSatirlariniTopla(root, floorMaxRows);
        _floorStartY.Clear();
        _floorActualHeight.Clear();
        // OG sistemi varsa OG bloğu için ayrı bir bant (OgBandYuksekligi) açılır. Trafo Katı boşsa bu
        // bant en üste (geleneksel OG girişi) konur; doluysa o katın hemen ÜSTÜNE eklenir → trafo o kat
        // seviyesinde görünür, panolarla çakışmaz. _ogTopY OG bloğunun çizim üst-Y'sidir.
        var ogSistem = _vm.Proje.OrtaGerilim;
        string? ogKat = null;
        if (ogSistem != null)
        {
            string t = (ogSistem.TrafoKat ?? "").Trim();
            ogKat = (!string.IsNullOrEmpty(t) && katlar.Contains(t)) ? t : null;
        }
        double yAcc = FloorTopMargin;
        _ogTopY = 0;
        bool ogBandiYerlesti = false;
        if (ogSistem != null && ogKat == null)
        {
            _ogTopY = 0;                              // en üstte
            yAcc = FloorTopMargin + OgBandYuksekligi; // ilk kat OG bandının altından başlar
            ogBandiYerlesti = true;
        }
        foreach (var kat in katlar)
        {
            if (ogSistem != null && !ogBandiYerlesti && kat == ogKat)
            {
                _ogTopY = yAcc + 8;                   // OG bloğu bu katın üstündeki banda
                yAcc += OgBandYuksekligi;
                ogBandiYerlesti = true;
            }
            _floorStartY[kat] = yAcc;
            int rows = floorMaxRows.GetValueOrDefault(kat, 1);
            // rows == 1 → standart FloorHeight; rows > 1 → genişlet
            double h = Math.Max(FloorHeight, rows * SameFloorChildYOffset + 40);
            _floorActualHeight[kat] = h;
            yAcc += h;
        }
        double totalCanvasH = yAcc;
        // ──────────────────────────────────────────────────────────────────────

        // Kat çizgileri ve etiketleri (yatay)
        for (int i = 0; i < katlar.Count; i++)
        {
            string kat = katlar[i];
            double y = _floorStartY.GetValueOrDefault(kat, FloorTopMargin + i * FloorHeight);
            double h = _floorActualHeight.GetValueOrDefault(kat, FloorHeight);

            // Kat bandı arka planı (çift sıralarda hafif tonlama)
            if (i % 2 == 0)
            {
                var bant = new Border
                {
                    Width = canvasW,
                    Height = h,
                    Background = new SolidColorBrush(Color.Parse("#0A1A0E"))
                };
                Canvas.SetLeft(bant, 0);
                Canvas.SetTop(bant, y);
                SemaCanvas.Children.Add(bant);
            }
            // Yatay kat çizgisi
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(0, y),
                EndPoint = new Avalonia.Point(canvasW, y),
                Stroke = new SolidColorBrush(Color.Parse("#1F3624")),
                StrokeThickness = 1,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }
            });
            // Kat etiketi
            var katLbl = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0E2210")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A4A20")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(6, 2),
                Child = new TextBlock
                {
                    Text = $"Kat {kat}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#8CB070")),
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                }
            };
            Canvas.SetLeft(katLbl, FloorLabelX);
            Canvas.SetTop(katLbl, y + 6);
            SemaCanvas.Children.Add(katLbl);
        }

        // Alt yatay kapatma çizgisi
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(0, totalCanvasH),
            EndPoint = new Avalonia.Point(canvasW, totalCanvasH),
            Stroke = new SolidColorBrush(Color.Parse("#1F3624")),
            StrokeThickness = 1
        });

        if (kokPanolar.Count == 0)
        {
            SemaCanvas.Width = canvasW;
            SemaCanvas.Height = totalCanvasH + 40;
            return;
        }

        // Pano X koordinatlarını hesapla (DFS) ve çiz
        var og = _vm.Proje.OrtaGerilim;
        Pano? ogHedefPano = og == null ? null
            : (kokPanolar.FirstOrDefault(p => p.Ad == og.AnaPanoAd)
               ?? kokPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano)
               ?? kokPanolar.FirstOrDefault());
        double ogHedefCx = 0;
        double ogKabloKanalX = 0;
        bool ogHedefBulundu = false;

        double curX = TreeStartX;
        foreach (var root in kokPanolar)
        {
            int cols = SubtreeColCount(root);
            int visualCols = DiagramColCount(root);
            double rootCx = curX + cols * ColWidth / 2.0;
            DrawNode(root, curX, rootCx);
            if (root == ogHedefPano)
            {
                ogHedefCx = rootCx;
                ogKabloKanalX = Math.Max(FloorLabelX + 42, curX - 34);
                ogHedefBulundu = true;
            }
            curX += visualCols * ColWidth;
        }

        // OG bloğu: hedef ana panonun üstüne şebeke → hücreler → trafo → AG bağlantı zincirini çiz.
        // _ogTopY, Trafo Katı'na göre bloğun dikey yerleşimini belirler (en üstte ya da kat üstü bant).
        if (og != null && ogHedefBulundu && ogHedefPano != null)
            DrawOrtaGerilim(og, ogHedefCx, FloorY(ogHedefPano.Kat), ogKabloKanalX, _ogTopY);

        SemaCanvas.Width = canvasW;
        SemaCanvas.Height = totalCanvasH + 40;

        // İçerik sınırı = gerçekten çizilen öğelerin (pano + jeneratör + OG) kapladığı kutu, her yöne
        // küçük bir boşlukla. Eskiden Y, tüm kat bantlarının yüksekliğine (totalCanvasH) zorlanıyordu;
        // çok satırlı/yüksek bir kat bandı (ör. çok sayıda aynı-kat alt pano) varken bu, sığdırmayı boş
        // bant boşluğuna göre hesaplayıp şemayı aşırı küçültüyordu. Artık yalnız içeriğe sığdırılır.
        if (_icerikMinX <= _icerikMaxX)
        {
            _icerikMinX -= 20; _icerikMaxX += 20;
            _icerikMinY -= 20; _icerikMaxY += 20;
        }

        // İlk çizimde görünümü otomatik olarak içeriğe sığdır (viewport boyutu hazır olunca dene)
        Dispatcher.UIThread.Post(IlkSigdirmayiDene, DispatcherPriority.Background);
    }

    /// <summary>Viewport boyutu ve içerik hazırsa görünümü bir kez içeriğe sığdırır.</summary>
    private void IlkSigdirmayiDene()
    {
        if (_ilkSigdirYapildi) return;
        if (SemaViewport == null || SemaViewport.Bounds.Width < 5 || SemaViewport.Bounds.Height < 5) return;
        if (_icerikMinX > _icerikMaxX) return;
        _ilkSigdirYapildi = true;
        SemaSigdir();
    }

    /// <summary>
    /// Bir panonun alt ağacının kaç X-sütunu kapladığını hesaplar.
    /// </summary>
    private int SubtreeColCount(Pano node)
    {
        var direct = node.AltPanolar.Where(c => c.BaglantiTipi == BaglantiTipi.Kablo).ToList();
        var normalFloorDirect = direct.Where(c => c.Kat != node.Kat).ToList();
        var sameFloorDirect = direct.Where(c => c.Kat == node.Kat).ToList();
        var bbGroups = node.AltPanolar
            .Where(c => c.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrEmpty(c.BaglandigiBusbarId))
            .GroupBy(c => c.BaglandigiBusbarId!).ToList();

        int cols = 0;
        foreach (var c in normalFloorDirect) cols += SubtreeColCount(c);
        foreach (var g in bbGroups)
        {
            // Busbar grubu: farklı katlardaki çıkışlar aynı X sütununu paylaşır (çakışmazlar),
            // bu yüzden grup genişliği = en geniş çıkış alt ağacı + 1 busbar kolonu (kompakt).
            // Aynı kattaki birden fazla çıkış için ek sütun ayrılır.
            int ayniKatMax = g.GroupBy(c => c.Kat ?? "").Select(grp => grp.Count()).DefaultIfEmpty(1).Max();
            int enGenis = g.Max(c => SubtreeColCount(c));
            int groupW = enGenis * ayniKatMax + 1;
            cols += groupW;
        }
        if (sameFloorDirect.Count > 0)
        {
            // Aynı kattaki alt panolar yatayda jeneratör hattıyla yarışmasın diye
            // parent altında dikey istiflenir; sadece en geniş alt ağaç kadar yer ister.
            cols = Math.Max(cols, sameFloorDirect.Max(SubtreeColCount));
        }
        return System.Math.Max(1, cols);
    }

    /// <summary>
    /// Bir panonun aynı kattaki alt ağacının dikey olarak kaç satır kapladığını verir
    /// (kendisi 1 + aynı-kat çocuklarının satır toplamı). Aynı-kat dizilim çakışmasını önler.
    /// </summary>
    /// <summary>
    /// Tüm ağacı dolaşıp her katın gereken dikey satır sayısını hesaplar. Bir pano, bulunduğu katta
    /// yeni bir dikey yığın başlatıyorsa (kök, busbar çocuğu veya üst panosundan farklı kattaki kablolu
    /// çocuk) o katta <see cref="SameFloorRowCount"/> kadar yer ister; aynı kat için en büyük değer alınır.
    /// </summary>
    private void KatSatirlariniTopla(Pano node, System.Collections.Generic.Dictionary<string, int> floorMaxRows)
    {
        bool yiginKoku = node.UstPano == null
            || node.BaglantiTipi == BaglantiTipi.Busbar
            || (node.UstPano != null && node.Kat != node.UstPano.Kat);
        if (yiginKoku)
        {
            string kat = node.Kat ?? "Z";
            int rows = SameFloorRowCount(node);
            if (!floorMaxRows.TryGetValue(kat, out int cur) || cur < rows)
                floorMaxRows[kat] = rows;
        }
        foreach (var c in node.AltPanolar)
            KatSatirlariniTopla(c, floorMaxRows);
    }

    private int SameFloorRowCount(Pano node)
    {
        var sameFloorDirect = node.AltPanolar
            .Where(c => c.BaglantiTipi == BaglantiTipi.Kablo && c.Kat == node.Kat)
            .ToList();
        var sameFloorBusbar = node.AltPanolar
            .Where(c => c.BaglantiTipi == BaglantiTipi.Busbar && c.Kat == node.Kat)
            .ToList();
        int rows = 1;
        // UPS kritik panosunun üstüne UPS+akü rozeti çizildiğinden, bir üstteki panoyla
        // çakışmaması için dikey istifte kendisine ek bir satır (rozet satırı) ayrılır.
        if (UpsRozetli(node)) rows += 1;
        foreach (var c in sameFloorDirect)
            rows += SameFloorRowCount(c);
        foreach (var c in sameFloorBusbar)
            rows += SameFloorRowCount(c);
        return rows;
    }

    /// <summary>
    /// Panonun üstüne UPS+akü rozeti çizilip çizilmeyeceğini döner (kritik UPS panosu mu).
    /// <see cref="DrawUpsRozet"/> koşuluyla aynıdır; yerleşimde rozet için yer ayırmak üzere kullanılır.
    /// </summary>
    private bool UpsRozetli(Pano p)
        => p.PanoTipi == PanoTipi.UpsPanosu && _vm.KritikPanoUps(p) != null;

    private bool AktifJeneratorTransferPanosuMu(Pano pano)
    {
        if (pano.PanoTipi != PanoTipi.TransferPanosu) return false;
        return _vm.Proje.JeneratorGruplari.Any(g => g.SenkronPanoAd == pano.Ad)
            || _vm.Proje.Jenerator?.TransferPanoAd == pano.Ad;
    }

    private bool JeneratorHedefPanosuMu(Pano pano)
    {
        // Hedef pano ana pano (tesis geneli) ya da bir tali pano (lokal bölge) olabilir.
        return _vm.Proje.JeneratorGruplari.Any(g => g.AnaPanoAd == pano.Ad)
            || _vm.Proje.Jenerator?.AnaPanoAd == pano.Ad;
    }

    private int DiagramColCount(Pano node)
    {
        // Jeneratör bloğu senkron panosu + jeneratör üniteleri için ana pano alt ağacının
        // sağında ek görsel sütun ister. Senkron/transfer panosu ayrıca kök pano olarak çizilmez.
        return SubtreeColCount(node) + (JeneratorHedefPanosuMu(node) ? JeneratorGorselKolonSayisi(node) : 0);
    }

    private int JeneratorGorselKolonSayisi(Pano anaPano)
    {
        var grup = _vm.Proje.JeneratorGruplari.FirstOrDefault(g => g.AnaPanoAd == anaPano.Ad);
        int adet = grup?.Jeneratorler.Count ?? (_vm.Proje.Jenerator?.AnaPanoAd == anaPano.Ad ? 1 : 0);
        return Math.Max(2, adet + 1);
    }

    private void DrawNode(Pano node, double startX, double centerX, double? yOverride = null)
    {
        double cy = yOverride ?? FloorY(node.Kat);

        // Önce bu node'un kendi çocuklarını yerleştir (parent çizilmeden önce hat çizimi için sırası önemli değil ama hat'lar pano üstüne binmemeli)
        var direct = node.AltPanolar.Where(c => c.BaglantiTipi == BaglantiTipi.Kablo).ToList();
        var normalFloorDirect = direct.Where(c => c.Kat != node.Kat).ToList();
        var sameFloorDirect = direct.Where(c => c.Kat == node.Kat).ToList();
        var bbGroups = node.AltPanolar
            .Where(c => c.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrEmpty(c.BaglandigiBusbarId))
            .GroupBy(c => c.BaglandigiBusbarId!).ToList();

        double curX = startX;

        // Direkt kablolu çocuklar: dikey ana bara + her çocuk farklı Y seviyesinden dal alır.
        // Böylece kablolar üst üste binmez; şema IEC tek hat standardına uygun "tarak" görünümü alır.
        const double BusBranchGap = 14.0;
        int nNormal = normalFloorDirect.Count;

        // 1) Her çocuğun X konumunu ve çakışmayan yatay dal kanalını (branchY) hesapla.
        var dallar = new System.Collections.Generic.List<(Pano child, double childStartX, double childCx, double childY, double branchY, double parentPortY, double childPortY, int yon, int cw)>();
        double busMaxY = cy + 22;
        double busMinY = cy - 22;
        int yukariDal = 0;
        int asagiDal = 0;
        for (int i = 0; i < nNormal; i++)
        {
            var child = normalFloorDirect[i];
            int cw = SubtreeColCount(child);
            double childStartX = curX;
            double childCx = curX + cw * ColWidth / 2.0;
            double childY = FloorY(child.Kat);
            int yon = childY < cy ? -1 : 1;
            int dalNo = yon < 0 ? yukariDal++ : asagiDal++;
            double parentPortY = cy + yon * 22;
            double childPortY = childY - yon * 22;
            double branchYpref = cy + yon * (22 + (dalNo + 1) * BusBranchGap);
            // Yatay dal, X-aralığı çakışan başka bir kabloyla aynı Y'ye düşmesin.
            // Hedef üst kattaysa kanalı yukarıda, alt kattaysa aşağıda arar.
            double branchY = KabloYatayKanal(branchYpref, centerX, childCx, yon);
            if (yon < 0)
            {
                if (branchY < busMinY) busMinY = branchY;
            }
            else if (branchY > busMaxY)
            {
                busMaxY = branchY;
            }
            dallar.Add((child, childStartX, childCx, childY, branchY, parentPortY, childPortY, yon, cw));
            curX += cw * ColWidth;
        }

        // 2) Dikey ana bara: hedef yönüne göre parent üstünden/altından dala kadar
        if (yukariDal > 0)
        {
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(centerX, cy - 22),
                EndPoint = new Avalonia.Point(centerX, busMinY),
                Stroke = new SolidColorBrush(Color.Parse("#4A6A38")),
                StrokeThickness = 2
            });
        }
        if (asagiDal > 0)
        {
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(centerX, cy + 22),
                EndPoint = new Avalonia.Point(centerX, busMaxY),
                Stroke = new SolidColorBrush(Color.Parse("#4A6A38")),
                StrokeThickness = 2
            });
        }

        // 3) Dalları çiz (yatay + dikey düşüş) ve alt ağaçları çiz
        foreach (var d in dallar)
        {
            var brush = new SolidColorBrush(Color.Parse("#4A6A38"));
            // Yatay dal: bara noktası → çocuk X (kendi kanalında)
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(centerX, d.branchY), EndPoint = new Avalonia.Point(d.childCx, d.branchY), Stroke = brush, StrokeThickness = 2 });
            // Dikey düşüş: dal noktası → çocuk üstü
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(d.childCx, d.branchY), EndPoint = new Avalonia.Point(d.childCx, d.childPortY), Stroke = brush, StrokeThickness = 2 });

            var lbl = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#060C07")),
                Padding = new Avalonia.Thickness(4, 2),
                CornerRadius = new Avalonia.CornerRadius(3),
                Child = new TextBlock
                {
                    Text = $"{d.child.UstPanoCikisSigortaA}A • {d.child.KabloMesafeM}m{KesitEk(d.child.KabloKesiti)}",
                    FontSize = 10,
                    Foreground = Brushes.LightGray
                }
            };
            Canvas.SetLeft(lbl, d.childCx + 4);
            Canvas.SetTop(lbl, d.branchY + (d.yon > 0 ? 2 : -18));
            SemaCanvas.Children.Add(lbl);

            DrawNode(d.child, d.childStartX, d.childCx);
        }

        int sameFloorBusbarRowCursor = 1;
        foreach (var c in sameFloorDirect)
            sameFloorBusbarRowCursor += SameFloorRowCount(c);

        // Busbar grupları — her grup için dikey busbar kolonu
        foreach (var g in bbGroups)
        {
            var bb = _vm.Proje.TumBusbarlar.FirstOrDefault(b => b.Id == g.Key);
            int ayniKatMaxG = g.GroupBy(c => c.Kat ?? "").Select(grp => grp.Count()).DefaultIfEmpty(1).Max();
            int enGenisG = g.Max(c => SubtreeColCount(c));
            int groupW = enGenisG * ayniKatMaxG + 1;
            // Busbar X = grup'un sol kenarından + colWidth/2 (ilk sütun busbar için)
            double busbarX = curX + ColWidth / 2.0;
            var kids = g.OrderBy(c => FloorY(c.Kat ?? "Z")).ToList();
            double topKidY = kids.Min(c => FloorY(c.Kat ?? "Z"));
            double bottomKidY = kids.Max(c => FloorY(c.Kat ?? "Z"));

            var bbBrush = new SolidColorBrush(Color.Parse("#5AAA38"));
            int bbYon = ((topKidY + bottomKidY) / 2.0) < cy ? -1 : 1;
            double bbParentPortY = cy + bbYon * 22;
            double bbRouteY = KabloYatayKanal(cy + bbYon * 36, centerX, busbarX, bbYon);

            double kidBaseX = curX + ColWidth; // busbar sütunu sonrası
            double seritGenislik = enGenisG * ColWidth;
            var katSerit = new System.Collections.Generic.Dictionary<string, int>();
            var busbarDallari = new System.Collections.Generic.List<(Pano child, double childStartX, double childCx, double childY, double branchY, double childPortY, int yon, int serit)>();
            foreach (var child in kids)
            {
                int cw = SubtreeColCount(child);
                string ckat = child.Kat ?? "";
                int serit = katSerit.TryGetValue(ckat, out int sv) ? sv : 0;
                katSerit[ckat] = serit + 1;
                double childStartX = kidBaseX + serit * seritGenislik;
                double childCx = childStartX + cw * ColWidth / 2.0;
                bool ayniKat = child.Kat == node.Kat;
                bool upsRozet = ayniKat && UpsRozetli(child);
                // UPS rozeti için üstte bir satır boşluk bırak (üstteki panoyla çakışmasın).
                if (upsRozet) sameFloorBusbarRowCursor += 1;
                int childRow = sameFloorBusbarRowCursor;
                double childY = ayniKat
                    ? cy + SameFloorChildYOffset * childRow
                    : FloorY(child.Kat ?? "Z");
                if (ayniKat)
                    sameFloorBusbarRowCursor += SameFloorRowCount(child) - (upsRozet ? 1 : 0);
                int childYon = childY < cy ? 1 : -1;
                double branchPref = childY + childYon * (28 + serit * 14);
                double branchY = KabloYatayKanal(branchPref, busbarX, childCx, childYon);
                double childPortY = childY + childYon * 22;
                busbarDallari.Add((child, childStartX, childCx, childY, branchY, childPortY, childYon, serit));
            }

            // Parent'tan busbar üstüne bağlantı (hedef yönüne göre yukarı/alta alınır)
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(centerX, bbParentPortY), EndPoint = new Avalonia.Point(centerX, bbRouteY), Stroke = bbBrush, StrokeThickness = 2 });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(centerX, bbRouteY), EndPoint = new Avalonia.Point(busbarX, bbRouteY), Stroke = bbBrush, StrokeThickness = 2 });
            // Dikey busbar hattı (KALIN) — parent bağlantısını ve TÜM çıkış kutularını kapsamalı.
            // Çıkış kutusu parent'ın üstündeki katta olabilir; bu yüzden hem yukarı hem aşağı uzanır.
            double busUst = Math.Min(bbRouteY, busbarDallari.Min(d => d.branchY));
            double busAlt = Math.Max(bbRouteY, busbarDallari.Max(d => d.branchY));
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(busbarX, busUst),
                EndPoint = new Avalonia.Point(busbarX, busAlt),
                Stroke = bbBrush,
                StrokeThickness = 5
            });
            // Busbar etiketi (tip + uzunluk)
            if (bb != null)
            {
                var bbLabel = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#0C1A0C")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#5AAA38")),
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Padding = new Avalonia.Thickness(5, 2),
                    Child = new TextBlock
                    {
                        Text = $"{bb.Ad} • {bb.BusbarTipiAd} • {bb.ToplamUzunlukM:N1}m",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.Parse("#A0E080"))
                    }
                };
                bbLabel.Cursor = new Cursor(StandardCursorType.Hand);
                bbLabel.ContextMenu = BusbarContextMenu(bb);
                ToolTip.SetTip(bbLabel, BusbarTooltip(bb)); // fareyle üzerine gelince toplam yük + akım
                Canvas.SetLeft(bbLabel, busbarX + 8);
                Canvas.SetTop(bbLabel, bbRouteY + (bbYon > 0 ? 4 : -22));
                SemaCanvas.Children.Add(bbLabel);
            }

            // Her çocuk: çıkış kutusu + yatay dal → çocuk.
            // Farklı katlardaki çıkışlar aynı X sütununu paylaşır (kompakt). Aynı kattaki birden fazla
            // çıkış, kat-içi şerit sayacıyla yan yana ayrı sütunlara konur (üst üste binmeyi önler).
            foreach (var d in busbarDallari)
            {
                // Çıkış kutusu sembolü
                var kutu = new Border
                {
                    Width = 16, Height = 16,
                    Background = new SolidColorBrush(Color.Parse("#1A3A20")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#80E060")),
                    BorderThickness = new Avalonia.Thickness(1.5),
                    CornerRadius = new Avalonia.CornerRadius(2),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                if (bb != null)
                {
                    kutu.PointerPressed += async (_, e) =>
                    {
                        var nokta = e.GetCurrentPoint(kutu);
                        if (!nokta.Properties.IsLeftButtonPressed) return;
                        e.Handled = true;
                        await BusbarKutuDuzenleAc(bb, d.child);
                    };
                }
                Canvas.SetLeft(kutu, busbarX - 8);
                Canvas.SetTop(kutu, d.branchY - 8);
                SemaCanvas.Children.Add(kutu);

                // Yatay dal kutu → çocuk
                SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(busbarX + 8, d.branchY),
                    EndPoint = new Avalonia.Point(d.childCx, d.branchY),
                    Stroke = new SolidColorBrush(Color.Parse("#4A6A38")),
                    StrokeThickness = 2
                });
                SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(d.childCx, d.branchY),
                    EndPoint = new Avalonia.Point(d.childCx, d.childPortY),
                    Stroke = new SolidColorBrush(Color.Parse("#4A6A38")),
                    StrokeThickness = 2
                });

                // Etiket: pozisyon + sigorta + kablo m
                var infoLbl = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#060C07")),
                    Padding = new Avalonia.Thickness(4, 2),
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Child = new TextBlock
                    {
                        Text = $"{PozisyonBul(bb, d.child)}m → {d.child.CikisKutusuSigortaA}A • {d.child.KabloMesafeM}m{KesitEk(d.child.KabloKesiti)}",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.Parse("#A0E080"))
                    }
                };
                Canvas.SetLeft(infoLbl, busbarX + 14);
                Canvas.SetTop(infoLbl, d.branchY + (d.yon > 0 ? 4 : -18));
                SemaCanvas.Children.Add(infoLbl);

                DrawNode(d.child, d.childStartX, d.childCx, d.childY);
            }

            curX += groupW * ColWidth;
        }

        // Aynı kattaki direkt çocuklar: parent'ın altında alt alta diz.
        // Her çocuk, kendi aynı-kat alt ağacının kapladığı satır sayısı kadar dikey yer
        // tutar; aksi halde derin alt panolar kardeşlerin üzerine biner.
        int rowCursor = 1;
        for (int i = 0; i < sameFloorDirect.Count; i++)
        {
            var child = sameFloorDirect[i];
            bool upsRozet = UpsRozetli(child);
            // UPS rozeti için üstte bir satır boşluk bırak (üst pano/kardeşle çakışmasın).
            if (upsRozet) rowCursor += 1;
            int cw = SubtreeColCount(child);
            double childCx = centerX;
            double childY = cy + SameFloorChildYOffset * rowCursor;
            double laneX = CableLaneX(centerX, normalFloorDirect.Count + i);
            double parentEdgeX = centerX - 65;
            double branchY = KabloYatayKanal(cy + 28 + i * 10, parentEdgeX, laneX, 1);
            // Merkezi UPS dağıtım panosu ana panodan AYRI BİR KOLON ile beslenir → teal, kalın hat.
            bool merkeziKolon = _vm.MerkeziDagitimPanosuMu(child);
            var brush = new SolidColorBrush(Color.Parse(merkeziKolon ? "#2AA0C0" : "#4A6A38"));
            double hatKalinlik = merkeziKolon ? 3 : 2;

            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(parentEdgeX, cy + 21),
                EndPoint = new Avalonia.Point(parentEdgeX, branchY),
                Stroke = brush,
                StrokeThickness = hatKalinlik
            });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(parentEdgeX, branchY),
                EndPoint = new Avalonia.Point(laneX, branchY),
                Stroke = brush,
                StrokeThickness = hatKalinlik
            });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(laneX, branchY),
                EndPoint = new Avalonia.Point(laneX, childY),
                Stroke = brush,
                StrokeThickness = hatKalinlik
            });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(laneX, childY),
                EndPoint = new Avalonia.Point(childCx - 65, childY),
                Stroke = brush,
                StrokeThickness = hatKalinlik
            });

            // Bu sabit-konumlu yatay parçaları dolu kanal olarak kaydet (başka kablolar üstüne düşmesin)
            KabloKanalKaydet(childY, laneX, childCx - 65);

            var lbl = new Border
            {
                Background = new SolidColorBrush(Color.Parse(merkeziKolon ? "#08222A" : "#060C07")),
                BorderBrush = merkeziKolon ? new SolidColorBrush(Color.Parse("#2AA0C0")) : null,
                BorderThickness = new Avalonia.Thickness(merkeziKolon ? 1 : 0),
                Padding = new Avalonia.Thickness(4, 2),
                CornerRadius = new Avalonia.CornerRadius(3),
                Child = new TextBlock
                {
                    Text = merkeziKolon
                        ? "⚡ UPS Kolonu (Ayrı)"
                        : $"{child.UstPanoCikisSigortaA}A • {child.KabloMesafeM}m{KesitEk(child.KabloKesiti)}",
                    FontSize = 10,
                    Foreground = merkeziKolon
                        ? new SolidColorBrush(Color.Parse("#8AD8E8"))
                        : Brushes.LightGray
                }
            };
            Canvas.SetLeft(lbl, laneX + 4);
            Canvas.SetTop(lbl, ((cy + 21) + (childY - 21)) / 2.0 - 10);
            SemaCanvas.Children.Add(lbl);

            double childStartX = childCx - cw * ColWidth / 2.0;
            DrawNode(child, childStartX, childCx, childY);

            // Sonraki kardeş, bu çocuğun aynı-kat alt ağacının altından başlasın.
            // (UPS rozet satırını yukarıda zaten ilerlettik; SameFloorRowCount onu da saydığından düş.)
            rowCursor += SameFloorRowCount(child) - (upsRozet ? 1 : 0);
        }

        // Düğümü çiz
        bool isSelected = node == _vm.SeciliPano;
        // Pano kutusu rengi tipine göre
        string bgColor = node.PanoTipi switch
        {
            PanoTipi.AnaPano => "#3A6620",
            PanoTipi.AydinlatmaPanosu => "#2A4A6A",
            PanoTipi.Kompanzasyon => "#5A3A18",
            PanoTipi.KontrolPanosu => "#3A2A66",
            PanoTipi.UpsPanosu => "#1A4A5A",
            PanoTipi.YanginPanosu => "#5A1A1A",
            _ => "#162810"
        };
        // Kontrol modunda pano eksikliklerine göre renklenir: kırmızı (açıklama eksik) >
        // sarı (kablo mesafesi varsayılan) > yeşil (eksik yok). Aksi halde tip/seçim rengi.
        string kutuBg = isSelected ? "#3A7A18" : bgColor;
        string kutuBorder = isSelected ? "#A0E050" : "#5A7A34";
        double kutuBorderTh = isSelected ? 2 : 1;
        if (_kontrolModu)
        {
            (kutuBg, kutuBorder) = PanoKontrolSeverity(node) switch
            {
                2 => ("#6A1A1A", "#E85050"), // kırmızı — açıklama eksik
                1 => ("#6A5A14", "#E8C24A"), // sarı — kablo mesafesi varsayılan
                _ => ("#1A4A24", "#56C878")  // yeşil — eksik yok
            };
            kutuBorderTh = 2;
        }
        var border = new Border
        {
            Width = 130,
            Height = 42,
            CornerRadius = new Avalonia.CornerRadius(20),
            Background = new SolidColorBrush(Color.Parse(kutuBg)),
            BorderBrush = new SolidColorBrush(Color.Parse(kutuBorder)),
            BorderThickness = new Avalonia.Thickness(kutuBorderTh),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        double cx = centerX;
        Canvas.SetLeft(border, cx - 65);
        Canvas.SetTop(border, cy - 21);
        IcerikGenislet(cx - 65, cy - 21, cx + 65, cy + 21); // sığdırma için içerik sınırı

        // Eşzamanlılık (diversite) faktörü ks — üst panosu olan her panonun sağ üst köşesinde.
        if (node.UstPano != null)
        {
            bool ksTam = Math.Abs(node.EszamanlilikFaktoru - 1.0) < 0.001;
            var ksEtiket = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0C1A0C")),
                BorderBrush = new SolidColorBrush(Color.Parse(ksTam ? "#4A6A38" : "#E0A030")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Padding = new Avalonia.Thickness(3, 0),
                Child = new TextBlock
                {
                    Text = $"ks {node.EszamanlilikFaktoru:0.##}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.Parse(ksTam ? "#9CC080" : "#F0C060"))
                }
            };
            ToolTip.SetTip(ksEtiket, $"Eşzamanlılık (diversite) faktörü: {node.EszamanlilikFaktoru:0.##}\n" +
                $"Bağlı akım {node.GirisAkimA:0.#} A → talep akım {node.TalepAkimA:0.#} A");
            Canvas.SetLeft(ksEtiket, cx + 40);
            Canvas.SetTop(ksEtiket, cy - 32);
            SemaCanvas.Children.Add(ksEtiket);
        }

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = node.Ad,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = isSelected ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal
        });
        stack.Children.Add(new TextBlock
        {
            // Kompanzasyon panosu aktif güç taşımaz → kondansatör bankı kVAr'ını göster (kW yerine).
            Text = node.PanoTipi == PanoTipi.Kompanzasyon
                ? $"{node.ToplamReaktifVAR/1000.0:N1} kVAr"
                : $"{node.ToplamGucW/1000.0:N1} kW",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#8CA070")),
            FontSize = 10
        });
        border.Child = stack;

        // Sağ Tık Menüsü
        var ctx = new ContextMenu();
        
        var miSec = new MenuItem { Header = "Panoyu Seç" };
        miSec.Click += (_, _) => {
            _vm.SeciliPano = node;
            PanoSeciciGuncelle();
            SemaCiz();
        };

        var miEkle = new MenuItem { Header = "Yeni Alt Pano Ekle" };
        miEkle.Click += (_, _) => {
            string yeniAd = $"{node.Ad}.{node.AltPanolar.Count + 1}";
            var tip = PanoTipi.TaliPano;
            
            // ViewModel'a eklerken root'a değil node altına bağlamak için:
            // Manuel ekleme:
            var pano = new Pano { Ad = yeniAd, Kat = node.Kat, PanoTipi = tip, UstPano = node, UstPanoAd = node.Ad };
            node.AltPanolar.Add(pano);
            _vm.Panolar.Add(pano);
            _vm.Proje.TumPanolar.Add(pano);
            
            _vm.SeciliPano = pano;
            PanoSeciciGuncelle();
            SemaCiz();
        };

        var miKablo = new MenuItem { Header = "Kablo Mesafesini Değiştir" };
        miKablo.Click += (_, _) => {
            _vm.SeciliPano = node;
            PanoSeciciGuncelle();
            SemaCiz();
            KabloMesafe.Focus(); // Üst bardaki textBox'a odaklar
        };

        var miSil = new MenuItem { Header = "Panoyu Sil" };
        miSil.Click += (_, _) => {
            _vm.PanoSil(node);
            _vm.TumunuHesapla();
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();
        };

        // Busbara Bağla — sadece tali/aydınlatma için ve en az 1 busbar varsa
        var miBusbar = new MenuItem { Header = "Busbara Bağla / Bağlantıyı Değiştir" };
        miBusbar.Click += async (_, _) => {
            if (_vm.Busbarlar.Count == 0)
            {
                PanoOzetText.Text = "⚠ Önce bir busbar hattı oluşturun (+ Busbar).";
                return;
            }
            var dlg = new PanoBusbarBaglaDialog(_vm, node);
            await dlg.ShowDialog(this);
            _vm.TumunuHesapla();
            TabloGuncelle();
            SemaCiz();
            PanoSeciciGuncelle();
        };

        // Üst Panoyu Değiştir — arama kutulu dialog ile yeni üst pano seç (kablolu/busbarlı fark etmez)
        var miUstDegistir = new MenuItem { Header = node.UstPano == null ? "Üst Panoya Bağla…" : "Üst Panoyu Değiştir…" };
        miUstDegistir.Click += async (_, _) =>
        {
            try
            {
                var dlg = new UstPanoSecDialog(_vm, node);
                await dlg.ShowDialog(this);
                if (dlg.Degisti)
                {
                    _vm.TumunuHesapla();
                    TabloGuncelle();
                    PanoSeciciGuncelle();
                    SemaCiz();
                }
            }
            catch (Exception ex)
            {
                PanoOzetText.Text = $"⚠ Üst pano değiştirilemedi: {ex.Message}";
            }
        };

        // Pano Düzenle — ad/kat/tip değiştir
        var miDuzenle = new MenuItem { Header = "Panoyu Düzenle (Ad / Kat / Tip)" };
        miDuzenle.Click += async (_, _) => {
            try
            {
                var dlg = new PanoDuzenleDialog(_vm, node);
                await dlg.ShowDialog(this);
                _vm.TumunuHesapla();
                TabloGuncelle();
                PanoSeciciGuncelle();
                SemaCiz();
            }
            catch (Exception ex)
            {
                PanoOzetText.Text = $"⚠ Düzenleme penceresi açılamadı: {ex.Message}";
            }
        };

        var miExcelAktar = new MenuItem
        {
            Header = "Excelden Aktar",
            IsEnabled = node.PanoTipi != PanoTipi.Kompanzasyon && node.PanoTipi != PanoTipi.KontrolPanosu
        };
        miExcelAktar.Click += (_, e) =>
        {
            _vm.SeciliPano = node;
            PanoSeciciGuncelle();
            TabloGuncelle();
            LinyeExcelIceAktar_OnClick(sender: null, e);
        };

        // UPS — kritik pano ekle/düzenle. Bir besleyen pano birden fazla UPS kritik panosu
        // besleyebilir; bu yüzden kritik panoya (teal) sağ-tıklayınca O UPS düzenlenir, normal
        // panoya sağ-tıklayınca her zaman YENİ bir kritik pano eklenir.
        var nodeUps = node.PanoTipi == PanoTipi.UpsPanosu ? _vm.KritikPanoUps(node) : null;
        var besleyenForUps = nodeUps != null ? node.UstPano : node;
        var miUps = new MenuItem
        {
            Header = nodeUps != null
                ? "🔋 UPS'i Düzenle"
                : "🔋 UPS ile Beslenen Kritik Pano Ekle"
        };
        miUps.Click += async (_, _) =>
        {
            if (besleyenForUps == null) return;
            try
            {
                var dlg = new UpsEkleDialog(_vm, besleyenForUps, nodeUps);
                await dlg.ShowDialog(this);
                _vm.TumunuHesapla();
                TabloGuncelle();
                PanoSeciciGuncelle();
                SemaCiz();
            }
            catch (Exception ex)
            {
                PanoOzetText.Text = $"⚠ UPS penceresi açılamadı: {ex.Message}";
            }
        };

        // Kritik Panoya Çevir / Çıkar — yalnız LOKAL modda. Mevcut panoyu üst panosundan UPS ile
        // beslenen kritik panoya dönüştürür (yükleri/alt panoları korunur) ya da geri alır.
        MenuItem? miKritikCevir = null;
        if (!_vm.MerkeziModda)
        {
            var nodeKritikUps = _vm.KritikPanoUps(node);
            if (nodeKritikUps != null)
            {
                miKritikCevir = new MenuItem { Header = "🔋 Kritik Panodan Çıkar" };
                miKritikCevir.Click += (_, _) =>
                {
                    _vm.UpsSil(nodeKritikUps);
                    TabloGuncelle();
                    PanoSeciciGuncelle();
                    SemaCiz();
                };
            }
            else if (node.UstPano != null &&
                     node.PanoTipi is PanoTipi.TaliPano or PanoTipi.AydinlatmaPanosu)
            {
                miKritikCevir = new MenuItem { Header = "🔋 Kritik Panoya Çevir" };
                miKritikCevir.Click += (_, _) =>
                {
                    try
                    {
                        _vm.KritikPanoyaCevir(node);
                        TabloGuncelle();
                        PanoSeciciGuncelle();
                        SemaCiz();
                    }
                    catch (Exception ex)
                    {
                        PanoOzetText.Text = $"⚠ {ex.Message}";
                    }
                };
            }
        }

        // Kat Kritik Panosu Ekle — yalnız MERKEZİ modda ve merkezi UPS dağıtım panosunda.
        MenuItem? miKatKritik = null;
        if (_vm.MerkeziModda && _vm.MerkeziDagitimPanosuMu(node))
        {
            miKatKritik = new MenuItem { Header = "🔋 Kat Kritik Panosu Ekle" };
            miKatKritik.Click += (_, _) =>
            {
                var kat = YeniPanoKat?.SelectedItem as string ?? node.Kat;
                _vm.MerkeziKatKritikPanosuEkle(kat);
                TabloGuncelle();
                PanoSeciciGuncelle();
                SemaCiz();
                PanoOzetText.Text = $"✓ '{kat}' katına kritik pano eklendi. Linyeleri sağ-tık → 'Kritik Panoya Taşı' ile aktarın.";
            };
        }

        // Jeneratör — bu panoyu hedefler. Ana pano: tesis geneli, tali pano: lokal bölge beslemesi.
        var miJenerator = new MenuItem
        {
            Header = _vm.JeneratorGrubuBul(node) != null
                ? "⚡ Jeneratörü Düzenle"
                : node.PanoTipi == PanoTipi.AnaPano
                    ? "⚡ Jeneratör Ekle (tesis geneli)"
                    : "⚡ Jeneratör Ekle (bu bölge)"
        };
        miJenerator.Click += async (_, _) =>
        {
            try
            {
                var dlg = new JeneratorEkleDialog(_vm, node);
                await dlg.ShowDialog(this);
                _vm.TumunuHesapla();
                TabloGuncelle();
                PanoSeciciGuncelle();
                SemaCiz();
            }
            catch (Exception ex)
            {
                PanoOzetText.Text = $"⚠ Jeneratör penceresi açılamadı: {ex.Message}";
            }
        };

        ctx.Items.Add(miSec);
        ctx.Items.Add(miDuzenle);
        ctx.Items.Add(miExcelAktar);
        if (node.PanoTipi != PanoTipi.KontrolPanosu) ctx.Items.Add(miEkle);
        if (node.UstPano != null && node.PanoTipi != PanoTipi.KontrolPanosu) ctx.Items.Add(miKablo);
        // Üst pano bağla/değiştir ve busbara bağla: ana pano (ve transfer) dışındaki tüm panolarda,
        // üst panosu olmayan (bağsız) panolar dahil — böylece bağsız panolar da bağlanabilir.
        bool baglanabilir = node.PanoTipi != PanoTipi.AnaPano
            && node.PanoTipi != PanoTipi.KontrolPanosu
            && !AktifJeneratorTransferPanosuMu(node);
        if (baglanabilir) ctx.Items.Add(miUstDegistir);
        if (baglanabilir) ctx.Items.Add(miBusbar);
        if (!_vm.MerkeziModda && node.PanoTipi != PanoTipi.Kompanzasyon
            && node.PanoTipi != PanoTipi.TransferPanosu
            && node.PanoTipi != PanoTipi.KontrolPanosu)
            ctx.Items.Add(miUps);
        if (miKritikCevir != null) ctx.Items.Add(miKritikCevir);
        if (miKatKritik != null) ctx.Items.Add(miKatKritik);
        bool jeneratorHedefiOlabilir = node.PanoTipi is PanoTipi.AnaPano or PanoTipi.TaliPano
            or PanoTipi.AydinlatmaPanosu or PanoTipi.UpsPanosu;
        if (jeneratorHedefiOlabilir) ctx.Items.Add(miJenerator);
        ctx.Items.Add(new Separator());
        ctx.Items.Add(miSil);
        border.ContextMenu = ctx;

        border.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                _vm.SeciliPano = node;
                PanoSeciciGuncelle();
                SemaCiz();
                e.Handled = true; // pano seçimi pan başlatmasın
            }
        };

        // Kontrol modunda fareyle üzerine gelince eksiklik listesi tooltip olarak görünür.
        if (_kontrolModu)
            ToolTip.SetTip(border, KontrolTooltip(node));

        SemaCanvas.Children.Add(border);

        // Yangın panosu: "Ana şalter öncesi" rozeti (ana pano yüküne katılmaz, ana giriş şalterinden
        // önce beslenir — tesiste elektrik kesilse de yangın panosu beslemede kalır).
        if (node.PanoTipi == PanoTipi.YanginPanosu)
        {
            var yanginRozet = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#3A0E0E")),
                BorderBrush = new SolidColorBrush(Color.Parse("#E05050")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Padding = new Avalonia.Thickness(3, 0),
                Child = new TextBlock
                {
                    Text = "🔥 Ana şalter öncesi",
                    FontSize = 8.5,
                    Foreground = new SolidColorBrush(Color.Parse("#F0A0A0"))
                }
            };
            Canvas.SetLeft(yanginRozet, cx - 60);
            Canvas.SetTop(yanginRozet, cy - 32);
            SemaCanvas.Children.Add(yanginRozet);
        }

        // Ana pano + trafo YOKSA: kutunun üstüne giriş hattı + ana giriş şalteri çiz
        // (trafo varsa giriş şalteri OG bloğunda trafo–ana pano hattında çizilir).
        if (node.PanoTipi == PanoTipi.AnaPano && _vm.Proje.OrtaGerilim == null)
        {
            double topY = cy - 21;
            var lineBrush = new SolidColorBrush(Color.Parse("#5A7A34"));
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(cx, topY - 40),
                EndPoint = new Avalonia.Point(cx, topY),
                Stroke = lineBrush, StrokeThickness = 2
            });
            GirisSalteriCiz(cx, topY - 22, node.GirisSalteriA, lineBrush);
        }

        // UPS kritik panosu ise: kutunun üstüne UPS + akü rozeti çiz
        var upsKritik = _vm.KritikPanoUps(node);
        if (node.PanoTipi == PanoTipi.UpsPanosu && upsKritik != null)
        {
            DrawUpsRozet(upsKritik, cx, cy);
        }

        // Bu pano bir jeneratör hedefiyse (ana pano = tesis geneli, tali pano = lokal bölge):
        // sağa transfer panosu + jeneratör çiz
        if (JeneratorHedefPanosuMu(node))
        {
            DrawJenerator(node, cx, cy, startX + SubtreeColCount(node) * ColWidth);
        }
    }

    /// <summary>
    /// UPS kritik panosunun üstüne UPS cihazı + akü grubu bilgi rozetini çizer.
    /// </summary>
    private void DrawUpsRozet(Ups ups, double cx, double cy)
    {
        var upsTanim = _vm.UpsTipleri.FirstOrDefault(u => u.Ad == ups.UpsTanimAd);
        double anmaGucuKVA = ups.SeciliUpsGucKVA > 0 ? ups.SeciliUpsGucKVA : upsTanim?.GucKVA ?? 0;
        string kvaTxt = anmaGucuKVA > 0 ? $"{anmaGucuKVA:N0}kVA" : $"~{ups.GerekliKVA:N0}kVA";

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(new TextBlock
        {
            Text = $"🔋 {(ups.Merkezi ? "MERKEZİ UPS" : "UPS")} {kvaTxt} • {ups.OtonomiDk}dk",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.Parse("#7AD8E0")),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"akü {ups.SeriBlokSayisi}S×{ups.ParalelStringSayisi}P • {ups.ToplamAh:N0}Ah",
            FontSize = 8,
            Foreground = new SolidColorBrush(Color.Parse("#5AA8B0")),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var rozet = new Button
        {
            Background = new SolidColorBrush(Color.Parse("#0A2228")),
            BorderBrush = new SolidColorBrush(Color.Parse(ups.YukYeterliMi && ups.AkuYeterliMi ? "#3A8A98" : "#A85020")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Padding = new Avalonia.Thickness(5, 2),
            Content = stack,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        rozet.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        rozet.Click += async (_, _) =>
        {
            var besleyen = _vm.UpsKritikPanoBul(ups)?.UstPano;
            if (besleyen == null) return;
            var dlg = new UpsEkleDialog(_vm, besleyen, ups);
            await dlg.ShowDialog(this);
            _vm.TumunuHesapla();
            TabloGuncelle();
            PanoSeciciGuncelle();
            SemaCiz();
        };
        Canvas.SetLeft(rozet, cx - 60);
        Canvas.SetTop(rozet, cy - 52);
        SemaCanvas.Children.Add(rozet);

        // Rozet → pano bağlantı çizgisi (kısa dikey)
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(cx, cy - 30),
            EndPoint = new Avalonia.Point(cx, cy - 21),
            Stroke = new SolidColorBrush(Color.Parse("#3A8A98")),
            StrokeThickness = 2
        });
    }

    /// <summary>
    /// Ana panonun sağına senkron panosu + jeneratör ünitelerini çizer.
    /// </summary>
    private void DrawJenerator(Pano anaPano, double anaCx, double anaCy, double subtreeRight)
    {
        var grup = _vm.Proje.JeneratorGruplari.FirstOrDefault(g => g.AnaPanoAd == anaPano.Ad);
        var legacy = grup == null ? _vm.Proje.Jenerator : null;
        var uniteler = grup?.Jeneratorler ?? (legacy == null ? new List<Jenerator>() : new List<Jenerator> { legacy });
        if (grup == null && legacy == null) return;

        var salterAd = grup?.TransferSalterAd ?? legacy?.TransferSalterAd;
        var salter = _vm.TransferSalterTipleri.FirstOrDefault(t => t.Ad == salterAd);

        double anaRightEdge = anaCx + 65; // pano width/2
        double senkronCx = Math.Max(anaRightEdge + 90, subtreeRight + 90);
        // jenCx: senkron barası ile jeneratör dairesi arasında kablo + kesit etiketi için yeterli boşluk
        double jenCx = senkronCx + 230;

        // Jeneratör katı: grup.Kat (veya eski Jenerator.Kat) doluysa o kat bandının Y'si; değilse ana pano Y'si.
        string jenKat = (grup?.Kat ?? legacy?.Kat ?? "").Trim();
        bool jenKatGecerli = !string.IsNullOrEmpty(jenKat)
            && (_vm.Proje.Bina?.KatEtiketleri()?.Contains(jenKat) ?? false);
        double yLevel = jenKatGecerli ? FloorY(jenKat) : anaCy;

        // ── Ana pano → Senkron pano kablosu ── (kat farklıysa ortogonal güzergah)
        var jenKabloBrush = new SolidColorBrush(Color.Parse("#E0B048"));
        if (Math.Abs(yLevel - anaCy) < 0.5)
        {
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(anaRightEdge, yLevel),
                EndPoint = new Avalonia.Point(senkronCx - 55, yLevel),
                Stroke = jenKabloBrush, StrokeThickness = 2
            });
        }
        else
        {
            double midX = Math.Max(anaRightEdge + 24, senkronCx - 95);
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(anaRightEdge, anaCy), EndPoint = new Avalonia.Point(midX, anaCy), Stroke = jenKabloBrush, StrokeThickness = 2 });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(midX, anaCy), EndPoint = new Avalonia.Point(midX, yLevel), Stroke = jenKabloBrush, StrokeThickness = 2 });
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(midX, yLevel), EndPoint = new Avalonia.Point(senkronCx - 55, yLevel), Stroke = jenKabloBrush, StrokeThickness = 2 });
        }

        double senkronHedefMesafeM = grup?.SenkronHedefPanoKabloMesafeM
            ?? legacy?.SenkronHedefPanoKabloMesafeM
            ?? 10;
        string senkronHedefKesit = grup?.SenkronHedefKabloKesiti ?? legacy?.SenkronHedefKabloKesiti ?? "";
        var senkronHedefMesafeLbl = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1208")),
            Padding = new Avalonia.Thickness(3, 1),
            Child = new TextBlock
            {
                Text = $"{senkronHedefMesafeM:N1} m{KesitEk(senkronHedefKesit)}",
                FontSize = 8.5,
                Foreground = new SolidColorBrush(Color.Parse("#FFD888"))
            }
        };
        Canvas.SetLeft(senkronHedefMesafeLbl, anaRightEdge + 8);
        Canvas.SetTop(senkronHedefMesafeLbl, anaCy + 3);
        SemaCanvas.Children.Add(senkronHedefMesafeLbl);

        // Şalter etiketi (ana pano çıkış kablosu üzerinde, ana pano seviyesinde)
        if (salter != null)
        {
            var salterLbl = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1208")),
                BorderBrush = new SolidColorBrush(Color.Parse("#5A3A18")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(3),
                Padding = new Avalonia.Thickness(4, 1),
                Child = new TextBlock
                {
                    Text = $"{salter.AnmaAkimA}A {salter.Tipi}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.Parse("#E0B048"))
                }
            };
            Canvas.SetLeft(salterLbl, anaRightEdge + 8);
            Canvas.SetTop(salterLbl, anaCy - 18);
            SemaCanvas.Children.Add(salterLbl);
        }

        // ── Senkron pano kutusu ──
        var senkronStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        senkronStack.Children.Add(new TextBlock
        {
            Text = "⇄ Senkron",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        senkronStack.Children.Add(new TextBlock
        {
            Text = grup?.SenkronPanoAd ?? legacy?.TransferPanoAd ?? "Senkron Pano",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#E0C088")),
            FontSize = 9
        });
        senkronStack.Children.Add(new TextBlock
        {
            Text = $"{uniteler.Count} ünite • {(grup?.ToplamGucKVA ?? legacy?.HesaplananYukKVA ?? 0):N0} kVA",
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#FFD888")),
            FontSize = 9
        });
        var senkronBorder = new Border
        {
            Width = 110,
            Height = 52,
            CornerRadius = new Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#5A3A18")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0B048")),
            BorderThickness = new Avalonia.Thickness(1.5),
            Child = senkronStack
        };
        Canvas.SetLeft(senkronBorder, senkronCx - 55);
        Canvas.SetTop(senkronBorder, yLevel - 26);
        SemaCanvas.Children.Add(senkronBorder);
        IcerikGenislet(senkronCx - 55, yLevel - 26, senkronCx + 55, yLevel + 26);

        // ── Besleme kapsamı rozeti (senkron kutusunun üstünde) ──
        bool lokal = (grup?.BeslemeKapsami ?? JeneratorBeslemeKapsami.TesisGeneli)
            == JeneratorBeslemeKapsami.LokalBolge;
        var kapsamBadge = new Border
        {
            Background = new SolidColorBrush(Color.Parse(lokal ? "#0E2A33" : "#0E2A0C")),
            BorderBrush = new SolidColorBrush(Color.Parse(lokal ? "#3AA0C0" : "#6AB048")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Padding = new Avalonia.Thickness(5, 1),
            Child = new TextBlock
            {
                Text = (lokal ? $"LOKAL BÖLGE → {anaPano.Ad}" : "TESİS GENELİ")
                       + (jenKatGecerli ? $"  •  Kat: {jenKat}" : ""),
                FontSize = 8.5,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse(lokal ? "#8AD0E8" : "#A8E088"))
            }
        };
        Canvas.SetLeft(kapsamBadge, senkronCx - 55);
        Canvas.SetTop(kapsamBadge, yLevel - 26 - 17);
        SemaCanvas.Children.Add(kapsamBadge);
        IcerikGenislet(senkronCx - 55, yLevel - 26 - 17, senkronCx + 75, yLevel - 26);

        // ── Senkron pano → jeneratör barası ──
        double trunkX = senkronCx + 92;
        double ilkY = yLevel - (uniteler.Count - 1) * 36;
        double sonY = yLevel + (uniteler.Count - 1) * 36;
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(senkronCx + 55, yLevel),
            EndPoint = new Avalonia.Point(trunkX, yLevel),
            Stroke = new SolidColorBrush(Color.Parse("#E0B048")),
            StrokeThickness = 2
        });
        if (uniteler.Count > 1)
        {
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(trunkX, ilkY),
                EndPoint = new Avalonia.Point(trunkX, sonY),
                Stroke = new SolidColorBrush(Color.Parse("#E0B048")),
                StrokeThickness = 2
            });
        }

        if (uniteler.Count == 0)
        {
            uniteler = new List<Jenerator> { new() };
        }

        for (int i = 0; i < uniteler.Count; i++)
        {
            var jen = uniteler[i];
            var jenTanim = _vm.JeneratorTipleri.FirstOrDefault(j => j.Ad == jen.JeneratorTanimAd);
            double unitY = yLevel + (i - (uniteler.Count - 1) / 2.0) * 72;

            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(trunkX, unitY),
                EndPoint = new Avalonia.Point(jenCx - 65, unitY),
                Stroke = new SolidColorBrush(Color.Parse("#E0B048")),
                StrokeThickness = 2
            });

            // Mesafe + kesit iki satırda (tek satır jeneratör dairesinin altında kalıyordu).
            var unitKabloStack = new StackPanel { Spacing = 0 };
            unitKabloStack.Children.Add(new TextBlock
            {
                Text = $"{jen.SenkronPanoKabloMesafeM:N1} m",
                FontSize = 8.5,
                Foreground = new SolidColorBrush(Color.Parse("#FFD888"))
            });
            if (!string.IsNullOrWhiteSpace(jen.SenkronKabloKesiti))
                unitKabloStack.Children.Add(new TextBlock
                {
                    Text = jen.SenkronKabloKesiti.Trim(),
                    FontSize = 8.5,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#FFE0A0"))
                });
            var unitMesafeLbl = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1208")),
                Padding = new Avalonia.Thickness(2, 0),
                Child = unitKabloStack
            };
            Canvas.SetLeft(unitMesafeLbl, trunkX + 6);
            Canvas.SetTop(unitMesafeLbl, unitY - 18);
            SemaCanvas.Children.Add(unitMesafeLbl);

            var jenStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            jenStack.Children.Add(new TextBlock
            {
                Text = $"⚡ JEN {i + 1}",
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });
            if (jenTanim != null)
            {
                var mod = grup?.Mod ?? jen.Mod;
                double kVA = JeneratorHesapServisi.JeneratorGucKVA(jenTanim, mod);
                string modKisa = mod == JeneratorMod.Prime ? "PRP" : "ESP";
                jenStack.Children.Add(new TextBlock
                {
                    Text = $"{kVA:N0} kVA {modKisa}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse("#FFD888")),
                    FontSize = 10
                });
                jenStack.Children.Add(new TextBlock
                {
                    Text = jenTanim.Ad,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse("#E0C088")),
                    FontSize = 9
                });
            }
            else
            {
                jenStack.Children.Add(new TextBlock
                {
                    Text = "(otomatik)",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse("#E0C088")),
                    FontSize = 9
                });
            }

            bool yeterli = grup?.YukYeterliMi ?? jen.YukYeterliMi;
            var jenBorder = new Border
            {
                Width = 130,
                Height = 60,
                CornerRadius = new Avalonia.CornerRadius(30),
                Background = new SolidColorBrush(Color.Parse(yeterli ? "#7A5A18" : "#7A2018")),
                BorderBrush = new SolidColorBrush(Color.Parse("#F0C868")),
                BorderThickness = new Avalonia.Thickness(2),
                Child = jenStack,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            Canvas.SetLeft(jenBorder, jenCx - 65);
            Canvas.SetTop(jenBorder, unitY - 30);
            IcerikGenislet(jenCx - 65, unitY - 30, jenCx + 65, unitY + 30);

            // Tıklama → düzenleme
            jenBorder.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(jenBorder).Properties.IsLeftButtonPressed)
                {
                    Jenerator_OnClick(s, e);
                    e.Handled = true; // jeneratör tıklaması pan başlatmasın
                }
            };

            SemaCanvas.Children.Add(jenBorder);
        }
    }

    /// <summary>Ana giriş şalteri sembolü (hat üzerinde kesici kare + çapraz kontak + anma akımı).
    /// Trafo–ana pano arası veya (trafo yoksa) ana pano girişinde kullanılır.</summary>
    private void GirisSalteriCiz(double x, double y, int anmaA, IBrush lineBrush)
    {
        var kare = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = 12, Height = 12,
            Stroke = lineBrush, StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#1A1208"))
        };
        Canvas.SetLeft(kare, x - 6);
        Canvas.SetTop(kare, y - 6);
        SemaCanvas.Children.Add(kare);
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(x - 4, y + 4),
            EndPoint = new Avalonia.Point(x + 4, y - 4),
            Stroke = lineBrush, StrokeThickness = 1.5
        });

        var lbl = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0C1A0C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#80B050")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Padding = new Avalonia.Thickness(3, 0),
            Child = new TextBlock
            {
                Text = anmaA > 0 ? $"Giriş Şalteri {anmaA}A" : "Giriş Şalteri",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#A0D070"))
            }
        };
        Canvas.SetLeft(lbl, x + 10);
        Canvas.SetTop(lbl, y - 8);
        SemaCanvas.Children.Add(lbl);
    }

    // ── Orta gerilim bloğu: şebeke → OG hücreleri → trafo → AG bağlantı → ana pano ──
    // ogTopY: bloğun çizim üst-Y'si (Trafo Katı'na göre en üstte 0 ya da seçilen kat üstündeki bant).
    private void DrawOrtaGerilim(OrtaGerilimSistemi og, double cx, double anaCy, double kabloKanalX, double ogTopY)
    {
        var trafo = _vm.Kutuphane.Trafolar.FirstOrDefault(t => t.Ad == og.TrafoTanimAd);

        var ogBrush = new SolidColorBrush(Color.Parse("#D8843A"));
        var lineBrush = new SolidColorBrush(Color.Parse("#C8702E"));

        // Dikey seviyeler (ogTopY ofseti ile — blok seçilen kata göre aşağı kayabilir)
        double sebekeY = ogTopY + 22;     // şebeke kutusu merkez
        double busY = ogTopY + 76;        // OG bara çizgisi
        double cellTopY = ogTopY + 98;    // hücre kutusu üst
        const double cellH = 38;
        double cellBottomY = cellTopY + cellH;
        double elbowY = ogTopY + 152;     // trafo koruma → trafo dirsek seviyesi
        double trafoY = ogTopY + 182;     // trafo merkez

        // Hücre dizilimi — verilen sırada, cx etrafında ortalanır. Trafo koruma hücresi varsa
        // trafo onun altına bağlanır; yoksa bara merkezine bağlanır.
        var hucreler = og.Hucreler;
        int n = hucreler.Count;
        const double cellPitch = 92;
        const double cellW = 78;
        double lineupW = n * cellPitch;
        double xStart = cx - lineupW / 2.0;

        double busLeft = Math.Min(xStart, cx - 70);
        double busRight = Math.Max(xStart + lineupW, cx + 70);

        // ── Şebeke kutusu (en üst) ──
        string sebekeBilgi = $"⚡ ŞEBEKE  {og.OgGerilimKV:0.#} kV";
        var sebekeBorder = OgKutu(sebekeBilgi,
            og.SebekeKisaDevreMVA > 0 ? $"Skq={og.SebekeKisaDevreMVA:0.#} MVA" : $"Ik={og.SebekeIkKA:0.#} kA",
            "#2A1A08", "#C8702E", "#E8B070", 150, 40);
        Canvas.SetLeft(sebekeBorder, cx - 75);
        Canvas.SetTop(sebekeBorder, sebekeY - 20);
        SemaCanvas.Children.Add(sebekeBorder);

        // Şebeke → bara dikey
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(cx, sebekeY + 20), EndPoint = new Avalonia.Point(cx, busY), Stroke = lineBrush, StrokeThickness = 2 });
        var sebekeHucreKabloLbl = OgHatEtiketi(
            OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.SebekeHucreXlpeKablosu));
        Canvas.SetLeft(sebekeHucreKabloLbl, cx + 7);
        Canvas.SetTop(sebekeHucreKabloLbl, sebekeY + 22);
        SemaCanvas.Children.Add(sebekeHucreKabloLbl);

        // ── OG bara (yatay) ──
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(busLeft, busY), EndPoint = new Avalonia.Point(busRight, busY), Stroke = ogBrush, StrokeThickness = 3 });

        // ── Hücreler ──
        double trafoKorumaX = cx; // varsayılan: bara merkezi
        bool trafoKorumaBulundu = false;
        for (int i = 0; i < n; i++)
        {
            var h = hucreler[i];
            var tanim = _vm.Kutuphane.OgHucreleri.FirstOrDefault(x => x.Ad == h.HucreTanimAd);
            double xi = xStart + cellPitch * (i + 0.5);

            // Bara → hücre stub + manevra sembolü
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(xi, busY), EndPoint = new Avalonia.Point(xi, cellTopY), Stroke = lineBrush, StrokeThickness = 2 });
            OgHucreSembol(xi, (busY + cellTopY) / 2.0, tanim?.KorumaTipi ?? OgHucreKorumaTipi.YukAyirici, lineBrush);

            // Hücre kutusu
            string ust = OgHucreKisaAd(h.HucreTipi);
            string alt = tanim != null ? $"{tanim.AnmaGerilimKV:0.#}kV {tanim.AnmaAkimA}A" : "(seçilmedi)";
            string renkBorder = h.AnmaAkimYeterliMi ? "#8A5A2A" : "#A03020";
            var box = OgKutu(ust, alt, "#241608", renkBorder, "#E8B070", cellW, cellH);
            Canvas.SetLeft(box, xi - cellW / 2.0);
            Canvas.SetTop(box, cellTopY);
            SemaCanvas.Children.Add(box);

            if (!trafoKorumaBulundu && h.HucreTipi == OgHucreTipi.TrafoKoruma)
            {
                trafoKorumaX = xi;
                trafoKorumaBulundu = true;
            }
        }

        // ── Trafo koruma hücresi → trafo (dirsek) ──
        double trafoBaslangicX = trafoKorumaBulundu ? trafoKorumaX : cx;
        double trafoBaslangicY = trafoKorumaBulundu ? cellBottomY : busY;
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(trafoBaslangicX, trafoBaslangicY), EndPoint = new Avalonia.Point(trafoBaslangicX, elbowY), Stroke = lineBrush, StrokeThickness = 2 });
        if (Math.Abs(trafoBaslangicX - cx) > 0.5)
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(trafoBaslangicX, elbowY), EndPoint = new Avalonia.Point(cx, elbowY), Stroke = lineBrush, StrokeThickness = 2 });
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(cx, elbowY), EndPoint = new Avalonia.Point(cx, trafoY - 18), Stroke = lineBrush, StrokeThickness = 2 });
        var hucreTrafoKabloLbl = OgHatEtiketi(
            OrtaGerilimHesapServisi.XlpeKabloEtiketi(og.HucreTrafoXlpeKablosu));
        Canvas.SetLeft(hucreTrafoKabloLbl, Math.Min(trafoBaslangicX, cx) + 7);
        Canvas.SetTop(hucreTrafoKabloLbl, elbowY - 16);
        SemaCanvas.Children.Add(hucreTrafoKabloLbl);

        // ── Trafo sembolü (IEC iki daire) + etiket ──
        var c1 = new Avalonia.Controls.Shapes.Ellipse
        { Width = 26, Height = 26, Stroke = new SolidColorBrush(Color.Parse("#E8A040")), StrokeThickness = 2, Fill = Brushes.Transparent };
        Canvas.SetLeft(c1, cx - 13); Canvas.SetTop(c1, trafoY - 18);
        var c2 = new Avalonia.Controls.Shapes.Ellipse
        { Width = 26, Height = 26, Stroke = new SolidColorBrush(Color.Parse("#E8A040")), StrokeThickness = 2, Fill = Brushes.Transparent };
        Canvas.SetLeft(c2, cx - 13); Canvas.SetTop(c2, trafoY - 4);
        SemaCanvas.Children.Add(c1);
        SemaCanvas.Children.Add(c2);

        // Trafo bilgi etiketi (sağ tarafta)
        string trafoTipAd = trafo == null ? "(tip seçilmedi)" : OgTrafoTipKisa(trafo.Tip);
        string trafoBilgi = trafo == null ? "TRAFO" : $"TRAFO {trafo.GucKVA:0} kVA";
        string trafoAlt = trafo == null ? "" :
            $"{trafoTipAd} • {trafo.PrimerGerilimKV:0.#}/{trafo.SekonderGerilimV / 1000.0:0.###}kV • uk%{trafo.UkYuzde:0.#}";
        var trafoLbl = OgKutu(trafoBilgi, trafoAlt, "#3A2008", og.TrafoYeterliMi ? "#E8A040" : "#A03020", "#F0C088", 190, 40);
        Canvas.SetLeft(trafoLbl, cx + 24);
        Canvas.SetTop(trafoLbl, trafoY - 10);
        trafoLbl.Cursor = new Cursor(StandardCursorType.Hand);
        trafoLbl.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(trafoLbl).Properties.IsLeftButtonPressed) { OrtaGerilim_OnClick(s, e); e.Handled = true; }
        };
        SemaCanvas.Children.Add(trafoLbl);

        // ── Trafo AG çıkışı → ana pano (bağlantı etiketi + pano dışı kanal rotası) ──
        double trafoAltY = trafoY + 26;
        double anaTopY = anaCy - 21;
        double girisY = anaTopY - 18;
        double kanalX = Math.Abs(kabloKanalX - cx) < 8 ? cx - ColWidth : kabloKanalX;

        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(cx, trafoAltY), EndPoint = new Avalonia.Point(cx, trafoAltY + 14), Stroke = lineBrush, StrokeThickness = 2 });
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(cx, trafoAltY + 14), EndPoint = new Avalonia.Point(kanalX, trafoAltY + 14), Stroke = lineBrush, StrokeThickness = 2 });
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(kanalX, trafoAltY + 14), EndPoint = new Avalonia.Point(kanalX, girisY), Stroke = lineBrush, StrokeThickness = 2 });
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(kanalX, girisY), EndPoint = new Avalonia.Point(cx, girisY), Stroke = lineBrush, StrokeThickness = 2 });
        SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
        { StartPoint = new Avalonia.Point(cx, girisY), EndPoint = new Avalonia.Point(cx, anaTopY), Stroke = lineBrush, StrokeThickness = 2 });

        // Trafo–ana pano arası giriş şalteri (ana giriş şalteri) — trafo AG çıkış akımına göre.
        var anaOgPano = _vm.Proje.TumPanolar.FirstOrDefault(p => p.Ad == og.AnaPanoAd)
            ?? _vm.Proje.KokPano;
        GirisSalteriCiz(cx, (girisY + anaTopY) / 2.0, anaOgPano?.GirisSalteriA ?? 0, lineBrush);

        string baglantiMetin = OgBaglantiKisa(og.CikisBaglanti);
        var baglantiLbl = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1208")),
            BorderBrush = new SolidColorBrush(Color.Parse("#8A5A2A")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Padding = new Avalonia.Thickness(4, 1),
            Child = new TextBlock { Text = baglantiMetin, FontSize = 9, Foreground = new SolidColorBrush(Color.Parse("#E8B070")) }
        };
        Canvas.SetLeft(baglantiLbl, Math.Min(kanalX, cx) + 6);
        Canvas.SetTop(baglantiLbl, girisY - 20);
        SemaCanvas.Children.Add(baglantiLbl);

        // ── Köşk başlık rozeti (sol üst) ──
        var koskLbl = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#241608")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C8702E")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(6, 2),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(og.TrafoKat)
                    ? $"🏭 Köşk: {OgKoskKisa(og.KoskTipi)}"
                    : $"🏭 Köşk: {OgKoskKisa(og.KoskTipi)}  •  Kat: {og.TrafoKat}",
                FontSize = 10, Foreground = new SolidColorBrush(Color.Parse("#E8B070")), FontWeight = Avalonia.Media.FontWeight.SemiBold
            }
        };
        Canvas.SetLeft(koskLbl, busLeft);
        Canvas.SetTop(koskLbl, ogTopY + 2);
        SemaCanvas.Children.Add(koskLbl);

        IcerikGenislet(Math.Min(busLeft - 10, kanalX - 12), ogTopY, busRight + 320, Math.Max(trafoAltY + 10, anaTopY + 4));
    }

    private static Border OgHatEtiketi(string text)
        => new()
        {
            Background = new SolidColorBrush(Color.Parse("#1A1208")),
            BorderBrush = new SolidColorBrush(Color.Parse("#6A421E")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Padding = new Avalonia.Thickness(3, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.Parse("#E8B070"))
            }
        };

    private static Border OgKutu(string ustText, string altText, string bg, string border, string fg, double w, double h)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = ustText, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
            Foreground = Brushes.White, FontSize = 9.5, FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        if (!string.IsNullOrEmpty(altText))
            stack.Children.Add(new TextBlock
            {
                Text = altText, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse(fg)), FontSize = 8.5
            });
        return new Border
        {
            Width = w, Height = h, CornerRadius = new Avalonia.CornerRadius(5),
            Background = new SolidColorBrush(Color.Parse(bg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Avalonia.Thickness(1.5),
            Child = stack
        };
    }

    private void OgHucreSembol(double x, double y, OgHucreKorumaTipi tip, IBrush brush)
    {
        if (tip == OgHucreKorumaTipi.Kesicili)
        {
            // Kesici: küçük kare
            var sq = new Avalonia.Controls.Shapes.Rectangle
            { Width = 8, Height = 8, Stroke = brush, StrokeThickness = 1.5, Fill = Brushes.Transparent };
            Canvas.SetLeft(sq, x - 4); Canvas.SetTop(sq, y - 4);
            SemaCanvas.Children.Add(sq);
        }
        else
        {
            // Yük ayırıcı: kısa eğik çizgi
            SemaCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            { StartPoint = new Avalonia.Point(x - 4, y + 5), EndPoint = new Avalonia.Point(x + 5, y - 5), Stroke = brush, StrokeThickness = 1.5 });
            if (tip == OgHucreKorumaTipi.YukAyiriciSigortali)
            {
                // Sigorta: küçük dikdörtgen
                var fr = new Avalonia.Controls.Shapes.Rectangle
                { Width = 6, Height = 10, Stroke = brush, StrokeThickness = 1, Fill = Brushes.Transparent };
                Canvas.SetLeft(fr, x + 4); Canvas.SetTop(fr, y - 5);
                SemaCanvas.Children.Add(fr);
            }
        }
    }

    private static string OgHucreKisaAd(OgHucreTipi t) => t switch
    {
        OgHucreTipi.Giris => "GİRİŞ",
        OgHucreTipi.Olcu => "ÖLÇÜ",
        OgHucreTipi.BaraBaglama => "BARA BAĞ.",
        OgHucreTipi.Cikis => "ÇIKIŞ",
        OgHucreTipi.TrafoKoruma => "TR. KORUMA",
        _ => t.ToString()
    };

    private static string OgTrafoTipKisa(TrafoTipi t) => t switch
    {
        TrafoTipi.Yagli => "Yağlı",
        TrafoTipi.Hermetik => "Hermetik",
        TrafoTipi.KuruTip => "Kuru Tip",
        _ => t.ToString()
    };

    private static string OgKoskKisa(KoskTipi k) => k switch
    {
        KoskTipi.BinaAlti => "Bina Altı",
        KoskTipi.DisMekanBetonKosk => "Dış Mekan Beton Köşk",
        KoskTipi.HariciYapi => "Harici Yapı",
        _ => k.ToString()
    };

    private static string OgBaglantiKisa(TrafoCikisBaglanti b)
    {
        string tip = b.Tip switch
        {
            TrafoCikisBaglantiTipi.Kablo => $"Kablo {b.KesitMm2}".Trim(),
            TrafoCikisBaglantiTipi.Bar24 => $"Bar-24 {b.ReferansAd}".Trim(),
            TrafoCikisBaglantiTipi.Hucre => "AG Hücre",
            _ => "Bağlantı"
        };
        return $"{tip} • {b.UzunlukM:0.#}m";
    }

    private async void OrtaGerilim_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OrtaGerilimDiyaloguAc();
    }

    private async Task OrtaGerilimDiyaloguAc()
    {
        // OG, ana panonun üstündeki besleme katmanıdır — önce bir ana pano bulunmalı.
        var anaPano = _vm.Proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano)
                      ?? _vm.Proje.KokPano
                      ?? _vm.Proje.TumPanolar.FirstOrDefault(p => p.UstPano == null);
        if (anaPano == null)
        {
            PanoOzetText.Text = "⚠ Önce bir ana pano ekleyin. OG sistemi ana panonun üstüne bağlanır.";
            return;
        }

        var dlg = new OrtaGerilimDialog(_vm, anaPano);
        await dlg.ShowDialog(this);
        PanoSeciciGuncelle();
        TabloGuncelle();
        SemaCizGecikmeli();
    }

}
