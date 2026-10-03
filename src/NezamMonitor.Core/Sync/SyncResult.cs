namespace NezamMonitor.Core.Sync;

public class SyncResult
{
    public bool IsSuccess { get; set; }
    public int CasesProcessed { get; set; }
    public int CasesNew { get; set; }
    public int CasesModified { get; set; }
    public string Log { get; set; } = "";
}
