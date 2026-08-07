namespace Kst.Acoustics.App.ViewModels;

public sealed class SurfacePreviewViewModel : ViewModelBase
{
    private string _category = string.Empty;
    private bool _isVisible = true;

    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public required int VertexCount { get; init; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
