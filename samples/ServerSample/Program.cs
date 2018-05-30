using System.IO;
using System.Net;
using System.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
using Microsoft.Extensions.Configuration;

namespace ServerSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;

            var config = new ConfigurationBuilder()
                .Build();

            var host = new WebHostBuilder()
                .UseConfiguration(config)
                .UseSetting(WebHostDefaults.PreventHostingStartupKey, "true")
                
                .UseKestrel(options =>
                {
                    // Default port
                    options.ListenLocalhost(5002);
                    options.ApplicationSchedulingMode = SchedulingMode.Inline;
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseLibuv()
                .UseStartup<Startup>()
                .Build();
            host.Run();
        }
    }
}

