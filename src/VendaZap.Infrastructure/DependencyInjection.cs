using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Serilog;
using StackExchange.Redis;
using System.Text;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Infrastructure.AI;
using VendaZap.Infrastructure.Caching;
using VendaZap.Infrastructure.Identity;
using VendaZap.Infrastructure.Messaging;
using VendaZap.Infrastructure.Messaging.Consumers;
using VendaZap.Infrastructure.Persistence;
using VendaZap.Infrastructure.Persistence.Repositories;
using VendaZap.Infrastructure.WhatsApp;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddDatabase(config)
            .AddIdentityServices(config)
            .AddCaching(config)
            .AddWhatsAppService(config)
            .AddAiService(config)
            .AddMessaging(config)
            .AddSignalRServices()
            .AddRepositories();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    private static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        var jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"] ?? "vendazap",
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"] ?? "vendazap-api",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Allow token from query string for SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("OwnerOnly", p => p.RequireRole("Owner"));
            options.AddPolicy("ManagerOrAbove", p => p.RequireRole("Owner", "Manager"));
            options.AddPolicy("AgentOrAbove", p => p.RequireRole("Owner", "Manager", "Agent"));
        });

        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
    {
        var redisConn = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConn));
            services.AddScoped<ICacheService, RedisCacheService>();
        }
        return services;
    }

    private static IServiceCollection AddWhatsAppService(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<IWhatsAppService, WhatsAppService>()
            .AddTransientHttpErrorPolicy(p =>
                p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        services.AddHttpClient<IWhatsAppClient, WhatsAppClient>()
            .AddTransientHttpErrorPolicy(p =>
                p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        return services;
    }

    private static IServiceCollection AddAiService(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IAiConversationService, OpenAiConversationService>();
        return services;
    }

    private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<InboundWhatsAppMessageConsumer>();
            x.AddConsumer<WhatsAppStatusUpdateConsumer>();
            x.AddConsumer<AbandonedCartConsumer>();
            x.AddConsumer<FollowUpJobConsumer>();
            x.AddConsumer<OutgoingWhatsAppMessageConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config["RabbitMQ:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "guest");
                    h.Password(config["RabbitMQ:Password"] ?? "guest");
                });

                cfg.ReceiveEndpoint("vendazap-inbound-messages", e =>
                {
                    e.PrefetchCount = 10;
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromMinutes(2)));
                    e.ConfigureConsumer<InboundWhatsAppMessageConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("vendazap-status-updates", e =>
                {
                    e.ConfigureConsumer<WhatsAppStatusUpdateConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("vendazap-abandoned-carts", e =>
                {
                    e.ConfigureConsumer<AbandonedCartConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("vendazap-follow-ups", e =>
                {
                    e.ConfigureConsumer<FollowUpJobConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("vendazap.outgoing-messages", e =>
                {
                    e.PrefetchCount = 20;
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromMinutes(2)));
                    e.ConfigureConsumer<OutgoingWhatsAppMessageConsumer>(ctx);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    private static IServiceCollection AddSignalRServices(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<INotificationService, SignalRNotificationService>();
        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IAutoReplyTemplateRepository, AutoReplyTemplateRepository>();
        return services;
    }
}
