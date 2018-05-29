using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spreads.SignalW;
using Spreads.SignalW.Client;
// using Microsoft.AspNet.SignalR;

namespace ServerSample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var mvcCoreBuilder = services.AddMvcCore();
            mvcCoreBuilder
                .AddFormatterMappings()
                .AddJsonFormatters()
                .AddCors();
            services.AddSignalW();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //app.UseSignalR(routes =>
            //{
            //    routes.MapHub<Echo2>("/default");
            //});

            app.Map("/api/signalw", signalw =>
            {
                signalw.UseSignalW((config) =>
                {
                    config.MapHub<Chat>("chat", Format.Text);
                });

                signalw.UseSignalW((config) =>
                {
                    config.MapHub<Echo>("echo", Format.Binary);
                });
            });
            app.Map("/api", apiApp =>
            {
                apiApp.UseMvc();
            });
            app.UseMvc();
        }
    }
}
