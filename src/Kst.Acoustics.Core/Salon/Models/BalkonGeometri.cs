using System;

namespace Kst.Acoustics.Core.Salon.Models;

/// Balkon mu loca mı (etiket/ayrım; geometri benzer).
public enum BalkonTipi { Balkon, Loca }

/// Yükseltilmiş balkon/loca platformu. Dikdörtgen footprint; belirli bir kotta döşeme +
/// alt (soffit) + çevre saçak + çevre parapet (korkuluk). Üzerine kademeli seyirci eklemek
/// için aynı kotta bir <see cref="SeyirciSekciyonu"/> tanımlanabilir.
public class BalkonGeometri : IBalkonGeometri
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Balkon";
    public BalkonTipi Tip { get; set; } = BalkonTipi.Balkon;

    public double GenislikM { get; set; } = 16.0;
    public double DerinlikM { get; set; } = 5.0;

    /// Balkon döşeme kotu (m).
    public double TabanKotuM { get; set; } = 5.0;
    /// Döşeme kalınlığı (m) — alt yüzey TabanKotu − Kalınlık'ta.
    public double KalinlikM { get; set; } = 0.4;
    /// Parapet (korkuluk) yüksekliği (m). 0 = parapet yok.
    public double ParapetM { get; set; } = 1.0;

    public double MerkezX { get; set; }
    public double MerkezY { get; set; }

    public override string ToString()
        => $"{Ad} ({GenislikM:F0}×{DerinlikM:F0} m @ {TabanKotuM:F1} m)";
}
