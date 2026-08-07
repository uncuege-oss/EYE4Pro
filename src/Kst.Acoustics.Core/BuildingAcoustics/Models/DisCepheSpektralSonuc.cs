namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public record DisCepheSpektralSonuc(
    double[] Frekanslar,
    double[] DisSpektrum,
    double[] DntSpektrum,
    double[] IcSpektrum,
    double DnTATr,
    double D2mNt50,
    double IcToplam,
    string Kaynak);
