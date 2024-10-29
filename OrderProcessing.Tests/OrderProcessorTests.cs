using Microsoft.Extensions.Logging;
using Moq;
using OrderProcessing.Interfaces;
using OrderProcessing.Models;
using OrderProcessing.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        [Fact]
        public async Task ProcessOrdersAsync_WhenOrderServiceThrows_PropagatesException()
        {
            // Arrange
            _orderServiceMock
                .Setup(x => x.FetchMedicalEquipmentOrdersAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("API unavailable"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _processor.ProcessOrdersAsync());

            Assert.Equal("API unavailable", exception.Message);

            // Verify no other operations were performed
            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<OrderItem>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessOrdersAsync_WhenAlertServiceThrows_ContinuesProcessingOtherItems()
        {
            // Arrange
            var orders = new List<Order>
            {
                new Order
                {
                    OrderId = "123",
                    Items = new List<OrderItem>
                    {
                        new OrderItem { Description = "Item1", Status = "Delivered", DeliveryNotification = 0 },
                        new OrderItem { Description = "Item2", Status = "Delivered", DeliveryNotification = 0 }
                    }
                }
            };

            _orderServiceMock
                .Setup(x => x.FetchMedicalEquipmentOrdersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            // Configure first alert to throw
            var expectedError = new Exception("Alert service error");
            _alertServiceMock
                .Setup(x => x.SendAlertMessageAsync(
                    "123",
                    It.Is<OrderItem>(item => item.Description == "Item1"),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedError);

            // Configure second alert to succeed
            _alertServiceMock
                .Setup(x => x.SendAlertMessageAsync(
                    "123",
                    It.Is<OrderItem>(item => item.Description == "Item2"),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act & Assert
            var actualError = await Assert.ThrowsAsync<Exception>(() => _processor.ProcessOrdersAsync());

            // Verify exception
            Assert.Same(expectedError, actualError);

            // Verify both alerts were attempted
            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    "123",
                    It.Is<OrderItem>(item => item.Description == "Item1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _alertServiceMock.Verify(
                x => x.SendAlertMessageAsync(
                    "123",
                    It.Is<OrderItem>(item => item.Description == "Item2"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData("DELIVERED")]
        [InlineData("Delivered")]
        [InlineData("delivered")]
        public async Task ProcessOrdersAsync_WithDifferentDeliveredCasing_ProcessesCorrectly(string status)
        {
            // Arrange
            var orders = new List<Order>
            {
                new()
                {
                    OrderId = "123",
                    Items = new List<OrderItem>
                    {
                        new() { Description = "Item1", Status = status, DeliveryNotification = 0 }
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
                    It.Is<OrderItem>(item => item.Description == "Item1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessOrdersAsync_WithMultipleOrders_HandlesAllOrders()
        {
            // Arrange
            var orders = new List<Order>
            {
                new()
                {
                    OrderId = "123",
                    Items = new List<OrderItem>
                    {
                        new() { Description = "Item1", Status = "Delivered", DeliveryNotification = 0 }
                    }
                },
                new()
                {
                    OrderId = "456",
                    Items = new List<OrderItem>
                    {
                        new() { Description = "Item2", Status = "Delivered", DeliveryNotification = 0 }
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
                Times.Exactly(2));

            _orderServiceMock.Verify(
                x => x.UpdateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }
}