namespace FixAnnoyingDHCPBug
{
    public class ServiceSettings
    {
        public string[] InterfaceNames { get; set; } = [];
        public int BounceDelaySeconds { get; set; } = 15;
        public int MaxRetries { get; set; } = 10;
        public int PeriodDelay { get; set; } = 10;
    }
}
