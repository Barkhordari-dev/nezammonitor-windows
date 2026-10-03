using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Reports;

/// <summary>
/// Business rules for stage report generation.
/// </summary>
public static class StageReportRules
{
    public static readonly HashSet<string> ExcludedStage1Dates = new()
    {
        "1405/04/01",
        "1405/03/29"
    };

    /// <summary>
    /// Check if a case is eligible for a specific stage report.
    /// </summary>
    public static (bool Eligible, string Reason) CheckEligibility(Case c, int stage, DateTime now)
    {
        if (stage < 1 || stage > 4)
            return (false, "مرحله نامعتبر");

        if (string.IsNullOrWhiteSpace(c.CaseNumber))
            return (false, "شماره پرونده خالی");

        if (string.IsNullOrWhiteSpace(c.Owner))
            return (false, "نام مالک خالی");

        return (true, "آماده تولید");
    }

    /// <summary>
    /// Find matching reports for a stage (handles both "مرحله اول" and "مرحله ۱")
    /// </summary>
    public static List<ReportRecord> FindMatchingReports(Case c, int stage)
    {
        var stageNum = ToPersianNum(stage);
        var stageName = StageName(stage);

        return c.Reports
            .Where(r => r.Stage.Contains($"مرحله {stageNum}") ||
                        r.Stage.Contains($"مرحله {stageName}") ||
                        r.Stage.Contains($"مرحله‌{stageNum}") ||
                        r.Stage.Contains($"مرحله‌{stageName}"))
            .ToList();
    }

    /// <summary>
    /// Generate output folder name: "01 - سميرا رجبي - 1404-514"
    /// </summary>
    public static string GenerateFolderName(int rowNumber, Case c)
    {
        var reversedCase = TextNormalizer.ReverseCaseNumber(c.CaseNumber);
        var safeOwner = TextNormalizer.SanitizeFileName(c.Owner);
        var rowNum = rowNumber.ToString("D2"); // 01, 02, ...
        return $"{rowNum} - {safeOwner} - {reversedCase}";
    }

    /// <summary>
    /// Generate report filename: "گزارش مرحله اول - ۱۴۰۵-۰۶-۰۵.docx"
    /// </summary>
    public static string GenerateReportFilename(int stage, DateTime reportDate, bool useLtr = false)
    {
        var stageWord = StageName(stage);
        var persianDate = PersianDateHelper.ToPersianDateFile(reportDate);
        if (useLtr)
            return $"گزارش مرحله {stageWord} - {persianDate} - LTR.docx";
        return $"گزارش مرحله {stageWord} - {persianDate}.docx";
    }

    public static string StageName(int stage) => stage switch
    {
        1 => "اول",
        2 => "دوم",
        3 => "سوم",
        4 => "چهارم",
        _ => stage.ToString()
    };

    public static string ToPersianNum(int n) => n switch
    {
        1 => "۱", 2 => "۲", 3 => "۳", 4 => "۴",
        _ => n.ToString()
    };

    /// <summary>
    /// Validate a template file for required placeholders.
    /// </summary>
    public static TemplateValidationResult ValidateTemplate(string templatePath)
    {
        var result = new TemplateValidationResult { IsValid = true, TemplatePath = templatePath };

        if (!File.Exists(templatePath))
        {
            result.IsValid = false;
            result.Errors.Add("فایل تمپلیت یافت نشد");
            return result;
        }

        try
        {
            using var doc = WordprocessingDocument.Open(templatePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                result.IsValid = false;
                result.Errors.Add("فایل Word خالی است");
                return result;
            }

            // Get all text from paragraphs
            var allText = string.Join(" ", body.Descendants<Paragraph>().Select(p => p.InnerText));

            // Required placeholders — search for canonical names (trailing spaces tolerated)
            var required = new[] { "کد_نوسازی", "تاریخ_گزارش", "شماره_پرونده", "مالک", "نوع_کاربری" };

            foreach (var placeholder in required)
            {
                if (allText.Contains(placeholder))
                    result.FoundPlaceholders.Add(placeholder);
                else
                    result.MissingPlaceholders.Add(placeholder);
            }

            // Optional placeholders
            var optional = new[] { "عنوان_بلوک", "شماره_پروانه", "تاریخ_صدور_پروانه", "تعداد_بلوک", "نوع_سازه", "گروه_ساختمانی", "تعداد_طبقات", "هماهنگکننده" };

            foreach (var placeholder in optional)
            {
                if (allText.Contains(placeholder))
                    result.FoundPlaceholders.Add(placeholder);
                else
                    result.OptionalMissing.Add(placeholder);
            }

            if (result.MissingPlaceholders.Count > 0)
            {
                result.IsValid = false;
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"خطا در خواندن تمپلیت: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Generates stage-specific Word documents from templates.
/// </summary>
public class StageReportGenerator
{
    private readonly string _templateDir;

    public StageReportGenerator(string templateDir)
    {
        _templateDir = templateDir;
        Directory.CreateDirectory(_templateDir);
    }

    /// <summary>
    /// Generate a single stage report for a case.
    /// </summary>
    public ReportGenerationResult GenerateReport(
        Case caseData,
        int stage,
        int rowNumber,
        string outputDir,
        DateTime now,
        string? templatePathOverride = null,
        bool useLtr = false)
    {
        var result = new ReportGenerationResult
        {
            CaseNumber = caseData.CaseNumber,
            Owner = caseData.Owner,
            Stage = stage,
            RowNumber = rowNumber
        };

        try
        {
            // Find template - use override if provided, otherwise auto-detect
            var templatePath = templatePathOverride ?? FindTemplate(stage);
            if (templatePath == null)
            {
                result.Success = false;
                result.ErrorMessage = "قالب یافت نشد";
                return result;
            }

            // Validate template
            var validation = StageReportRules.ValidateTemplate(templatePath);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = $"قالب ناقص: {string.Join(", ", validation.MissingPlaceholders)}";
                return result;
            }

            // Generate folder and filename
            var folderName = StageReportRules.GenerateFolderName(rowNumber, caseData);
            var fileName = StageReportRules.GenerateReportFilename(stage, now, useLtr);
            var folderPath = Path.Combine(outputDir, folderName);
            Directory.CreateDirectory(folderPath);
            var outputPath = Path.Combine(folderPath, fileName);

            // Check for existing file
            if (File.Exists(outputPath))
            {
                // Overwrite existing file
                File.Delete(outputPath);
            }

            // Copy template and replace placeholders
            File.Copy(templatePath, outputPath, false);
            ReplacePlaceholders(outputPath, caseData, rowNumber, now, useLtr);

            // Verify file was created
            if (!File.Exists(outputPath))
            {
                result.Success = false;
                result.ErrorMessage = "فایل خروجی ایجاد نشد";
                return result;
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.FolderPath = folderPath;
            result.FileName = fileName;
            result.ReportDate = PersianDateHelper.ToPersianDateDigits(now);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"خطا: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Generate reports for multiple cases.
    /// </summary>
    public List<ReportGenerationResult> GenerateBatch(
        IReadOnlyList<(Case Case, int RowNumber)> cases,
        int stage,
        string outputDir,
        DateTime now)
    {
        var results = new List<ReportGenerationResult>();
        foreach (var (c, row) in cases)
        {
            results.Add(GenerateReport(c, stage, row, outputDir, now));
        }
        return results;
    }

    /// <summary>
    /// List available templates in the templates directory.
    /// </summary>
    public List<TemplateInfo> ListTemplates()
    {
        var templates = new List<TemplateInfo>();
        if (!Directory.Exists(_templateDir))
            return templates;

        var files = Directory.GetFiles(_templateDir, "*.docx", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var stage = 0;
            if (fileName.Contains("اول")) stage = 1;
            else if (fileName.Contains("دوم")) stage = 2;
            else if (fileName.Contains("سوم")) stage = 3;

            var discipline = "";
            if (fileName.Contains("مکانیک") || fileName.Contains("مكانيك")) discipline = "مکانیک";
            else if (fileName.Contains("معماری") || fileName.Contains("معماري")) discipline = "معماری";
            else if (fileName.Contains("عمران")) discipline = "عمران";
            else if (fileName.Contains("برق")) discipline = "برق";

            templates.Add(new TemplateInfo
            {
                Path = file,
                FileName = fileName,
                Discipline = discipline,
                Stage = stage
            });
        }

        return templates;
    }

    /// <summary>
    /// Find template file by stage.
    /// </summary>
    public string? FindTemplateForStage(int stage)
    {
        return FindTemplate(stage);
    }

    private string? FindTemplate(int stage)
    {
        var stageWord = StageReportRules.StageName(stage);
        var stageNum = StageReportRules.ToPersianNum(stage);

        if (!Directory.Exists(_templateDir))
            return null;

        var allTemplates = Directory.GetFiles(_templateDir, "*.docx", SearchOption.AllDirectories);

        // Try exact match patterns
        var patterns = new[]
        {
            $"مکانیک مرحله {stageWord}.docx",
            $"مکانیک مرحله {stageNum}.docx",
            $"مرحله {stageWord}.docx",
            $"مرحله {stageNum}.docx",
        };

        foreach (var pattern in patterns)
        {
            var match = allTemplates.FirstOrDefault(t =>
                Path.GetFileNameWithoutExtension(t) == Path.GetFileNameWithoutExtension(pattern));
            if (match != null) return match;
        }

        // Fuzzy search — only match templates that contain the stage word/number
        foreach (var t in allTemplates)
        {
            var fileName = Path.GetFileNameWithoutExtension(t);
            if (fileName.Contains(stageWord) || fileName.Contains(stageNum))
                return t;
        }

        // NO FALLBACK — do not silently use another stage's template
        return null;
    }

    /// <summary>
    /// Replace all placeholders in the DOCX file.
    /// </summary>
    /// <summary>
    /// Reverse slash-separated components for RTL-aware presentation.
    /// 1405/128 → 128/1405
    /// 1405/03/11 → 11/03/1405
    /// 1405 → 1405 (unchanged)
    /// </summary>
    public static string ReverseSlashComponents(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('/'))
            return input;
        var parts = input.Split('/');
        Array.Reverse(parts);
        return string.Join("/", parts);
    }

    private void ReplacePlaceholders(string docxPath, Case caseData, int rowNumber, DateTime now, bool useLtr = false)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return;

        var spec = caseData.Specification;
        var engineers = caseData.Engineers;

        // Persian date for report
        var persianReportDate = PersianDateHelper.ToPersianDateDigits(now);

        // Helper: normalize to canonical ASCII first, then convert to Persian for Word
        Func<string, string> toWord = s => DigitNormalizer.ToPersianDigits(DigitNormalizer.ToAsciiDigits(s ?? ""));

        // Helper: reverse slash-separated components for LTR presentation
        // Example: 1405/128 → 128/1405, 1405/03/11 → 11/03/1405
        Func<string, string> rtlReverse = useLtr
            ? (s => ReverseSlashComponents(s))
            : (s => s);

        var replacements = new Dictionary<string, string>
        {
            // Identifiers — convert to Persian + apply LTR for slash-containing values
            ["«شماره_پرونده»"] = rtlReverse(toWord(caseData.CaseNumber)),
            ["«شماره_سریال»"] = toWord(caseData.Serial),
            ["«کد_نوسازی»"] = toWord(spec?.RenovationCode ?? ""),
            ["«شماره_پروانه»"] = rtlReverse(toWord(spec?.PermitNumber ?? "")),
            ["«تعداد_بلوک»"] = toWord(spec?.BlockCount ?? ""),
            ["«تعداد_طبقات»"] = toWord(spec?.Floors ?? ""),
            ["«تعداد_واحد»"] = toWord(spec?.Units ?? ""),

            // Dates — convert to Persian + apply LTR for slash-containing values
            ["«تاریخ_گزارش»"] = rtlReverse(toWord(persianReportDate)),
            ["«تاریخ_صدور_پروانه»"] = rtlReverse(toWord(spec?.PermitDate ?? "")),
            ["«تاریخ_ترخیص»"] = rtlReverse(toWord(spec?.ReleaseDate ?? "")),
            ["«مساحت_زمین»"] = toWord(spec?.LandArea ?? ""),
            ["«متراژ_پاراف»"] = toWord(spec?.ParafArea ?? ""),

            // Text values — pass through as-is (already Persian text)
            ["«مالک»"] = caseData.Owner,
            ["«همراه»"] = caseData.OwnerMobile,
            ["«مسئولیت»"] = caseData.Responsibility,
            ["«نوع_کاربری»"] = spec?.UsageType ?? "",
            ["«گروه_ساختمانی»"] = spec?.BuildingGroup ?? "",
            ["«نوع_سازه»"] = spec?.StructureType ?? "",
            ["«عنوان_بلوک»"] = spec?.BlockTitle ?? "",
            ["«صادرکننده_پروانه»"] = spec?.Issuer ?? "",
            ["«محدوده_طرح»"] = spec?.PlanZone ?? "",
            ["«آدرس»"] = spec?.Address ?? "",
            ["«ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.Name ?? "",
            ["«ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.Name ?? "",
            ["«ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.Name ?? "",
            ["«ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.Name ?? "",
            ["«هماهنگکننده»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("هماهنگ"))?.Name ?? "",
            // فیلدهای جدید: شماره تماس و صلاحیت ناظرین
            ["«تلفن_ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.Phone ?? "",
            ["«تلفن_ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.Phone ?? "",
            ["«تلفن_ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.Phone ?? "",
            ["«تلفن_ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.Phone ?? "",
            ["«صلاحیت_ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.SupervisionLevel ?? "",
        };

        // Also add trailing-space variants (Word may add spaces)
        var extraReplacements = new Dictionary<string, string>();
        foreach (var kv in replacements)
        {
            var trimmed = kv.Key.TrimEnd();
            if (trimmed != kv.Key && !replacements.ContainsKey(trimmed))
                extraReplacements[trimmed] = kv.Value;
        }
        foreach (var kv in extraReplacements)
            replacements[kv.Key] = kv.Value;

        // Replace in all paragraphs (handles split runs)
        foreach (var para in body.Descendants<Paragraph>())
            ReplaceInParagraph(para, replacements);

        // Replace in tables
        foreach (var table in body.Descendants<Table>())
            foreach (var para in table.Descendants<Paragraph>())
                ReplaceInParagraph(para, replacements);

        doc.MainDocumentPart?.Document?.Save();
    }

    /// <summary>
    /// Replace variables in a paragraph, handling split runs by merging text first.
    /// </summary>
    private static void ReplaceInParagraph(Paragraph para, Dictionary<string, string> replacements)
    {
        var runs = para.Descendants<Run>().ToList();
        if (runs.Count == 0) return;

        // Concatenate all run texts
        var fullText = string.Concat(runs.Select(r => r.InnerText));

        // Normalize: collapse multiple spaces between « and » to single space
        // This handles Word adding extra spaces inside placeholders
        var normalized = Regex.Replace(fullText, @"«\s+", "«");
        normalized = Regex.Replace(normalized, @"\s+»", "»");

        // Check if any variable exists (check both original and normalized)
        bool hasVariable = replacements.Keys.Any(k => fullText.Contains(k) || normalized.Contains(k));
        if (!hasVariable) return;

        // Replace in normalized text
        foreach (var (key, value) in replacements)
        {
            // Also try with normalized key
            var normKey = Regex.Replace(key, @"«\s+", "«");
            normKey = Regex.Replace(normKey, @"\s+»", "»");
            
            normalized = normalized.Replace(normKey, value);
        }

        // Put all text in the first run, clear the rest
        var firstRun = runs[0];
        var textEl = firstRun.GetFirstChild<Text>();
        if (textEl != null)
            textEl.Text = normalized;
        else
            firstRun.AppendChild(new Text(fullText));

        for (int i = 1; i < runs.Count; i++)
        {
            var t = runs[i].GetFirstChild<Text>();
            if (t != null) t.Text = "";
        }
    }
}

/// <summary>
/// Template validation result.
/// </summary>
public sealed class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public string TemplatePath { get; set; } = "";
    public List<string> FoundPlaceholders { get; set; } = new();
    public List<string> MissingPlaceholders { get; set; } = new();
    public List<string> OptionalMissing { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Template information.
/// </summary>
public sealed class TemplateInfo
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Discipline { get; set; } = "";
    public int Stage { get; set; }
}

/// <summary>
/// Result of a single report generation.
/// </summary>
public sealed class ReportGenerationResult
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public int RowNumber { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string? OutputPath { get; set; }
    public string? FolderPath { get; set; }
    public string? FileName { get; set; }
    public string ReportDate { get; set; } = "";
}
