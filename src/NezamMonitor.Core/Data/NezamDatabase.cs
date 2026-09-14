using System.Data;
using Microsoft.Data.Sqlite;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Data;
/// <summary>SQLite persistence for cases, snapshots, and changes.</summary>
public sealed class NezamDatabase : IDisposable
{
    private readonly SqliteConnection _conn;

    public NezamDatabase(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        Initialize();
    }

    public void Dispose() { _conn.Dispose(); }

    private void Execute(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void Initialize()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS Snapshots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                CaseCount INTEGER DEFAULT 0,
                Status TEXT DEFAULT 'pending'
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Cases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SnapshotId INTEGER NOT NULL,
                CaseNumber TEXT NOT NULL,
                Serial TEXT DEFAULT '',
                Owner TEXT DEFAULT '',
                OwnerMobile TEXT DEFAULT '',
                Responsibility TEXT DEFAULT '',
                CapacityDate TEXT DEFAULT '',
                Office TEXT DEFAULT '',
                ReportDate1 TEXT DEFAULT '',
                ReportDate2 TEXT DEFAULT '',
                ReportDate3 TEXT DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Specifications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                BuildingGroup TEXT DEFAULT '',
                RenovationCode TEXT DEFAULT '',
                PlanInstructionNo TEXT DEFAULT '',
                PlanInstructionType TEXT DEFAULT '',
                PlanInstructionDate TEXT DEFAULT '',
                StructureType TEXT DEFAULT '',
                BlockTitle TEXT DEFAULT '',
                BlockCount TEXT DEFAULT '',
                Floors TEXT DEFAULT '',
                Units TEXT DEFAULT '',
                Issuer TEXT DEFAULT '',
                PermitNumber TEXT DEFAULT '',
                PermitDate TEXT DEFAULT '',
                ReleaseDate TEXT DEFAULT '',
                LandArea TEXT DEFAULT '',
                ParafArea TEXT DEFAULT '',
                Address TEXT DEFAULT '',
                PlanZone TEXT DEFAULT '',
                UsageType TEXT DEFAULT '',
                CapacityArea TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Engineers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Discipline TEXT DEFAULT '',
                Name TEXT DEFAULT '',
                Role TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Fees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Discipline TEXT DEFAULT '',
                ServiceType TEXT DEFAULT '',
                Stage TEXT DEFAULT '',
                StartDate TEXT DEFAULT '',
                EndDate TEXT DEFAULT '',
                Amount TEXT DEFAULT '',
                PayStatus TEXT DEFAULT '',
                ConfirmStatus TEXT DEFAULT '',
                AmountType TEXT DEFAULT '',
                Description TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                RowNo TEXT DEFAULT '',
                ReportType TEXT DEFAULT '',
                Stage TEXT DEFAULT '',
                Engineer TEXT DEFAULT '',
                Discipline TEXT DEFAULT '',
                VisitDate TEXT DEFAULT '',
                CeilingCount TEXT DEFAULT '',
                Indicator TEXT DEFAULT '',
                HasFile INTEGER DEFAULT 0,
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Changes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SnapshotId INTEGER NOT NULL,
                CaseNumber TEXT NOT NULL,
                Owner TEXT DEFAULT '',
                ChangeType TEXT DEFAULT '',
                FieldName TEXT DEFAULT '',
                OldValue TEXT DEFAULT '',
                NewValue TEXT DEFAULT '',
                FOREIGN KEY (SnapshotId) REFERENCES Snapshots(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS ActiveSnapshot (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                SnapshotId INTEGER NOT NULL
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS GeneratedReports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseNumber TEXT NOT NULL,
                Stage INTEGER NOT NULL,
                TemplateName TEXT DEFAULT '',
                OutputPath TEXT DEFAULT '',
                CreatedAt TEXT DEFAULT '',
                Status TEXT DEFAULT 'generated',
                SentAt TEXT DEFAULT '',
                OwnerName TEXT DEFAULT '',
                NumberFormat TEXT DEFAULT 'Persian'
            )
            """);

        // Migration: add columns if missing
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN OwnerName TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN NumberFormat TEXT DEFAULT 'Persian'"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FileExists INTEGER DEFAULT 1"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FolderName TEXT"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN ActionLog TEXT"); } catch { }
        
        // FollowUpEdits table for persisting follow-up edits
        Execute("""
            CREATE TABLE IF NOT EXISTS FollowUpEdits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseNumber TEXT NOT NULL,
                Description TEXT DEFAULT '',
                CaseNumberEdit TEXT DEFAULT '',
                OwnerEdit TEXT DEFAULT '',
                AddressEdit TEXT DEFAULT '',
                OwnerMobileEdit TEXT DEFAULT '',
                CreatedAt TEXT DEFAULT '',
                UpdatedAt TEXT DEFAULT '',
                UNIQUE(CaseNumber)
            )
            """);
    }

    public long CreateSnapshot(DateTime now)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Snapshots (CreatedAt, Status) VALUES (@t, 'pending'); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@t", now.ToString("O"));
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
    }

    public void FinalizeSnapshot(long id, int caseCount)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE Snapshots SET Status='done', CaseCount=@c WHERE Id=@id";
        cmd.Parameters.AddWithValue("@c", caseCount);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public long GetLastValidSnapshotId()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Snapshots WHERE Status='done' ORDER BY Id DESC LIMIT 1";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
    }

    public List<Case> LoadCases(long snapshotId)
    {
        var cases = new List<Case>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Cases WHERE SnapshotId=@sid";
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var c = new Case
            {
                Id = Convert.ToInt64(reader["Id"]),
                CaseNumber = reader.GetString("CaseNumber"),
                Serial = reader.GetString("Serial"),
                Owner = reader.GetString("Owner"),
                OwnerMobile = reader.GetString("OwnerMobile"),
                Responsibility = reader.GetString("Responsibility"),
                CapacityDate = reader.GetString("CapacityDate"),
                Office = reader.GetString("Office"),
                ReportDate1 = reader.GetString("ReportDate1"),
                ReportDate2 = reader.GetString("ReportDate2"),
                ReportDate3 = reader.GetString("ReportDate3")
            };
            LoadSpecification(c);
            LoadEngineers(c);
            LoadFees(c);
            LoadReports(c);
            cases.Add(c);
        }
        return cases;
    }

    private void LoadSpecification(Case c)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Specifications WHERE CaseId=@cid LIMIT 1";
        cmd.Parameters.AddWithValue("@cid", c.Id);
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            c.Specification = new CaseSpecification
            {
                BuildingGroup = r.GetString("BuildingGroup"),
                RenovationCode = r.GetString("RenovationCode"),
                PlanInstructionNo = r.GetString("PlanInstructionNo"),
                PlanInstructionType = r.GetString("PlanInstructionType"),
                PlanInstructionDate = r.GetString("PlanInstructionDate"),
                StructureType = r.GetString("StructureType"),
                BlockTitle = r.GetString("BlockTitle"),
                BlockCount = r.GetString("BlockCount"),
                Floors = r.GetString("Floors"),
                Units = r.GetString("Units"),
                Issuer = r.GetString("Issuer"),
                PermitNumber = r.GetString("PermitNumber"),
                PermitDate = r.GetString("PermitDate"),
                ReleaseDate = r.GetString("ReleaseDate"),
                LandArea = r.GetString("LandArea"),
                ParafArea = r.GetString("ParafArea"),
                Address = r.GetString("Address"),
                PlanZone = r.GetString("PlanZone"),
                UsageType = r.GetString("UsageType"),
                CapacityArea = r.GetString("CapacityArea")
            };
        }
    }

    private void LoadEngineers(Case c)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Engineers WHERE CaseId=@cid";
        cmd.Parameters.AddWithValue("@cid", c.Id);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            c.Engineers.Add(new Engineer(r.GetString("Discipline"), r.GetString("Name"), r.GetString("Role")));
    }

    private void LoadFees(Case c)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Fees WHERE CaseId=@cid";
        cmd.Parameters.AddWithValue("@cid", c.Id);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            c.Fees.Add(new Fee(r.GetString("Discipline"), r.GetString("ServiceType"), r.GetString("Stage"),
                r.GetString("StartDate"), r.GetString("EndDate"), r.GetString("Amount"),
                r.GetString("PayStatus"), r.GetString("ConfirmStatus"), r.GetString("AmountType"),
                r.GetString("Description")));
    }

    private void LoadReports(Case c)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Reports WHERE CaseId=@cid";
        cmd.Parameters.AddWithValue("@cid", c.Id);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            c.Reports.Add(new ReportRecord(r.GetString("RowNo"), r.GetString("ReportType"), r.GetString("Stage"),
                r.GetString("Engineer"), r.GetString("Discipline"), r.GetString("VisitDate"),
                r.GetString("CeilingCount"), r.GetString("Indicator"), r.GetInt32("HasFile") == 1));
    }

    public int GetActiveSnapshotId()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT SnapshotId FROM ActiveSnapshot WHERE Id=1";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public void SetActiveSnapshot(long snapshotId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO ActiveSnapshot (Id, SnapshotId) VALUES (1, @sid)";
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        cmd.ExecuteNonQuery();
    }

    public List<(string CaseNumber, int Stage, string OutputPath, string CreatedAt, string Status, string OwnerName, string NumberFormat, string FolderName)> GetGeneratedReports()
    {
        var list = new List<(string, int, string, string, string, string, string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT CaseNumber, Stage, OutputPath, CreatedAt, Status, COALESCE(OwnerName,''), COALESCE(NumberFormat,'Persian'), COALESCE(FolderName,'') FROM GeneratedReports ORDER BY CaseNumber, Stage";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7)));
        return list;
    }

    public bool HasGeneratedReport(string caseNumber, int stage)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        cmd.Parameters.AddWithValue("@st", stage);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void DeleteGeneratedReport(string caseNumber, int stage)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        cmd.Parameters.AddWithValue("@st", stage);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGeneratedReportByPath(string outputPath)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM GeneratedReports WHERE OutputPath=@op";
        cmd.Parameters.AddWithValue("@op", outputPath);
        cmd.ExecuteNonQuery();
    }

    public void SaveGeneratedReport(string caseNumber, int stage, string template, string outputPath, string ownerName = "", string numberFormat = "Persian")
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO GeneratedReports (CaseNumber, Stage, TemplateName, OutputPath, CreatedAt, Status, OwnerName, NumberFormat)
            VALUES (@cn, @st, @tp, @op, @at, 'generated', @on, @nf)";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        cmd.Parameters.AddWithValue("@st", stage);
        cmd.Parameters.AddWithValue("@tp", template);
        cmd.Parameters.AddWithValue("@op", outputPath);
        cmd.Parameters.AddWithValue("@at", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
        cmd.Parameters.AddWithValue("@on", ownerName);
        cmd.Parameters.AddWithValue("@nf", numberFormat);
        cmd.ExecuteNonQuery();
    }

    public (int added, int removed, int existing) SyncOutputFolder(string outputPath)
    {
        int added = 0, removed = 0, existing = 0;

        // Get all existing records
        var existingReports = GetGeneratedReports();
        var existingPaths = new HashSet<string>(existingReports.Select(r => r.OutputPath));

        // Scan output folder
        var allFiles = new HashSet<string>();
        if (Directory.Exists(outputPath))
        {
            foreach (var file in Directory.GetFiles(outputPath, "*.docx", SearchOption.AllDirectories))
            {
                allFiles.Add(file);
            }
        }

        // Find new files not in DB
        foreach (var file in allFiles)
        {
            if (!existingPaths.Contains(file))
            {
                // Extract folder name and case info from path
                var folder = Path.GetDirectoryName(file) ?? "";
                var folderName = new DirectoryInfo(folder).Name;
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Try to extract case number and owner from folder name
                // Format: "01 - owner - 514-1404"
                var parts = folderName.Split(" - ", 3);
                var ownerName = parts.Length >= 2 ? parts[1].Trim() : "";
                var caseNumber = ""; // Will try to find from DB later

                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "INSERT INTO GeneratedReports (CaseNumber, Stage, TemplateName, OutputPath, CreatedAt, Status, OwnerName, NumberFormat, FolderName, FileExists) VALUES (@cn, @st, @tp, @op, @at, @st2, @on, @nf, @fn, 1)";
                cmd.Parameters.AddWithValue("@cn", caseNumber);
                cmd.Parameters.AddWithValue("@st", 1);
                cmd.Parameters.AddWithValue("@tp", "مکانیک مرحله اول");
                cmd.Parameters.AddWithValue("@op", file);
                cmd.Parameters.AddWithValue("@at", File.GetCreationTime(file).ToString("yyyy/MM/dd HH:mm"));
                cmd.Parameters.AddWithValue("@st2", "synced");
                cmd.Parameters.AddWithValue("@on", ownerName);
                cmd.Parameters.AddWithValue("@nf", "Persian");
                cmd.Parameters.AddWithValue("@fn", folderName);
                cmd.ExecuteNonQuery();
                added++;
            }
            else
            {
                existing++;
            }
        }

        // Find files in DB but not on disk
        foreach (var report in existingReports)
        {
            if (!allFiles.Contains(report.OutputPath))
            {
                // Mark as missing
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "UPDATE GeneratedReports SET FileExists=0 WHERE OutputPath=@op";
                cmd.Parameters.AddWithValue("@op", report.OutputPath);
                cmd.ExecuteNonQuery();
                removed++;
            }
        }

        return (added, removed, existing);
    }

    // ========== FollowUpEdits Methods ==========

    /// <summary>
    /// دریافت ویرایش‌های پیگیری برای یک پرونده
    /// </summary>
    public (string Description, string CaseNumberEdit, string OwnerEdit, string AddressEdit, string OwnerMobileEdit)? GetFollowUpEdit(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit FROM FollowUpEdits WHERE CaseNumber=@cn";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return (
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4)
            );
        }
        return null;
    }

    /// <summary>
    /// دریافت تمام ویرایش‌های پیگیری
    /// </summary>
    public Dictionary<string, (string Description, string CaseNumberEdit, string OwnerEdit, string AddressEdit, string OwnerMobileEdit)> GetAllFollowUpEdits()
    {
        var result = new Dictionary<string, (string, string, string, string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit FROM FollowUpEdits";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result[r.GetString(0)] = (r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5));
        }
        return result;
    }

    /// <summary>
    /// ذخیره ویرایش‌های پیگیری
    /// </summary>
    public void SaveFollowUpEdit(string caseNumber, string description, string caseNumberEdit, string ownerEdit, string addressEdit, string ownerMobileEdit)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt)
            VALUES (@cn, @desc, @cnEdit, @ownerEdit, @addrEdit, @mobileEdit, COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), @now), @now)";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        cmd.Parameters.AddWithValue("@desc", description);
        cmd.Parameters.AddWithValue("@cnEdit", caseNumberEdit);
        cmd.Parameters.AddWithValue("@ownerEdit", ownerEdit);
        cmd.Parameters.AddWithValue("@addrEdit", addressEdit);
        cmd.Parameters.AddWithValue("@mobileEdit", ownerMobileEdit);
        cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// حذف ویرایش‌های پیگیری
    /// </summary>
    public void DeleteFollowUpEdit(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM FollowUpEdits WHERE CaseNumber=@cn";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        cmd.ExecuteNonQuery();
    }

    public string GetSetting(string key, string defaultValue = "")
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=@k";
        cmd.Parameters.AddWithValue("@k", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    public Dictionary<string, string> LoadSettings()
    {
        var settings = new Dictionary<string, string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            settings[r.GetString(0)] = r.GetString(1);
        }
        return settings;
    }

    public void SaveSetting(string key, string value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v)";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }

    public List<(string CaseNumber, string Owner, string Field, string OldValue, string NewValue)> GetChanges(long snapshotId)
    {
        var list = new List<(string, string, string, string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT CaseNumber, Owner, FieldName, OldValue, NewValue FROM Changes WHERE SnapshotId=@sid";
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        return list;
    }

    public void SaveChanges(long snapshotId, IReadOnlyList<Core.Diff.FieldChange> changes)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Changes (SnapshotId, CaseNumber, Owner, ChangeType, FieldName, OldValue, NewValue) VALUES (@sid, @cn, @owner, @ct, @fn, @ov, @nv)";
        cmd.Parameters.AddWithValue("@sid", snapshotId);
        cmd.Parameters.AddWithValue("@cn", "");
        cmd.Parameters.AddWithValue("@owner", "");
        cmd.Parameters.AddWithValue("@ct", "");
        cmd.Parameters.AddWithValue("@fn", "");
        cmd.Parameters.AddWithValue("@ov", "");
        cmd.Parameters.AddWithValue("@nv", "");
        
        foreach (var ch in changes)
        {
            cmd.Parameters["@cn"].Value = ch.CaseNumber;
            cmd.Parameters["@owner"].Value = ch.Owner;
            cmd.Parameters["@ct"].Value = ch.Type.ToString();
            cmd.Parameters["@fn"].Value = ch.Field;
            cmd.Parameters["@ov"].Value = ch.OldValue;
            cmd.Parameters["@nv"].Value = ch.NewValue;
            cmd.ExecuteNonQuery();
        }
    }

    public string GetTheme()
    {
        return GetSetting("theme", "");
    }

    public void SaveTheme(string json)
    {
        SaveSetting("theme", json);
    }

    // ========== CaseScraper Methods ==========

    /// <summary>
    /// بارگذاری مشخصات برای یک پرونده
    /// </summary>
    public CaseSpecification? LoadSpecificationForCase(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT s.* FROM Specifications s 
                           INNER JOIN Cases c ON s.CaseId = c.Id 
                           WHERE c.CaseNumber = @cn LIMIT 1";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return new CaseSpecification
            {
                BuildingGroup = r.GetString("BuildingGroup"),
                RenovationCode = r.GetString("RenovationCode"),
                PlanInstructionNo = r.GetString("PlanInstructionNo"),
                PlanInstructionType = r.GetString("PlanInstructionType"),
                PlanInstructionDate = r.GetString("PlanInstructionDate"),
                StructureType = r.GetString("StructureType"),
                BlockTitle = r.GetString("BlockTitle"),
                BlockCount = r.GetString("BlockCount"),
                Floors = r.GetString("Floors"),
                Units = r.GetString("Units"),
                Issuer = r.GetString("Issuer"),
                PermitNumber = r.GetString("PermitNumber"),
                PermitDate = r.GetString("PermitDate"),
                ReleaseDate = r.GetString("ReleaseDate"),
                LandArea = r.GetString("LandArea"),
                ParafArea = r.GetString("ParafArea"),
                Address = r.GetString("Address"),
                PlanZone = r.GetString("PlanZone"),
                UsageType = r.GetString("UsageType"),
                CapacityArea = r.GetString("CapacityArea")
            };
        }
        return null;
    }

    /// <summary>
    /// دریافت تعداد گزارش‌ها برای یک پرونده
    /// </summary>
    public int LoadReportCountForCase(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM Reports r 
                           INNER JOIN Cases c ON r.CaseId = c.Id 
                           WHERE c.CaseNumber = @cn";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    /// <summary>
    /// دریافت تعداد مهندسین برای یک پرونده
    /// </summary>
    public int LoadEngineerCountForCase(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM Engineers e 
                           INNER JOIN Cases c ON e.CaseId = c.Id 
                           WHERE c.CaseNumber = @cn";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    /// <summary>
    /// دریافت تعداد حق‌الزحمه‌ها برای یک پرونده
    /// </summary>
    public int LoadFeeCountForCase(string caseNumber)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM Fees f 
                           INNER JOIN Cases c ON f.CaseId = c.Id 
                           WHERE c.CaseNumber = @cn";
        cmd.Parameters.AddWithValue("@cn", caseNumber);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    /// <summary>
        /// ذخیره اسنپشات و Cases (برای SyncOrchestrator و ExtractionController)
        /// </summary>
        public void SaveSnapshot(long snapshotId, List<Case> cases)
        {
            // Wrap entire save in a transaction for 100x speed improvement
            using var transaction = _conn.BeginTransaction();
            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT OR REPLACE INTO Snapshots (Id, CreatedAt, CaseCount, Status) 
                                   VALUES (@id, @createdAt, @caseCount, @status)";
                cmd.Parameters.AddWithValue("@id", snapshotId);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("O"));
                cmd.Parameters.AddWithValue("@caseCount", cases.Count);
                cmd.Parameters.AddWithValue("@status", "done");
                cmd.ExecuteNonQuery();

                foreach (var c in cases)
                {
                    SaveCase(snapshotId, c, transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

                /// <summary>
                /// ذخیره اسنپشات (برای ExtractionController - آرایه)
                /// </summary>
                public void SaveSnapshot(long snapshotId, Case[] cases)
                {
                    SaveSnapshot(snapshotId, cases.ToList());
                }

                /// <summary>
                /// ذخیره یک Case
                /// </summary>
        private void SaveCase(long snapshotId, Case c, SqliteTransaction? transaction = null)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO Cases (SnapshotId, CaseNumber, Serial, Owner, OwnerMobile, Responsibility, CapacityDate, Office, ReportDate1, ReportDate2, ReportDate3)
                               VALUES (@sid, @cn, @serial, @owner, @mobile, @resp, @cap, @office, @rd1, @rd2, @rd3);
                               SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            cmd.Parameters.AddWithValue("@cn", c.CaseNumber);
            cmd.Parameters.AddWithValue("@serial", c.Serial);
            cmd.Parameters.AddWithValue("@owner", c.Owner);
            cmd.Parameters.AddWithValue("@mobile", c.OwnerMobile);
            cmd.Parameters.AddWithValue("@resp", c.Responsibility);
            cmd.Parameters.AddWithValue("@cap", c.CapacityDate);
            cmd.Parameters.AddWithValue("@office", c.Office);
            cmd.Parameters.AddWithValue("@rd1", c.ReportDate1);
            cmd.Parameters.AddWithValue("@rd2", c.ReportDate2);
            cmd.Parameters.AddWithValue("@rd3", c.ReportDate3);
            var caseId = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        
            if (c.Specification != null)
            {
                SaveSpecification(caseId, c.Specification, transaction);
            }
        
            foreach (var e in c.Engineers)
            {
                SaveEngineer(caseId, e, transaction);
            }
        
            foreach (var f in c.Fees)
            {
                SaveFee(caseId, f, transaction);
            }
        
            foreach (var r in c.Reports)
            {
                SaveReport(caseId, r, transaction);
            }
        }

        private void SaveSpecification(long caseId, CaseSpecification s, SqliteTransaction? transaction = null)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO Specifications (CaseId, BuildingGroup, RenovationCode, PlanInstructionNo, PlanInstructionType, PlanInstructionDate, StructureType, BlockTitle, BlockCount, Floors, Units, Issuer, PermitNumber, PermitDate, ReleaseDate, LandArea, ParafArea, Address, PlanZone, UsageType, CapacityArea)
                               VALUES (@cid, @bg, @rc, @pin, @pit, @pid, @st, @bt, @bc, @fl, @un, @is, @pn, @pd, @rd, @la, @pa, @addr, @pz, @ut, @ca)";
            cmd.Parameters.AddWithValue("@cid", caseId);
            cmd.Parameters.AddWithValue("@bg", s.BuildingGroup);
            cmd.Parameters.AddWithValue("@rc", s.RenovationCode);
            cmd.Parameters.AddWithValue("@pin", s.PlanInstructionNo);
            cmd.Parameters.AddWithValue("@pit", s.PlanInstructionType);
            cmd.Parameters.AddWithValue("@pid", s.PlanInstructionDate);
            cmd.Parameters.AddWithValue("@st", s.StructureType);
            cmd.Parameters.AddWithValue("@bt", s.BlockTitle);
            cmd.Parameters.AddWithValue("@bc", s.BlockCount);
            cmd.Parameters.AddWithValue("@fl", s.Floors);
            cmd.Parameters.AddWithValue("@un", s.Units);
            cmd.Parameters.AddWithValue("@is", s.Issuer);
            cmd.Parameters.AddWithValue("@pn", s.PermitNumber);
            cmd.Parameters.AddWithValue("@pd", s.PermitDate);
            cmd.Parameters.AddWithValue("@rd", s.ReleaseDate);
            cmd.Parameters.AddWithValue("@la", s.LandArea);
            cmd.Parameters.AddWithValue("@pa", s.ParafArea);
            cmd.Parameters.AddWithValue("@addr", s.Address);
            cmd.Parameters.AddWithValue("@pz", s.PlanZone);
            cmd.Parameters.AddWithValue("@ut", s.UsageType);
            cmd.Parameters.AddWithValue("@ca", s.CapacityArea);
            cmd.ExecuteNonQuery();
        }

        private void SaveEngineer(long caseId, Engineer e, SqliteTransaction? transaction = null)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO Engineers (CaseId, Discipline, Name, Role) VALUES (@cid, @d, @n, @r)";
            cmd.Parameters.AddWithValue("@cid", caseId);
            cmd.Parameters.AddWithValue("@d", e.Discipline);
            cmd.Parameters.AddWithValue("@n", e.Name);
            cmd.Parameters.AddWithValue("@r", e.Role);
            cmd.ExecuteNonQuery();
        }

        private void SaveFee(long caseId, Fee f, SqliteTransaction? transaction = null)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO Fees (CaseId, Discipline, ServiceType, Stage, StartDate, EndDate, Amount, PayStatus, ConfirmStatus, AmountType, Description)
                               VALUES (@cid, @d, @st, @s, @sd, @ed, @a, @ps, @cs, @at, @desc)";
            cmd.Parameters.AddWithValue("@cid", caseId);
            cmd.Parameters.AddWithValue("@d", f.Discipline);
            cmd.Parameters.AddWithValue("@st", f.ServiceType);
            cmd.Parameters.AddWithValue("@s", f.Stage);
            cmd.Parameters.AddWithValue("@sd", f.StartDate);
            cmd.Parameters.AddWithValue("@ed", f.EndDate);
            cmd.Parameters.AddWithValue("@a", f.Amount);
            cmd.Parameters.AddWithValue("@ps", f.PayStatus);
            cmd.Parameters.AddWithValue("@cs", f.ConfirmStatus);
            cmd.Parameters.AddWithValue("@at", f.AmountType);
            cmd.Parameters.AddWithValue("@desc", f.Description);
            cmd.ExecuteNonQuery();
        }

        private void SaveReport(long caseId, ReportRecord r, SqliteTransaction? transaction = null)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO Reports (CaseId, RowNo, ReportType, Stage, Engineer, Discipline, VisitDate, CeilingCount, Indicator, HasFile)
                               VALUES (@cid, @rn, @rt, @s, @e, @d, @vd, @cc, @ind, @hf)";
            cmd.Parameters.AddWithValue("@cid", caseId);
            cmd.Parameters.AddWithValue("@rn", r.RowNo);
            cmd.Parameters.AddWithValue("@rt", r.ReportType);
            cmd.Parameters.AddWithValue("@s", r.Stage);
            cmd.Parameters.AddWithValue("@e", r.Engineer);
            cmd.Parameters.AddWithValue("@d", r.Discipline);
            cmd.Parameters.AddWithValue("@vd", r.VisitDate);
            cmd.Parameters.AddWithValue("@cc", r.CeilingCount);
            cmd.Parameters.AddWithValue("@ind", r.Indicator);
            cmd.Parameters.AddWithValue("@hf", r.HasFile ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

    // ========== HistoryViewModel Methods ==========

    /// <summary>
    /// دریافت تمام اسنپشات‌ها
    /// </summary>
    public List<(long Id, string CreatedAt, int CaseCount, string Status)> GetSnapshots()
    {
        var list = new List<(long, string, int, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT Id, CreatedAt, CaseCount, Status FROM Snapshots ORDER BY Id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((
                r.GetInt64(0),
                r.GetString(1),
                r.GetInt32(2),
                r.GetString(3)
            ));
        }
        return list;
    }

    /// <summary>
    /// حذف اسنپشات
    /// </summary>
    public void DeleteSnapshot(long snapshotId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Snapshots WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", snapshotId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
        /// ادغام اسنپشات‌ها (برای HistoryViewModel)
        /// </summary>
        public long MergeSnapshots(List<long> snapshotIds)
        {
            if (snapshotIds.Count < 2)
                throw new ArgumentException("حداقل ۲ اسنپ‌شات برای ادغام لازم است");
        
            // انتخاب اولین اسنپشات به عنوان مقصد
            var targetId = snapshotIds[0];
        
            // انتقال Cases از بقیه اسنپشات‌ها به مقصد
            for (int i = 1; i < snapshotIds.Count; i++)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = @"UPDATE Cases SET SnapshotId=@targetId WHERE SnapshotId=@sourceId";
                cmd.Parameters.AddWithValue("@targetId", targetId);
                cmd.Parameters.AddWithValue("@sourceId", snapshotIds[i]);
                cmd.ExecuteNonQuery();
            
                // حذف اسنپشات منبع
                DeleteSnapshot(snapshotIds[i]);
            }
        
            // بروزرسانی تعداد Cases در اسنپشات مقصد
            using var updateCmd = _conn.CreateCommand();
            updateCmd.CommandText = @"UPDATE Snapshots SET CaseCount = (SELECT COUNT(*) FROM Cases WHERE SnapshotId=@id) WHERE Id=@id";
            updateCmd.Parameters.AddWithValue("@id", targetId);
            updateCmd.ExecuteNonQuery();
        
            return targetId;
        }
}
