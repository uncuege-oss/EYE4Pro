using System;

namespace Kst.Acoustics.Core.Measurement.Analysis;

public static class StepResponseCalculator
{
    public static float[] Calculate(float[] impulseResponse)
    {
        float[] stepResponse = new float[impulseResponse.Length];
        float sum = 0;
        for (int i = 0; i < impulseResponse.Length; i++)
        {
            sum += impulseResponse[i];
            stepResponse[i] = sum;
        }
        return stepResponse;
    }
}
