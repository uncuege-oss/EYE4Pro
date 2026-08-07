using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationConstants
{
    /// <summary>
    /// Standard 1/3 octave band center frequencies for sound insulation calculations (50 Hz - 5000 Hz).
    /// </summary>
    public static readonly ReadOnlyCollection<int> ThirdOctaveBands = new(new[]
    {
        50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000
    });

    /// <summary>
    /// Standard octave band center frequencies.
    /// </summary>
    public static readonly ReadOnlyCollection<int> OctaveBands = new(new[]
    {
        63, 125, 250, 500, 1000, 2000, 4000
    });
}
