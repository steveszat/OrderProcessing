namespace OrderProcessing.Models
{
    public class Order
    {
        public string OrderId { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }

    public class OrderItem
    {
        public string Description { get; set; }
        public string Status { get; set; }
        public int DeliveryNotification { get; set; }
    }
}