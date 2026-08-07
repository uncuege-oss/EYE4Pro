namespace Kst.Acoustics.Metraj.Filters;

public enum FilterLogic
{
    And,
    Or
}

public class FilterCombinator : IMetrajFilter
{
    private readonly List<IMetrajFilter> _filters = new();
    private readonly FilterLogic _logic;

    public FilterCombinator(FilterLogic logic = FilterLogic.And)
    {
        _logic = logic;
    }

    public void Add(IMetrajFilter filter)
    {
        _filters.Add(filter);
    }

    public bool IsMatch(Models.MetrajEntity entity)
    {
        if (_filters.Count == 0) return true; // Hiç filtre yoksa her şey geçer.

        if (_logic == FilterLogic.And)
        {
            return _filters.All(f => f.IsMatch(entity));
        }
        else
        {
            return _filters.Any(f => f.IsMatch(entity));
        }
    }
}
