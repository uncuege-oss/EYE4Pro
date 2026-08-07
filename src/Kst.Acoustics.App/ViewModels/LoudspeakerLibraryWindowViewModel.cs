using System.Collections.ObjectModel;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed class LoudspeakerLibraryWindowViewModel : ViewModelBase
{
    private readonly LoudspeakerDatabase _db = LoudspeakerDatabase.Instance;

    public LoudspeakerLibraryWindowViewModel()
    {
        Reload();
    }

    public ObservableCollection<LoudspeakerRowViewModel> Rows { get; } = [];

    public LoudspeakerRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }
    private LoudspeakerRowViewModel? _selectedRow;

    public bool CanDelete => SelectedRow is not null;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
    private string _statusText = $"{LoudspeakerDatabase.Instance.All.Count} hoparlör yüklendi.";

    public void ImportCf1(string filePath)
    {
        var speaker = ClfParser.ParseCf1(filePath);
        if (!_db.Add(speaker))
        {
            _db.Update(speaker);
        }

        Reload();
        StatusText = $"'{speaker.DisplayName}' içe aktarıldı.";
    }

    public void DeleteSelected()
    {
        if (SelectedRow is null) return;

        var name = SelectedRow.DisplayName;
        _db.Remove(SelectedRow.Id);
        SelectedRow = null;
        Reload();
        StatusText = $"'{name}' silindi.";
    }

    public void Reload()
    {
        Rows.Clear();
        foreach (var speaker in _db.All.OrderBy(s => s.Manufacturer).ThenBy(s => s.ModelName))
        {
            Rows.Add(new LoudspeakerRowViewModel(speaker));
        }

        StatusText = $"{Rows.Count} hoparlör yüklendi.";
    }
}

public sealed class LoudspeakerRowViewModel(AcousticLoudspeaker loudspeaker)
{
    public AcousticLoudspeaker Loudspeaker { get; } = loudspeaker;
    public string Id => Loudspeaker.Id;
    public string Manufacturer => Loudspeaker.Manufacturer;
    public string ModelName => Loudspeaker.ModelName;
    public string DisplayName => Loudspeaker.DisplayName;
    public string SensitivityMid => Loudspeaker.Sensitivity.Length > 4
        ? $"{Loudspeaker.Sensitivity[4]:0.#} dB"
        : "-";
    public string Resolution => Loudspeaker.Balloons.FirstOrDefault() is { } b
        ? $"{b.Resolution}°"
        : "-";
}
