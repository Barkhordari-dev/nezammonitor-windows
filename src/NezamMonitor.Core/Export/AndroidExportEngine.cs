using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Export;

/// <summary>
/// Creates a versioned .nzmdata package from Windows database for Android import.
/// Format: ZIP containing manifest.json + data.json + metadata.json
/// </summary>
public sealed class AndroidExportEngine
{
    private readonly NezamDatabase _db;
    private const string FORMAT_NAME = "NezamMonitor.AndroidData";
    private const string FORMAT_VERSION = "1.0";

    public AndroidExportEngine(NezamDatabase db) => _db = db;

    /// <summary>
    /// Export current snapshot data to a .nzmdata file.
    /// </summary>
    public ExportResult Export(string outputPath, string? applicationVersion = null)
    {
        var result = new ExportResult();

        try
        {
            // 1. Load current active snapshot data
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                result.Errors.Add("Active snapshot not found.");
                return result;
            }

            var cases = _db.LoadCases(snapshotId);
            result.RecordCount = cases.Count;

            // 2. Build export data
            var exportData = BuildExportData(cases, snapshotId);

            // 3. Serialize to JSON
            var camelCaseOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var dataJson = JsonSerializer.Serialize(exportData, camelCaseOptions);

            // 4. Build manifest
            var manifest = new ExportManifest
            {
                Format = FORMAT_NAME,
                Version = FORMAT_VERSION,
                FormatVersion = "1",
                CreatedAt = DateTime.UtcNow.ToString("o"),
                SourceApplication = "NezamMonitor Windows",
                SourceApplicationVersion = applicationVersion ?? "1.0",
                RecordCount = cases.Count,
                SchemaVersion = "1",
                Checksum = ComputeChecksum(dataJson),
                ExportedEntities = new List<string>
                {
                    "cases", "specifications", "engineers", "fees", "reports", "followUpEdits", "feeReceipts"
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifest, camelCaseOptions);

            // 5. Build metadata
            var metadata = new ExportMetadata
            {
                VersionApp = applicationVersion ?? "1.0",
                VersionPlatform = "Windows",
                ExportTime = DateTime.UtcNow.ToString("o"),
                Currency = "IRR",
                DateStandard = "Persian Solar Hijri",
                TotalCases = cases.Count,
                TotalEngineers = cases.SelectMany(c => c.Engineers).Count(),
                TotalFees = cases.SelectMany(c => c.Fees).Count(),
                TotalReports = cases.SelectMany(c => c.Reports).Count()
            };

            var metadataJson = JsonSerializer.Serialize(metadata, camelCaseOptions);

            // 6. Create ZIP package
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using (var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                AddEntry(zip, "manifest.json", manifestJson);
                AddEntry(zip, "data.json", dataJson);
                AddEntry(zip, "metadata.json", metadataJson);

                // Empty attachments directory placeholder
                AddEntry(zip, "attachments/.gitkeep", "");
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.Manifest = manifest;
            result.FileSize = new FileInfo(outputPath).Length;

            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Export failed: {ex.Message}");
            return result;
        }
    }

    private ExportData BuildExportData(List<Case> cases, long snapshotId)
    {
        var data = new ExportData
        {
            Cases = new List<ExportCase>()
        };

        // Get follow-up edits
        var followUpEdits = _db.GetAllFollowUpEdits();

        foreach (var c in cases)
        {
            var exportCase = new ExportCase
            {
                CaseNumber = c.CaseNumber,
                Serial = c.Serial,
                Owner = c.Owner,
                OwnerMobile = c.OwnerMobile,
                Responsibility = c.Responsibility,
                CapacityDate = c.CapacityDate,
                Office = c.Office,
                ReportDate1 = c.ReportDate1,
                ReportDate2 = c.ReportDate2,
                ReportDate3 = c.ReportDate3,
                OwnerFather = c.OwnerFather,
                OwnerNationalCode = c.OwnerNationalCode,
                OwnerAddress = c.OwnerAddress,
                OwnerZip = c.OwnerZip,
                OwnerTel = c.OwnerTel,
                OwnerBirthLoc = c.OwnerBirthLoc
            };

            // Specification
            if (c.Specification != null)
            {
                exportCase.Specification = new ExportSpecification
                {
                    Address = c.Specification.Address,
                    BuildingGroup = c.Specification.BuildingGroup,
                    RenovationCode = c.Specification.RenovationCode,
                    PlanInstructionNo = c.Specification.PlanInstructionNo,
                    PlanInstructionType = c.Specification.PlanInstructionType,
                    PlanInstructionDate = c.Specification.PlanInstructionDate,
                    LandArea = c.Specification.LandArea,
                    ParafArea = c.Specification.ParafArea,
                    CapacityArea = c.Specification.CapacityArea,
                    StructureType = c.Specification.StructureType,
                    BlockTitle = c.Specification.BlockTitle,
                    BlockCount = c.Specification.BlockCount,
                    Floors = c.Specification.Floors,
                    Units = c.Specification.Units,
                    Issuer = c.Specification.Issuer,
                    PermitNumber = c.Specification.PermitNumber,
                    PermitDate = c.Specification.PermitDate,
                    ReleaseDate = c.Specification.ReleaseDate,
                    PlanZone = c.Specification.PlanZone,
                    UsageType = c.Specification.UsageType
                };
            }

            // Engineers (v2: phone + levels)
            exportCase.Engineers = c.Engineers.Select(e => new ExportEngineer
            {
                Discipline = e.Discipline,
                Name = e.Name,
                Role = e.Role,
                Phone = e.Phone,
                DesignLevel = e.DesignLevel,
                SupervisionLevel = e.SupervisionLevel,
                ExecutionLevel = e.ExecutionLevel
            }).ToList();

            // Fees — mapped by FeesViewModel.Load() rules (type of service derived from stage + amount ordering)
            var svcMap = new Dictionary<Fee, string>();
            {
                var stage0 = c.Fees.Where(f => (f.Stage ?? "").Trim() == "0").ToList();
                var ordered0 = stage0.OrderByDescending(f => ParseAmountForExport(f.Amount)).ToList();
                string[] order0 = { "حسن انجام کار", "نظارت سهم سازمان", "مالیات نظارت سهم سازمان", "بیمه" };
                for (int i = 0; i < ordered0.Count; i++)
                    svcMap[ordered0[i]] = i < order0.Length ? order0[i] : order0[^1];
            }
            exportCase.Fees = c.Fees.Select(f =>
            {
                string svc;
                var st = (f.Stage ?? "").Trim();
                if (st != "0") svc = "نظارت";
                else if (!svcMap.TryGetValue(f, out svc!)) svc = "بیمه";
                return new ExportFee
                {
                    Discipline = f.Discipline,
                    ServiceType = svc,
                    Stage = f.Stage ?? "",
                    StartDate = f.StartDate,
                    EndDate = f.EndDate,
                    Amount = f.Amount,
                    PayStatus = f.PayStatus,
                    ConfirmStatus = f.ConfirmStatus,
                    AmountType = f.AmountType,
                    Description = f.Description
                };
            }).ToList();

            // Reports
            exportCase.Reports = c.Reports.Select(r => new ExportReport
            {
                RowNo = r.RowNo,
                ReportType = r.ReportType,
                Stage = r.Stage,
                Engineer = r.Engineer,
                Discipline = r.Discipline,
                VisitDate = r.VisitDate,
                CeilingCount = r.CeilingCount,
                Indicator = r.Indicator,
                HasFile = r.HasFile
            }).ToList();

            // Follow-up edits
            if (followUpEdits.TryGetValue(c.CaseNumber, out var edit))
            {
                exportCase.FollowUpEdit = new ExportFollowUpEdit
                {
                    Description = edit.Description,
                    CaseNumberEdit = edit.CaseNumberEdit,
                    OwnerEdit = edit.OwnerEdit,
                    AddressEdit = edit.AddressEdit,
                    OwnerMobileEdit = edit.OwnerMobileEdit
                };
            }

            data.Cases.Add(exportCase);
        }

        // FeeReceipts (v2)
        try
        {
            var receipts = _db.GetFeeReceipts();
            data.FeeReceipts = receipts.Select(r => new ExportFeeReceipt
            {
                ReceiptDate = r.ReceiptDate,
                Amount = r.Amount,
                RelatedEndDatesRaw = r.RelatedEndDatesRaw,
                IsAdvance = r.IsAdvance,
                FeeSum = r.FeeSum,
                Changes = r.Changes,
                IsRecentlyUpdated = r.IsRecentlyUpdated,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();
        }
        catch { /* FeeReceipts table may not exist on old DB */ }

        return data;
    }

    private static long ParseAmountForExport(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var s = amount.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
        s = s.Replace("۰","0").Replace("۱","1").Replace("۲","2").Replace("۳","3").Replace("۴","4").Replace("۵","5").Replace("۶","6").Replace("۷","7").Replace("۸","8").Replace("۹","9").Replace("٠","0").Replace("١","1").Replace("٢","2").Replace("٣","3").Replace("٤","4").Replace("٥","5").Replace("٦","6").Replace("٧","7").Replace("٨","8").Replace("٩","9");
        return long.TryParse(s, out var v) ? v : 0;
    }
    private static void AddEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ComputeChecksum(string data)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}

// ========== Export Models ==========

public class ExportResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = "";
    public ExportManifest? Manifest { get; set; }
    public long FileSize { get; set; }
    public int RecordCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ExportManifest
{
    public string Format { get; set; } = "";
    public string Version { get; set; } = "";
    public string FormatVersion { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string SourceApplication { get; set; } = "";
    public string SourceApplicationVersion { get; set; } = "";
    public int RecordCount { get; set; }
    public string SchemaVersion { get; set; } = "";
    public string Checksum { get; set; } = "";
    public List<string> ExportedEntities { get; set; } = new();
}

public class ExportMetadata
{
    public string VersionApp { get; set; } = "";
    public string VersionPlatform { get; set; } = "";
    public string ExportTime { get; set; } = "";
    public string Currency { get; set; } = "";
    public string DateStandard { get; set; } = "";
    public int TotalCases { get; set; }
    public int TotalEngineers { get; set; }
    public int TotalFees { get; set; }
    public int TotalReports { get; set; }
}

public class ExportData
{
    public List<ExportCase> Cases { get; set; } = new();
    public List<ExportFeeReceipt> FeeReceipts { get; set; } = new();
}

public class ExportCase
{
    public string CaseNumber { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Owner { get; set; } = "";
    public string OwnerMobile { get; set; } = "";
    public string Responsibility { get; set; } = "";
    public string CapacityDate { get; set; } = "";
    public string Office { get; set; } = "";
    public string ReportDate1 { get; set; } = "";
    public string ReportDate2 { get; set; } = "";
    public string ReportDate3 { get; set; } = "";
    public string OwnerFather { get; set; } = "";
    public string OwnerNationalCode { get; set; } = "";
    public string OwnerAddress { get; set; } = "";
    public string OwnerZip { get; set; } = "";
    public string OwnerTel { get; set; } = "";
    public string OwnerBirthLoc { get; set; } = "";
    public ExportSpecification? Specification { get; set; }
    public List<ExportEngineer> Engineers { get; set; } = new();
    public List<ExportFee> Fees { get; set; } = new();
    public List<ExportReport> Reports { get; set; } = new();
    public ExportFollowUpEdit? FollowUpEdit { get; set; }
}

public class ExportSpecification
{
    public string Address { get; set; } = "";
    public string BuildingGroup { get; set; } = "";
    public string RenovationCode { get; set; } = "";
    public string PlanInstructionNo { get; set; } = "";
    public string PlanInstructionType { get; set; } = "";
    public string PlanInstructionDate { get; set; } = "";
    public string LandArea { get; set; } = "";
    public string ParafArea { get; set; } = "";
    public string CapacityArea { get; set; } = "";
    public string StructureType { get; set; } = "";
    public string BlockTitle { get; set; } = "";
    public string BlockCount { get; set; } = "";
    public string Floors { get; set; } = "";
    public string Units { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public string PermitDate { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string PlanZone { get; set; } = "";
    public string UsageType { get; set; } = "";
}

public class ExportEngineer
{
    public string Discipline { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Phone { get; set; } = "";
    public string DesignLevel { get; set; } = "";
    public string SupervisionLevel { get; set; } = "";
    public string ExecutionLevel { get; set; } = "";
}

public class ExportFeeReceipt
{
    public string ReceiptDate { get; set; } = "";
    public long Amount { get; set; }
    public string RelatedEndDatesRaw { get; set; } = "";
    public bool IsAdvance { get; set; }
    public long FeeSum { get; set; }
    public string Changes { get; set; } = "";
    public bool IsRecentlyUpdated { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class ExportFee
{
    public string Discipline { get; set; } = "";
    public string ServiceType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Amount { get; set; } = "";
    public string PayStatus { get; set; } = "";
    public string ConfirmStatus { get; set; } = "";
    public string AmountType { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ExportReport
{
    public string RowNo { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Engineer { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string VisitDate { get; set; } = "";
    public string CeilingCount { get; set; } = "";
    public string Indicator { get; set; } = "";
    public bool HasFile { get; set; }
}

public class ExportFollowUpEdit
{
    public string Description { get; set; } = "";
    public string CaseNumberEdit { get; set; } = "";
    public string OwnerEdit { get; set; } = "";
    public string AddressEdit { get; set; } = "";
    public string OwnerMobileEdit { get; set; } = "";
}
