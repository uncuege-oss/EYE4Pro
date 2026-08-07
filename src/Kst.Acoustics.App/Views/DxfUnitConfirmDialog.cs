using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kst.Acoustics.App.Views;

/// <summary>
/// Programatik olarak inşa edilen küçük modal: DXF içe aktarma sonrası
/// kullanıcıdan birim onayı alır. Geri dönüş: seçilen ölçek (metre cinsinden)
/// ya da iptal için null.
/// </summary>
public sealed class DxfUnitConfirmDialog : Window
{
    private static readonly (string Label, double Scale)[] CommonOptions =
    {
        ("Milimetre (mm)", 0.001),
        ("Santimetre (cm)", 0.01),
        ("Metre (m)",       1.0),
        ("İnç (inch)",      0.0254),
        ("Fit (feet)",      0.3048),
    };

    private readonly ComboBox _unitCombo;
    private readonly IReadOnlyList<(string Label, double Scale)> _options;
    private double? _result;

    private DxfUnitConfirmDialog(
        string summary,
        double suggestedScale,
        bool detected,
        IReadOnlyList<string>? qualityItems)
    {
        _options = BuildOptions(suggestedScale);

        Title           = "DXF Birim Onayı";
        Width           = 520;
        Height          = qualityItems is { Count: > 0 } ? 430 : 320;
        CanResize       = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background      = Brushes.White;

        var titleText = new TextBlock
        {
            Text       = detected ? "DXF birimi algılandı — onaylayın" : "⚠ DXF dosyasında birim belirtilmemiş",
            FontSize   = 15,
            FontWeight = FontWeight.Bold,
            Foreground = detected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
            Margin     = new Thickness(0, 0, 0, 8)
        };

        var summaryText = new TextBlock
        {
            Text         = summary,
            FontSize     = 12,
            Foreground   = Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 12)
        };

        var hint = new TextBlock
        {
            Text         = detected
                ? "Aşağıdaki birim DXF'in $INSUNITS alanından okundu. Yanlışsa değiştirebilirsiniz."
                : "Yanlış birim seçimi tüm akustik hesabı bozar. Lütfen DXF'in çizildiği birimi seçin.",
            FontSize     = 11,
            Foreground   = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 8)
        };

        _unitCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource         = BuildLabels(_options),
            SelectedIndex       = FindIndexForScale(suggestedScale)
        };

        var okButton = new Button
        {
            Content             = "Onayla",
            Width               = 100,
            Margin              = new Thickness(0, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += (_, _) =>
        {
            _result = _options[_unitCombo.SelectedIndex < 0 ? 0 : _unitCombo.SelectedIndex].Scale;
            Close();
        };

        var cancelButton = new Button
        {
            Content             = "İptal",
            Width               = 100,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cancelButton.Click += (_, _) => { _result = null; Close(); };

        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 16, 0, 0),
            Children            = { okButton, cancelButton }
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20)
        };
        root.Children.Add(titleText);
        root.Children.Add(summaryText);
        root.Children.Add(hint);
        root.Children.Add(_unitCombo);
        if (BuildQualityBlock(qualityItems) is { } qualityBlock)
            root.Children.Add(qualityBlock);
        root.Children.Add(buttons);
        Content = root;
    }

    private static Control? BuildQualityBlock(IReadOnlyList<string>? qualityItems)
    {
        if (qualityItems is not { Count: > 0 }) return null;

        var list = new StackPanel { Spacing = 3 };
        int count = System.Math.Min(qualityItems.Count, 6);
        for (int i = 0; i < count; i++)
        {
            list.Children.Add(new TextBlock
            {
                Text = qualityItems[i],
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x24, 0x3B, 0x52)),
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (qualityItems.Count > count)
        {
            list.Children.Add(new TextBlock
            {
                Text = $"- {qualityItems.Count - count} ek kontrol raporda gösterilecek.",
                FontSize = 11,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF7, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC7, 0xD8, 0xE8)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 12, 0, 0),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Kalite kontrolü",
                        FontSize = 12,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.Black
                    },
                    list
                }
            }
        };
    }

    private static int FindCommonOptionIndexForScale(double scale)
    {
        for (int i = 0; i < CommonOptions.Length; i++)
            if (System.Math.Abs(CommonOptions[i].Scale - scale) < 1e-9) return i;
        return -1;
    }

    private int FindIndexForScale(double scale)
    {
        for (int i = 0; i < _options.Count; i++)
            if (System.Math.Abs(_options[i].Scale - scale) < 1e-9) return i;
        return 0;
    }

    private static IReadOnlyList<(string Label, double Scale)> BuildOptions(double suggestedScale)
    {
        var options = new List<(string Label, double Scale)>();
        if (double.IsFinite(suggestedScale) &&
            suggestedScale > 0 &&
            FindCommonOptionIndexForScale(suggestedScale) < 0)
        {
            options.Add(($"Algılanan ölçek (1 birim = {FormatScale(suggestedScale)} m)", suggestedScale));
        }

        options.AddRange(CommonOptions);
        return options;
    }

    private static string[] BuildLabels(IReadOnlyList<(string Label, double Scale)> options)
    {
        var labels = new string[options.Count];
        for (int i = 0; i < options.Count; i++)
            labels[i] = options[i].Label;
        return labels;
    }

    private static string FormatScale(double scale)
        => scale.ToString("G6", CultureInfo.InvariantCulture);

    /// <summary>Diyaloğu modal aç; null = iptal, değer = seçilen ölçek (metre).</summary>
    public static async Task<double?> ShowAsync(
        Window owner,
        string summary,
        double suggestedScale,
        bool detected,
        IReadOnlyList<string>? qualityItems = null)
    {
        var dlg = new DxfUnitConfirmDialog(summary, suggestedScale, detected, qualityItems);
        await dlg.ShowDialog(owner);
        return dlg._result;
    }
}
