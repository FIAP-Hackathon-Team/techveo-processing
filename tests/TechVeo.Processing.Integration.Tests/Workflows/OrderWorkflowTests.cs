using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TechVeo.Processing.Application.Commands.CreateOrder;
using TechVeo.Processing.Application.Dto;
using TechVeo.Processing.Application.Services.Interfaces;
using TechVeo.Processing.Domain.Repositories;
using TechVeo.Processing.Integration.Tests.Fixtures;
using TechVeo.Shared.Domain.Enums;
using TechVeo.Shared.Domain.UoW;

namespace TechVeo.Processing.Integration.Tests.Workflows;

public class OrderWorkflowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IMediator _mediator;
    private readonly IOrderRepository _orderRepository;

    public OrderWorkflowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _mediator = _fixture.ServiceProvider.GetRequiredService<IMediator>();
        _orderRepository = _fixture.ServiceProvider.GetRequiredService<IOrderRepository>();
    }

    [Fact(DisplayName = "Should complete full order workflow from creation to delivery")]
    [Trait("Integration", "OrderWorkflow")]
    public async Task CompleteOrderWorkflow_ShouldTransitionThroughAllStatuses()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Mock backoffice service to return products
        var backofficeService = _fixture.ServiceProvider.GetRequiredService<IBackofficeService>();
        Mock.Get(backofficeService)
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>
            {
                new ProductDto(productId, "Pizza", "pizza.jpg", 25.00m)
            });

        // Act & Assert - Create Order
        var createCommand = new CreateOrderCommand(
            customerId,
            new List<CreateOrderCommand.Item>
            {
                new CreateOrderCommand.Item(productId, 2)
            });

        var orderDto = await _mediator.Send(createCommand);

        await _fixture.DbContext.SaveChangesAsync();

        orderDto.Should().NotBeNull();
        orderDto.Status.Should().Be(OrderStatusType.Pending);
        orderDto.Amount.Should().Be(50.00m);

        // Get order from repository
        var order = await _orderRepository.GetByIdAsync(orderDto.Id);
        order.Should().NotBeNull();

        // Transition to Received
        order!.Receive();
        order.Status.Should().Be(OrderStatusType.Received);

        // Transition to InPreparation
        order.Prepare();
        order.Status.Should().Be(OrderStatusType.InPreparation);

        // Transition to Ready
        order.Ready();
        order.Status.Should().Be(OrderStatusType.Ready);

        // Transition to Delivered
        order.Deliver();
        order.Status.Should().Be(OrderStatusType.Delivered);

        // Verify final state
        order.Status.Should().Be(OrderStatusType.Delivered);
        order.Items.Should().HaveCount(1);
        order.Amount.Should().Be(50.00m);
    }

    [Fact(DisplayName = "Should create multiple orders independently")]
    [Trait("Integration", "OrderWorkflow")]
    public async Task CreateMultipleOrders_ShouldMaintainIndependence()
    {
        // Arrange
        var customer1Id = Guid.NewGuid();
        var customer2Id = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var backofficeService = _fixture.ServiceProvider.GetRequiredService<IBackofficeService>();
        Mock.Get(backofficeService)
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>
            {
                new ProductDto(productId, "Burger", "burger.jpg", 15.00m)
            });

        // Act - Create first order
        var command1 = new CreateOrderCommand(
            customer1Id,
            new List<CreateOrderCommand.Item> { new(productId, 1) });

        var order1 = await _mediator.Send(command1);

        // Act - Create second order
        var command2 = new CreateOrderCommand(
            customer2Id,
            new List<CreateOrderCommand.Item> { new(productId, 3) });

        var order2 = await _mediator.Send(command2);

        // Assert
        order1.Should().NotBeNull();
        order2.Should().NotBeNull();
        order1.Id.Should().NotBe(order2.Id);
        order1.CustomerId.Should().Be(customer1Id);
        order2.CustomerId.Should().Be(customer2Id);
        order1.Amount.Should().Be(15.00m);
        order2.Amount.Should().Be(45.00m);
    }

    [Fact(DisplayName = "Should handle order cancellation")]
    [Trait("Integration", "OrderWorkflow")]
    public async Task CancelOrder_ShouldUpdateStatusToCancelled()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var backofficeService = _fixture.ServiceProvider.GetRequiredService<IBackofficeService>();
        Mock.Get(backofficeService)
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>
            {
                new ProductDto(productId, "Salad", "salad.jpg", 12.00m)
            });

        var createCommand = new CreateOrderCommand(
            customerId,
            new List<CreateOrderCommand.Item> { new(productId, 1) });

        var orderDto = await _mediator.Send(createCommand);

        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var order = await _orderRepository.GetByIdAsync(orderDto.Id);
        order!.Cancel();

        // Assert
        order.Status.Should().Be(OrderStatusType.Cancelled);
    }
}
