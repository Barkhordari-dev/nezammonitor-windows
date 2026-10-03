using System.Text;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Export;

/// <summary>
/// Exports unique owners as vCard (.vcf) contacts from follow-up data.
/// Groups by owner name, includes property address, phone, father name, description.
/// </summary>
public sealed class OwnerVCardExporter
{
    private readonly NezamDatabase _db;

    public OwnerVCardExporter(NezamDatabase db) => _db = db;

    public OwnerVCardExportResult Export(string outputPath, HashSet<string>? selectedCases = null)
    {
        var result = new OwnerVCardExportResult();

        try
        {
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                result.Errors.Add("داده‌ای موجود نیست.");
                return result;
            }

            var cases = _db.LoadCases(snapshotId);
            if (cases.Count == 0)
            {
                result.Errors.Add("پرونده‌ای یافت نشد.");
                return result;
            }

            // Filter by selected cases if provided
            if (selectedCases != null && selectedCases.Count > 0)
            {
                cases = cases.Where(c => selectedCases.Contains(c.CaseNumber)).ToList();
                if (cases.Count == 0)
                {
                    result.Errors.Add("هیچ پرونده‌ای در انتخاب یافت نشد.");
                    return result;
                }
            }

            // Get follow-up edits (descriptions etc.)
            var edits = _db.GetAllFollowUpEdits();

            // Group by owner name
            var ownerMap = new Dictionary<string, OwnerInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cases)
            {
                var ownerName = NormalizeText(c.Owner);
                if (string.IsNullOrEmpty(ownerName)) continue;

                if (!ownerMap.TryGetValue(ownerName, out var info))
                {
                    info = new OwnerInfo { Name = ownerName };
                    ownerMap[ownerName] = info;
                }

                // Merge phone: keep first non-empty
                if (string.IsNullOrEmpty(info.Phone) && !string.IsNullOrEmpty(c.OwnerMobile))
                    info.Phone = c.OwnerMobile;

                // Landline
                if (string.IsNullOrEmpty(info.Landline) && !string.IsNullOrEmpty(c.OwnerTel))
                    info.Landline = c.OwnerTel;

                // Address: keep first non-empty (owner address)
                if (string.IsNullOrEmpty(info.Address) && !string.IsNullOrEmpty(c.OwnerAddress))
                    info.Address = NormalizeText(c.OwnerAddress);

                // Father name: keep first non-empty
                if (string.IsNullOrEmpty(info.FatherName) && !string.IsNullOrEmpty(c.OwnerFather))
                    info.FatherName = NormalizeText(c.OwnerFather);

                // Description: from follow-up edits
                if (edits.TryGetValue(c.CaseNumber, out var edit) && !string.IsNullOrEmpty(edit.Description))
                {
                    if (string.IsNullOrEmpty(info.Description))
                        info.Description = NormalizeText(edit.Description);
                }

                // Add project
                var propertyAddress = NormalizeText(c.Specification?.Address ?? "");
                var usageType = NormalizeText(c.Specification?.UsageType ?? "");
                var description = (edits.TryGetValue(c.CaseNumber, out var e) && !string.IsNullOrEmpty(e.Description))
                    ? NormalizeText(e.Description) : "";

                info.Projects.Add(new OwnerProjectInfo
                {
                    CaseNumber = c.CaseNumber,
                    UsageType = usageType,
                    PropertyAddress = propertyAddress,
                    ReportCount = c.Reports.Count,
                    Description = description
                });
            }

            if (ownerMap.Count == 0)
            {
                result.Errors.Add("مالکی یافت نشد.");
                return result;
            }

            // Build vCard lines
            var lines = new List<string>();
            foreach (var owner in ownerMap.Values.OrderBy(o => o.Name))
            {
                lines.AddRange(BuildVCardLines(owner));
            }

            // Fold long lines
            var foldedLines = new List<string>();
            foreach (var line in lines)
            {
                foldedLines.AddRange(FoldLine(line));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            // Write with explicit CRLF using StreamWriter
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                foreach (var line in foldedLines)
                {
                    writer.Write(line);
                    writer.Write("\r\n");
                }
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.OwnerCount = ownerMap.Count;
            result.ProjectCount = cases.Count;
            result.FileSize = new FileInfo(outputPath).Length;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"خطا: {ex.Message}");
        }

        return result;
    }

    private static List<string> BuildVCardLines(OwnerInfo owner)
    {
        var lines = new List<string>();

        lines.Add("BEGIN:VCARD");
        lines.Add("VERSION:3.0");

        // N:LastName;FirstName;;; — trimmed, no leading/trailing spaces
        var cleanName = System.Text.RegularExpressions.Regex.Replace(owner.Name.Trim(), @"\s+", " ");
        var nameParts = cleanName.Split(' ', 2);
        var firstName = nameParts.Length > 1 ? nameParts[0].Trim() : "";
        var lastName = nameParts.Length > 1 ? nameParts[1].Trim() : nameParts[0].Trim();
        lines.Add($"N:{lastName};{firstName};;;");
        lines.Add($"FN:{cleanName}");

        // Phones
        if (!string.IsNullOrEmpty(owner.Phone))
        {
            var mobile = FormatMobile(owner.Phone);
            if (!string.IsNullOrEmpty(mobile))
                lines.Add($"TEL;TYPE=CELL:{mobile}");
        }
        if (!string.IsNullOrEmpty(owner.Landline))
        {
            var landline = FormatLandline(owner.Landline);
            if (!string.IsNullOrEmpty(landline))
                lines.Add($"TEL;TYPE=WORK:{landline}");
        }

        // TITLE: first case number
        lines.Add("TITLE:پرونده");
        // ADR: standard vCard 7-component format: PO Box;Extended;Street;City;Region;Postal;Country
        if (!string.IsNullOrEmpty(owner.Address))
            lines.Add($"ADR;TYPE=HOME:;;{owner.Address};;;");

        // NOTE: father + description + projects
        var noteLines = new List<string>();

        if (!string.IsNullOrEmpty(owner.FatherName))
            noteLines.Add($"نام پدر: {owner.FatherName}");

        if (!string.IsNullOrEmpty(owner.Description))
            noteLines.Add($"توضیحات: {owner.Description}");

        if (owner.Projects.Count > 0)
        {
            if (noteLines.Count > 0) noteLines.Add("");
            noteLines.Add("پرونده‌ها:");
            foreach (var p in owner.Projects)
            {
                var usage = !string.IsNullOrEmpty(p.UsageType) ? $" — {p.UsageType}" : "";
                noteLines.Add($"  {p.CaseNumber}{usage} | گزارش: {p.ReportCount}");
                if (!string.IsNullOrEmpty(p.PropertyAddress))
                    noteLines.Add($"     آدرس ملک: {p.PropertyAddress}");
            }
        }

        if (noteLines.Count > 0)
        {
            // Use placeholder to avoid double-escaping of \n
            const string NL_PLACEHOLDER = "\x01NL\x01";
            var noteValue = string.Join(NL_PLACEHOLDER, noteLines)
                .Replace(",", "\\,")
                .Replace(";", "\\;")
                .Replace(NL_PLACEHOLDER, "\\n");
            lines.Add($"NOTE:{noteValue}");
        }

        lines.Add("END:VCARD");
        return lines;
    }

    /// <summary>Format mobile: ensure starts with 0, 10+ digits.</summary>
    private static string FormatMobile(string phone)
    {
        var cleaned = CleanPhoneRaw(phone);
        if (string.IsNullOrEmpty(cleaned)) return "";
        // Must start with 09
        if (cleaned.StartsWith("9") && cleaned.Length >= 10)
            cleaned = "0" + cleaned;
        if (!cleaned.StartsWith("09")) return "";
        // Validate: must be all digits and 10-11 characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^09\d{8,9}$"))
            return "";
        return cleaned;
    }

    /// <summary>Format landline: ensure starts with 0, add 035 area code if missing.</summary>
    private static string FormatLandline(string phone)
    {
        var cleaned = CleanPhoneRaw(phone);
        if (string.IsNullOrEmpty(cleaned)) return "";

        // Check BEFORE adding 0: if it's 8 digits without leading 0, it's a local number
        if (!cleaned.StartsWith("0") && cleaned.Length >= 7 && cleaned.Length <= 8)
            cleaned = "035" + cleaned;  // Add area code directly
        else if (!cleaned.StartsWith("0"))
            cleaned = "0" + cleaned;    // Just add 0

        // If starts with 0 but no proper area code (length <= 8), add 035
        if (cleaned.StartsWith("0") && cleaned.Length <= 8)
            cleaned = "035" + cleaned[1..];

        // Validate: must be all digits and at least 10 characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^0\d{9,10}$"))
            return "";

        return cleaned;
    }

    private static string CleanPhoneRaw(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        return phone
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("/", "")
            .Replace(".", "")
            .Replace("+98", "0")
            .Trim();
    }

    // ==================== Folding ====================

    private static List<string> FoldLine(string line)
    {
        var result = new List<string>();
        var lineBytes = Encoding.UTF8.GetBytes(line);

        if (lineBytes.Length <= 75)
        {
            result.Add(line);
            return result;
        }

        int pos = 0;
        int firstEnd = FindFoldPoint(lineBytes, 0, 75);
        result.Add(Encoding.UTF8.GetString(lineBytes, 0, firstEnd));
        pos = firstEnd;

        while (pos < lineBytes.Length)
        {
            int end = FindFoldPoint(lineBytes, pos, 74);
            var chunk = Encoding.UTF8.GetString(lineBytes, pos, end - pos);
            result.Add(" " + chunk);
            pos = end;
        }

        return result;
    }

    private static int FindFoldPoint(byte[] bytes, int start, int maxOctets)
    {
        int end = start + maxOctets;
        if (end >= bytes.Length) return bytes.Length;
        while (end > start && (bytes[end] & 0xC0) == 0x80)
            end--;
        return end;
    }

    // ==================== Normalize ====================

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("ي", "ی")
            .Replace("ك", "ک")
            .Replace("أ", "ا")
            .Replace("ؤ", "و")
            .Replace("إ", "ا")
            .Replace("ة", "ه")
            .Replace("٤", "4").Replace("٥", "5").Replace("٦", "6")
            .Replace("٧", "7").Replace("٨", "8").Replace("٩", "9").Replace("٠", "0");
    }
}

// ==================== Models ====================

public sealed class OwnerInfo
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Landline { get; set; } = "";
    public string Address { get; set; } = "";
    public string FatherName { get; set; } = "";
    public string Description { get; set; } = "";
    public List<OwnerProjectInfo> Projects { get; set; } = new();
}

public sealed class OwnerProjectInfo
{
    public string CaseNumber { get; set; } = "";
    public string UsageType { get; set; } = "";
    public string PropertyAddress { get; set; } = "";
    public int ReportCount { get; set; }
    public string Description { get; set; } = "";
}

public sealed class OwnerVCardExportResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = "";
    public int OwnerCount { get; set; }
    public int ProjectCount { get; set; }
    public long FileSize { get; set; }
    public List<string> Errors { get; set; } = new();
}
