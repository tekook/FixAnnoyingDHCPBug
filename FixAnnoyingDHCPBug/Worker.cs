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
        private bool _stopped = false;
        private readonly SemaphoreSlim _semaphore = new(0);
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
        /// Reset the service and start again.
        /// </summary>
        public void TriggerByPowerEvent()
        {
            this.Log(LogLevel.Information, "Service triggered by PowerEvent.");
            this._stopped = false;
            this._retryCount = 0;
            this._semaphore.Release();
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
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        this.Log(LogLevel.Debug, "Worker running at: {time}", DateTimeOffset.Now);

                        if (this._stopped == false)
                        {
                            var ifaces = this.Results.Where(x => x.Value == TaskResult.Retry).Select(x => x.Key).ToArray();
                            foreach (var iface in ifaces)
                            {
                                this.Results[iface] = await this.CheckAndFixIntercace(iface, stoppingToken);
                            }

                            if (!this.Results.Any(x => x.Value == TaskResult.Retry))
                            {
                                this.Log(LogLevel.Information, "All interfaces have reached a non retry state. Shutting down.");
                                this._stopped = true;
                            }



                            this._retryCount++;
                            if (this._retryCount == this._settings.MaxRetries)
                            {
                                this.logger.LogWarning("Max retry count ({MaxRetries}) reached. Shutting down.", this._settings.MaxRetries);
                                this._stopped = true;
                            }
                            else
                            {
                                this.Log(LogLevel.Debug, "Retry {Retry} of {MaxRetries}", this._retryCount, this._settings.MaxRetries);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogCritical(ex, "Service encountered a critical error - stopping worker");
                        this._stopped = true;
                    }
                    if (this._stopped)
                    {
                        await this._semaphore.WaitAsync(stoppingToken);
                    }
                    else
                    {
                        await Task.WhenAny(
                            Task.Delay(this._period, stoppingToken),
                            this._semaphore.WaitAsync(stoppingToken));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                this.Log(LogLevel.Debug, "Worker termination requested.");
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
                this.Log(LogLevel.Information, "DHCP is not enabled on interface '{InterfaceName}'.", interfaceName);
                return TaskResult.NoDHCPEnabled;
            }

            bool hasGateway = ipProperties.GatewayAddresses.Any(g => !g.Address.ToString().Equals("0.0.0.0"));

            if (hasGateway)
            {
                this.Log(LogLevel.Information, "Success: Default gateway detected on interface '{InterfaceName}'.", interfaceName);
                return TaskResult.Success;
            }

            this.logger.LogWarning("DHCP is active but no default gateway was found on '{InterfaceName}'.", interfaceName);
            this.Log(LogLevel.Information, "Disabling interface '{InterfaceName}'...", interfaceName);
            this.ToggleInterface(interfaceName, false);

            await Task.Delay(this._delay, cancellationToken);
            this.Log(LogLevel.Information, "Re-enabling interface '{InterfaceName}'...", interfaceName);
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

        private void Log(LogLevel level, string message, params object?[] args)
        {
            if (this.logger.IsEnabled(level))
            {
                this.logger.Log(level, message, args);
            }
        }
    }
}
