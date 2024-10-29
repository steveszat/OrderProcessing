using Microsoft.Extensions.Logging;
using OrderProcessing.Interfaces;
using OrderProcessing.Models;

public class OrderProcessor : IOrderProcessor
{
    private readonly IOrderService _orderService;
    private readonly IAlertService _alertService;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IOrderService orderService,
        IAlertService alertService,
        ILogger<OrderProcessor> logger)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting order processing");
            var orders = await _orderService.FetchMedicalEquipmentOrdersAsync(cancellationToken);

            foreach (var order in orders)
            {
                await ProcessOrderAsync(order, cancellationToken);
            }

            _logger.LogInformation("Completed order processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during order processing");
            throw;
        }
    }

    private async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken)
    {
        Exception? firstError = null;
        var hasDeliveredItems = false;

        foreach (var item in order.Items)
        {
            if (IsItemDelivered(item))
            {
                try
                {
                    await _alertService.SendAlertMessageAsync(order.OrderId, item, cancellationToken);
                    IncrementDeliveryNotification(item);
                    hasDeliveredItems = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Error processing item {Description} in order {OrderId}",
                        item.Description, order.OrderId);
                    firstError = ex;
                    // Continue processing other items
                }
            }
        }

        if (hasDeliveredItems && firstError == null)
        {
            try
            {
                await _orderService.UpdateOrderAsync(order, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to update order {OrderId}", order.OrderId);
                firstError = ex;
            }
        }

        if (firstError != null)
        {
            throw firstError;
        }
    }

    private static bool IsItemDelivered(OrderItem item)
    {
        return item.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase);
    }

    private static void IncrementDeliveryNotification(OrderItem item)
    {
        item.DeliveryNotification++;
    }
}