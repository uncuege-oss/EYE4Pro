using System.Text;

namespace Kst.Acoustics.Core.Common;

/// <summary>
/// Proje dosyasını aynı klasörde geçici dosyaya yazıp atomik olarak hedefe taşır.
/// Mevcut hedef varsa son sağlam sürüm <c>.bak</c> olarak korunur.
/// </summary>
public static class GuvenliDosyaYazmaServisi
{
    public static void Yaz(string path, string icerik)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(icerik);

        var (hedef, gecici, yedek) = YollariHazirla(path);
        try
        {
            File.WriteAllText(gecici, icerik, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            using (var stream = new FileStream(gecici, FileMode.Open, FileAccess.Write, FileShare.None))
                stream.Flush(flushToDisk: true);
            HedefeTasi(hedef, gecici, yedek);
        }
        finally
        {
            GeciciyiSil(gecici);
        }
    }

    public static async Task YazAsync(
        string path,
        string icerik,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(icerik);

        var (hedef, gecici, yedek) = YollariHazirla(path);
        try
        {
            await using (var stream = new FileStream(
                             gecici,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 128 * 1024,
                             options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(
                             stream,
                             new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                             bufferSize: 128 * 1024,
                             leaveOpen: true))
            {
                await writer.WriteAsync(icerik.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            HedefeTasi(hedef, gecici, yedek);
        }
        finally
        {
            GeciciyiSil(gecici);
        }
    }

    private static (string Hedef, string Gecici, string Yedek) YollariHazirla(string path)
    {
        var hedef = Path.GetFullPath(path);
        var klasor = Path.GetDirectoryName(hedef) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(klasor);
        var ad = Path.GetFileName(hedef);
        return (
            hedef,
            Path.Combine(klasor, $".{ad}.{Guid.NewGuid():N}.tmp"),
            hedef + ".bak");
    }

    private static void HedefeTasi(string hedef, string gecici, string yedek)
    {
        if (!File.Exists(hedef))
        {
            File.Move(gecici, hedef);
            return;
        }

        try
        {
            File.Replace(gecici, hedef, yedek, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(hedef, yedek, overwrite: true);
            File.Move(gecici, hedef, overwrite: true);
        }
    }

    private static void GeciciyiSil(string gecici)
    {
        if (!File.Exists(gecici))
            return;
        try
        {
            File.Delete(gecici);
        }
        catch
        {
            // Geçici dosya temizleme hatası asıl kayıt hatasını gölgelememeli.
        }
    }
}
