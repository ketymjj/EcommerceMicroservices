using Microsoft.Extensions.Configuration;
using Shared.Messaging.Clients;
using Shared.Messaging.Interfaces;
using Shared.Security.Services;
using Shared.Security.Interfaces;
using Shared.Models;
using Shared.Security.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
        services.AddSingleton<IRabbitMqClient, RabbitMqClient>();

        return services;
    }

    public static IServiceCollection AddJwtServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TokenParameters>(configuration.GetSection("Jwt"));

        services.AddSingleton<IJwtTokenService, JwtTokenGenerator>();
        services.AddSingleton<ITokenValidator, JwtTokenValidator>();

        return services;
    }
}
