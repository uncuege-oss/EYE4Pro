# EYE4PRO

EYE4PRO; akustik, elektrik, bina geometrisi, mimari, arazi, peyzaj ve metraj
modullerini ayni .NET/Avalonia cozumunde gelistiren masaustu uygulamasidir.

Ozel GitHub deposu: https://github.com/uncuege-oss/EYE4Pro

## Gereksinimler

- .NET SDK 10.0.300 (kokteki `global.json` surumu sabitler)
- Git
- Windows, macOS veya Linux icin bir .NET IDE'si
- DWG donusumu kullanilacaksa ODA File Converter

Windows'ta depoyu OneDrive disinda, kisa bir yola klonlamak onerilir:

```powershell
git clone https://github.com/uncuege-oss/EYE4Pro.git C:\dev\EYE4Pro
cd C:\dev\EYE4Pro
```

Flash bellekteki `eye4pro_yedek` klasoru yalnizca kurtarma yedegidir. Gelistirme
dogrudan flash bellek uzerinde yapilmaz; her bilgisayarda yerel disk ve GitHub
deposu kullanilir.

## Derleme ve Calistirma

```bash
dotnet restore src/Kst.Acoustics.slnx
dotnet build src/Kst.Acoustics.slnx
dotnet run --project src/Kst.Acoustics.App/Kst.Acoustics.App.csproj
```

Metraj uygulamasi:

```bash
dotnet run --project src/Kst.Acoustics.Metraj.App/Kst.Acoustics.Metraj.App.csproj
```

## Testler

```bash
dotnet run --project src/Kst.Acoustics.Tests/Kst.Acoustics.Tests.csproj
dotnet run --project src/Kst.Acoustics.Core.Tests/Kst.Acoustics.Core.Tests.csproj
dotnet test src/Kst.Acoustics.Metraj.Tests/Kst.Acoustics.Metraj.Tests.csproj
```

M12'nin istege bagli buyuk regresyon ornekleri `EYE4PRO_M12_REGRESSION_FILES`
ortam degiskeniyle verilebilir. Birden cok yol, isletim sisteminin yol ayiricisi ile
ayrilir (`;` Windows, `:` macOS/Linux).

## Projeler

- `Kst.Acoustics.App`: Ana Avalonia uygulamasi ve tum kullanici modulleri
- `Kst.Acoustics.Core`: Ortak is mantigi ve dosya formatlari
- `Kst.Acoustics.Metraj` / `.App`: CAD metraj motoru ve arayuzu
- `Kst.Acoustics.ImportPreview`: DXF/proje inceleme komut satiri araci
- `Kst.Acoustics.ThemeLab`: Tema gelistirme araci
- `Kst.Acoustics.Tests`, `Kst.Acoustics.Core.Tests`, `Kst.Acoustics.Metraj.Tests`: Testler

## Yerel Veriler

Kaynak kod Git ile paylasilir. Kullanici kutuphaneleri ve uygulama ayarlari ise
isletim sisteminin kullanici veri klasorlerinde tutulur ve Git'e eklenmez. Bu veriler
iki bilgisayar arasinda tasinacaksa uygulamalar kapaliyken ayrica yedeklenmelidir.

- Windows: `%APPDATA%` ve `%LOCALAPPDATA%` altindaki `EYE4PRO` / `KST\EYE4Pro` dizinleri
- macOS: kullanici uygulama-verisi klasorlerindeki ayni uygulama dizinleri
- Metraj ayari: kullanici klasorundeki `.kst-metraj/config.json`

## Git Calisma Akisi

Calismaya baslamadan once `git pull --rebase` calistirin. Bilgisayar degistirmeden
once degisiklikleri test edin, commit edin ve push edin. `bin`, `obj`, `publish`,
uygulama paketleri ve makineye ozel ayarlar depoya girmez.
