using OrderProcessing.Models;

namespace OrderProcessing.Interfaces
{
    public interface IOrderService
    {
        Task<IEnumerable<Order>> FetchMedicalEquipmentOrdersAsync(CancellationToken cancellationToken = default);
        Task UpdateOrderAsync(Order order, CancellationToken cancellationToken = default);
    }
}