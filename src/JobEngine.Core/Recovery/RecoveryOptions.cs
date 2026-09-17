namespace JobEngine.Core.Recovery;

public class RecoveryOptions
{
    public const string SectionName = "Recovery";

    public int StaleClaimThresholdSeconds { get; set; } = 900;

    public int ScanIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 100;
}