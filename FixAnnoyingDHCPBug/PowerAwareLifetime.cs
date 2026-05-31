using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using System.ServiceProcess;

namespace FixAnnoyingDHCPBug
{
    public class PowerAwareLifetime : WindowsServiceLifetime
    {
        private readonly ILogger<PowerAwareLifetime> _logger;
        private readonly Worker _worker;

        public PowerAwareLifetime(
            IHostEnvironment environment,
            IHostApplicationLifetime applicationLifetime,
            ILoggerFactory loggerFactory,
            ILogger<PowerAwareLifetime> logger,
            IOptions<HostOptions> hostOptions,
            IEnumerable<IHostedService> hostedServices)
            : base(environment, applicationLifetime, loggerFactory, hostOptions)
        {
            this._logger = logger;
            this._worker = hostedServices.OfType<Worker>().First();

            this.CanHandlePowerEvent = true;
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (this._logger.IsEnabled(LogLevel.Information))
            {
                this._logger.LogInformation("Power event detected: {Status}", powerStatus);
            }

            if (powerStatus == PowerBroadcastStatus.ResumeAutomatic)
            {
                if (this._logger.IsEnabled(LogLevel.Information))
                {
                    this._logger.LogInformation("System resumed from Fast Startup / Hibernation. Re-triggering network check...");
                }
                this._worker.TriggerByPowerEvent();
            }

            return base.OnPowerEvent(powerStatus);
        }
    }
}
