using System.Text.RegularExpressions;
using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Filters;

public interface IMetrajFilter
{
    bool IsMatch(MetrajEntity entity);
}

public class LayerFilter : IMetrajFilter
{
    private readonly Regex? _regex;
    private readonly string? _layerName;

    public LayerFilter(string pattern, bool useRegex = false)
    {
        if (useRegex)
        {
            _regex = new Regex(pattern, RegexOptions.IgnoreCase);
        }
        else
        {
            _layerName = pattern;
        }
    }

    public bool IsMatch(MetrajEntity entity)
    {
        if (_regex != null)
        {
            return _regex.IsMatch(entity.Layer);
        }
        return string.Equals(entity.Layer, _layerName, StringComparison.OrdinalIgnoreCase);
    }
}

public class ColorFilter : IMetrajFilter
{
    private readonly short? _aci;
    private readonly (byte R, byte G, byte B)? _rgb;

    public ColorFilter(short aci)
    {
        _aci = aci;
    }

    public ColorFilter(byte r, byte g, byte b)
    {
        _rgb = (r, g, b);
    }

    public bool IsMatch(MetrajEntity entity)
    {
        var targetColor = entity.EffectiveColor;
        if (_aci.HasValue)
        {
            return targetColor.Aci == _aci.Value;
        }

        if (_rgb.HasValue && targetColor.R.HasValue)
        {
            return targetColor.R == _rgb.Value.R &&
                   targetColor.G == _rgb.Value.G &&
                   targetColor.B == _rgb.Value.B;
        }

        return false;
    }
}

public class TextFilter : IMetrajFilter
{
    private readonly Regex? _regex;
    private readonly string? _exactText;

    public TextFilter(string pattern, bool useRegex = false)
    {
        if (useRegex)
        {
            _regex = new Regex(pattern, RegexOptions.IgnoreCase);
        }
        else
        {
            _exactText = pattern;
        }
    }

    public bool IsMatch(MetrajEntity entity)
    {
        string? content = null;
        if (entity is MetrajText text) content = text.Value;
        else if (entity is MetrajMText mtext) content = mtext.Value;

        if (content == null) return false;

        if (_regex != null)
        {
            return _regex.IsMatch(content);
        }

        return string.Equals(content, _exactText, StringComparison.OrdinalIgnoreCase);
    }
}
