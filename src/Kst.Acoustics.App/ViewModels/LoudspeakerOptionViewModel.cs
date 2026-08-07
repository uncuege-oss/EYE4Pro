namespace Kst.Acoustics.App.ViewModels;

public sealed class LoudspeakerOptionViewModel(string? id, string displayName)
{
    public string? Id { get; } = id;
    public string DisplayName { get; } = displayName;

    public override string ToString() => DisplayName;
}
