using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TechVeo.Processing.Application.Services.Interfaces;
using TechVeo.Processing.Infra.Persistence.Contexts;
using TechVeo.Shared.Domain.UoW;
using TechVeo.Shared.Infra.Extensions;
using TechVeo.Shared.Infra.Persistence.UoW;

namespace TechVeo.Processing.Integration.Tests.Fixtures;

public class IntegrationTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public OrderContext DbContext { get; }

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();

        // Configure InfraOptions for OrderContext
        services.AddOptions<InfraOptions>();

        // Configure in-memory database
        services.AddDbContext<OrderContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Configure Unit of Work
        services.TryAddScoped<IUnitOfWorkTransaction, UnitOfWorkTransaction>();
        services.TryAddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<OrderContext>());

        // Register application services
        services.AddMediatR(typeof(Application.Commands.CreateOrder.CreateOrderCommand).Assembly);

        // Register domain repositories
        services.AddScoped<Domain.Repositories.IOrderRepository, Infra.Persistence.Repositories.OrderRepository>();

        // Mock external services
        var backofficeServiceMock = new Mock<IBackofficeService>();
        services.AddScoped(_ => backofficeServiceMock.Object);

        var orderNumberServiceMock = new Mock<IOrderNumberService>();
        orderNumberServiceMock.Setup(x => x.GetAsync()).ReturnsAsync(1);
        services.AddScoped(_ => orderNumberServiceMock.Object);

        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<OrderContext>();
    }

    public void Dispose()
    {
        DbContext?.Database.EnsureDeleted();
        DbContext?.Dispose();

        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
