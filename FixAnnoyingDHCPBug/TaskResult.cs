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
