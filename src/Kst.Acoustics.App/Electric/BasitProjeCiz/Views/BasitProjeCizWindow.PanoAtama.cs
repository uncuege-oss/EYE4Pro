using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class BasitProjeCizWindow
{
    private void MahalPanoMenusunuAc(GeometryPreviewControl preview, double worldX, double worldY, Point canvasNoktasi)
    {
        if (!_vm.AydinlatmaAkisiAktif || _vm.AydinlatmaAsamasi != BpcAydinlatmaAsamasi.Pano ||
            _vm.SeciliKat is not { } kat) return;
        _vm.PickRoomAt(worldX, worldY);
        if (_vm.SeciliOda is not { } oda) return;
        _vm.SeciliElemanlar.Clear();
        _vm.SeciliEleman = null;
        _vm.GuncelleElemanVurgu();
        var menu = new ContextMenu
        {
            Placement = PlacementMode.AnchorAndGravity,
            PlacementTarget = preview,
            PlacementRect = new Rect(canvasNoktasi, new Size(1, 1)),
            PlacementAnchor = PopupAnchor.BottomRight,
            PlacementGravity = PopupGravity.BottomRight
        };
        menu.Items.Add(new MenuItem { Header = $"{oda.Ad} · Bağlı pano", IsEnabled = false });
        var panolar = kat.Elemanlar.Where(p => p.Tip == BpcElemanTipi.KatPanosu).OrderBy(p => p.Ad).ToList();
        if (panolar.Count == 0)
            menu.Items.Add(new MenuItem { Header = "Önce bu kata pano yerleştirin", IsEnabled = false });
        foreach (var pano in panolar)
        {
            var ad = pano.Ad;
            if (panolar.Count(p => p.Ad == ad) > 1) ad += $" ({pano.X:0.##}, {pano.Y:0.##} m)";
            var item = new MenuItem { Header = pano.BagliOdaIds.Contains(oda.Id) ? $"✓ {ad}" : ad };
            item.Click += (_, _) => _vm.MahalePanoAta(oda.Id, pano.Id);
            menu.Items.Add(item);
        }
        if (_vm.Katlar.Any(k => k.Elemanlar.Any(p => p.BagliOdaIds.Contains(oda.Id))))
        {
            menu.Items.Add(new Separator());
            var kaldir = new MenuItem { Header = "Pano atamasını kaldır" };
            kaldir.Click += (_, _) => _vm.MahalePanoAta(oda.Id, null);
            menu.Items.Add(kaldir);
        }
        menu.Open(preview);
    }
}
