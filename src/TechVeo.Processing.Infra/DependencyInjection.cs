using Microsoft.Extensions.DependencyInjection;
using TechVeo.Processing.Application.Clients;
using TechVeo.Processing.Application.Services;
using TechVeo.Processing.Infra.Clients;
using TechVeo.Processing.Infra.Services;
using TechVeo.Shared.Infra.Extensions;

namespace TechVeo.Processing.Infra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfra(this IServiceCollection services)
    {
        services.AddSharedInfra(new InfraOptions
        {
            ApplicationAssembly = typeof(Application.DependencyInjection).Assembly
        });

        services.AddScoped<IVideoProcessingService, VideoProcessingService>();
        services.AddScoped<IGenerativeClient, GeminiGenerativeClient>();

        return services;
    }
}
