using System.Diagnostics;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>DWG'yi kaynak dosyaya dokunmadan geçici DXF'e çevirir; oturum bitene kadar çıktıyı korur.</summary>
public sealed class MimariCadDosyasi : IDisposable
{
    public string DxfYolu { get; }
    private readonly string? _geciciKlasor;
    private MimariCadDosyasi(string yol, string? klasor = null) { DxfYolu = yol; _geciciKlasor = klasor; }

    public static async Task<MimariCadDosyasi> AcAsync(string kaynak, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(kaynak)) throw new FileNotFoundException("Mimari proje bulunamadı.", kaynak);
        var uzanti = Path.GetExtension(kaynak);
        if (uzanti.Equals(".dxf", StringComparison.OrdinalIgnoreCase)) return new(Path.GetFullPath(kaynak));
        if (!uzanti.Equals(".dwg", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("DWG veya DXF dosyası seçin.");
        var motor = MotorBul();
        if (motor is null) throw new InvalidOperationException("DWG okumak için ODA File Converter veya AutoCAD gereklidir. Kurun ya da projeyi DXF olarak seçin. Özel motor yolu EYE4PRO_CAD_CONVERTER ortam değişkeniyle belirtilebilir.");
        string klasor = Path.Combine(Path.GetTempPath(), "EYE4PRO-Bina", Guid.NewGuid().ToString("N"));
        var oturum = new MimariCadDosyasi(Path.Combine(klasor, "output", "source.dxf"), klasor);
        try
        {
            string input = Path.Combine(klasor, "input"), output = Path.Combine(klasor, "output");
            Directory.CreateDirectory(input); Directory.CreateDirectory(output);
            string kopya = Path.Combine(input, "source.dwg");
            File.Copy(kaynak, kopya);
            var start = new ProcessStartInfo(motor) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = klasor };
            if (Path.GetFileName(motor).Equals("accoreconsole.exe", StringComparison.OrdinalIgnoreCase))
            {
                string script = Path.Combine(klasor, "convert.scr");
                await File.WriteAllTextAsync(script, $"FILEDIA\n0\n_DXFOUT\n\"{oturum.DxfYolu.Replace('\\', '/')}\"\n_Version\n2013\n16\n_QUIT\n_Yes\n", cancellationToken);
                foreach (var arg in new[] { "/i", kopya, "/s", script, "/l", "en-US" }) start.ArgumentList.Add(arg);
            }
            else foreach (var arg in new[] { input, output, "ACAD2013", "DXF", "0", "1", "*.dwg" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("CAD dönüştürücü başlatılamadı.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("DWG dönüşümü 3 dakika içinde tamamlanamadı.");
            }
            await stdout; var hata = await stderr;
            if (process.ExitCode != 0 || !File.Exists(oturum.DxfYolu) || new FileInfo(oturum.DxfYolu).Length == 0)
                throw new InvalidOperationException($"DWG dönüştürülemedi (motor kodu {process.ExitCode}). {hata.Replace("\0", "")[..Math.Min(hata.Replace("\0", "").Length, 600)]}");
            return oturum;
        }
        catch { oturum.Dispose(); throw; }
    }

    private static string? MotorBul()
    {
        string? configured = Environment.GetEnvironmentVariable("EYE4PRO_CAD_CONVERTER");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string path = Path.Combine(dir.Trim('"'), OperatingSystem.IsWindows() ? "ODAFileConverter.exe" : "ODAFileConverter");
            if (File.Exists(path)) return path;
        }
        if (OperatingSystem.IsWindows())
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (string parent in new[] { Path.Combine(programFiles, "ODA"), Path.Combine(programFiles, "Open Design Alliance"), Path.Combine(programFiles, "Autodesk") })
                if (Directory.Exists(parent))
                    foreach (string dir in Directory.EnumerateDirectories(parent).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                        foreach (string exe in new[] { "ODAFileConverter.exe", "accoreconsole.exe" })
                            if (File.Exists(Path.Combine(dir, exe))) return Path.Combine(dir, exe);
        }
        string mac = "/Applications/ODAFileConverter.app/Contents/MacOS/ODAFileConverter";
        return File.Exists(mac) ? mac : null;
    }

    public void Dispose()
    {
        if (_geciciKlasor is null) return;
        try { Directory.Delete(_geciciKlasor, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
