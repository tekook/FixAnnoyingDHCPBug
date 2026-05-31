using Microsoft.Extensions.Options;
using System.Diagnostics;
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
        /// <summary>
        /// Main Loop
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug("Service started.");
                this.logger.LogDebug("Interface: {interfaces}; Delay: {delay}, Retries: {retries}, Period: {period}",
                                     string.Join(", ", this._settings.InterfaceNames),
                                     this._settings.BounceDelaySeconds,
                                     this._settings.MaxRetries,
                                     this._settings.PeriodDelay);
            }
            using PeriodicTimer timer = new(this._period);
            foreach (var interfaceName in this._settings.InterfaceNames)
            {
                this.Results.Add(interfaceName, TaskResult.Retry);
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    this.LogDebug("Worker running at: {time}", DateTimeOffset.Now);

                    var ifaces = this.Results.Where(x => x.Value == TaskResult.Retry).Select(x => x.Key).ToArray();
                    foreach (var iface in ifaces)
                    {
                        this.Results[iface] = await this.CheckAndFixIntercace(iface, stoppingToken);
                    }

                    if (!this.Results.Any(x => x.Value == TaskResult.Retry))
                    {
                        this.LogInformation("All interfaces have reached a non retry state. Shutting down.");
                        this.hostApplicationLifetime.StopApplication();
                    }



                    this._retryCount++;
                    if (this._retryCount == this._settings.MaxRetries)
                    {
                        this.logger.LogWarning("Max retry count ({MaxRetries}) reached. Shutting down.", this._settings.MaxRetries);
                        this.hostApplicationLifetime.StopApplication();
                        break;
                    }
                    else
                    {
                        this.LogDebug("Retry {Retry} of {MaxRetries}", this._retryCount, this._settings.MaxRetries);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogDebug("Worker termination requested.");
            }
            catch (Exception ex)
            {
                this.logger.LogCritical(ex, "An error occurred during task execution.");
                throw;
            }
        }
        /// <summary>
        /// Checks the given interface for existing, DHCP-enabled and the default gateway.
        /// If DHCP is enabled and no gateway is found, the interface will get toggled and retried.
        /// </summary>
        /// <param name="interfaceName">Name of the interface</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The result of the check</returns>
        private async Task<TaskResult> CheckAndFixIntercace(string interfaceName, CancellationToken cancellationToken)
        {
            NetworkInterface? networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni => ni.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));

            if (networkInterface == null)
            {
                this.logger.LogWarning("Interface '{InterfaceName}' not found.", interfaceName);
                return TaskResult.InterfaceNotFound;
            }

            IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
            bool isDhcpEnabled = ipProperties.GetIPv4Properties()?.IsDhcpEnabled ?? false;

            if (!isDhcpEnabled)
            {
                this.LogInformation("DHCP is not enabled on interface '{InterfaceName}'.", interfaceName);
                return TaskResult.NoDHCPEnabled;
            }

            bool hasGateway = ipProperties.GatewayAddresses.Any(g => !g.Address.ToString().Equals("0.0.0.0"));

            if (hasGateway)
            {
                this.LogInformation("Success: Default gateway detected on interface '{InterfaceName}'.", interfaceName);
                return TaskResult.Success;
            }

            this.logger.LogWarning("DHCP is active but no default gateway was found on '{InterfaceName}'.", interfaceName);
            this.LogInformation("Disabling interface '{InterfaceName}'...", interfaceName);
            this.ToggleInterface(interfaceName, false);

            await Task.Delay(this._delay, cancellationToken);
            this.LogInformation("Re-enabling interface '{InterfaceName}'...", interfaceName);
            this.ToggleInterface(interfaceName, true);

            await Task.Delay(this._delay, cancellationToken);
            return TaskResult.Retry;
        }
        /// <summary>
        /// Disabled or enabled the interface via the netsh command.
        /// </summary>
        /// <param name="interfaceName">Name of interface</param>
        /// <param name="enable">True for enabling, false for disabling.</param>
        private void ToggleInterface(string interfaceName, bool enable)
        {
            string action = enable ? "enable" : "disable";

            using var process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = $"interface set interface name=\"{interfaceName}\" admin={action}";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                this.logger.LogError("Failed to {Action} interface ({Interface}) via netsh. Exit code: {Code}", action, interfaceName, process.ExitCode);
            }
        }
        private void LogInformation(string message, params object?[] args)
        {
            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.LogInformation(message, args);
            }
        }
        private void LogDebug(string message, params object?[] args)
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.LogDebug(message, args);
            }
        }
    }
}
