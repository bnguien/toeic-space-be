using Microsoft.EntityFrameworkCore;
using ToeicSpace.Payment.Domain.Entities;

namespace ToeicSpace.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponReservation> CouponReservations => Set<CouponReservation>();
    public DbSet<PurchaseOrder> Orders => Set<PurchaseOrder>();
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
}
