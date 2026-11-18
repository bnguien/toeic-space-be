using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Payment.Application;
using ToeicSpace.Payment.Application.DTOs.SePay;
using ToeicSpace.Payment.Application.DTOs.Coupons;
using ToeicSpace.Payment.Application.Interfaces.Repositories;
using ToeicSpace.Payment.Application.Features.Coupons.Commands.CreateCoupon;
using ToeicSpace.Payment.Application.Features.Coupons.Queries.GetCoupon;
using ToeicSpace.Payment.Application.Features.SePay.Commands.ProcessSePayWebhook;
using ToeicSpace.Payment.Application.Interfaces.Services;
using ToeicSpace.Payment.Domain.Entities;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class PaymentFeatureTests
{
    [Fact]
    public async Task MediatR_DispatchesCouponCommandsAndQueries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddSingleton<FakeCoupons>();
        services.AddSingleton<ICouponService>(p => p.GetRequiredService<FakeCoupons>());
        services.AddSingleton<ICouponReadRepository>(p => p.GetRequiredService<FakeCoupons>());
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var coupon = await sender.Send(new CreateCouponCommand(new Coupon { Code = "FEATURE" }));
        Assert.NotEqual(Guid.Empty, coupon.Id);
        Assert.Equal(coupon.Id, (await sender.Send(new GetCouponQuery(coupon.Id))).Coupon.Id);
    }

    [Fact]
    public async Task MediatR_DispatchesAuthenticatedWebhookAfterControllerCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddSingleton<ISePayWebhookProcessor, FakeWebhook>();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        await sender.Send(new ProcessSePayWebhookCommand(new SePayWebhook(1, null, null, null, null, null, null, 0, null)));
        Assert.Equal(1, provider.GetRequiredService<ISePayWebhookProcessor>() is FakeWebhook fake ? fake.Calls : 0);
    }

    private sealed class FakeCoupons : ICouponService, ICouponReadRepository
    {
        private Coupon? _coupon;
        public Task<Coupon> CreateAsync(Coupon coupon, CancellationToken ct)
        { _coupon = coupon; return Task.FromResult(coupon); }
        public Task<Coupon?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_coupon?.Id == id ? _coupon : null);
        public Task<CouponWithUsage?> GetWithUsageAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_coupon?.Id == id ? new CouponWithUsage(_coupon, 0, 0, null) : null);
        public Task<CouponPage> SearchAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new CouponPage(page, pageSize, _coupon is null ? 0 : 1,
                _coupon is null ? [] : [new CouponWithUsage(_coupon, 0, 0, null)]));
        public Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Coupon>>(_coupon is null ? [] : [_coupon]);
        public Task<Coupon> UpdateAsync(Guid id, Coupon values, CancellationToken ct) => Task.FromResult(values);
        public Task<Coupon> DeactivateAsync(Guid id, CancellationToken ct) => Task.FromResult(_coupon!);
        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            _coupon = null;
            return Task.CompletedTask;
        }
        public Task<decimal> PreviewAsync(string code, Guid courseId, decimal amount, Guid userId, CancellationToken ct) =>
            Task.FromResult(0m);
    }

    private sealed class FakeWebhook : ISePayWebhookProcessor
    {
        public int Calls { get; private set; }
        public Task ProcessAsync(SePayWebhook webhook, CancellationToken ct)
        { Calls++; return Task.CompletedTask; }
    }
}
