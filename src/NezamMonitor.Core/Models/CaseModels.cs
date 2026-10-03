namespace NezamMonitor.Core.Models;

/// <summary>Normalized case identity used as stable key.</summary>
public sealed record CaseKey(string CaseNumber, string Serial)
{
    public override string ToString() => $"{CaseNumber}|{Serial}";
}

/// <summary>A single monitored case (پرونده).</summary>
public sealed class Case
{
    public long Id { get; set; }
    public string CaseNumber { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Owner { get; set; } = "";
    public string OwnerMobile { get; set; } = "";
    public string Responsibility { get; set; } = "";
    public string CapacityDate { get; set; } = "";   // تاریخ کسر ظرفیت
    public string Office { get; set; } = "";          // دفتر
    public string ReportDate1 { get; set; } = "";     // گزارش مرحله ۱
    public string ReportDate2 { get; set; } = "";
    public string ReportDate3 { get; set; } = "";
    public string OwnerFather { get; set; } = "";         // نام پدر
    public string OwnerNationalCode { get; set; } = "";   // کد ملی
    public string OwnerAddress { get; set; } = "";        // آدرس مالک
    public string OwnerZip { get; set; } = "";            // کد پستی
    public string OwnerTel { get; set; } = "";            // تلفن
    public string OwnerBirthLoc { get; set; } = "";       // زادگاه
    public CaseSpecification? Specification { get; set; }
    public List<Engineer> Engineers { get; set; } = new();
    public List<Fee> Fees { get; set; } = new();
    public List<ReportRecord> Reports { get; set; } = new();

    public CaseKey Key => new(CaseNumber, Serial);
}

/// <summary>Building/permit specification block (مشخصات بلوک).</summary>
public sealed class CaseSpecification
{
    public string BuildingGroup { get; set; } = "";       // گروه ساختمانی
    public string RenovationCode { get; set; } = "";      // کد نوسازی
    public string PlanInstructionNo { get; set; } = "";   // شماره دستور تهیه نقشه
    public string PlanInstructionType { get; set; } = ""; // نوع دستور تهیه نقشه
    public string PlanInstructionDate { get; set; } = ""; // تاریخ دستور تهیه نقشه
    public string LandArea { get; set; } = "";            // مساحت زمین
    public string ParafArea { get; set; } = "";           // متراژ پاراف
    public string CapacityArea { get; set; } = "";        // متراژ کسر ظرفیت نظارت
    public string StructureType { get; set; } = "";       // نوع سازه
    public string BlockTitle { get; set; } = "";          // عنوان بلوک
    public string BlockCount { get; set; } = "";          // تعداد بلوک
    public string Floors { get; set; } = "";              // تعداد طبقات
    public string Units { get; set; } = "";               // تعداد واحد
    public string Issuer { get; set; } = "";              // صادر کننده
    public string PermitNumber { get; set; } = "";        // شماره پروانه
    public string PermitDate { get; set; } = "";          // تاریخ صدور پروانه
    public string ReleaseDate { get; set; } = "";         // تاریخ ترخیص
    public string PlanZone { get; set; } = "";            // محدوده طرح
    public string Address { get; set; } = "";             // آدرس
    public string UsageType { get; set; } = "";           // نوع کاربری
}

/// <summary>Responsible engineer/inspector (ناظر).</summary>
public sealed record Engineer(string Discipline, string Name, string Role = "", string Phone = "", string DesignLevel = "", string SupervisionLevel = "", string ExecutionLevel = "")
{
    /// <summary>Stable identity: normalized discipline + name + role.</summary>
    public string Identity => $"{TextNormalizer.Normalize(Discipline)}|{TextNormalizer.Normalize(Name)}|{TextNormalizer.Normalize(Role)}";
}

/// <summary>A fee/payment record (حق‌الزحمه).</summary>
public sealed record Fee(
    string Discipline,       // رشته
    string ServiceType,      // نوع خدمت
    string Stage,            // شماره مرحله
    string StartDate,        // تاریخ شروع
    string EndDate,          // تاریخ پایان
    string Amount,           // مبلغ
    string PayStatus,        // وضعیت پرداخت
    string ConfirmStatus,    // وضعیت تایید
    string AmountType,       // نوع مبلغ
    string Description = "") // توضیحات
{
    public string Identity => $"{TextNormalizer.Normalize(Discipline)}|{TextNormalizer.Normalize(ServiceType)}|{TextNormalizer.Normalize(Stage)}|{TextNormalizer.Normalize(AmountType)}";
}

/// <summary>
/// آیتم نتیجه اسکن برای ذخیره در دیتابیس (فقط آخرین اسکن)
/// </summary>
public class LastScanResultItem
{
    public string CaseNumber { get; set; } = "";
    public int Stage { get; set; }
    public string Owner { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public string Companion { get; set; } = "";
    public string StageName { get; set; } = "";
    public string Status { get; set; } = "";
    public string GeneratedAt { get; set; } = "";
    public string GeneratedAtShamsi { get; set; } = "";
    public string PermitDate { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string DaysRemaining { get; set; } = "";
    public string DeadlineStatus { get; set; } = "";
    public string BuildingGroup { get; set; } = "";
    public bool HasReport { get; set; }
}

/// <summary>A single report record (گزارش).</summary>
public sealed record ReportRecord(
    string RowNo,            // ردیف
    string ReportType,       // نوع گزارش
    string Stage,            // مرحله
    string Engineer,         // مهندس ناظر
    string Discipline,       // مسئولیت
    string VisitDate,        // تاریخ بازدید
    string CeilingCount,     // تعداد سقف اجرا شده
    string Indicator,        // شماره اندیکاتور
    bool HasFile)
{
    public string Identity => $"{TextNormalizer.Normalize(RowNo)}|{TextNormalizer.Normalize(ReportType)}|{TextNormalizer.Normalize(Stage)}|{TextNormalizer.Normalize(Engineer)}|{TextNormalizer.Normalize(VisitDate)}";
}
