using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kst.Acoustics.Metraj.Config;

namespace Kst.Acoustics.Metraj.App;

public class ConfigViewModel : INotifyPropertyChanged
{
    private string? _odaPath;
    private string? _validationMessage;

    public string? OdaPath
    {
        get => _odaPath;
        set => SetProperty(ref _odaPath, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ConfigViewModel()
    {
        var config = ConfigStore.Load();
        OdaPath = config.OdaConverterPath;
        SaveCommand = new RelayCommand(Save);
    }

    private void Save()
    {
        var config = ConfigStore.Load();
        config.OdaConverterPath = OdaPath;
        ConfigStore.Save(config);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
