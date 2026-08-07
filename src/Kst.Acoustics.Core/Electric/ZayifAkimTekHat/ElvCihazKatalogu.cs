namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

/// <summary>
/// Rack kabin ve ana merkez içine eklenebilecek hazır cihaz kataloğu.
/// Kullanıcı bu listeden seçebilir veya manuel yeni cihaz adı girebilir.
/// </summary>
public static class ElvCihazKatalogu
{
    /// <summary>
    /// Data sistemi — Ana Merkez (sistem odası / MDF - ana dağıtım) rack kabinine konulabilecek
    /// tipik omurga, sunucu, güç ve altyapı cihazları.
    /// </summary>
    public static IReadOnlyList<string> DataAnaMerkezCihazlari { get; } = new[]
    {
        // Ağ omurgası
        "Omurga (Core) Switch L3",
        "Omurga Switch L3 24 Port 10G",
        "Omurga Switch L3 48 Port 10G",
        "Omurga Switch L3 32 Port 40G/100G",
        "Yedekli Omurga Switch Şasesi",
        "Dağıtım (Distribution) Switch L3",
        "Dağıtım Switch L3 24 Port",
        "Dağıtım Switch L3 48 Port",
        "Yönetilebilir Switch 24 Port",
        "Yönetilebilir Switch 48 Port",
        "Fiber Omurga Switch (SFP+)",
        "Fiber Switch 24 Port SFP",
        "Fiber Switch 24 Port SFP+",
        "Fiber Switch 48 Port SFP+",
        "SFP Modül 1G Multimode",
        "SFP Modül 1G Singlemode",
        "SFP+ Modül 10G Multimode",
        "SFP+ Modül 10G Singlemode",
        "QSFP+ Modül 40G",
        "QSFP28 Modül 100G",
        "Router (Kenar Yönlendirici)",
        "Yedekli Router",
        "SD-WAN Cihazı",
        "Firewall / UTM Güvenlik Duvarı",
        "Yedekli Firewall / UTM",
        "VPN Ağ Geçidi",
        "IDS/IPS Güvenlik Cihazı",
        "Web Uygulama Güvenlik Duvarı (WAF)",
        "Yük Dengeleyici (Load Balancer)",
        // WAN / servis girişi
        "Modem",
        "4G/5G Yedek Modem",
        "Fiber Sonlandırma (ONT)",
        "Metro Ethernet Sonlandırma Cihazı",
        "WAN Optik Dağıtım Paneli",
        "ODF (Optik Dağıtım Çerçevesi)",
        // Kablosuz
        "Kablosuz Kontrolcü (WLAN Controller)",
        "Yedek Kablosuz Kontrolcü",
        "Bulut Ağ Yönetim Ağ Geçidi",
        // Sunucu / depolama
        "Sunucu (Rack Tipi)",
        "Sanallaştırma Sunucusu",
        "Etki Alanı / DNS / DHCP Sunucusu",
        "Dosya Sunucusu",
        "Uygulama Sunucusu",
        "Veritabanı Sunucusu",
        "Yedekleme Sunucusu",
        "Ağ İzleme / NMS Sunucusu",
        "Log / Syslog Sunucusu",
        "Blade Sunucu Şasesi",
        "NAS / Depolama Ünitesi",
        "SAN Depolama",
        "Yedekleme Ünitesi",
        "Tape Kütüphanesi",
        "Sunucu HDD 2 TB",
        "Sunucu HDD 4 TB",
        "Sunucu HDD 8 TB",
        "Sunucu HDD 12 TB",
        "Sunucu SSD 1 TB",
        "Sunucu SSD 2 TB",
        "Sunucu SSD 4 TB",
        "RAID Kontrol Kartı",
        "Zaman Sunucusu (NTP/GPS)",
        // Yönetim
        "KVM Switch",
        "Rack KVM Konsol 1U",
        "Konsol Sunucu (Terminal Server)",
        "IP KVM Switch",
        // Sonlandırma / patch
        "Patch Panel 24 (Cat6/6A)",
        "Patch Panel 48 (Cat6/6A)",
        "Patch Panel 24 Port Cat6",
        "Patch Panel 24 Port Cat6A",
        "Patch Panel 48 Port Cat6",
        "Patch Panel 48 Port Cat6A",
        "Fiber Patch Panel",
        "Fiber Patch Panel 12 Port",
        "Fiber Patch Panel 24 Port",
        "Fiber Patch Panel 48 Port",
        "Medya Dönüştürücü (Fiber-Bakır)",
        // Güç
        "Rack UPS (Online)",
        "Online UPS 1 kVA",
        "Online UPS 2 kVA",
        "Online UPS 3 kVA",
        "Online UPS 6 kVA",
        "Online UPS 10 kVA",
        "Harici Akü Grubu",
        "PDU (Yönetilebilir Güç Dağıtım)",
        "PDU 8'li 1U",
        "PDU 12'li 1U",
        "ATS (Otomatik Transfer Anahtarı)",
        // Ortam / mekanik
        "Ortam İzleme (Sıcaklık/Nem Sensörü)",
        "Su Baskını Sensörü",
        "Rack Kapı Kontak Sensörü",
        "Rack Fan / Soğutma Ünitesi",
        "Kablo Düzenleyici (Yatay)",
        "Kablo Düzenleyici (Dikey)",
        "Rack Rafı",
        "Kör Panel 1U",
        "Kör Panel 2U",
        "Topraklama Barası"
    };

    /// <summary>
    /// Data sistemi — Kat rack kabini (IDF - ara dağıtım) içine konulabilecek tipik kenar/erişim
    /// katmanı cihazları. Kat rack'i omurgaya (Ana Merkez) fiber uplink ile bağlanır.
    /// </summary>
    public static IReadOnlyList<string> DataKatRackCihazlari { get; } = new[]
    {
        // Erişim (Access) katmanı
        "Kenar (Access) Switch 8 Port",
        "Kenar (Access) Switch 16 Port",
        "Kenar (Access) Switch 24 Port",
        "Kenar (Access) Switch 48 Port",
        "PoE Switch 8 Port",
        "PoE Switch 16 Port",
        "PoE Switch 24 Port",
        "PoE Switch 48 Port",
        "PoE+ Switch 8 Port",
        "PoE+ Switch 16 Port",
        "PoE+ Switch 24 Port",
        "PoE+ Switch 48 Port",
        "PoE++ Switch 24 Port",
        "PoE++ Switch 48 Port",
        "PoE+ Switch (802.3at/bt)",
        "Yönetilebilir Switch 8 Port",
        "Fiber Uplink Switch (SFP)",
        "Endüstriyel Ethernet Switch",
        "PoE Enjektör",
        "PoE Extender",
        "SFP Modül 1G Multimode",
        "SFP Modül 1G Singlemode",
        "SFP+ Modül 10G Multimode",
        "SFP+ Modül 10G Singlemode",
        // Sonlandırma / patch
        "Patch Panel 24 (Cat6/6A)",
        "Patch Panel 48 (Cat6/6A)",
        "Patch Panel 24 Port Cat6",
        "Patch Panel 24 Port Cat6A",
        "Patch Panel 48 Port Cat6",
        "Patch Panel 48 Port Cat6A",
        "Fiber Patch Panel (Uplink)",
        "Fiber Patch Panel 12 Port",
        "Fiber Patch Panel 24 Port",
        "ODF (Kat Optik Sonlandırma)",
        "Medya Dönüştürücü (Fiber-Bakır)",
        // Güç
        "Kat UPS (Line-Interactive)",
        "Line-Interactive UPS 1 kVA",
        "Online UPS 1 kVA",
        "Online UPS 2 kVA",
        "Online UPS 3 kVA",
        "PDU (Güç Dağıtım)",
        "PDU 8'li 1U",
        "PDU 12'li 1U",
        // Ortam / düzenleme
        "Ortam Sensörü (Sıcaklık/Nem)",
        "Rack Fan Ünitesi",
        "Kablo Düzenleyici (Yatay)",
        "Kablo Düzenleyici (Dikey)",
        "Konsol Bağlantı Modülü",
        "Rack Rafı",
        "Kör Panel 1U",
        "Kör Panel 2U",
        "Topraklama Barası"
    };

    /// <summary>
    /// Data sistemi birleşik kataloğu (Ana Merkez + Kat rack). Bağlam bilinmediğinde
    /// (geriye dönük uyumluluk) tüm data cihazları tek listede döner.
    /// </summary>
    public static IReadOnlyList<string> DataCihazlari { get; } =
        DataAnaMerkezCihazlari.Concat(DataKatRackCihazlari).Distinct().ToList();

    /// <summary>CCTV sistemi için tipik aktif/pasif cihazlar.</summary>
    public static IReadOnlyList<string> KameraCihazlari { get; } = new[]
    {
        "NVR 4 Kanal",
        "NVR 8 Kanal",
        "NVR 16 Kanal",
        "NVR 32 Kanal",
        "NVR 64 Kanal",
        "NVR 128 Kanal",
        "NVR 256 Kanal",
        "AI NVR 16 Kanal",
        "AI NVR 32 Kanal",
        "AI NVR 64 Kanal",
        "AI NVR 128 Kanal",
        "AI NVR 256 Kanal",
        "DVR/XVR 8 Kanal",
        "DVR/XVR 16 Kanal",
        "DVR/XVR 32 Kanal",
        "Kayıt Sunucusu",
        "Yedek Kayıt Sunucusu",
        "VMS Yönetim Sunucusu",
        "Video Analiz Sunucusu",
        "Failover Sunucusu",
        "Depolama Sunucusu",
        "IP SAN / NAS Depolama",
        "JBOD Disk Ünitesi",
        "RAID Kontrol Kartı",
        "Gözetim HDD 1 TB",
        "Gözetim HDD 2 TB",
        "Gözetim HDD 4 TB",
        "Gözetim HDD 6 TB",
        "Gözetim HDD 8 TB",
        "Gözetim HDD 10 TB",
        "Gözetim HDD 12 TB",
        "Gözetim HDD 16 TB",
        "Gözetim HDD 18 TB",
        "Gözetim HDD 20 TB",
        "Gözetim HDD 24 TB",
        "Gözetim HDD 26 TB",
        "Gözetim HDD 32 TB",
        "Gözetim SSD 1 TB",
        "Gözetim SSD 2 TB",
        "Gözetim SSD 4 TB",
        "PoE Switch 8 Port",
        "PoE Switch 16 Port",
        "PoE Switch 24 Port",
        "PoE Switch 48 Port",
        "PoE+ Switch 24 Port",
        "PoE+ Switch 48 Port",
        "PoE++ Switch 24 Port",
        "Core Switch 24 Port",
        "Core Switch 48 Port",
        "Endüstriyel PoE Switch",
        "PoE Enjektör",
        "PoE Extender",
        "SFP Modül 1G",
        "SFP+ Modül 10G",
        "Patch Panel 24 Port",
        "Patch Panel 48 Port",
        "Fiber Patch Panel",
        "Medya Dönüştürücü",
        "Firewall / Güvenlik Duvarı",
        "Router",
        "Video Wall Kontrolcü",
        "Video Decoder",
        "Video Encoder",
        "HDMI Matris",
        "KVM Konsol",
        "Operatör İş İstasyonu",
        "CCTV Klavye / Joystick",
        "Rack Tipi Monitör",
        "NTP Zaman Sunucusu",
        "GPS Zaman Sunucusu",
        "I/O Alarm Modülü",
        "Ses Giriş/Çıkış Modülü",
        "UPS",
        "Redundant Güç Kaynağı",
        "DC Güç Kaynağı",
        "PDU (Güç Dağıtım)",
        "Otomatik Transfer Anahtarı (ATS)",
        "Rack Fan Ünitesi",
        "Rack Termostatı",
        "Ortam İzleme Sensörü",
        "Kablo Düzenleyici",
        "Rack Rafı",
        "Kör Panel 1U",
        "Kör Panel 2U"
    };

    /// <summary>CCTV izleme/güvenlik odası operatör noktası cihazları.</summary>
    public static IReadOnlyList<string> CctvIzlemeNoktasiCihazlari { get; } = new[]
    {
        "CCTV Operatör İş İstasyonu",
        "VMS İstemci Bilgisayarı",
        "CCTV Klavye / Joystick Kontrol Ünitesi",
        "KVM Konsolu",
        "KVM Switch 8 Port",
        "KVM Switch 16 Port",
        "Video Decoder 4 Kanal",
        "Video Decoder 8 Kanal",
        "Video Decoder 16 Kanal",
        "Video Wall Kontrol Cihazı",
        "HDMI Matrix 4x4",
        "HDMI Matrix 8x8",
        "HDMI Matrix 16x16",
        "CCTV Monitör 22 inç",
        "CCTV Monitör 24 inç",
        "CCTV Monitör 27 inç",
        "CCTV Monitör 32 inç",
        "CCTV Monitör 43 inç",
        "CCTV Monitör 55 inç",
        "Video Wall Paneli 46 inç",
        "Video Wall Paneli 55 inç",
        "Masaüstü Mikrofon",
        "Aktif Hoparlör Seti",
        "Operatör Kulaklığı",
        "UPS 1 kVA",
        "UPS 2 kVA",
        "UPS 3 kVA",
        "Enerji Dağıtım Ünitesi (PDU)",
        "Masaüstü Network Switch 8 Port",
        "Fiber Medya Dönüştürücü"
    };

    public static IReadOnlyList<string> AnonsAnalogCihazlari { get; } =
        ElvAnalogAnonsAnaMerkezKatalogu.Kalemler.Select(k => k.Ad).ToList();

    public static IReadOnlyList<string> AnonsDijitalAnaMerkezCihazlari { get; } = new[]
    {
        "Dijital Anons Kontrol Sunucusu",
        "Yedek Dijital Anons Kontrol Sunucusu",
        "Dijital Ses Matrisi (DSP)",
        "SIP Anons Sunucusu",
        "IP Acil Anons Mikrofonu",
        "IP İtfaiyeci Mikrofonu",
        "Yangın Senaryo I/O Modülü",
        "Sistem Denetim Modülü",
        "Omurga Switch L3 24 Port",
        "Fiber Switch 24 Port SFP+",
        "PoE Ağ Switch 24 Port",
        "PoE Ağ Switch 48 Port",
        "IP Ses Ağ Geçidi",
        "Analog-IP Ses Dönüştürücü",
        "IP Hat İzleme Modülü",
        "NTP Zaman Sunucusu",
        "SFP Modül 1G Singlemode",
        "SFP+ Modül 10G Singlemode",
        "Fiber Patch Panel 24 Port",
        "Patch Panel 24 Port Cat6A",
        "Firewall / Ağ Güvenlik Cihazı",
        "UPS",
        "PDU (Güç Dağıtım)"
    };

    public static IReadOnlyList<string> AnonsDijitalKatRackCihazlari { get; } = new[]
    {
        "Yönetilebilir PoE Switch 8 Port",
        "Yönetilebilir PoE Switch 16 Port",
        "Yönetilebilir PoE Switch 24 Port",
        "Yönetilebilir PoE Switch 48 Port",
        "Yönetilebilir PoE+ Switch 24 Port",
        "Yönetilebilir PoE+ Switch 48 Port",
        "Fiber Uplink Switch (SFP)",
        "SFP Modül 1G Singlemode",
        "SFP+ Modül 10G Singlemode",
        "Patch Panel 24 Port Cat6A",
        "Patch Panel 48 Port Cat6A",
        "Fiber Patch Panel (Uplink)",
        "IP Hat İzleme Modülü",
        "Kat UPS (Online)",
        "PDU (Güç Dağıtım)",
        "Ortam Sensörü (Sıcaklık/Nem)",
        "Kablo Düzenleyici (Yatay)",
        "Rack Fan Ünitesi"
    };

    public static IReadOnlyList<string> AnonsCihazlari { get; } =
        AnonsAnalogCihazlari.Concat(AnonsDijitalAnaMerkezCihazlari).Concat(AnonsDijitalKatRackCihazlari).Distinct().ToList();

    /// <summary>TV ana santrali için tipik aktif/pasif cihazlar.</summary>
    public static IReadOnlyList<string> TvCihazlari { get; } = new[]
    {
        "TV Ana Santral",
        "Multiswitch 9/8",
        "Multiswitch 9/16",
        "Multiswitch 17/16",
        "Uydu Alıcı Modülü",
        "Headend Modül",
        "RF Amplifikatör",
        "Splitter",
        "Tap-off",
        "Topraklama Bloğu",
        "Patch Panel Koaksiyel",
        "UPS",
        "PDU (Güç Dağıtım)"
    };

    public static IReadOnlyList<string> TvDirektLnbCihazlari { get; } = new[]
    {
        "Uydu Çanağı 80 cm", "Uydu Çanağı 90 cm", "Single LNB", "Twin LNB", "Quad LNB",
        "Octo LNB", "DiSEqC 2x1", "DiSEqC 4x1", "Uydu Alıcısı / STB", "Çift Tuner PVR"
    };

    public static IReadOnlyList<string> TvMerkeziCihazlari { get; } = TvCihazlari;

    public static IReadOnlyList<string> IpTvCihazlari { get; } = new[]
    {
        "SAT>IP Gateway 4 Tuner", "SAT>IP Gateway 8 Tuner", "IP-TV Headend", "IP Streamer",
        "Encoder 4 Kanal", "Encoder 8 Kanal", "Transcoder Sunucusu", "IP-TV Middleware Sunucusu",
        "EPG Sunucusu", "CAS/DRM Sunucusu", "Core Switch 24 Port", "Core Switch 48 Port",
        "Managed Switch 8 Port", "Managed Switch 16 Port", "Managed Switch 24 Port",
        "Managed Switch 48 Port", "SFP Modül 1G", "SFP+ Modül 10G", "Smart TV",
        "IP-TV Set-Top Box", "UPS 2 kVA", "UPS 3 kVA"
    };

    /// <summary>Yangın algılama santrali için tipik cihaz/modül kalemleri.</summary>
    public static IReadOnlyList<string> YanginCihazlari { get; } = new[]
    {
        "Adresli Yangın Algılama Santrali",
        "Konvansiyonel Yangın Santrali",
        "Loop Kartı",
        "Giriş/Çıkış Modülü",
        "Röle Modülü",
        "İzolatör Modül",
        "Tekrarlayıcı Panel",
        "Grafik İzleme Paneli",
        "Akü Grubu",
        "Güç Kaynağı",
        "UPS",
        "PDU (Güç Dağıtım)"
    };

    /// <summary>Telefon santrali ve dağıtım kalemleri.</summary>
    public static IReadOnlyList<string> TelefonCihazlari { get; } = new[]
    {
        "Telefon Santrali (PBX) 8 Abone",
        "Telefon Santrali (PBX) 16 Abone",
        "Telefon Santrali (PBX) 32 Abone",
        "IP Santral (IP-PBX)",
        "Analog Hat Kartı 8 Port",
        "Analog Hat Kartı 16 Port",
        "Telefon Dağıtım Bloğu (Reglet) 10'lu",
        "Krone Modülü 10 Çift",
        "110 Blok 100 Çift",
        "Ankastre Telefon Dağıtım Kutusu",
        "Parafudr Modülü",
        "UPS",
        "PDU (Güç Dağıtım)"
    };

    /// <summary>Diafon / kapı zili santrali ve kat kalemleri.</summary>
    public static IReadOnlyList<string> DiafonCihazlari { get; } = new[]
    {
        "Diafon Santrali",
        "Görüntülü Diafon Santrali",
        "Kapı Paneli (Zil Panosu)",
        "Görüntülü Kapı Paneli",
        "Kapıcı Ünitesi",
        "Daire Diafon Ünitesi",
        "Görüntülü Daire Ünitesi (Monitör)",
        "Elektrikli Kapı Kilidi (Kilit Karşılığı)",
        "Kat Dağıtıcı Modülü",
        "Diafon Güç Kaynağı 12/24 V",
        "UPS"
    };

    // ─── Kablo kütüphanesi (Kat İçi / Merkeze kablo tipi seçimi) ─────────────────

    /// <summary>Data sistemi kat içi / omurga kabloları: bakır (Cat) ve fiber optik.</summary>
    public static IReadOnlyList<string> DataKablolari { get; } = new[]
    {
        // Bakır (twisted pair)
        "Cat5e U/UTP",
        "Cat6 U/UTP",
        "Cat6 F/UTP",
        "Cat6A U/FTP",
        "Cat6A S/FTP",
        "Cat7 S/FTP",
        "Cat7A S/FTP",
        "Cat8 S/FTP",
        // Fiber optik — multimode
        "Fiber OM3 Multimode",
        "Fiber OM4 Multimode",
        "Fiber OM5 Multimode",
        "Fiber 4 Core Multimode",
        "Fiber 6 Core Multimode",
        "Fiber 12 Core Multimode",
        // Fiber optik — singlemode
        "Fiber OS2 Singlemode",
        "Fiber 12 Core Singlemode",
        "Fiber 24 Core Singlemode",
        "Fiber 48 Core Singlemode"
    };

    /// <summary>CCTV sistemi kabloları: veri (Cat/fiber) + koaksiyel/siyam koaksiyel.</summary>
    public static IReadOnlyList<string> KameraKablolari { get; } = new[]
    {
        "Cat5e U/UTP",
        "Cat6 U/UTP",
        "Cat6A U/FTP",
        "Cat6A S/FTP",
        "Fiber OM4 Multimode",
        "Fiber OS2 Singlemode",
        "Fiber 6 Core Multimode",
        "Fiber 12 Core Singlemode",
        // Koaksiyel (analog/HD-CVI/TVI)
        "RG-59/U Koaksiyel",
        "RG-6/U Koaksiyel",
        "Siyam Koaksiyel (RG59+2x0.75)"
    };

    /// <summary>Acil anons sistemi kabloları: yangına dayanıklı (FE180) hoparlör hatları.</summary>
    public static IReadOnlyList<string> AnonsKablolari { get; } = new[]
    {
        "2x1.5 mm² FE180",
        "2x2.5 mm² FE180",
        "4x1.5 mm² FE180",
        "NHXH FE180 2x1.5",
        "NHXH FE180 2x2.5",
        "JE-H(St)H FE180 2x2x0.8",
        "Cat6 F/UTP LSZH",
        "Cat6A F/UTP LSZH",
        "Cat6A S/FTP LSZH",
        "Fiber OM4 Multimode",
        "Fiber OS2 Singlemode"
    };

    /// <summary>TV sistemi kabloları: koaksiyel (Cat kablo kullanılmaz).</summary>
    public static IReadOnlyList<string> TvKablolari { get; } = new[]
    {
        "RG-6/U Koaksiyel",
        "RG-11/U Koaksiyel",
        "RG-59/U Koaksiyel"
    };

    /// <summary>Yangın algılama sistemi kabloları: yangın/haberleşme kabloları (Cat kullanılmaz).</summary>
    public static IReadOnlyList<string> YanginKablolari { get; } = new[]
    {
        "J-Y(St)Y 1x2x0.8 Yangın Kablosu",
        "J-Y(St)Y 2x2x0.8 Yangın Kablosu",
        "JE-H(St)H FE180 1x2x0.8",
        "JE-H(St)H FE180 2x2x0.8",
        "NHXH FE180 2x1.5"
    };

    /// <summary>Telefon sistemi kabloları: çok çiftli haberleşme kabloları.</summary>
    public static IReadOnlyList<string> TelefonKablolari { get; } = new[]
    {
        "J-Y(St)Y 2x2x0.6 Telefon Kablosu",
        "J-Y(St)Y 5x2x0.6 Telefon Kablosu",
        "J-Y(St)Y 10x2x0.6 Telefon Kablosu",
        "J-Y(St)Y 20x2x0.6 Telefon Kablosu",
        "PDV-K 2x2x0.5",
        "Cat6 U/UTP"
    };

    /// <summary>Diafon / kapı zili sistemi kabloları.</summary>
    public static IReadOnlyList<string> DiafonKablolari { get; } = new[]
    {
        "J-Y(St)Y 3x2x0.8 Diafon Kablosu",
        "J-Y(St)Y 5x2x0.8 Diafon Kablosu",
        "LIYCY 6x0.50",
        "LIYCY 8x0.50",
        "Cat6 U/UTP (IP Diafon)"
    };
}
