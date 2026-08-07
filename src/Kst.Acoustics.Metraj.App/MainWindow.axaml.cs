using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Metraj.App;

namespace Kst.Acoustics.Metraj.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        
        var config = Kst.Acoustics.Metraj.Config.ConfigStore.Load();
        if (string.IsNullOrEmpty(config.OdaConverterPath))
        {
            var configWindow = new ConfigWindow();
            await configWindow.ShowDialog(this);
        }
    }

    private async void OnAddTemplateClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Şablon Dosyaları Seçin",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("CAD Dosyaları") { Patterns = new[] { "*.dwg", "*.dxf" } } }
        });

        if (files.Count >= 1)
        {
            var vm = (MainViewModel)DataContext!;
            foreach (var file in files)
            {
                vm.TemplatePaths.Add(file.Path.LocalPath);
            }
        }
    }

    private async void OnSelectProjectClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Proje Dosyası Seçin",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("CAD Dosyaları") { Patterns = new[] { "*.dwg", "*.dxf" } } }
        });

        if (files.Count >= 1)
        {
            var vm = (MainViewModel)DataContext!;
            vm.ProjectPath = files[0].Path.LocalPath;
        }
    }

    private async void OnOpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        var configWindow = new ConfigWindow();
        await configWindow.ShowDialog(this);
    }

    private async void OnOpenRuleEditorClick(object? sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext!;
        if (string.IsNullOrEmpty(vm.ProjectPath))
        {
            vm.StatusText = "Uyarı: Önce bir proje dosyası seçmelisiniz.";
            return;
        }
        var ruleEditor = new RuleEditor(vm.ProjectPath);
        await ruleEditor.ShowDialog(this);
    }
}