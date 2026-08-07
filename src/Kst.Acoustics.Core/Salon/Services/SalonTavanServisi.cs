using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Tavan kesit profili yardımcıları. Profil, derinlik (Y) boyunca (y, kot) kontrol
/// noktalarıdır; aralarda doğrusal enterpolasyon, uçların dışında uç kotu (clamp)
/// uygulanır. Profil yoksa (≥2 nokta değilse) tavan düzdür: `SalonYuksekligiM`.
public static class SalonTavanServisi
{
    /// Profil aktif mi (en az 2 geçerli kırılım noktası)?
    public static bool ProfilliMi(SalonGeometri salon) => salon.TavanProfili.Count >= 2;

    /// Y'ye göre sıralanmış kırılım noktaları (kopya).
    public static List<TavanKirilimNoktasi> SiraliNoktalar(SalonGeometri salon)
        => salon.TavanProfili.OrderBy(n => n.YM).ToList();

    /// Verilen derinlikte tavan kotu (m). Profil yoksa düz tavan yüksekliği döner.
    public static double Kot(SalonGeometri salon, double y)
    {
        if (!ProfilliMi(salon))
            return salon.SalonYuksekligiM > 0 ? salon.SalonYuksekligiM : 3.0;

        var n = SiraliNoktalar(salon);
        if (y <= n[0].YM) return n[0].KotM;
        if (y >= n[^1].YM) return n[^1].KotM;
        for (int i = 0; i + 1 < n.Count; i++)
        {
            if (y > n[i + 1].YM) continue;
            double dy = n[i + 1].YM - n[i].YM;
            if (dy < 1e-12) return n[i + 1].KotM;
            double t = (y - n[i].YM) / dy;
            return n[i].KotM + t * (n[i + 1].KotM - n[i].KotM);
        }
        return n[^1].KotM;
    }

    /// Maksimum tavan kotu (platform yüksekliği clamp'i için).
    public static double MaksKot(SalonGeometri salon)
        => ProfilliMi(salon)
            ? salon.TavanProfili.Max(n => n.KotM)
            : (salon.SalonYuksekligiM > 0 ? salon.SalonYuksekligiM : 3.0);
}
