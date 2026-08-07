using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Faz dağılım servisi — R/S/T faz dengeleme.
/// </summary>
public static class FazDagilimServisi
{
    /// <summary>
    /// Monofaze linyeler R/S/T fazlarına dengeli dağıtılır.
    /// Greedy yaklaşım: en düşük toplam güce sahip faza ata.
    /// Trifaze linyeler R+S+T'ye eşit katkı yapar.
    /// </summary>
    public static FazDagilimi OtomatikFazAta(List<Linye> linyeler, double fazGerilimV = 230, double cosF = 0.85)
    {
        if (fazGerilimV <= 0) fazGerilimV = 230;
        cosF = NumberHelper.NormalizeCosF(cosF, 0.85);
        var dağılım = new FazDagilimi();
        double rW = 0, sW = 0, tW = 0;

        // Önce trifaze linyeler — her faza eşit katkı
        foreach (var l in linyeler.Where(x => x.FazTipi == FazTipi.Trifaze))
        {
            var perFaz = l.ToplamGucW / 3.0;
            rW += perFaz;
            sW += perFaz;
            tW += perFaz;
            // Trifaze linyelerin faz ataması anlamsız ama R olarak işaretlenir
            l.FazAtamasi = FazAtamasi.R;
        }

        // Monofaze linyeler — en düşük faza ata (büyükten küçüğe sıralayarak daha iyi denge)
        var monofazeler = linyeler
            .Where(x => x.FazTipi == FazTipi.Monofaze)
            .OrderByDescending(x => x.ToplamGucW)
            .ToList();

        foreach (var l in monofazeler)
        {
            var min = Math.Min(rW, Math.Min(sW, tW));
            if (min == rW)
            {
                l.FazAtamasi = FazAtamasi.R;
                rW += l.ToplamGucW;
            }
            else if (min == sW)
            {
                l.FazAtamasi = FazAtamasi.S;
                sW += l.ToplamGucW;
            }
            else
            {
                l.FazAtamasi = FazAtamasi.T;
                tW += l.ToplamGucW;
            }
        }

        dağılım.R_W = rW;
        dağılım.S_W = sW;
        dağılım.T_W = tW;

        // Faz akımları (aktif güçten faz-nötr gerilimi ve cosφ ile)
        dağılım.R_A = rW / (fazGerilimV * cosF);
        dağılım.S_A = sW / (fazGerilimV * cosF);
        dağılım.T_A = tW / (fazGerilimV * cosF);

        return dağılım;
    }
}
