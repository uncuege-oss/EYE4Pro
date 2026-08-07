using Kst.Acoustics.Metraj.Template;
using Kst.Acoustics.Metraj.Models;
using System.Collections.Generic;

namespace Kst.Acoustics.Metraj.Counting;

public class ExplodedCounter
{
    private readonly TemplateMatcher _matcher;

    public ExplodedCounter(TemplateMatcher matcher)
    {
        _matcher = matcher;
    }

    public Dictionary<BlockCountKey, int> Count(List<MetrajEntity> entities)
    {
        var standalone = entities.Where(e => !e.IsFromBlock).ToList();
        var matchResults = _matcher.Match(standalone);
        var results = new Dictionary<BlockCountKey, int>();

        foreach (var match in matchResults)
        {
            // For exploded results, we assume default color (ACI 7) for the block key 
            // unless we want to track layer colors.
            var key = new BlockCountKey(match.TemplateName, match.LayerName, new MetrajColor(7), string.Empty);
            if (results.ContainsKey(key)) results[key] += match.Count;
            else results[key] = match.Count;
        }

        return results;
    }
}
