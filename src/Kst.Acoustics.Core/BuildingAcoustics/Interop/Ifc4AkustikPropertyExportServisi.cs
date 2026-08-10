using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Interop;

/// <summary>
/// Harici IFC kütüphanesine ihtiyaç duymadan, M08 sonuçlarını IFC4 STEP içinde
/// IFCSPACE + Pset_EYE4PRO_Acoustic olarak taşır. Bu bir akustik özellik aktarımıdır;
/// M50 geometrisinin tam IFC temsilinin yerini almaz.
/// </summary>
public static class Ifc4AkustikPropertyExportServisi
{
    private const string IfcAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    public static string Olustur(
        BinaProje proje,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        DateTimeOffset? zamanUtc = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var hesap = new BinaAkustikDegerlendirmeServisi().Hesapla(proje, haricTutulanSonucIdleri);
        proje = hesap.HesaplananProje;
        var rows = hesap.Satirlar;
        var timestamp = (zamanUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var body = new List<string>();
        var id = 1;
        int Add(string value) { body.Add($"#{id}={value};"); return id++; }

        var person = Add("IFCPERSON($,$,'EYE4PRO',$,$,$,$,$)");
        var org = Add("IFCORGANIZATION($,'EYE4PRO',$,$,$)");
        var personOrg = Add($"IFCPERSONANDORGANIZATION(#{person},#{org},$)");
        var app = Add($"IFCAPPLICATION(#{org},'1.0','EYE4PRO M08','EYE4PRO_M08')");
        var owner = Add($"IFCOWNERHISTORY(#{personOrg},#{app},$,.ADDED.,$,#{personOrg},#{app},{timestamp.ToUnixTimeSeconds()})");
        var metre = Add("IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.)");
        var units = Add($"IFCUNITASSIGNMENT((#{metre}))");
        var project = Add($"IFCPROJECT('{GlobalId(proje.Id, "project")}',#{owner},{S(proje.ProjeAdi)}," +
                          $"{S("M08 hesap tabanlı mimari akustik ön değerlendirme")},$,$,$,$,#{units})");
        var site = Add($"IFCSITE('{GlobalId(proje.Id, "site")}',#{owner},'Site',$,$,$,$,$,.ELEMENT.,$,$,$,$,$)");
        var building = Add($"IFCBUILDING('{GlobalId(proje.Id, "building")}',#{owner},{S(proje.ProjeAdi)},$,$,$,$,$,.ELEMENT.,$,$,$)");
        Add($"IFCRELAGGREGATES('{GlobalId(proje.Id, "project-site")}',#{owner},$,$,#{project},(#{site}))");
        Add($"IFCRELAGGREGATES('{GlobalId(proje.Id, "site-building")}',#{owner},$,$,#{site},(#{building}))");

        var storeyIds = new List<int>();
        foreach (var kat in proje.Katlar.OrderBy(x => x.KatNo).ThenBy(x => x.Ad, StringComparer.Ordinal))
        {
            var katSeed = $"{kat.BinaGeometriKatId}|{kat.KatNo}|{kat.Ad}";
            var storey = Add($"IFCBUILDINGSTOREY('{GlobalId(proje.Id, "storey:" + katSeed)}',#{owner},{S(kat.Ad)},$,$,$,$,$,.ELEMENT.,$)");
            storeyIds.Add(storey);
            var spaces = new List<int>();
            foreach (var mekan in kat.Mekanlar.OrderBy(x => x.Id))
            {
                var space = Add($"IFCSPACE('{GlobalId(mekan.Id, "space")}',#{owner},{S(mekan.Ad)}," +
                                $"{S(mekan.KullanimTipi)},$,$,$,$,.ELEMENT.,.INTERNAL.,$)");
                spaces.Add(space);

                var physicalIds = mekan.Bolmeler.Select(x => x.Id).ToHashSet();
                var roomRows = rows.Where(x =>
                        physicalIds.Contains(x.SonucId) ||
                        x.SonucId == RaporSonucIdleri.DisCephe(mekan.Id) ||
                        x.SonucId == RaporSonucIdleri.Tesisat(mekan.Id, false) ||
                        x.SonucId == RaporSonucIdleri.Tesisat(mekan.Id, true) ||
                        x.SonucId == RaporSonucIdleri.Reverberasyon(mekan.Id) ||
                        (string.Equals(x.Kat, kat.KatNo.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) &&
                         string.Equals(x.KaynakMekan, mekan.Ad, StringComparison.Ordinal)))
                    .OrderBy(x => x.SonucId).ThenBy(x => x.SonucTuru);
                foreach (var row in roomRows)
                    AddResultPropertySet(Add, owner, space, row);
            }
            if (spaces.Count > 0)
                Add($"IFCRELAGGREGATES('{GlobalId(proje.Id, "storey-spaces:" + katSeed)}',#{owner},$,$,#{storey},({Refs(spaces)}))");
        }
        if (storeyIds.Count > 0)
            Add($"IFCRELAGGREGATES('{GlobalId(proje.Id, "building-storeys")}',#{owner},$,$,#{building},({Refs(storeyIds)}))");

        var sb = new StringBuilder();
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine("FILE_DESCRIPTION(('ViewDefinition [ReferenceView]','EYE4PRO M08 acoustic property export'),'2;1');");
        sb.AppendLine($"FILE_NAME('m08-acoustic.ifc','{timestamp:yyyy-MM-ddTHH:mm:ssZ}',('EYE4PRO'),('EYE4PRO'),'EYE4PRO M08','EYE4PRO','');");
        sb.AppendLine("FILE_SCHEMA(('IFC4'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");
        foreach (var line in body) sb.AppendLine(line);
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");
        return sb.ToString();
    }

    private static void AddResultPropertySet(Func<string, int> add, int owner, int space, RaporSatiri row)
    {
        var properties = new List<int>
        {
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_ResultId',$,IFCIDENTIFIER({S(row.SonucId.ToString("D"))}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_ResultType',$,IFCLABEL({S(row.SonucTuruEtiketi)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_CalculatedValue',$,IFCREAL({N(row.Deger)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_Unit',$,IFCLABEL({S(row.Birim)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_Limit',$,IFCREAL({N(row.LimitDeger)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_Status',$,IFCLABEL({S(row.Durum)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_DataSource',$,IFCTEXT({S(row.VeriKaynagi)}),$)"),
            add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_Confidence',$,IFCINTEGER({row.GuvenPuani}),$)"),
        };
        if (double.IsFinite(row.MarjDb))
            properties.Add(add($"IFCPROPERTYSINGLEVALUE('EYE4PRO_Margin',$,IFCREAL({N(row.MarjDb)}),$)"));
        var pset = add($"IFCPROPERTYSET('{GlobalId(row.SonucId, "pset")}',#{owner},'Pset_EYE4PRO_Acoustic'," +
                       $"{S("Hesap tabanlı ön değerlendirme; saha ölçümü değildir.")},({Refs(properties)}))");
        add($"IFCRELDEFINESBYPROPERTIES('{GlobalId(row.SonucId, "rel")}',#{owner},$,$,(#{space}),#{pset})");
    }

    private static string Refs(IEnumerable<int> ids) => string.Join(',', ids.Select(x => $"#{x}"));
    private static string N(double value)
    {
        if (!double.IsFinite(value)) return "0.";
        var text = value.ToString("0.#################", CultureInfo.InvariantCulture);
        return text.Contains('.') ? text : text + ".";
    }

    private static string S(string? value)
    {
        if (value == null) return "$";
        var sb = new StringBuilder("'");
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == '\'') sb.Append("''");
            else if (rune.IsAscii && rune.Value is >= 32 and <= 126 && rune.Value != '\\')
                sb.Append((char)rune.Value);
            else if (rune.Value <= 0xFFFF)
                sb.Append("\\X2\\").Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture)).Append("\\X0\\");
            else
            {
                var utf16 = rune.ToString();
                sb.Append("\\X2\\");
                foreach (var ch in utf16) sb.Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                sb.Append("\\X0\\");
            }
        }
        return sb.Append('\'').ToString();
    }

    /// <summary>Kararlı, IFC GlobalId sözcük dağarcığına uygun 22 karakterlik kimlik.</summary>
    public static string GlobalId(Guid source, string scope)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString("N") + "|" + scope));
        var chars = new char[22];
        var bitBuffer = 0;
        var bitCount = 0;
        var byteIndex = 0;
        for (var i = 0; i < chars.Length; i++)
        {
            while (bitCount < 6)
            {
                bitBuffer = (bitBuffer << 8) | bytes[byteIndex++];
                bitCount += 8;
            }
            bitCount -= 6;
            chars[i] = IfcAlphabet[(bitBuffer >> bitCount) & 0x3F];
            bitBuffer &= (1 << bitCount) - 1;
        }
        return new string(chars);
    }
}
