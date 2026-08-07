namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

/// 7 oktav bant (63, 125, 250, 500, 1000, 2000, 4000 Hz) hava sesi hesap sonucu.
public sealed record OktavBantSonucu(
    double[] R_f,       // R'(f) — indeks 0=63Hz … 6=4kHz
    double   RwHesap,
    double   CHesap,
    double   CtrHesap,
    string   Kaynak);   // "OktavVeri" | "ModelTahmin" | "TekSayi"

/// 7 oktav bant darbe sesi hesap sonucu.
public sealed record OktavDarbeSonucu(
    double[] Ln_f,      // L'n(f) — indeks 0=63Hz … 6=4kHz
    double   LnwHesap,
    string   Kaynak);   // "OktavVeri" | "ModelTahmin" | "TekSayi"

/// 16 üçüncü-oktav bant (100–3150 Hz) hava sesi hesap sonucu.
public sealed record UcuncuOktavHavaSonucu(
    double[] R_f,
    double   RwHesap,
    double   CHesap,
    double   CtrHesap,
    string   Kaynak);   // "UcuncuOktavVeri" | "ModelTahmin"
