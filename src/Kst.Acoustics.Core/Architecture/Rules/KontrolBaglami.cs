using System.Collections.Generic;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// Kural calistiricinin ihtiyac duydugu proje ve opsiyonel taslak verilerini tasir.
/// </summary>
public class KontrolBaglami
{
    public MimariProje? MimariProje { get; set; }
    public IhtiyacProgrami? IhtiyacProgrami { get; set; }
    public PlanTaslagi? PlanTaslagi { get; set; }
    public object? CekirdekTaslagi { get; set; }
    public List<string> AktifKaynakProfilleri { get; set; } = new();
    public Dictionary<string, string> Parametreler { get; set; } = new();

    public bool ProjeVar => MimariProje is not null;
    public bool IhtiyacProgramiVar => IhtiyacProgrami is not null;
    public bool GeometriVerisiVar => PlanTaslagi is not null || CekirdekTaslagi is not null;

    public static KontrolBaglami Projeden(MimariProje proje)
        => new()
        {
            MimariProje = proje,
            IhtiyacProgrami = proje.IhtiyacProgrami,
            PlanTaslagi = proje.PlanTaslagi
        };
}
