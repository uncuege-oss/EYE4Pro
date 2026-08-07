using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum BagimsizBolumTipi
{
    Konut,
    Ofis,
    Ticari,
    OrtakAlan,
    TeknikHacim,
    Diger
}

public class BagimsizBolum : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _ad = "";
    public string Ad
    {
        get => _ad;
        set
        {
            if (_ad == value) return;
            _ad = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Etiket));
        }
    }

    private BagimsizBolumTipi _tip = BagimsizBolumTipi.Konut;
    public BagimsizBolumTipi Tip
    {
        get => _tip;
        set { if (_tip == value) return; _tip = value; OnPropertyChanged(); }
    }

    private string _renk = "#2D6CDF";
    public string Renk
    {
        get => _renk;
        set { if (_renk == value) return; _renk = value; OnPropertyChanged(); }
    }

    public string Etiket => string.IsNullOrWhiteSpace(Ad) ? "Bağımsız Bölüm" : Ad;

    public override string ToString() => Etiket;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
