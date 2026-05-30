using System.Diagnostics;
using System.Reflection;

namespace FixAnnoyingDHCPBug
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string serviceName = "FixAnnoyingDHCPBug";
            if (args.Length > 0)
            {
                string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                ;

                if (args[0] == "/Install")
                {
                    Process.Start("sc.exe", $"create \"{serviceName}\" binPath= \"{exePath}\" start= auto obj= \"LocalSystem\"");
                    Process.Start("sc.exe", $"start \"{serviceName}\"");
                    return;
                }
                if (args[0] == "/Uninstall")
                {
                    Process.Start("sc.exe", $"stop \"{serviceName}\"")?.WaitForExit();
                    Process.Start("sc.exe", $"delete \"{serviceName}\"");
                    return;
                }
            }

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = serviceName;
            });
            builder.Services.Configure<ServiceSettings>(builder.Configuration.GetSection("ServiceSettings"));
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
        }
    }
}
