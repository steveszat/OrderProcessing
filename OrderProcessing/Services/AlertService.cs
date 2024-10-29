using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderProcessing.Interfaces;
using OrderProcessing.Models;
using System.Text;

namespace OrderProcessing.Services
{
    public class AlertService : IAlertService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AlertService> _logger;
        private readonly IConfiguration _configuration;

        public AlertService(
            HttpClient httpClient,
            ILogger<AlertService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task SendAlertMessageAsync(string orderId, OrderItem item, CancellationToken cancellationToken = default)
        {
            try
            {
                var alertApiUrl = _configuration["ApiEndpoints:Alerts"];
                _logger.LogInformation("Sending alert for order {OrderId}, item {Description}", orderId, item.Description);

                var alertData = new
                {
                    Message = $"Alert for delivered item: Order {orderId}, Item: {item.Description}, " +
                             $"Delivery Notifications: {item.DeliveryNotification}"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(alertData),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(alertApiUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Successfully sent alert for order {OrderId}, item {Description}",
                    orderId, item.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert for order {OrderId}, item {Description}",
                    orderId, item.Description);
                throw;
            }
        }
    }
}