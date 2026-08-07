using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    // ─── Oda polygon düzenleme ────────────────────────────────────────────

    // ─── Çoklu oda seçimi ─────────────────────────────────────────────────

    /// Cmd/Ctrl ile seçilen odalar. Canvas highlight'ı SeciliOdaIdleri ile sürülür.
    public List<BasitProjeOda> SeciliOdalar { get; } = new();

    /// Seçili odaların Id'leri — GeometryPreviewControl.SelectedRoomIds'e bağlanır.
    /// Öncelik: 1) Pano seçiliyse panoyu bağlı odalar  2) Mahal listesi vurgusu  3) Normal seçim
    public IEnumerable<string> SeciliOdaIdleri
    {
        get
        {
            if (SeciliEleman?.Tip == BpcElemanTipi.KatPanosu)
            {
                if (_panoOdaVurguOdaId != null)
                    return [_panoOdaVurguOdaId];
                return SeciliEleman.BagliOdaIds;
            }
            if (_mahalListesiVurguOdaId != null)
                return [_mahalListesiVurguOdaId];
            return new HashSet<string>(SeciliOdalar.Select(o => o.Id));
        }
    }

    // Kat panosu bağlı oda listesinden gelen tek-oda vurgu kaynağı.
    private string? _panoOdaVurguOdaId;

    // Mahal listesi penceresinden gelen tek-oda vurgu kaynağı.
    private string? _mahalListesiVurguOdaId;

    public void PanoOdaVurgula(string? odaId)
    {
        _panoOdaVurguOdaId = odaId;
        GuncelleOdaVurgu();
    }

    /// Mahal listesinden tıklanan oda canvas'ta vurgulanır. null → vurgu kaldır.
    public void MahalListesiOdaVurgula(string? odaId)
    {
        _mahalListesiVurguOdaId = odaId;
        GuncelleOdaVurgu();
    }

    /// OnSeciliOdaChanged'in SeciliOdalar'ı sıfırlamasını engellemek için (çoklu seçim sırasında).
    private bool _odaCokluSecimGuncelleniyor;

    public void GuncelleOdaVurgu() => OnPropertyChanged(nameof(SeciliOdaIdleri));

    [RelayCommand]
    private void GeriAl()
    {
        if (_undoStack.Count == 0) return;
        var snapshot = _undoStack.Pop();
        var kat = SnapshotKatiniBul(snapshot.KatId, snapshot.KatNo);
        if (kat == null) return;
        _redoStack.Push(new KatUndoSnapshot(
            kat.Id,
            kat.KatNo,
            SeciliOda?.Id,
            kat.Odalar.Select(CloneOda).ToList()));
        kat.Odalar.Clear();
        foreach (var o in snapshot.Odalar) kat.Odalar.Add(o);
        OnPropertyChanged(nameof(Odalar));
        SeciliKat = kat;
        SeciliOda = snapshot.SeciliOdaId is { } id
            ? kat.Odalar.FirstOrDefault(o => o.Id == id)
            : kat.Odalar.FirstOrDefault();
        Guncelle3DGorunum();
    }

    [RelayCommand]
    private void IleriAl()
    {
        if (_redoStack.Count == 0) return;
        var snapshot = _redoStack.Pop();
        var kat = SnapshotKatiniBul(snapshot.KatId, snapshot.KatNo);
        if (kat == null) return;
        _undoStack.Push(new KatUndoSnapshot(
            kat.Id,
            kat.KatNo,
            SeciliOda?.Id,
            kat.Odalar.Select(CloneOda).ToList()));
        kat.Odalar.Clear();
        foreach (var o in snapshot.Odalar) kat.Odalar.Add(o);
        OnPropertyChanged(nameof(Odalar));
        SeciliKat = kat;
        SeciliOda = snapshot.SeciliOdaId is { } id
            ? kat.Odalar.FirstOrDefault(o => o.Id == id)
            : kat.Odalar.FirstOrDefault();
        Guncelle3DGorunum();
    }

    [RelayCommand]
    private void GeriAlGenel()
    {
        if (_elemanUndoStack.Count > 0)
            ElemanGeriAl();
        else
            GeriAl();
    }

    [RelayCommand]
    private void IleriAlGenel()
    {
        if (_elemanRedoStack.Count > 0)
            ElemanIleriAl();
        else
            IleriAl();
    }

    private static BasitProjeOda CloneOda(BasitProjeOda o) => new()
    {
        Id      = o.Id,
        Ad      = o.Ad,
        AlanM2  = o.AlanM2,
        GecisSecili = o.GecisSecili,
        AsmaTavan = o.AsmaTavan,
        MahalListesineDahil = o.MahalListesineDahil,
        Poligon = o.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
    };

    private BasitProjeKat? SnapshotKatiniBul(string katId, int katNo)
        => Katlar.FirstOrDefault(k => k.Id == katId)
           ?? Katlar.FirstOrDefault(k => k.KatNo == katNo);

    private void UndoRedoYiginlariniTemizle()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _elemanUndoStack.Clear();
        _elemanRedoStack.Clear();
    }

    private sealed record KatUndoSnapshot(string KatId, int KatNo, string? SeciliOdaId, List<BasitProjeOda> Odalar);

    private static double AlanHesapla(List<Point2D> poly)
    {
        if (poly.Count < 3) return 0;
        double s = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return System.Math.Abs(s) * 0.5;
    }

    /// Viewport tıklamasını oda seçimine çevirir.
    public void PickRoomAt(double worldX, double worldY, bool multiSelect = false)
    {
        var katSec = SeciliKat;
        if (katSec == null) return;
        double ox = katSec.OriginOffset.X, oy = katSec.OriginOffset.Y;
        var local = new Point2D(worldX - ox, worldY - oy);

        BasitProjeOda? tiklanan = null;
        foreach (var oda in katSec.Odalar)
        {
            if (PoligonYardimci.IsPointInside(local, oda.Poligon)) { tiklanan = oda; break; }
        }

        if (tiklanan == null)
        {
            // Boşluğa tıklama: çoklu modda dokunma, normal modda seçimi temizle
            if (!multiSelect)
            {
                SeciliOdalar.Clear();
                SeciliOda = null;   // OnSeciliOdaChanged SeciliOdalar'ı zaten boş bırakır
                GuncelleOdaVurgu();
            }
            return;
        }

        if (multiSelect)
        {
            // Ctrl/Cmd: listeye ekle/çıkar (toggle)
            if (!SeciliOdalar.Remove(tiklanan))
                SeciliOdalar.Add(tiklanan);
            // Tek oda kaldıysa düzenlenebilir göster, çoklu ise gizle
            _odaCokluSecimGuncelleniyor = true;
            SeciliOda = SeciliOdalar.Count == 1 ? SeciliOdalar[0] : null;
            _odaCokluSecimGuncelleniyor = false;
            GuncelleOdaVurgu();
        }
        else
        {
            // Normal tıklama: tek seçim (OnSeciliOdaChanged SeciliOdalar'ı senkronlar)
            SeciliOda = tiklanan;
        }
    }


}
