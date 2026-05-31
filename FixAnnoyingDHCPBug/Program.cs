using System.Diagnostics;
using System.Reflection;

namespace FixAnnoyingDHCPBug
{
    public class Program
    {
        private const string _serviceName = "FixAnnoyingDHCPBug";
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                ;

                if (args[0] == "/Install")
                {
                    Process.Start("sc.exe", $"create \"{_serviceName}\" binPath= \"{exePath}\" start= auto obj= \"LocalSystem\"")?.WaitForExit();
                    Process.Start("sc.exe", $"start \"{_serviceName}\"")?.WaitForExit();
                    return;
                }
                if (args[0] == "/Uninstall")
                {
                    Process.Start("sc.exe", $"stop \"{_serviceName}\"")?.WaitForExit();
                    Process.Start("sc.exe", $"delete \"{_serviceName}\"")?.WaitForExit();
                    return;
                }
            }

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = _serviceName;
            });
            builder.Services.AddSingleton<IHostLifetime, PowerAwareLifetime>();
            builder.Services.Configure<ServiceSettings>(builder.Configuration.GetSection("ServiceSettings"));
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
