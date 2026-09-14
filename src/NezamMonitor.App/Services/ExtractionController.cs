using System.IO;
using NezamMonitor.App.ViewModels;
using System.Threading;
using System.Windows.Input;
using NezamMonitor.Core.Api;
using NezamMonitor.Core.Browser;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.Services;

/// <summary>
/// Extraction controller — API-first with Playwright fallback for Reports.
/// </summary>
public sealed class ExtractionController : ViewModelBase
{
    private static ExtractionController? _instance;
    private static readonly object _lock = new();

    private bool _isBusy;
    private bool _isPaused;
    private string _statusMessage = "آماده";
    private double _progress;
    private string _logText = "";
    private CancellationTokenSource? _cts;
    private ManualResetEventSlim? _pauseEvent;

    public static ExtractionController Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                    _instance ??= new ExtractionController();
            return _instance;
        }
    }

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }
    private int _maxCases;
    public int MaxCases { get => _maxCases; set => SetProperty(ref _maxCases, value); }
    private bool _filterIncompleteOnly;
    public bool FilterIncompleteOnly { get => _filterIncompleteOnly; set => SetProperty(ref _filterIncompleteOnly, value); }
    private bool _extractEngineers = true;
    public bool ExtractEngineers { get => _extractEngineers; set => SetProperty(ref _extractEngineers, value); }
    private bool _extractFees = true;
    public bool ExtractFees { get => _extractFees; set => SetProperty(ref _extractFees, value); }
    private bool _extractSpecifications = true;
    public bool ExtractSpecifications { get => _extractSpecifications; set => SetProperty(ref _extractSpecifications, value); }
    private bool _extractReports = true;
    public bool ExtractReports { get => _extractReports; set => SetProperty(ref _extractReports, value); }
    private bool _extractReportFiles = false;
    public bool ExtractReportFiles { get => _extractReportFiles; set => SetProperty(ref _extractReportFiles, value); }
    private string _outputPath = "";
        public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
        private string _username = "";
        public string Username { get => _username; set => SetProperty(ref _username, value); }
        private string _password = "";
        public string Password { get => _password; set => SetProperty(ref _password, value); }
        private bool _rememberMe = false;
        public bool RememberMe { get => _rememberMe; set => SetProperty(ref _rememberMe, value); }
    private bool _updateAllSpecifications = true;
    public bool UpdateAllSpecifications { get => _updateAllSpecifications; set => SetProperty(ref _updateAllSpecifications, value); }
    private int _startFromIndex = 1;
    public int StartFromIndex { get => _startFromIndex; set => SetProperty(ref _startFromIndex, value); }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand LoadLatestCommand { get; }

    private ExtractionController()
        {
            StartCommand = new AsyncRelayCommand(StartExtractionAsync, () => !IsBusy);
            StopCommand = new RelayCommand(StopExtraction, () => IsBusy);
            PauseResumeCommand = new RelayCommand(PauseResume);
            LoadLatestCommand = new RelayCommand(LoadLatest);

            // Default output path
            var db = DatabaseService.Instance;
            _outputPath = db.GetSetting("output_path") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outputs");
        }

    public void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    private async Task StartExtractionAsync()
    {
        IsBusy = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);

        try
        {
            var db = DatabaseService.Instance;

            // Use credentials from UI (Username/Password properties)
            // Fall back to database if UI is empty
            var username = Username;
            var password = Password;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                var settings = db.LoadSettings();
                if (settings.TryGetValue("username", out var u)) username = u;
                if (settings.TryGetValue("password", out var p)) password = p;
            }

            // Save credentials for next time if RememberMe is checked
            if (RememberMe && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                db.SaveSetting("username", username);
                db.SaveSetting("password", password);
                db.SaveSetting("remember_me", "true");
            }

            AppendLog("شروع استخراج API...");

            var startTime = DateTime.Now;
            var cases = await ExtractViaApiAsync(username, password, db);

            // Download report files
            if (cases.Count > 0 && ExtractReportFiles)
            {
                AppendLog("دانلود فایل‌های گزارش‌ها...");
                await DownloadReportFilesAsync(cases, username, password);
            }

            var elapsed = DateTime.Now - startTime;
            AppendLog($"تکمیل: {cases.Count} پرونده در {elapsed.TotalSeconds:F0} ثانیه");

            if (cases.Count > 0)
            {
                AppendLog("ذخیره در دیتابیس...");
                StatusMessage = "ذخیره اسنپشات...";
                // Run DB save on background thread to avoid UI freeze
                await Task.Run(() =>
                {
                    var snapshotId = db.CreateSnapshot(startTime);
                    db.SaveSnapshot(snapshotId, cases);
                    db.FinalizeSnapshot(snapshotId, cases.Count);
                    db.SetActiveSnapshot(snapshotId);
                });
                AppendLog($"ذخیره شد ✓ — {cases.Count} پرونده فعال شد");
            }
            else
            {
                AppendLog("پرونده‌ای استخراج نشد");
            }

            StatusMessage = $"تکمیل — {cases.Count} پرونده";
        }
        catch (OperationCanceledException)
        {
            AppendLog("استخراج متوقف شد.");
            StatusMessage = "متوقف شد";
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
        }
        finally
        {
            IsBusy = false;
            IsPaused = false;
            Progress = 0;
            _cts?.Dispose();
            _cts = null;
            _pauseEvent?.Dispose();
            _pauseEvent = null;
        }
    }

    /// <summary>
    /// PRIMARY PATH: API-based extraction.
    /// Login → Case list → Engineers → Fees.
    /// Returns list of Cases with specs, engineers, fees populated.
    /// Reports NOT included (no API endpoint found).
    /// </summary>
    private async Task<List<Case>> ExtractViaApiAsync(string username, string password, NezamDatabase db)
    {
        var cases = new List<Case>();

        try
        {
            // Login
            StatusMessage = "ورود به سیستم...";
            using var api = new NezamApiClient();
            var loginOk = await api.LoginAsync(username, password);
            if (!loginOk)
            {
                AppendLog("خطا: ورود API ناموفق");
                return cases;
            }
            AppendLog($"ورود API موفق ✓ (userId={api.UserId})");

            // Save token for next launch
            db.SaveSetting("api_token", api.Token);

            // Get all cases
            StatusMessage = "دریافت لیست پرونده‌ها...";
            var apiCases = await api.GetCasesRawAsync();
            AppendLog($"{apiCases.Count} پرونده دریافت شد");

            // Apply start/max filters
            var startIdx = Math.Max(0, StartFromIndex - 1);
            var endIdx = MaxCases > 0 ? Math.Min(startIdx + MaxCases, apiCases.Count) : apiCases.Count;
            var casesToProcess = apiCases.Skip(startIdx).Take(endIdx - startIdx).ToList();

            int processed = 0;
            int total = casesToProcess.Count;

            foreach (var apiCase in casesToProcess)
            {
                _cts?.Token.ThrowIfCancellationRequested();
                _pauseEvent?.Wait(_cts?.Token ?? CancellationToken.None);

                processed++;
                Progress = (double)processed / total * 100;
                StatusMessage = IsPaused
                    ? $"متوقف شده — {processed}/{total}"
                    : $"استخراج {processed}/{total}";

                var serial = NezamApiClient.S(apiCase, "das_serial");
                var caseNum = $"{NezamApiClient.S(apiCase, "das_year")}/{NezamApiClient.S(apiCase, "das_number")}";
                var owner = $"{NezamApiClient.S(apiCase, "own_name")} {NezamApiClient.S(apiCase, "own_famil")}".Trim();

                AppendLog($"[{processed}/{total}] {serial} | {caseNum} | {owner}");

                try
                {
                    // Map case from API
                    var caseModel = ApiExtractor.MapCase(apiCase);

                    // Filter incomplete if requested
                    if (FilterIncompleteOnly && db != null)
                    {
                        var cn = caseModel.CaseNumber;
                        var specOk = !ExtractSpecifications || db.LoadSpecificationForCase(cn) != null;
                        var engOk = !ExtractEngineers || (db.LoadEngineerCountForCase(cn)) > 0;
                        var feesOk = !ExtractFees || (db.LoadFeeCountForCase(cn)) > 0;
                        if (specOk && engOk && feesOk)
                        {
                            AppendLog($"  SKIP: {owner} (داده‌ها موجود است)");
                            continue;
                        }
                    }

                    // Get engineers
                    if (ExtractEngineers)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;
                        var apiEngineers = await api.GetEngineersRawAsync(dbId);
                        caseModel.Engineers = ApiExtractor.MapEngineers(apiEngineers);
                    }

                    // Get fees
                    if (ExtractFees)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal) && dbVal is int dbI) dbId = dbI;
                        var apiFees = await api.GetFeesRawAsync(dbId);
                        caseModel.Fees = ApiExtractor.MapFees(apiFees);
                    }

                    // Get reports
                    if (ExtractReports)
                    {
                        var dbId = 0;
                        if (apiCase.TryGetValue("db_id", out var dbVal2) && dbVal2 is int dbI2) dbId = dbI2;
                        var apiReports = await api.GetReportsRawAsync(dbId);
                        caseModel.Reports = ApiExtractor.MapReports(apiReports);
                    }

                    // Skip if specs already complete
                    if (!UpdateAllSpecifications && caseModel.Specification != null)
                    {
                        var s = caseModel.Specification;
                        bool specComplete = !string.IsNullOrEmpty(s.BuildingGroup)
                            && !string.IsNullOrEmpty(s.PermitNumber)
                            && !string.IsNullOrEmpty(s.StructureType);
                        if (specComplete) caseModel.Specification = null;
                    }

                    cases.Add(caseModel);
                    AppendLog($"  OK: {owner} — Eng:{caseModel.Engineers.Count} Fees:{caseModel.Fees.Count}");
                }
                catch (Exception ex)
                {
                    AppendLog($"  FAIL: {owner} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"خطای API: {ex.Message}");
        }

        return cases;
    }

    /// <summary>
    /// FALLBACK PATH: Playwright-based Reports extraction only.
    /// Opens browser, navigates to each case's reports page, extracts report data.
    /// </summary>
    private async Task ExtractReportsViaPlaywrightAsync(
        List<Case> cases, string username, string password, NezamDatabase db)
    {
        PlaywrightBrowser? browser = null;
        try
        {
            browser = new PlaywrightBrowser();
            await browser.LaunchAsync(headless: false);
            AppendLog("مرورگر برای گزارش‌ها راه‌اندازی شد ✓");

            var scraper = new CaseScraper(browser);
            var loginOk = await scraper.LoginAsync(username, password);
            if (!loginOk)
            {
                AppendLog("خطا: ورود مرورگر ناموفق — گزارش‌ها استخراج نشد");
                return;
            }
            await scraper.NavigateToTableAsync();

            int reportCount = 0;
            for (int i = 0; i < cases.Count; i++)
            {
                _cts?.Token.ThrowIfCancellationRequested();
                _pauseEvent?.Wait(_cts?.Token ?? CancellationToken.None);

                var c = cases[i];
                StatusMessage = $"گزارش {i + 1}/{cases.Count}";

                try
                {
                    var row = await scraper.FindRowForReportsAsync(c.Serial);
                    if (row == null)
                    {
                        AppendLog($"  گزارش: ردیف {c.Serial} پیدا نشد");
                        continue;
                    }

                    var reports = await scraper.ExtractReportsFromRowAsync(row);
                    if (reports.Count > 0)
                    {
                        c.Reports = reports;
                        reportCount += reports.Count;
                        AppendLog($"  گزارش: {c.CaseNumber} — {reports.Count} گزارش");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"  گزارش خطا: {c.CaseNumber} - {ex.Message}");
                }
            }

            AppendLog($"گزارش‌ها: {reportCount} مورد از {cases.Count} پرونده");
        }
        catch (Exception ex)
        {
            AppendLog($"خطای مرورگر: {ex.Message}");
        }
        finally
        {
            browser?.Dispose();
        }
    }

    private void StopExtraction()
    {
        _cts?.Cancel();
        _pauseEvent?.Set();
        StatusMessage = "در حال توقف...";
    }

    /// <summary>
    /// Download report files from API and save to outputs folder.
    /// Structure: outputs/{row}_{owner}_{caseNumber}/{reportNum}_{reportType}.{ext}
    /// </summary>
    private async Task DownloadReportFilesAsync(List<Case> cases, string username, string password)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            // Login for file download
            var payload = System.Text.Json.JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
            var loginReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://service.yazdnezam.ir:8033/panel/api/login")
            { Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
            var loginResp = await http.SendAsync(loginReq);
            var token = System.Text.Json.JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);

            // Save token for next launch (so we don't need to login again)
            DatabaseService.Instance.SaveSetting("api_token", token);

            var userResp = await http.GetAsync("http://service.yazdnezam.ir:8033/panel/api/user");
            var userJson = System.Text.Json.JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
            var userId = userJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
            var cityId = userJson.RootElement.GetProperty("user").GetProperty("user_shahrestan").GetInt32();

            // Get raw cases for db_id mapping
            var caseResp = await http.GetAsync($"http://service.yazdnezam.ir:8033/panel/api/showParvandeNezaratMeybod/{userId}/{cityId}");
            var rawCases = System.Text.Json.JsonDocument.Parse(await caseResp.Content.ReadAsStringAsync());

            // Build dbId lookup
            var dbIdMap = new Dictionary<string, int>();
            foreach (var rc in rawCases.RootElement.EnumerateArray())
            {
                var serial = NezamApiClient.S(rc, "das_serial");
                if (rc.TryGetProperty("db_id", out var dbVal) && dbVal.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dbIdMap[serial] = dbVal.GetInt32();
            }

            var outputBase = string.IsNullOrEmpty(OutputPath) 
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outputs") 
                : OutputPath;
            Directory.CreateDirectory(outputBase);

            int totalFiles = 0;
            int caseNum = 0;

            foreach (var c in cases)
            {
                caseNum++;
                if (!dbIdMap.TryGetValue(c.Serial, out var dbId)) continue;

                // Get reports for this case
                var rptPayload = System.Text.Json.JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = cityId.ToString() });
                var rptReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://service.yazdnezam.ir:8033/panel/api/getGozareshat")
                { Content = new System.Net.Http.StringContent(rptPayload, System.Text.Encoding.UTF8, "application/json") };
                var rptResp = await http.SendAsync(rptReq);
                var rptJson = System.Text.Json.JsonDocument.Parse(await rptResp.Content.ReadAsStringAsync());

                if (rptJson.RootElement.GetArrayLength() == 0) continue;

                // Create case folder
                var safeOwner = (c.Owner ?? "").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                var safeCaseNum = (c.CaseNumber ?? "").Replace("/", "_");
                var caseFolder = Path.Combine(outputBase, $"{caseNum}_{safeOwner}_{safeCaseNum}");
                Directory.CreateDirectory(caseFolder);

                int reportNum = 0;
                foreach (var r in rptJson.RootElement.EnumerateArray())
                {
                    reportNum++;
                    var imageName = NezamApiClient.S(r, "image_name");
                    if (string.IsNullOrEmpty(imageName)) continue;

                    var reportType = NezamApiClient.S(r, "brt_report_title");
                    var safeReportType = reportType.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                    var ext = Path.GetExtension(imageName).ToLower();
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                    var downloadUrl = $"http://service.yazdnezam.ir:8033/panel/Panel/public/img/gozaresh/{imageName}";
                    var fileName = $"{reportNum}_{safeReportType}{ext}";
                    var filePath = Path.Combine(caseFolder, fileName);

                    try
                    {
                        var resp = await http.GetAsync(downloadUrl);
                        if (resp.IsSuccessStatusCode)
                        {
                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(filePath, bytes);
                            totalFiles++;
                        }
                    }
                    catch { }
                }

                if (caseNum % 10 == 0 || caseNum == cases.Count)
                    AppendLog($"  فایل‌ها: {caseNum}/{cases.Count} ({totalFiles} فایل)");
            }

            AppendLog($"دانلود فایل‌ها: {totalFiles} فایل در {cases.Count} پرونده");
        }
        catch (Exception ex)
        {
            AppendLog($"خطای دانلود فایل‌ها: {ex.Message}");
        }
    }

    private void PauseResume()
    {
        if (_pauseEvent == null) return;
        if (IsPaused)
        {
            _pauseEvent.Set();
            IsPaused = false;
            AppendLog("▶ ادامه استخراج");
            StatusMessage = "در حال استخراج...";
        }
        else
        {
            _pauseEvent.Reset();
            IsPaused = true;
            AppendLog("⏸ وقفه");
            StatusMessage = "متوقف شده...";
        }
    }

    private void LoadLatest()
    {
        try
        {
            var db = DatabaseService.Instance;
            var snapshotId = db.GetLastValidSnapshotId();
            if (snapshotId == 0)
            {
                AppendLog("هنوز داده‌ای ذخیره نشده");
                StatusMessage = "داده‌ای موجود نیست";
                return;
            }
            var cases = db.LoadCases(snapshotId);
            AppendLog($"آخرین اسنپ‌شات: {cases.Count} پرونده بارگذاری شد");
            StatusMessage = $"{cases.Count} پرونده بارگذاری شد";
        }
        catch (Exception ex)
        {
            AppendLog($"خطا: {ex.Message}");
            StatusMessage = "خطا";
        }
    }
}
