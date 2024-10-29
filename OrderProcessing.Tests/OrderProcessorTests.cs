using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Interfaces;
using OrderProcessing.Models;
using OrderProcessing.Services;
using Xunit;

namespace OrderProcessing.Tests
{
    public class OrderProcessorTests
    {
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly Mock<IAlertService> _alertServiceMock;
        private readonly Mock<ILogger<OrderProcessor>> _loggerMock;
        private readonly OrderProcessor _processor;

        public OrderProcessorTests()
        {
            _orderServiceMock = new Mock<IOrderService>();
            _alertServiceMock = new Mock<IAlertService>();
            _loggerMock = new Mock<ILogger<OrderProcessor>>();

            _processor = new OrderProcessor(
                _orderServiceMock.Object,
                _alertServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessOrdersAsync_WithDeliveredItems_SendsAlertsAndUpdatesOrders()
        {
            // Arrange
            var orders = new List<Order>
            {
                new()
                {
                    OrderId = "123",
                    Items = new List<OrderItem>
                    {
                        new() { Description = "Item1", Status = "Delivered", DeliveryNotification = 0 },
                        new() { Description = "Item2", Status = "InProgress", DeliveryNotification = 0 }
                    }
                }
            };

            _orderServiceMock
                .Setup(x => x.FetchMedicalEquipmentOrdersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            // Act
            await _processor.ProcessOrdersAsync();

            // Assert
            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    It.Is<string>(id => id == "123"),
                    It.Is<OrderItem>(item => item.Description == "Item1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderServiceMock.Verify(
                x => x.UpdateOrderAsync(
                    It.Is<Order>(o => o.OrderId == "123"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(1, orders[0].Items[0].DeliveryNotification);
            Assert.Equal(0, orders[0].Items[1].DeliveryNotification);
        }

        [Fact]
        public async Task ProcessOrdersAsync_WithNoDeliveredItems_DoesNotSendAlertsOrUpdateOrders()
        {
            // Arrange
            var orders = new List<Order>
            {
                new()
                {
                    OrderId = "123",
                    Items = new List<OrderItem>
                    {
                        new() { Description = "Item1", Status = "InProgress", DeliveryNotification = 0 },
                        new() { Description = "Item2", Status = "Pending", DeliveryNotification = 0 }
                    }
                }
            };

            _orderServiceMock
                .Setup(x => x.FetchMedicalEquipmentOrdersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            // Act
            await _processor.ProcessOrdersAsync();

            // Assert
            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<OrderItem>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            _orderServiceMock.Verify(
                x => x.UpdateOrderAsync(
                    It.IsAny<Order>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            Assert.All(orders[0].Items, item => Assert.Equal(0, item.DeliveryNotification));
        }

        [Fact]
        public async Task ProcessOrdersAsync_WithNoOrders_DoesNothing()
        {
            // Arrange
            _orderServiceMock
                .Setup(x => x.FetchMedicalEquipmentOrdersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<Order>());

            // Act
            await _processor.ProcessOrdersAsync();

            // Assert
            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<OrderItem>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            _orderServiceMock.Verify(
                x => x.UpdateOrderAsync(
                    It.IsAny<Order>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}