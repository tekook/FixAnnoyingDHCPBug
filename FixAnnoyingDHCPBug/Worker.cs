using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;

namespace FixAnnoyingDHCPBug
{
    public class Worker : BackgroundService
    {
        private readonly TimeSpan _period;
        private readonly TimeSpan _delay;
        private readonly ILogger<Worker> logger;
        private readonly IHostApplicationLifetime hostApplicationLifetime;
        private readonly ServiceSettings _settings;
        private int _retryCount = 0;
        private readonly Dictionary<string, TaskResult> Results = [];

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime hostApplicationLifetime, IOptions<ServiceSettings> settings)
        {
            this.logger = logger;
            this.hostApplicationLifetime = hostApplicationLifetime;
            this._settings = settings.Value;
            this._period = TimeSpan.FromSeconds(settings.Value.PeriodDelay);
            this._delay = TimeSpan.FromSeconds(settings.Value.BounceDelaySeconds);

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Service started.");
                logger.LogDebug("Interface: {interfaces}", string.Join(", ", _settings.InterfaceNames));
                logger.LogDebug("Delay: {delay}", _settings.BounceDelaySeconds);
                logger.LogDebug("Retries: {retries}", _settings.MaxRetries);
                logger.LogDebug("Period: {period}", _settings.PeriodDelay);
            }
            using PeriodicTimer timer = new(_period);
            foreach (var interfaceName in _settings.InterfaceNames)
            {
                Results.Add(interfaceName, TaskResult.Retry);
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);

                    var ifaces = Results.Where(x => x.Value == TaskResult.Retry).Select(x => x.Key).ToArray();
                    foreach (var iface in ifaces)
                    {
                        Results[iface] = await CheckAndFixIntercace(iface, stoppingToken);
                    }

                    if (!Results.Any(x => x.Value == TaskResult.Retry))
                    {
                        logger.LogInformation("All interfaces have reached a non retry state. Shutting down.");
                        hostApplicationLifetime.StopApplication();
                    }



                    _retryCount++;
                    if (_retryCount == _settings.MaxRetries)
                    {
                        logger.LogWarning("Max retry count ({MaxRetries}) reached. Shutting down.", _settings.MaxRetries);
                        hostApplicationLifetime.StopApplication();
                        break;
                    }
                    else
                    {
                        logger.LogDebug("Retry {Retry} of {MaxRetries}", _retryCount, _settings.MaxRetries);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Worker termination requested.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "An error occurred during task execution.");
                throw;
            }
        }
        private async Task<TaskResult> CheckAndFixIntercace(string interfaceName, CancellationToken cancellationToken)
        {
            NetworkInterface? networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => ni.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));

            if (networkInterface == null)
            {
                logger.LogWarning("Interface '{InterfaceName}' not found.", interfaceName);
                return TaskResult.InterfaceNotFound;
            }

            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
            bool isDhcpEnabled = ipProperties.GetIPv4Properties()?.IsDhcpEnabled ?? false;

            if (!isDhcpEnabled)
            {
                logger.LogInformation("DHCP is not enabled on interface '{InterfaceName}'. Skipping check.", interfaceName);
                return TaskResult.NoDHCPEnabled;
            }

            bool hasGateway = ipProperties.GatewayAddresses.Any(g => !g.Address.ToString().Equals("0.0.0.0"));

            if (hasGateway)
            {
                logger.LogInformation("Success: Default gateway detected on interface '{InterfaceName}'.", interfaceName);
                return TaskResult.Success;
            }

            logger.LogWarning("DHCP is active but no default gateway was found on '{InterfaceName}'.", interfaceName);

            logger.LogInformation("Disabling interface '{InterfaceName}'...", interfaceName);
            ToggleInterface(networkInterface.Id, false);

            await Task.Delay(_delay, cancellationToken);

            logger.LogInformation("Re-enabling interface '{InterfaceName}'...", interfaceName);
            ToggleInterface(networkInterface.Id, true);

            await Task.Delay(_delay, cancellationToken);
            return TaskResult.Retry;
        }

        private void ToggleInterface(string interfaceGuid, bool enable)
        {
            string action = enable ? "enable" : "disable";

            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = $"interface set interface name=\"{interfaceGuid}\" admin={action}";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                logger.LogError("Failed to {Action} interface via netsh. Exit code: {Code}", action, process.ExitCode);
            }
        }
    }
}
