using System;
using System.Collections.Generic;
using System.Text;

namespace FixAnnoyingDHCPBug
{
    public class ServiceSettings
    {
        public string InterfaceName { get; set; } = "Ethernet";
        public int BounceDelaySeconds { get; set; } = 15;
        public int MaxRetries { get; set; } = 10;
        public int PeriodDelay { get; set; } = 10;
    }
}
