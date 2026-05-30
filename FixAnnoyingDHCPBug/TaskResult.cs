using System;
using System.Collections.Generic;
using System.Text;

namespace FixAnnoyingDHCPBug
{
    public enum TaskResult
    {
        Success,
        Retry,
        InterfaceNotFound,
        NoDHCPEnabled,
        GatewayFound
    }
}
