namespace OrderProcessing.Interfaces
{
    public interface IOrderProcessor
    {
        Task ProcessOrdersAsync(CancellationToken cancellationToken = default);
    }
}