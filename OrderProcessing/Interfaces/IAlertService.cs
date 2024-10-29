using OrderProcessing.Models;

namespace OrderProcessing.Interfaces
{
    public interface IAlertService
    {
        Task SendAlertMessageAsync(string orderId, OrderItem item, CancellationToken cancellationToken = default);
    }
}