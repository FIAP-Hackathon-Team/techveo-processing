using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechVeo.Processing.Application.Dto;
using TechVeo.Processing.Application.Services.Interfaces;
using TechVeo.Processing.Infra.Persistence.Contexts;
using TechVeo.Processing.Infra.Persistence.Queries;
using TechVeo.Shared.Domain.Enums;
using TechVeo.Shared.Infra.Extensions;

namespace TechVeo.Processing.Integration.Tests.Queries;

public class OrderQueryProviderTests : IDisposable
{
    private readonly OrderContext _context;
    private readonly Mock<IBackofficeService> _backofficeServiceMock;
    private readonly OrderQueryProvider _queryProvider;
    private readonly Faker _faker;

    public OrderQueryProviderTests()
    {
        // IOptions of InfraOptions is not needed for in-memory tests
        var infraOptions = Options.Create(new InfraOptions());

        var options = new DbContextOptionsBuilder<OrderContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new OrderContext(infraOptions, options);
        _backofficeServiceMock = new Mock<IBackofficeService>();
        _queryProvider = new OrderQueryProvider(_context);
        _faker = new Faker();
    }

    [Fact(DisplayName = "Should return all orders with items")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrdersAsync_ShouldReturnAllOrders_WithItems()
    {
        // Arrange
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        var products = new List<ProductDto>
        {
            new ProductDto(productId1, "Product 1", "image1.jpg", 10.00m),
            new ProductDto(productId2, "Product 2", "image2.jpg", 20.00m)
        };

        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var order1 = new Domain.Entities.Video(1, Guid.NewGuid());
        order1.AddItem(new Domain.Entities.OrderItem(productId1, 10.00m, 2));

        var order2 = new Domain.Entities.Video(2, null);
        order2.AddItem(new Domain.Entities.OrderItem(productId2, 20.00m, 1));

        await _context.Orders.AddRangeAsync(order1, order2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var firstOrder = result.First();
        firstOrder.Number.Should().Be(1);
        firstOrder.Items.Should().HaveCount(1);
        firstOrder.Items.First().Quantity.Should().Be(2);

        var secondOrder = result.Last();
        secondOrder.Number.Should().Be(2);
        secondOrder.Items.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Should return orders ordered by creation date")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrdersAsync_ShouldReturnOrders_OrderedByCreatedAt()
    {
        // Arrange
        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());

        var order1 = new Domain.Entities.Video(1, Guid.NewGuid());
        var order2 = new Domain.Entities.Video(2, Guid.NewGuid());
        var order3 = new Domain.Entities.Video(3, Guid.NewGuid());

        await _context.Orders.AddRangeAsync(order3, order1, order2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrdersAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(o => o.Number).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "Should return empty list when no orders exist")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrdersAsync_ShouldReturnEmptyList_WhenNoOrdersExist()
    {
        // Arrange
        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());

        // Act
        var result = await _queryProvider.GetOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should return only ready orders")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetReadyOrdersAsync_ShouldReturnOnlyReadyOrders()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var products = new List<ProductDto>
        {
            new ProductDto(productId, "Product 1", "image1.jpg", 10.00m)
        };

        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var pendingOrder = new Domain.Entities.Video(1, Guid.NewGuid());
        pendingOrder.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));

        var receivedOrder = new Domain.Entities.Video(2, Guid.NewGuid());
        receivedOrder.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        receivedOrder.Receive();

        var preparingOrder = new Domain.Entities.Video(3, Guid.NewGuid());
        preparingOrder.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        preparingOrder.Receive();
        preparingOrder.Prepare();

        var readyOrder1 = new Domain.Entities.Video(4, Guid.NewGuid());
        readyOrder1.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        readyOrder1.Receive();
        readyOrder1.Prepare();
        readyOrder1.Ready();

        var readyOrder2 = new Domain.Entities.Video(5, Guid.NewGuid());
        readyOrder2.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        readyOrder2.Receive();
        readyOrder2.Prepare();
        readyOrder2.Ready();

        await _context.Orders.AddRangeAsync(
            pendingOrder,
            receivedOrder,
            preparingOrder,
            readyOrder1,
            readyOrder2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetReadyOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(o => o.Status == OrderStatusType.Ready);
        result.Select(o => o.Number).Should().Contain(new[] { 4, 5 });
    }

    [Fact(DisplayName = "Should return ready orders ordered by creation date")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetReadyOrdersAsync_ShouldReturnOrders_OrderedByCreatedAt()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var products = new List<ProductDto>
        {
            new ProductDto(productId, "Product", "image.jpg", 10.00m)
        };

        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var order1 = CreateReadyOrder(1, productId);
        var order2 = CreateReadyOrder(2, productId);
        var order3 = CreateReadyOrder(3, productId);

        await _context.Orders.AddRangeAsync(order3, order1, order2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetReadyOrdersAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(o => o.Number).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "Should return empty list when no ready orders exist")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetReadyOrdersAsync_ShouldReturnEmptyList_WhenNoReadyOrdersExist()
    {
        // Arrange
        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());

        var pendingOrder = new Domain.Entities.Video(1, Guid.NewGuid());
        await _context.Orders.AddAsync(pendingOrder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetReadyOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should include product information from backoffice service")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrdersAsync_ShouldIncludeProductInformation_FromBackofficeService()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedProductName = "Test Product";
        var expectedImageUrl = "test-image.jpg";
        var expectedPrice = 25.00m;

        var products = new List<ProductDto>
        {
            new ProductDto(productId, expectedProductName, expectedImageUrl, expectedPrice)
        };

        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var order = new Domain.Entities.Video(1, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId, expectedPrice, 3));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrdersAsync();

        // Assert
        var orderDto = result.Single();
        var itemDto = orderDto.Items.Single();

        itemDto.Price.Should().Be(expectedPrice);
        itemDto.Quantity.Should().Be(3);
    }

    [Fact(DisplayName = "Should handle orders with multiple items")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrdersAsync_ShouldHandleOrders_WithMultipleItems()
    {
        // Arrange
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var productId3 = Guid.NewGuid();

        var products = new List<ProductDto>
        {
            new ProductDto(productId1, "Product 1", "image1.jpg", 10.00m),
            new ProductDto(productId2, "Product 2", "image2.jpg", 15.00m),
            new ProductDto(productId3, "Product 3", "image3.jpg", 20.00m)
        };

        _backofficeServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var order = new Domain.Entities.Video(1, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId1, 10.00m, 2));
        order.AddItem(new Domain.Entities.OrderItem(productId2, 15.00m, 1));
        order.AddItem(new Domain.Entities.OrderItem(productId3, 20.00m, 3));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrdersAsync();

        // Assert
        var orderDto = result.Single();
        orderDto.Items.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Should return order by Id when it exists")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WhenItExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var order = new Domain.Entities.Video(100, customerId);
        order.AddItem(new Domain.Entities.OrderItem(productId, 15.50m, 2));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.Number.Should().Be(100);
        result.CustomerId.Should().Be(customerId);
        result.Amount.Should().Be(31.00m);
        result.Status.Should().Be(OrderStatusType.Pending);
        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(2);
        result.Items[0].Price.Should().Be(15.50m);
    }

    [Fact(DisplayName = "Should return null when order does not exist")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Should return order without customer")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WithoutCustomer()
    {
        // Arrange
        var productId = Guid.NewGuid();

        var order = new Domain.Entities.Video(200, null);
        order.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().BeNull();
        result.Number.Should().Be(200);
    }

    [Fact(DisplayName = "Should return order with multiple items")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WithMultipleItems()
    {
        // Arrange
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var productId3 = Guid.NewGuid();

        var order = new Domain.Entities.Video(300, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId1, 10.00m, 2));
        order.AddItem(new Domain.Entities.OrderItem(productId2, 15.00m, 1));
        order.AddItem(new Domain.Entities.OrderItem(productId3, 20.00m, 3));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.Items.Sum(i => i.Price * i.Quantity).Should().Be(result.Amount);
    }

    [Fact(DisplayName = "Should return order with no items")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WithNoItems()
    {
        // Arrange
        var order = new Domain.Entities.Video(400, Guid.NewGuid());

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.Amount.Should().Be(0m);
    }

    [Theory(DisplayName = "Should return order with different statuses")]
    [Trait("Integration", "OrderQueryProvider")]
    [InlineData(OrderStatusType.Pending)]
    [InlineData(OrderStatusType.Received)]
    [InlineData(OrderStatusType.InPreparation)]
    [InlineData(OrderStatusType.Ready)]
    [InlineData(OrderStatusType.Delivered)]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WithCorrectStatus(OrderStatusType expectedStatus)
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = new Domain.Entities.Video(500, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));

        // Set the status based on test parameter
        if (expectedStatus >= OrderStatusType.Received)
            order.Receive();
        if (expectedStatus >= OrderStatusType.InPreparation)
            order.Prepare();
        if (expectedStatus >= OrderStatusType.Ready)
            order.Ready();
        if (expectedStatus == OrderStatusType.Delivered)
            order.Deliver();

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(expectedStatus);
    }

    [Fact(DisplayName = "Should handle Guid.Empty gracefully")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnNull_WithEmptyGuid()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(emptyGuid);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Should return correct order when multiple orders exist")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldReturnCorrectOrder_WhenMultipleExist()
    {
        // Arrange
        var productId = Guid.NewGuid();
        
        var order1 = new Domain.Entities.Video(701, Guid.NewGuid());
        order1.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        
        var order2 = new Domain.Entities.Video(702, Guid.NewGuid());
        order2.AddItem(new Domain.Entities.OrderItem(productId, 20.00m, 2));
        
        var order3 = new Domain.Entities.Video(703, Guid.NewGuid());
        order3.AddItem(new Domain.Entities.OrderItem(productId, 30.00m, 3));

        await _context.Orders.AddRangeAsync(order1, order2, order3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order2.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order2.Id);
        result.Number.Should().Be(702);
        result.Amount.Should().Be(40.00m);
    }

    [Fact(DisplayName = "Should include items in GetOrderByIdAsync")]
    [Trait("Integration", "OrderQueryProvider")]
    public async Task GetOrderByIdAsync_ShouldIncludeItems()
    {
        // Arrange
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        
        var order = new Domain.Entities.Video(800, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId1, 12.50m, 2));
        order.AddItem(new Domain.Entities.OrderItem(productId2, 8.00m, 3));

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Clear context to ensure fresh load
        _context.ChangeTracker.Clear();

        // Act
        var result = await _queryProvider.GetOrderByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Price == 12.50m && i.Quantity == 2);
        result.Items.Should().Contain(i => i.Price == 8.00m && i.Quantity == 3);
    }

    private static Domain.Entities.Video CreateReadyOrder(int number, Guid productId)
    {
        var order = new Domain.Entities.Video(number, Guid.NewGuid());
        order.AddItem(new Domain.Entities.OrderItem(productId, 10.00m, 1));
        order.Receive();
        order.Prepare();
        order.Ready();
        return order;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
