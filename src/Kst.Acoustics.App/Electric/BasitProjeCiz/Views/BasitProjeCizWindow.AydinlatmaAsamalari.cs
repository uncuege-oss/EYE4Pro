using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class BasitProjeCizWindow
{
    private void AsamaSembolBarGuncelle()
    {
        AsamaSembolBar.Children.Clear();
        if (!_vm.AsamaliPaftaAktif) return;
        foreach (var sembol in _vm.AsamaSembolleri)
        {
            var icerik = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            icerik.Children.Add(SembolMiniCanvasOlustur(sembol));
            if (_vm.AsamaBuatMi || (_vm.AydinlatmaAkisiAktif && _vm.AydinlatmaAsamasi == BpcAydinlatmaAsamasi.Pano))
                icerik.Children.Add(new TextBlock { Text = sembol.Ad, VerticalAlignment = VerticalAlignment.Center });
            var buton = new Button
            {
                Content = icerik, Padding = new Thickness(6, 2),
                Command = _vm.ElemanEkleModeBaslatCommand, CommandParameter = sembol
            };
            ToolTip.SetTip(buton, sembol.Ad);
            Avalonia.Automation.AutomationProperties.SetName(buton, sembol.Ad);
            AsamaSembolBar.Children.Add(buton);
        }
    }

    private void AsamaElemanSil_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SeciliElemanlar.Count > 0) _vm.SeciliElemanlariSil();
        else _vm.SeciliElemanSilCommand.Execute(null);
    }

    private void AsamaManuelKablo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_vm.AsamaAraclariEtkin) return;
        if (_vm.KabloModuAktif) { _vm.KabloModuKapat(); return; }
        _vm.KabloModuToggleCommand.Execute(null);
        if (_vm.AsamaMahalKablosuMu) _vm.AydinlatmaManuelModuSecCommand.Execute(null);
        else if (_vm.AsamaPanoKablosuMu) _vm.SeciliBuatPanoBaglaCommand.Execute(null);
    }

    private void AsamaSigortaOlustur_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.PrizAkisiAktif)
        {
            if (!_vm.PrizProjesiniTamamla()) return;
            _vm.PrizAsamasi = BpcPrizAsamasi.Sigortalar;
            _vm.PanoIcerikAcik = true;
        }
        else if (_vm.PanoAydinlatmaSigortalariniOlustur())
            _vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Sigortalar;
    }

    private void PrizPlaninaGec_OnClick(object? sender, RoutedEventArgs e)
        => _vm.AktifPafta = BpcPaftaTipi.Priz;
}
