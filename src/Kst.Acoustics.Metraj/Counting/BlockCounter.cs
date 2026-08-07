using Kst.Acoustics.Metraj.Models;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Metraj.Counting;

public class BlockCounter
{
    public Dictionary<BlockCountKey, int> Count(MetrajProject project)
    {
        var results = new Dictionary<BlockCountKey, int>();
        foreach (var ins in project.AllInserts)
        {
            var key = CreateKey(ins);
            if (results.ContainsKey(key))
                results[key]++;
            else
                results[key] = 1;
        }
        return results;
    }

    private BlockCountKey CreateKey(MetrajInsert ins)
    {
        string blockName = ins.BlockName;
        
        // Öznitelikleri (Attributes) sıralı bir stringe çevirerek hash/key oluşturuyoruz
        var attrs = ins.Attributes?
            .OrderBy(a => a.Key)
            .Select(a => $"{a.Key}={a.Value}")
            .ToList() ?? new List<string>();
        
        string attrHash = string.Join("|", attrs);
        
        // Fix A3: Include EffectiveColor in the key
        return new BlockCountKey(blockName, ins.Layer, ins.EffectiveColor, attrHash);
    }
}
