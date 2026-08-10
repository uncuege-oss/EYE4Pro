using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public class DuseyMekanBirlesimi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool AktifMi { get; set; } = true;
    public List<DuseyMekanBirlesimUyesi> Uyeler { get; set; } = new();

    // ── Yıkıcı patlatma geri-alma yükü ──────────────────────────────────────
    // Patlat sırasında üst odanın duvarları alt odaya taşınır ve üst oda kattan
    // çıkarılır. Geri alabilmek için patlat ÖNCESİ etkilenen iki katın oda
    // listelerinin derin kopyası burada saklanır (projeyle birlikte serileşir).

    /// Patlatmanın etkilediği üst kat numarası (yüksek katlı / kaldırılan oda tarafı).
    public int UstKatNo { get; set; }
    /// Patlatmanın etkilediği alt kat numarası (hayatta kalan oda tarafı).
    public int AltKatNo { get; set; }
    /// Patlat öncesi üst katın oda listesi (derin kopya). Geri-al için.
    public List<Mekan>? GeriAlmaUstKatMekanlari { get; set; }
    /// Patlat öncesi alt katın oda listesi (derin kopya). Geri-al için.
    public List<Mekan>? GeriAlmaAltKatMekanlari { get; set; }
}

public class DuseyMekanBirlesimUyesi
{
    /// <summary>M50 katının kararlı kimliği; eski belgelerde boş olabilir.</summary>
    public string KatId { get; set; } = "";
    public int KatNo { get; set; }
    public Guid MekanId { get; set; }
    /// Bu mekanın tavanı (üst birleşim üyesine bağlanan yüzey) açık mı.
    /// Varsayılan true: eski zincir-tipi birleşimlerle geri uyumlu.
    public bool TavanAcik { get; set; } = true;
    /// Bu mekanın döşemesi (alt birleşim üyesine bağlanan yüzey) açık mı.
    /// Varsayılan true: eski zincir-tipi birleşimlerle geri uyumlu.
    public bool DosemeAcik { get; set; } = true;
}
