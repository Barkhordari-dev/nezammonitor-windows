namespace NezamMonitor.Core.Models;

/// <summary>سند دریافتی مرتبط با حق‌الزحمه — یک واریز با چند تاریخ پایان مرتبط</summary>
public sealed class FeeReceipt
{
    public long Id { get; set; }
    /// <summary>تاریخ واریز شمسی yyyy/MM/dd</summary>
    public string ReceiptDate { get; set; } = "";
    /// <summary>مبلغ واریز (ریال) — عدد خام</summary>
    public long Amount { get; set; }
    /// <summary>تاریخ‌های پایان مرتبط (; جدا) — هر مقدار دقیقا برابر Fee.EndDate</summary>
    public string RelatedEndDatesRaw { get; set; } = "";
    public List<string> RelatedEndDates
    {
        get => string.IsNullOrWhiteSpace(RelatedEndDatesRaw) ? new() : RelatedEndDatesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        set => RelatedEndDatesRaw = string.Join(";", value ?? new List<string>());
    }
    /// <summary>آیا علی‌الحساب است؟ اگر true با تایید شده + StartDate محاسبه می‌شود، وگرنه پرداخت شده</summary>
    public bool IsAdvance { get; set; }
    /// <summary>جمع حق‌الزحمه‌های مرتبط (محاسبه شده)</summary>
    public long FeeSum { get; set; }
    /// <summary>اختلاف = Amount - FeeSum (منفی = کسری)</summary>
    public long Difference => Amount - FeeSum;
    /// <summary>توضیح تغییرات آخرین بروزرسانی</summary>
    public string Changes { get; set; } = "";
    /// <summary>آیا بعد از آخرین بروزرسانی تغییر کرده؟ (سبز)</summary>
    public bool IsRecentlyUpdated { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}
