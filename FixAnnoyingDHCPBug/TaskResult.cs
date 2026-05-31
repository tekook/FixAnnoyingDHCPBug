namespace FixAnnoyingDHCPBug
{
    public enum TaskResult
    {
        /// <summary>
        /// Interface has DHCP enabled and an Default-Gateway
        /// </summary>
        Success,
        /// <summary>
        /// Interface is due for a retry
        /// </summary>
        Retry,
        /// <summary>
        /// Interface is not found in the system.
        /// </summary>
        InterfaceNotFound,
        /// <summary>
        /// Interface does not have DHCP enabled.
        /// </summary>
        NoDHCPEnabled
    }
}
