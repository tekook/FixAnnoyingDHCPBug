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
                logger.LogDebug("Interface: {interface}", _settings.InterfaceName);
                logger.LogDebug("Delay: {delay}", _settings.BounceDelaySeconds);
                logger.LogDebug("Retries: {retries}", _settings.MaxRetries);
                logger.LogDebug("Period: {period}", _settings.PeriodDelay);
            }
            using PeriodicTimer timer = new(_period);

            try
            {
                while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                    await DoWorkAsync(stoppingToken);

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
        private async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            NetworkInterface? networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => ni.Name.Equals(_settings.InterfaceName, StringComparison.OrdinalIgnoreCase));

            if (networkInterface == null)
            {
                logger.LogWarning("Interface '{InterfaceName}' not found.", _settings.InterfaceName);
                hostApplicationLifetime.StopApplication();
                return;
            }

            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
            bool isDhcpEnabled = ipProperties.GetIPv4Properties()?.IsDhcpEnabled ?? false;

            if (!isDhcpEnabled)
            {
                logger.LogInformation("DHCP is not enabled on interface '{InterfaceName}'. Skipping check.", _settings.InterfaceName);
                hostApplicationLifetime.StopApplication();
                return;
            }

            bool hasGateway = ipProperties.GatewayAddresses.Any(g => !g.Address.ToString().Equals("0.0.0.0"));

            if (hasGateway)
            {
                logger.LogInformation("Success: Default gateway detected on interface '{InterfaceName}'. Shutting down service.", _settings.InterfaceName);
                hostApplicationLifetime.StopApplication();
                return;
            }

            logger.LogWarning("DHCP is active but no default gateway was found on '{InterfaceName}'.", _settings.InterfaceName);
            _retryCount++;

            if (_retryCount > _settings.MaxRetries)
            {
                throw new InvalidOperationException($"Maximum retry limit ({_settings.MaxRetries}) reached. Gateway could not be recovered.");
            }

            logger.LogInformation("Retry attempt {Count} of {Max}. Disabling interface...", _retryCount, _settings.MaxRetries);
            ToggleInterface(networkInterface.Id, false);

            await Task.Delay(_delay, cancellationToken);

            logger.LogInformation("Re-enabling interface...");
            ToggleInterface(networkInterface.Id, true);

            await Task.Delay(_delay, cancellationToken);
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
