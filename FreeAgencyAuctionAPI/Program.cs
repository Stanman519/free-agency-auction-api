using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FreeAgencyAuctionAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // .ConfigureLogging((context, logging) =>
                // {
                //     logging.ClearProviders();
                //     logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                //     logging.AddDebug();
                //     logging.AddConsole();
                //     //logging.AddTraceSource();
                //     
                // })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config
                        //.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true) 
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
                    Console.WriteLine(hostingContext.HostingEnvironment.EnvironmentName);
                })


                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}