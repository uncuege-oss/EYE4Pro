using Kst.Acoustics.Metraj.Io;

namespace Kst.Acoustics.Metraj.Tests;

public class DwgToDxfConverterTests
{
    [Fact]
    public void IsConverterAvailable_ShouldNotThrow()
    {
        var converter = new DwgToDxfConverter();
        // Sadece çağrılabildiğini kontrol ediyoruz, sistemde yüklü olmayabilir (False dönebilir)
        bool available = converter.IsConverterAvailable();
        Assert.True(true); // Buraya kadar geldiyse kod sağlamdır.
    }

    [Fact]
    public async Task ConvertAsync_WithMissingFile_ThrowsFileNotFound()
    {
        var converter = new DwgToDxfConverter();
        await Assert.ThrowsAsync<FileNotFoundException>(() => converter.ConvertAsync("non_existent.dwg"));
    }

    [Fact]
    public void DefaultPath_ShouldBeValidFormat()
    {
        var converter = new DwgToDxfConverter();
        string path = converter.ConverterPath;
        Assert.False(string.IsNullOrWhiteSpace(path));
        
        // macOS bundle path check
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            Assert.Contains("ODAFileConverter.app", path);
        }
    }
}
