using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>Projenin kalıcı içeriğindeki değişimleri izler; seçim, filtre ve hesap bildirimi değişiklik değildir.</summary>
public sealed class KesifProjeDegisiklikIzleyici : IDisposable
{
    private readonly Dictionary<INotifyPropertyChanged, int> _nesneler = new();
    private readonly Dictionary<INotifyCollectionChanged, KoleksiyonTakibi> _koleksiyonlar = new();
    private readonly Dictionary<Type, HashSet<string>> _geciciAlanlar = new();

    public event EventHandler? Degisti;

    public KesifProjeDegisiklikIzleyici(KesifProje proje)
    {
        TakipEt(proje, true);
    }

    private static IEnumerable<object> AltNesneler(object nesne) => nesne switch
    {
        KesifProje p => [p.Bilgiler, p.ManuelPozlar, p.BirimFiyatListeleri, p.Tablolar, p.ReferansDosyalari],
        KesifBirimFiyatListesi l => [l.Pozlar],
        KesifTablosu t => [t.AltTablolar, t.Satirlar],
        KesifSatir s => [s.OtomatikKatkilar],
        KesifReferansDosyasi r => [r.MetrajSatirlari],
        _ => []
    };

    private void TakipEt(object nesne, bool izle)
    {
        if (nesne is INotifyCollectionChanged koleksiyon)
        {
            if (izle)
            {
                if (_koleksiyonlar.TryGetValue(koleksiyon, out var mevcut)) { mevcut.BagSayisi++; return; }
                var yeniTakip = new KoleksiyonTakibi(((IEnumerable)nesne).Cast<object>().ToList());
                _koleksiyonlar.Add(koleksiyon, yeniTakip);
                koleksiyon.CollectionChanged += Koleksiyon_Degisti;
                foreach (var oge in yeniTakip.Ogeler) TakipEt(oge, true);
            }
            else if (_koleksiyonlar.TryGetValue(koleksiyon, out var takip) && --takip.BagSayisi == 0)
            {
                koleksiyon.CollectionChanged -= Koleksiyon_Degisti;
                _koleksiyonlar.Remove(koleksiyon);
                foreach (var oge in takip.Ogeler) TakipEt(oge, false);
            }
            return;
        }
        if (nesne is not INotifyPropertyChanged bildirim) return;
        if (izle)
        {
            if (_nesneler.TryGetValue(bildirim, out var sayi)) { _nesneler[bildirim] = sayi + 1; return; }
            _nesneler.Add(bildirim, 1);
            bildirim.PropertyChanged += Alan_Degisti;
        }
        else
        {
            if (!_nesneler.TryGetValue(bildirim, out var sayi)) return;
            if (sayi > 1) { _nesneler[bildirim] = sayi - 1; return; }
            bildirim.PropertyChanged -= Alan_Degisti;
            _nesneler.Remove(bildirim);
        }
        foreach (var alt in AltNesneler(nesne)) TakipEt(alt, izle);
    }

    private void Alan_Degisti(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is null) return;
        var tip = sender.GetType();
        if (!_geciciAlanlar.TryGetValue(tip, out var gecici))
        {
            gecici = tip.GetProperties().Where(p => Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
                .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            _geciciAlanlar.Add(tip, gecici);
        }
        if (e.PropertyName is not null && gecici.Contains(e.PropertyName)) return;
        Degisti?.Invoke(this, EventArgs.Empty);
    }

    private void Koleksiyon_Degisti(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not INotifyCollectionChanged koleksiyon || !_koleksiyonlar.TryGetValue(koleksiyon, out var takip)) return;
        var guncel = ((IEnumerable)sender).Cast<object>().ToList();
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var oge in takip.Ogeler) TakipEt(oge, false);
            foreach (var oge in guncel) TakipEt(oge, true);
        }
        else if (e.Action != NotifyCollectionChangedAction.Move)
        {
            if (e.OldItems is not null) foreach (var oge in e.OldItems) TakipEt(oge, false);
            if (e.NewItems is not null) foreach (var oge in e.NewItems) TakipEt(oge, true);
        }
        takip.Ogeler = guncel;
        Degisti?.Invoke(this, EventArgs.Empty);
    }

    private void AbonelikleriKaldir()
    {
        foreach (var nesne in _nesneler.Keys) nesne.PropertyChanged -= Alan_Degisti;
        foreach (var koleksiyon in _koleksiyonlar.Keys) koleksiyon.CollectionChanged -= Koleksiyon_Degisti;
        _nesneler.Clear(); _koleksiyonlar.Clear();
    }

    public void Dispose() => AbonelikleriKaldir();

    private sealed class KoleksiyonTakibi(List<object> ogeler)
    {
        public int BagSayisi = 1;
        public List<object> Ogeler = ogeler;
    }
}
