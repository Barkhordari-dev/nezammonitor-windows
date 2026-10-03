using System.Text;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Export;

/// <summary>
/// Exports unique engineers as vCard (.vcf) contacts for phone import.
/// Groups by name, deduplicates projects, includes owner phone/address.
/// </summary>
public sealed class VCardExporter
{
    private readonly NezamDatabase _db;

    public VCardExporter(NezamDatabase db) => _db = db;

    public VCardExportResult Export(string outputPath)
    {
        var result = new VCardExportResult();

        try
        {
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                result.Errors.Add("داده‌ای موجود نیست. ابتدا استخراج کنید.");
                return result;
            }

            var cases = _db.LoadCases(snapshotId);
            if (cases.Count == 0)
            {
                result.Errors.Add("پرونده‌ای یافت نشد.");
                return result;
            }

            // Group engineers by normalized name
            var engineerMap = new Dictionary<string, EngineerInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cases)
            {
                foreach (var e in c.Engineers)
                {
                    var key = NormalizeText(e.Name);
                    if (string.IsNullOrEmpty(key)) continue;

                    if (!engineerMap.TryGetValue(key, out var info))
                    {
                        info = new EngineerInfo
                        {
                            Name = NormalizeText(e.Name),
                            Discipline = NormalizeText(e.Discipline),
                            Phone = e.Phone,
                            DesignLevel = e.DesignLevel,
                            SupervisionLevel = e.SupervisionLevel,
                            ExecutionLevel = e.ExecutionLevel
                        };
                        engineerMap[key] = info;
                    }

                    if (string.IsNullOrEmpty(info.Phone) && !string.IsNullOrEmpty(e.Phone))
                        info.Phone = e.Phone;

                    info.DesignLevel = MergeLevel(info.DesignLevel, e.DesignLevel);
                    info.SupervisionLevel = MergeLevel(info.SupervisionLevel, e.SupervisionLevel);
                    info.ExecutionLevel = MergeLevel(info.ExecutionLevel, e.ExecutionLevel);

                    info.Projects.Add(new ProjectInfo
                    {
                        CaseNumber = c.CaseNumber,
                        Owner = NormalizeText(c.Owner),
                        UsageType = NormalizeText(c.Specification?.UsageType ?? ""),
                        OwnerMobile = c.OwnerMobile,
                        OwnerTel = c.OwnerTel,
                        OwnerAddress = NormalizeText(c.Specification?.Address ?? c.OwnerAddress ?? "")
                    });
                }
            }

            if (engineerMap.Count == 0)
            {
                result.Errors.Add("مهندسی یافت نشد.");
                return result;
            }

            // Build vCard lines with folding
            var lines = new List<string>();
            foreach (var eng in engineerMap.Values.OrderBy(e => e.Name))
            {
                lines.AddRange(BuildVCardLines(eng));
            }

            // Fold long lines (>75 octets) per vCard 3.0 spec
            var foldedLines = new List<string>();
            foreach (var line in lines)
            {
                foldedLines.AddRange(FoldLine(line));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var content = string.Join("\r\n", foldedLines) + "\r\n";
            // Write WITHOUT BOM for maximum compatibility
            File.WriteAllText(outputPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            result.Success = true;
            result.OutputPath = outputPath;
            result.EngineerCount = engineerMap.Count;
            result.ProjectCount = cases.Count;
            result.FileSize = new FileInfo(outputPath).Length;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"خطا: {ex.Message}");
        }

        return result;
    }

    private static List<string> BuildVCardLines(EngineerInfo eng)
    {
        var lines = new List<string>();

        lines.Add("BEGIN:VCARD");
        lines.Add("VERSION:3.0");

        // N (Name) — mandatory in vCard 3.0: N:Last;First;;;
        var nameParts = eng.Name.Trim().Split(' ', 2);
        var firstName = nameParts.Length > 1 ? nameParts[0] : "";
        var lastName = nameParts.Length > 1 ? nameParts[1] : nameParts[0];
        lines.Add($"N:{lastName};{firstName};;;");
        lines.Add($"FN:{eng.Name}");

        // ORG: discipline + levels
        var orgParts = new List<string>();
        if (!string.IsNullOrEmpty(eng.Discipline))
            orgParts.Add(eng.Discipline);
        var levelParts = new List<string>();
        if (!string.IsNullOrEmpty(eng.SupervisionLevel))
            levelParts.Add($"نظارت: پایه {eng.SupervisionLevel}");
        if (!string.IsNullOrEmpty(eng.DesignLevel))
            levelParts.Add($"طراحی: پایه {eng.DesignLevel}");
        if (!string.IsNullOrEmpty(eng.ExecutionLevel))
            levelParts.Add($"اجرا: پایه {eng.ExecutionLevel}");
        if (levelParts.Count > 0)
            orgParts.Add(string.Join(" / ", levelParts));
        if (orgParts.Count > 0)
            lines.Add($"ORG:{string.Join(" — ", orgParts)}");

        // Title: discipline only
        if (!string.IsNullOrEmpty(eng.Discipline))
            lines.Add($"TITLE:{eng.Discipline}");

        // Engineer phone
        if (!string.IsNullOrEmpty(eng.Phone))
        {
            var phone = CleanPhone(eng.Phone);
            if (!string.IsNullOrEmpty(phone))
                lines.Add($"TEL;TYPE=CELL:{phone}");
        }

        // Note: unique projects with owner info
        var uniqueProjects = eng.Projects
            .GroupBy(p => p.CaseNumber)
            .Select(g => g.First())
            .ToList();

        if (uniqueProjects.Count > 0)
        {
            var noteLines = new List<string>();
            noteLines.Add("ناظر پروژه‌ها:");
            foreach (var p in uniqueProjects)
            {
                var usage = !string.IsNullOrEmpty(p.UsageType) ? $" ({p.UsageType})" : "";
                noteLines.Add($"  {p.CaseNumber} - {p.Owner}{usage}");

                var ownerPhone = !string.IsNullOrEmpty(p.OwnerMobile)
                    ? CleanPhone(p.OwnerMobile)
                    : !string.IsNullOrEmpty(p.OwnerTel)
                        ? CleanPhone(p.OwnerTel)
                        : "";
                if (!string.IsNullOrEmpty(ownerPhone))
                    noteLines.Add($"  تلفن: {ownerPhone}");

                if (!string.IsNullOrEmpty(p.OwnerAddress?.Trim()))
                    noteLines.Add($"  آدرس: {p.OwnerAddress.Trim()}");
            }

            // Join with literal \n for vCard, escape special chars
            var noteValue = string.Join("\\n", noteLines)
                .Replace(",", "\\,")
                .Replace(";", "\\;");
            lines.Add($"NOTE:{noteValue}");
        }

        lines.Add("END:VCARD");
        return lines;
    }

    /// <summary>Clean phone number: convert to +98 international format.</summary>
    /// <summary>
    /// Fold a vCard line at 75 octets per vCard 3.0 spec.
    /// Continuation lines start with a space.
    /// </summary>
    private static List<string> FoldLine(string line)
    {
        var result = new List<string>();
        var lineBytes = Encoding.UTF8.GetBytes(line);

        if (lineBytes.Length <= 75)
        {
            result.Add(line);
            return result;
        }

        // First line: up to 75 octets
        int pos = 0;
        int firstEnd = FindFoldPoint(lineBytes, 0, 75);
        result.Add(Encoding.UTF8.GetString(lineBytes, 0, firstEnd));
        pos = firstEnd;

        // Continuation lines: up to 74 octets (1 byte reserved for leading space)
        while (pos < lineBytes.Length)
        {
            int end = FindFoldPoint(lineBytes, pos, 74);
            var chunk = Encoding.UTF8.GetString(lineBytes, pos, end - pos);
            result.Add(" " + chunk);
            pos = end;
        }

        return result;
    }

    /// <summary>
    /// Find how many octets we can take starting at 'start' without exceeding 'maxOctets',
    /// without splitting a multi-byte UTF-8 character.
    /// </summary>
    private static int FindFoldPoint(byte[] bytes, int start, int maxOctets)
    {
        int end = start + maxOctets;
        if (end >= bytes.Length) return bytes.Length;

        // Don't split UTF-8 multi-byte sequences (bytes 10xxxxxx are continuation bytes)
        while (end > start && (bytes[end] & 0xC0) == 0x80)
            end--;

        return end;
    }

    private static string CleanPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        var cleaned = phone
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("/", "")
            .Replace(".", "")
            .Trim();
        // Convert 09xx to +989xx
        if (cleaned.StartsWith("0") && cleaned.Length >= 10)
            cleaned = "+98" + cleaned[1..];
        return cleaned;
    }

    /// <summary>Normalize Arabic characters to Persian equivalents for all text.</summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("ي", "ی")  // Arabic Yeh → Persian Yeh
            .Replace("ك", "ک")  // Arabic Keh → Persian Keh
            .Replace("أ", "ا")  // Arabic Alef Hamza → Alef
            .Replace("ؤ", "و")  // Arabic Waw Hamza → Waw
            .Replace("إ", "ا")  // Arabic Alef Hamza Below → Alef
            .Replace("ة", "ه")  // Arabic Teh Marbuta → Heh
            .Replace("٤", "4")
            .Replace("٥", "5")
            .Replace("٦", "6")
            .Replace("٧", "7")
            .Replace("٨", "8")
            .Replace("٩", "9")
            .Replace("٠", "0");
    }

    private static string MergeLevel(string existing, string candidate)
    {
        if (string.IsNullOrEmpty(existing)) return candidate;
        if (string.IsNullOrEmpty(candidate)) return existing;
        if (int.TryParse(existing, out var e) && int.TryParse(candidate, out var c))
            return Math.Max(e, c).ToString();
        return existing;
    }
}

public sealed class EngineerInfo
{
    public string Name { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string Phone { get; set; } = "";
    public string DesignLevel { get; set; } = "";
    public string SupervisionLevel { get; set; } = "";
    public string ExecutionLevel { get; set; } = "";
    public List<ProjectInfo> Projects { get; set; } = new();
}

public sealed class ProjectInfo
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string UsageType { get; set; } = "";
    public string OwnerMobile { get; set; } = "";
    public string OwnerTel { get; set; } = "";
    public string OwnerAddress { get; set; } = "";
}

public sealed class VCardExportResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = "";
    public int EngineerCount { get; set; }
    public int ProjectCount { get; set; }
    public long FileSize { get; set; }
    public List<string> Errors { get; set; } = new();
}
