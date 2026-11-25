using Avalonia.Controls;

namespace Kst.Acoustics.App.Views;

internal static class WindowNavigation
{
    public static void CopyPlacement(Window source, Window target)
    {
        target.WindowStartupLocation = WindowStartupLocation.Manual;
        target.SizeToContent = SizeToContent.Manual;
        // ClientSize is the actual size, including manually resized/snapped windows.
        // Keep dimensions in logical units; Position is already in screen pixels.
        var size = source.ClientSize;
        if (size.Width > 0 && size.Height > 0)
        {
            target.MinWidth = System.Math.Min(target.MinWidth, size.Width);
            target.MinHeight = System.Math.Min(target.MinHeight, size.Height);
            target.Width = size.Width;
            target.Height = size.Height;
        }
        target.Position = source.Position;
        target.WindowState = source.WindowState;
    }
}
