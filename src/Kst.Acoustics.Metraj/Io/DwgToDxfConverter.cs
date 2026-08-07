using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kst.Acoustics.Metraj.Io;

public class DwgToDxfConverter
{
    private readonly string _converterPath;
    private readonly int _timeoutMs;

    public string ConverterPath => _converterPath;

    public DwgToDxfConverter(string? converterPath = null, int timeoutSeconds = 60)
    {
        _converterPath = converterPath ?? GetDefaultConverterPath();
        _timeoutMs = timeoutSeconds * 1000;
    }

    public async Task<string> ConvertAsync(string dwgPath)
    {
        if (!File.Exists(dwgPath))
            throw new FileNotFoundException("DWG dosyası bulunamadı.", dwgPath);

        if (!IsConverterAvailable())
            throw new Exception($"ODA File Converter bulunamadı: {_converterPath}. Lütfen PATH'e ekleyin veya tam yolu belirtin.");

        string tempDir = Path.Combine(Path.GetTempPath(), "KstMetraj", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string dwgDir = Path.GetDirectoryName(dwgPath) ?? throw new Exception("Geçersiz DWG yolu.");
        string dwgFileName = Path.GetFileName(dwgPath);
        
        // ODA File Converter dizin bazlı çalışır. 
        // Yanlışlıkla tüm klasörü dönüştürmemek için dosyayı geçici bir giriş klasörüne kopyalıyoruz.
        string inputDir = Path.Combine(tempDir, "input");
        string outputDir = Path.Combine(tempDir, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.Copy(dwgPath, Path.Combine(inputDir, dwgFileName));

        // Komut formatı: ODAFileConverter "input_folder" "output_folder" "output_version" "output_format" "recursive" "audit" "filter"
        // Örnek: ODAFileConverter "/in" "/out" "ACAD2018" "DXF" "0" "1" "*.dwg"
        string arguments = $"\"{inputDir}\" \"{outputDir}\" \"ACAD2018\" \"DXF\" \"0\" \"1\" \"{dwgFileName}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _converterPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var task = process.WaitForExitAsync();
            
            if (await Task.WhenAny(task, Task.Delay(_timeoutMs)) != task)
            {
                process.Kill(true);
                throw new TimeoutException("ODA File Converter zaman aşımına uğradı (60s).");
            }

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"ODA File Converter hatası (Kod: {process.ExitCode}): {error}");
            }

            string expectedDxfName = Path.ChangeExtension(dwgFileName, ".dxf");
            string dxfPath = Path.Combine(outputDir, expectedDxfName);

            if (!File.Exists(dxfPath))
                throw new FileNotFoundException("Dönüştürme işlemi tamamlandı ancak DXF dosyası oluşmadı.");

            return dxfPath;
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    public bool IsConverterAvailable()
    {
        if (Path.IsPathRooted(_converterPath))
            return File.Exists(_converterPath);

        // PATH kontrolü
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return false;

        char sep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var dir in pathEnv.Split(sep))
        {
            string fullPath = Path.Combine(dir, _converterPath);
            if (File.Exists(fullPath)) return true;
        }

        return false;
    }

    private string GetDefaultConverterPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "ODAFileConverter.exe";
        
        return "/Applications/ODAFileConverter.app/Contents/MacOS/ODAFileConverter";
    }
}
