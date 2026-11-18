using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Payment.Application.Interfaces.Services;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Infrastructure.Persistence;
using ToeicSpace.Payment.Infrastructure.Persistence.Repositories;
using ToeicSpace.Payment.Infrastructure.Services.Content;
using ToeicSpace.Payment.Infrastructure.Services.Coupons;
using ToeicSpace.Payment.Infrastructure.Services.Orders;
using ToeicSpace.Payment.Infrastructure.Services.SePay;

namespace ToeicSpace.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("PaymentDatabase")
            ?? throw new InvalidOperationException("PaymentDatabase connection string is required.");
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<CouponReservationRepository>();
        services.AddScoped<IPaymentReadRepository, PaymentReadRepository>();
        services.AddScoped<ICouponReadRepository, CouponReadRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISePayWebhookProcessor, SePayWebhookProcessor>();
        services.AddScoped<OrderMaintenanceRunner>();
        services.AddHostedService<OrderMaintenance>();
        services.AddHttpClient<ContentCheckoutClient>(http =>
        {
            var url = config["Content:BaseUrl"]
                ?? throw new InvalidOperationException("Content:BaseUrl is required.");
            http.BaseAddress = new Uri(url.EndsWith('/') ? url : url + "/");
            http.Timeout = TimeSpan.FromSeconds(8);
        }).RemoveAllLoggers();
        return services;
    }
}
