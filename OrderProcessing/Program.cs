using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderProcessing.Interfaces;
using OrderProcessing.Services;
using Microsoft.Extensions.Logging;

namespace OrderProcessing
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var host = CreateHostBuilder(args).Build();

                using (var scope = host.Services.CreateScope())
                {
                    var orderProcessor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();
                    await orderProcessor.ProcessOrdersAsync();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unhandled exception: {ex}");
                return 1;
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient<IOrderService, OrderService>();
                    services.AddHttpClient<IAlertService, AlertService>();

                    services.AddTransient<IOrderProcessor, OrderProcessor>();

                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                });
    }
}