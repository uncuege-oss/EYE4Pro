using System;

namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public static class DiffuserSuitabilityAnalyzer
{
    public static void Analyze(DiffuserDesignInput input, DiffuserDesignResult result)
    {
        if (input.TargetFrequencyHz < 100)
        {
            result.Suitability = DiffuserSuitability.NotRecommended;
            result.Warnings.Add("Hedef frekans çok düşük; bu bölgede difüzör yerine bas tuzağı veya membran/rezonatör tipi yutucu daha uygundur.");
        }
        else if (input.TargetFrequencyHz < 160)
        {
            result.Suitability = DiffuserSuitability.Conditional;
            result.Warnings.Add("Hedef frekans düşük bölgede; panel derinliği büyüyeceği için uygulama alanı kontrol edilmelidir.");
        }

        if (input.SchroederFrequencyHz.HasValue && input.TargetFrequencyHz < input.SchroederFrequencyHz.Value * 0.75)
        {
            result.Suitability = Max(result.Suitability, DiffuserSuitability.Conditional);
            result.Warnings.Add("Hedef frekans Schroeder geçişinin belirgin altında; modal problem için önce düşük frekans yutucuları değerlendirilmelidir.");
        }

        result.Recommendation = result.Suitability switch
        {
            DiffuserSuitability.Suitable => "Difüzör tasarımı bu bant için uygulanabilir. Arka duvar, yan ilk yansıma bölgesi veya tavan bulutu konumu değerlendirilebilir.",
            DiffuserSuitability.Conditional => "Difüzör uygulanabilir ancak düşük frekans davranışı, panel derinliği ve yerleşim mesafesi birlikte kontrol edilmelidir.",
            _ => "Bu hedef için difüzör ana çözüm olarak önerilmez; bas tuzağı veya ayarlı yutucu tercih edilmelidir."
        };
    }

    private static DiffuserSuitability Max(DiffuserSuitability left, DiffuserSuitability right)
    {
        return (DiffuserSuitability)Math.Max((int)left, (int)right);
    }
}
