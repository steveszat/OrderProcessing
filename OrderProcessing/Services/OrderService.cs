using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderProcessing.Interfaces;
using OrderProcessing.Models;
using System.Text;
using System.Text.Json;

namespace OrderProcessing.Services
{
    public class OrderService : IOrderService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _configuration;

        public OrderService(
            HttpClient httpClient,
            ILogger<OrderService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<IEnumerable<Order>> FetchMedicalEquipmentOrdersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var ordersApiUrl = _configuration["ApiEndpoints:Orders"];
                _logger.LogInformation("Fetching orders from {Url}", ordersApiUrl);

                var response = await _httpClient.GetAsync(ordersApiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var ordersData = await response.Content.ReadAsStringAsync(cancellationToken);
                var orders = JsonConvert.DeserializeObject<List<Order>>(ordersData);

                _logger.LogInformation("Successfully fetched {Count} orders", orders?.Count ?? 0);
                return orders ?? Enumerable.Empty<Order>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch orders from API");
                throw;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse orders response");
                throw;
            }
        }

        public async Task UpdateOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            try
            {
                var updateApiUrl = _configuration["ApiEndpoints:UpdateOrder"];
                _logger.LogInformation("Updating order {OrderId}", order.OrderId);

                var content = new StringContent(
                    JsonConvert.SerializeObject(order),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(updateApiUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Successfully updated order {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order {OrderId}", order.OrderId);
                throw;
            }
        }
    }
}