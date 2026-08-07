using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kst.Acoustics.App.Views;

public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// Kaydedilmemiş değişiklik varken yıkıcı bir geçiş (başka proje açma, yeni içe aktarma,
/// ana ekrana dönme, pencereyi kapatma) yapılmadan önce kullanıcıya sorulur.
/// </summary>
public sealed class UnsavedChangesDialog : Window
{
    private UnsavedChangesChoice _result = UnsavedChangesChoice.Cancel;

    private UnsavedChangesDialog(string message)
    {
        Title                 = "Kaydedilmemiş değişiklikler";
        Width                 = 420;
        Height                = 180;
        CanResize             = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = Brushes.White;

        var text = new TextBlock
        {
            Text         = message,
            FontSize     = 13,
            Foreground   = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 16)
        };

        var saveButton = CreateButton("Kaydet", UnsavedChangesChoice.Save);
        var discardButton = CreateButton("Kaydetmeden Çık", UnsavedChangesChoice.Discard);
        var cancelButton = CreateButton("İptal", UnsavedChangesChoice.Cancel);

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing             = 8,
            Children            = { saveButton, discardButton, cancelButton }
        };

        Content = new StackPanel
        {
            Margin   = new Thickness(20),
            Children = { text, buttons }
        };
    }

    private Avalonia.Controls.Button CreateButton(string label, UnsavedChangesChoice choice)
    {
        var button = new Avalonia.Controls.Button { Content = label, Width = 130 };
        button.Click += (_, _) => { _result = choice; Close(); };
        return button;
    }

    /// <summary>Diyaloğu modal açar; kullanıcı Kaydet/Kaydetmeden Çık/İptal seçimini döner.</summary>
    public static async Task<UnsavedChangesChoice> ShowAsync(Window owner, string message)
    {
        var dlg = new UnsavedChangesDialog(message);
        await dlg.ShowDialog(owner);
        return dlg._result;
    }
}
