using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

public sealed record CompositeElement(WallAssembly Assembly, double Area);

public sealed record CompositeResult(
    List<CompositeElement> Elements,
    Dictionary<int, double> TransmissionLossValues,
    RwRatingResult RwRating,
    double TotalArea
);
