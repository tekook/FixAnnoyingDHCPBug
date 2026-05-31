namespace FixAnnoyingDHCPBug
{
    public class ServiceSettings
    {
        /// <summary>
        /// List of the interfaces to perform a check on.
        /// </summary>
        public string[] InterfaceNames { get; set; } = [];
        /// <summary>
        /// How many seconds to wait between toggling the interface.
        /// </summary>
        public int BounceDelay { get; set; } = 15;
        /// <summary>
        /// How many retries the service should to until giving up.
        /// </summary>
        public int MaxRetries { get; set; } = 10;
        /// <summary>
        /// Delay betweewn the checks.
        /// </summary>
        public int PeriodDelay { get; set; } = 10;
        /// <summary>
        /// Initial Delay after service startup or resume.
        /// </summary>
        public int InitialDelay { get; set; } = 30;
    }
}
